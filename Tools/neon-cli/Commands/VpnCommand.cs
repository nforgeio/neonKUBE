//-----------------------------------------------------------------------------
// FILE:	    VpnCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Hive;
using Neon.IO;

namespace NeonCli
{
    /// <summary>
    /// Implements hive VPN commands.
    /// </summary>
    public class VpnCommand : CommandBase
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Information about a certificate from a certificate authority index. 
        /// </summary>
        private class CertInfo
        {
            public bool         IsValid { get; set; }
            public DateTime     ValidUntil { get; set; }
            public string       Thumbprint { get; set; }
            public string       Name { get; set; }
        }

        //---------------------------------------------------------------------
        // Implementation

        private const string usage = @"
Manages a hive's VPN.

USAGE:

    neon vpn ca HIVE-DEF CA-FOLDER
    ------------------------------
    Generates the certificate authority and server certificate 
    files as well as the root client certificate to be used 
    for securing a neonHIVE VPN.  This command is generally
    executed internally during hive setup.

    HIVE-DEF    - Path to the hive definition file.

    CA-FOLDER   - Folder path where the VPN certificates and 
                  authority will be created or accessed.

    neon vpn crl update
    -------------------
    Ensures that all OpenVPN servers have the most recent 
    certificate revocation list (CRL).
    
    neon vpn user create [--root] [--days=365] USER
    -----------------------------------------------
    Creates a new login for the currently logged in hive.
    The new login file will be written to the current
    directory.

    USER        - New user name (letters, digits, dashes, 
                  underscores, or periods).
    
    --root      - Grants the new user root privileges.

    --days=#    - Number of days before the new login will
                  expire (defaults to 365 days).

    neon vpn user ls
    neon vpn user list 
    ------------------
    Lists the currently valid users for the currently
    logged in hive.

    neon vpn user revoke [--restart-vpn] THUMBPRINT
    -----------------------------------------------
    Revokes a user login certificate with the associated thumprint.
    User [neon vpn user list] to see the current user certificates
    and their thumprints.

    THUMBPRINT      - Thumbprint (serial number) for the certificate.

    --restart-vpn   - Restarts the hive's OpenVPN servers to ensure
                      that any existing connections for the user are 
                      immediately closed.  Otherwise by default, it can
                      take up to an hour for revoked users to be dropped.
";

        private const string MustHaveRootPrivileges = "*** ERROR: Root hive privileges are required.";
        private const string VpnNotEnabled          = "*** ERROR: VPN is not enabled for this hive.";

        private string          caFolder;
        private HiveLogin       hiveLogin;
        private HiveProxy       hive;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public VpnCommand()
        {
            // These commands are designed to only run in a Docker Ubuntu container.
            // This means that the [/dev/shm] tmpfs file system is available with
            // up to 64MB of storage.  We're going to manage the hive CA files
            // there for better security since this storage is volatile.

            caFolder = "/dev/shm/ca";
        }

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "vpn" }; }
        }

        /// <inheritdoc/>
        public override string[] ExtendedOptions
        {
            get
            {
                return new string[]
                    {
                        "--all",
                        "--days",
                        "--restart-vpn",
                        "--root"
                    };
            }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <summary>
        /// Verifies that we're not running in direct mode.
        /// </summary>
        private void DirectNotAllowed()
        {
            // This command cannot run in [no-shim] mode because OpenSSL
            // has issues with Windows style file paths.

            if (!HiveHelper.InToolContainer)
            {
                Console.Error.WriteLine("*** ERROR: This VPN command cannot be run in [no-shim] mode.");
                Program.Exit(1);
            }
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            if (commandLine.HasHelpOption)
            {
                Console.WriteLine(usage);
                Program.Exit(0);
            }

            // Process the command arguments.

            if (commandLine.Arguments.Length == 0)
            {
                Console.WriteLine(usage);
                Program.Exit(1);
            }

            var command = commandLine.Arguments.FirstOrDefault();

            commandLine = commandLine.Shift(1);

            switch (command)
            {
                case "ca":

                    if (commandLine.Arguments.Length != 2)
                    {
                        Console.WriteLine(usage);
                        Program.Exit(1);
                    }

                    var defPath = commandLine.Arguments[0];

                    InitializeCA(defPath, commandLine.Arguments[1]);
                    break;

                case "crl":

                    if (commandLine.Arguments.Length != 1 || commandLine.Arguments[0] != "update")
                    {
                        Console.WriteLine(usage);
                        Program.Exit(1);
                    }

                    UpdateCRL();
                    break;

                case "user":

                    command     = commandLine.Arguments.FirstOrDefault();
                    commandLine = commandLine.Shift(1);

                    switch (command)
                    {
                        case "create":

                            UserCreate(commandLine);
                            break;

                        case "ls":
                        case "list":

                            UserList();
                            break;

                        case "revoke":

                            UserRevoke(commandLine);
                            break;

                        default:

                            Console.Error.WriteLine($"*** ERROR: Unexpected [{command}] command.");
                            Program.Exit(1);
                            break;
                    }
                    break;

                default:

                    Console.WriteLine(usage);
                    Program.Exit(1);
                    break;
            }
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            var commandLine = shim.CommandLine;

            // Shim command: neon vpn ca HIVE-DEF CA-FOLDER
            //
            // We need to copy the [HIVE-DEF] and the contents of the 
            // [CA-FOLDER] into a shim subfolder, update the command line 
            // and execute it, and then copy the file contents back out 
            // to the original folder when the command completes.

            if (commandLine.Arguments.Length == 4 &&
                commandLine.Arguments[0] == "vpn" &&
                commandLine.Arguments[1] == "ca")
            {
                var hiveDefPath     = commandLine.Arguments[2];
                var caFolderPath    = commandLine.Arguments[3];
                var shimmedCaFolder = Path.Combine(shim.ShimExternalFolder, "ca");

                shim.AddFile(hiveDefPath);

                foreach (var file in Directory.GetFiles(caFolderPath, "*.*", SearchOption.TopDirectoryOnly))
                {
                    File.Copy(file, Path.Combine(shimmedCaFolder, Path.GetFileName(file)));
                }

                shim.ReplaceItem(caFolderPath, $"{DockerShim.ShimInternalFolder}/ca");

                shim.SetPostAction(
                    exitCode =>
                    {
                        if (exitCode == 0)
                        {
                            foreach (var file in Directory.GetFiles(shimmedCaFolder, "*.*", SearchOption.TopDirectoryOnly))
                            {
                                File.Copy(file, Path.Combine(caFolderPath, Path.GetFileName(file)));
                            }
                        }
                    });

                return new DockerShimInfo(shimability: DockerShimability.Required);
            }

            // Shim command: neon vpn cert user create USER
            //
            // We need to copy the new hive login file to the current directory
            // after the command runs in Docker.  Note that the shimmed command
            // writes the file name for the new login to [new-login.txt].

            if (commandLine.Arguments.Length == 4 &&
                commandLine.Arguments[0] == "vpn" &&
                commandLine.Arguments[1] == "user" &&
                commandLine.Arguments[2] == "create")
            {
                var username = commandLine.Arguments[3];

                shim.SetPostAction(
                    exitCode =>
                    {
                        if (exitCode == 0)
                        {
                            var loginName          = File.ReadAllText(Path.Combine(shim.ShimExternalFolder, "new-login.txt"));
                            var generatedLoginPath = Path.Combine(shim.ShimExternalFolder, loginName);
                            var outputLoginPath    = Path.GetFullPath(loginName);
                            var generatedLoginText = File.ReadAllText(generatedLoginPath);

                            File.WriteAllText(outputLoginPath, generatedLoginText);
                            Console.WriteLine($"*** Created login: {outputLoginPath}");
                        }
                    });

                return new DockerShimInfo(shimability: DockerShimability.Required);
            }

            // Shim command: neon vpn user revoke [--restart-vpn] THUMBPRINT
            //
            // No special actions required.

            return new DockerShimInfo(shimability: DockerShimability.Required);
        }

        /// <summary>
        /// Returns the OpenSSL client certificate configuration for a named user.
        /// </summary>
        /// <param></param>
        /// <param name="hiveDefinition">The hive definition.</param>
        /// <param name="user">The user name.</param>
        /// <param name="rootPrivileges">Indicates whether the user has root hive priviledges.</param>
        /// <returns>The configuration file text.</returns>
        private string GetClientConfig(HiveDefinition hiveDefinition, string user, bool rootPrivileges)
        {
            // Make sure the user name is reasonable and doesn't conflict with
            // any of the other ca/cert file names.

            var nameOK = true;

            if (user.Length == 0)
            {
                nameOK = false;
            }

            foreach (var ch in user)
            {
                if (!(char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.'))
                {
                    nameOK = false;
                    break;
                }
            }

            if (!nameOK)
            {
                throw new Exception($"User name [{user}] is invalid.  Names must consist of letters, digits, dashes, underscores or periods only.");
            }

            switch (user.ToLower())
            {
                case "ca":
                case "ca-sign":
                case "client":
                case "crl":
                case "dh2048":
                case "index":
                case "serial":
                case "server":

                    throw new Exception($"User name [{user}] conflicts with a reserved file.  Choose another name.");
            }

            var rootSuffix = rootPrivileges ? " (is-root)" : string.Empty;

            var clientCnf =
$@"# client.cnf
# This configuration file is used by the 'req' command when a certificate is created for [{user}].
[ req ]
default_bits            = 2048
default_md              = sha256
encrypt_key             = no
prompt                  = no
string_mask             = utf8only
distinguished_name      = client1_distinguished_name
req_extensions          = req_cert_extensions
# attributes            = req_attributes

[ client1_distinguished_name ]
countryName             = {hiveDefinition.Vpn.CertCountryCode}
#stateOrProvinceName    = Utrecht
#localityName           = HomeTown
organizationName        = {hiveDefinition.Vpn.CertOrganization}
#organizationalUnitName = My Department Name
commonName              = {user}{rootSuffix}

[ req_cert_extensions ]
nsCertType              = client
subjectAltName          = email:nobody@nowhere.com
";
            return clientCnf;
        }

        /// <summary>
        /// Initializes the VPN certificate authority, and creates the OpenVPN server and 
        /// root client certificates.
        /// </summary>
        /// <param name="defPath">Path to the hive definition file.</param>
        /// <param name="targetFolder">The output folder.</param>
        private void InitializeCA(string defPath, string targetFolder)
        {
            DirectNotAllowed();

            var hiveDefinition = HiveDefinition.FromFile(defPath, strict: true);

            // This implements the steps described here:
            // 
            //      http://www.macfreek.nl/memory/Create_a_OpenVPN_Certificate_Authority

            // Initialize
            // ----------

            Directory.CreateDirectory(caFolder);

            // Initialize the file paths.
            //
            // IMPORTANT:
            //
            // Do not change these file names because the [VpnCaFiles] class 
            // depends on this naming convention.

            var indexPath     = Path.Combine(caFolder, "index.txt");
            var caSignCnfPath = Path.Combine(caFolder, "ca-sign.cnf");
            var caCnfPath     = Path.Combine(caFolder, "ca.cnf");
            var caKeyPath     = Path.Combine(caFolder, "ca.key");
            var caReqPath     = Path.Combine(caFolder, "ca.req");
            var caCrtPath     = Path.Combine(caFolder, "ca.crt");
            var dhParamPath   = Path.Combine(caFolder, "dhparam.pem");
            var serverCnfPath = Path.Combine(caFolder, "server.cnf");
            var serverKeyPath = Path.Combine(caFolder, "server.key");
            var serverReqPath = Path.Combine(caFolder, "server.req");
            var serverCrtPath = Path.Combine(caFolder, "server.crt");
            var rootCnfPath   = Path.Combine(caFolder, $"{HiveConst.RootUser}.cnf");
            var rootReqPath   = Path.Combine(caFolder, $"{HiveConst.RootUser}.req");
            var rootKeyPath   = Path.Combine(caFolder, $"{HiveConst.RootUser}.key");
            var rootCrtPath   = Path.Combine(caFolder, $"{HiveConst.RootUser}.crt");
            var taKeyPath     = Path.Combine(caFolder, "ta.key");
            var crlnumberPath = Path.Combine(caFolder, "crlnumber");
            var crlPath       = Path.Combine(caFolder, "crl.pem");

            // Create an empty certificate index file.

            File.WriteAllText(indexPath, string.Empty);

            // CA Configuration Files
            // ----------------------
            // Create configuration files. In our setup, [ca-sign.cnf] contains the configuration for signing certificates. 
            // We only use it in conjunction with the [openssl ca command]. It described the folder structure within the [ca]
            // directory, the location of support files for the CA, as well as properties of the signed certificates (duration, 
            // restricted usage) as well as the policy for the name ("distinguished name") of signed certificates. Finally, 
            // it lists the policy for certification revocation lists. For this small-scale CA, there is no public URL to 
            // download the CRL; I plan to distribute it manually.

            var caSignCnf =
$@"# ca-sign.cnf
# This configuration file is used by the 'ca' command, to create signed certificates.
[ ca ]
default_ca              = CA_default            # The default ca section

[ CA_default ]
dir                     = {$"{this.caFolder}"}           # Where everything is kept
certs                   = $dir/                 # Where the issued certs are kept
crl_dir                 = $dir/                 # Where the issued crl are kept
new_certs_dir           = $dir/                 # default place for new certs

private_key             = $dir/ca.key           # The private key
certificate             = $dir/ca.crt           # The CA root certificate
database                = $dir/index.txt        # List of signed certificates
serial                  = $dir/serial           # The current serial number
crlnumber               = $dir/crlnumber        # the current crl number
crl                     = $dir/crl.pem          # The current CRL
RANDFILE                = $dir/.rand            # private random number file

unique_subject          = no                    # allow multiple certificates with same subject.
default_md              = sha256                # Use hash algorithm specified in the request
default_days            = 365000                # client certificates last about 1000 years
default_crl_days        = 30                    # How often clients should download the CRL

#x509_extensions        = X509_ca               # The x509 extensions for the root certificate
#x509_extensions        = X509_server           # The x509 extensions for a server certificate
x509_extensions         = X509_client           # The x509 extensions for a client certificate

# These options control what fields from the distinguished name to show before signing.
# They are required to make sure all fields are shown.
name_opt                = ca_default            # Subject Name options
cert_opt                = ca_default            # Certificate field options

copy_extensions         = copy                  # Accept requested extensions

policy                  = policy_dn

[ X509_ca ]
# X509v3 extensions for the root certificate
basicConstraints        = CA:TRUE
nsCertType              = sslCA                 # restrict the usage
keyUsage                = keyCertSign, cRLSign  # restrict the usage
subjectKeyIdentifier    = hash
authorityKeyIdentifier  = keyid:always,issuer:always
#subjectAltName          = email:move            # Move email address from DN to extensions
#crlDistributionPoints   = URI:http://www.example.com/example_ca.crl

[ X509_server ]
# X509v3 extensions for server certificates
basicConstraints        = CA:FALSE
nsCertType              = server                # restrict the usage
keyUsage                = digitalSignature, keyEncipherment
extendedKeyUsage        = serverAuth            # restrict the usage
subjectKeyIdentifier    = hash
authorityKeyIdentifier  = keyid,issuer
#subjectAltName         = email:move            # Move email address from DN to extensions
#crlDistributionPoints  = URI:http://www.example.com/example_ca.crl

[ X509_client ]
# X509v3 extensions for client certificates
basicConstraints        = CA:FALSE
nsCertType              = client                # restrict the usage
keyUsage                = digitalSignature      # restrict the usage
extendedKeyUsage        = clientAuth            # restrict the usage
subjectKeyIdentifier    = hash
authorityKeyIdentifier  = keyid,issuer
#subjectAltName         = email:move            # Move email address from DN to extensions
#crlDistributionPoints  = URI:http://www.example.com/example_ca.crl

[ policy_dn ]
countryName             = supplied              # required parameter, any value allowed
stateOrProvinceName     = optional
localityName            = optional
organizationName        = match                 # required, and must match root certificate
organizationalUnitName  = optional
commonName              = supplied              # required parameter, any value allowed
emailAddress            = optional              # email in DN is deprecated, use subjectAltName
";
            File.WriteAllText(caSignCnfPath, caSignCnf);

            // The x509_extensions sections are not really required by openssl or openvpn, but 
            // adds extra security by telling OpenVPN that clients may connect to servers 
            // only. nsCertType is required for the OpenVPN option ns-cert-type server|client; 
            // keyUsage and extendedKeyUsage are required for remote-cert-tls server|client.

            // [ca.cnf] defines the distinguished name for the certificate authority. It also 
            // contains the key length (2048 is recommended nowadays, over the default of 1024), 
            // and if the key should be encrypted.

            var caCnf =
$@"# ca.cnf
# This configuration file is used by the 'req' command when the root certificates is created.
[ req ]
default_bits            = 2048                  # default strength of client certificates
default_md              = sha256
encrypt_key             = no                    # ""no"" is equivalent to -nodes
prompt                  = no
string_mask             = utf8only
distinguished_name      = ca_distinguished_name     # root certificate name
req_extensions          = req_cert_extensions
#attributes             = req_attributes

[ ca_distinguished_name ]
# root certificate name
countryName             = {hiveDefinition.Vpn.CertCountryCode}
#stateOrProvinceName    = Utrecht
#localityName           = Hometown
organizationName        = {hiveDefinition.Vpn.CertOrganization}
#organizationalUnitName = My Department Name
commonName              = ca
#emailAddress           = nobody@nowhere.org   # email in DN is deprecated, use subjectAltName

[ req_cert_extensions ]
nsCertType              = server
#subjectAltName         = email:nobody@nowhere.org
";
            File.WriteAllText(caCnfPath, caCnf);

            // Note that in the above examples, the email address is specified in the [subjectAltName], 
            // instead of in the distinguished name. This is in accordance with PKIX standards.

            // Build CA certificate
            // --------------------
            // If your CA should be valid after the year 2038, be sure to use openssl 0.9.9 or higher.
            //
            // First create a request with the correct name, and then self-sign a certificate and create 
            // a serial number file. 

            Program.Execute("openssl", "req", "-new",
                "-config", caCnfPath,
                "-keyout", caKeyPath,
                "-out", caReqPath);

            Program.Execute("openssl", "ca", "-batch",
                "-config", caSignCnfPath,
                "-extensions", "X509_ca",
                "-days", 365000,
                "-create_serial",
                "-selfsign",
                "-keyfile", caKeyPath,
                "-in", caReqPath,
                "-out", caCrtPath);

            // Generate Prime Numbers (the Diffie Hellman parameters)
            // ------------------------------------------------------

            Program.Execute("openssl", "dhparam", "-out", dhParamPath, "2048");

            // Build server certificate
            // ------------------------

            // First, create a configuration for the server, similar to [ca.cnf]:

            var serverCnf =
$@"# server.cnf
# This configuration file is used by the 'req' command when the server certificate is created.
[ req ]
default_bits            = 2048
default_md              = sha256
encrypt_key             = no
prompt                  = no
string_mask             = utf8only
distinguished_name      = server_distinguished_name
req_extensions          = req_cert_extensions
#attributes             = req_attributes

[ server_distinguished_name ]
countryName             = {hiveDefinition.Vpn.CertCountryCode}
#stateOrProvinceName    = 
#localityName           = 
organizationName        = {hiveDefinition.Vpn.CertOrganization}
#organizationalUnitName = My Department Name
commonName              = server
#emailAddress           = 

[ req_cert_extensions ]
nsCertType              = server
#subjectAltName         = email:nobody@nowhere.org
";
            File.WriteAllText(serverCnfPath, serverCnf);

            // Create the server request and private key.

            Program.Execute("openssl", "req", "-new",
                "-config", serverCnfPath,
                "-keyout", serverKeyPath,
                "-out", serverReqPath);

            // Create the server certificate.

            Program.Execute("openssl", "ca", "-batch",
                "-config", caSignCnfPath,
                "-extensions", "X509_server",
                "-in", serverReqPath,
                "-out", serverCrtPath);

            // Build the [root] client certificate.

            try
            {
                File.WriteAllText(rootCnfPath, GetClientConfig(hiveDefinition, HiveConst.RootUser, rootPrivileges: true));

                Program.Execute("openssl", "req", "-new",
                    "-config", rootCnfPath,
                    "-keyout", rootKeyPath,
                    "-out", rootReqPath);

                Program.Execute("openssl", "ca", "-batch",
                    "-config", caSignCnfPath,
                    "-out", rootCrtPath,
                    "-in", rootReqPath);
            }
            finally
            {
                if (File.Exists(rootCnfPath))
                {
                    File.Delete(rootCnfPath);
                }
            }

            // Initialize the Certificate Revocation List (CLR) number file
            // and then generate the initial (empty) CRL.

            File.WriteAllText(crlnumberPath, "00");

            Program.Execute("openssl", "ca",
                "-config", caSignCnfPath,
                "-gencrl",
                "-out", crlPath);

            // As one final additional step, we're going to generate a shared
            // key that OpenVPN can use to quickly reject packets that didn't
            // come from a client with the key.  This provides a decent amount
            // of DOS protection, especially for VPNs that only use the UDP
            // transport.

            Program.Execute("openvpn", "--genkey", "--secret", taKeyPath);

            // Copy all of the CA files to the target folder.

            Directory.CreateDirectory(targetFolder);

            foreach (var file in Directory.GetFiles(caFolder, "*.*", SearchOption.TopDirectoryOnly))
            {
                File.Copy(file, Path.Combine(targetFolder, Path.GetFileName(file)));
            }
        }

        /// <summary>
        /// Initializes the hive login and hive proxy and verifies that the
        /// current user has root privileges and the hive enables a VPN.
        /// </summary>
        private void RootLogin()
        {
            hiveLogin = Program.ConnectHive();

            if (!hiveLogin.Definition.Vpn.Enabled)
            {
                Console.Error.WriteLine(VpnNotEnabled);
                Program.Exit(1);
            }

            if (string.IsNullOrEmpty(hiveLogin.VpnCredentials.CaZipKey))
            {
                Console.Error.WriteLine(MustHaveRootPrivileges);
                Program.Exit(1);
            }

            hive = HiveHelper.OpenHive(hiveLogin);
        }

        /// <summary>
        /// Retrieves the VPN's certificate authority files from the hive Vault.
        /// </summary>
        /// <returns>The <see cref="VpnCaFiles"/>.</returns>
        private VpnCaFiles GetVpnCaFiles()
        {
            var manager  = hive.GetReachableManager();
            var response = manager.SudoCommand($"export VAULT_TOKEN={hiveLogin.VaultCredentials.RootToken} && vault read -format=json /neon-secret/vpn/ca.zip.encrypted", RunOptions.Redact);

            if (response.ExitCode != 0)
            {
                Console.Error.Write(response.ErrorText);
                Program.Exit(response.ExitCode);
            }

            var rObject  = JObject.Parse(response.OutputText);
            var dObject  = (JObject)rObject.GetValue("data");
            var zipBytes = Convert.FromBase64String((string)dObject.GetValue("value"));

            return VpnCaFiles.LoadZip(zipBytes, hiveLogin.VpnCredentials.CaZipKey);
        }

        /// <summary>
        /// Lists the known certificates.
        /// </summary>
        /// <param name="vpnCaFiles">The VPN user certificates.</param>
        /// <returns>The user certificate information.</returns>
        private List<CertInfo> ListCerts(VpnCaFiles vpnCaFiles)
        {
            // The issued certificates are listed in the [index.txt] file within the encrypted
            // certificate authority ZIP file stored in the Vault at [/neon-secret/vpn/ca.zip.encrypted].
            // This file is formatted something like:
            //
            //      V	30160714162952Z	    D4C549184FCBD2FC	unknown	/C=US/O=dev-vpn-ca/CN=ca
            //      V   30160714163000Z     D4C549184FCBD2FD    unknown /C=US/O=dev-vpn-ca/CN=server
            //      V   30160714163001Z     D4C549184FCBD2FE    unknown /C=US/O=dev-vpn-ca/CN=root
            //
            // where:
            //
            //      Column 0: V or R, indicating valid or revoked
            //      Column 1: expiration date
            //      Column 2: appears to always be empty
            //      Column 3: certificate serial number (or thumbprint)
            //      Column 4: ??
            //      Column 5: country, organization, and common name
            //
            // Note that the columns are separed by TABs.

            var indexText  = vpnCaFiles.GetFile("index.txt");

            // We're filter out the [ca] and [server] certificates and then output details for
            // the remaining user certificates.

            var certificates = new List<CertInfo>();

            using (var reader = new StringReader(indexText))
            {
                foreach (var line in reader.Lines())
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    var columns = line.Split('\t');

                    if (columns.Length != 6)
                    {
                        continue;
                    }

                    // Column[1] in [index.txt] is the certificate expiration date.
                    // OpenSSL appears to generate two different formats, one with
                    // a 2-digit year like:
                    //
                    //      yyMMddHHmmssZ
                    //
                    // and one with a 4-digit year like:
                    //
                    //      yyyyMMddHHmmssZ
                    //
                    // This seems a bit strange but we'll go with the flow and choose
                    // the format based on the string length.

                    DateTime validUntil;

                    if (columns[1].Length == "yyMMddHHmmssZ".Length)
                    {
                        validUntil = DateTime.ParseExact(columns[1], "yyMMddHHmmssZ", CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        validUntil = DateTime.ParseExact(columns[1], "yyyyMMddHHmmssZ", CultureInfo.InvariantCulture);
                    }

                    var info =
                        new CertInfo()
                        {
                            IsValid    = columns[0] == "V",
                            ValidUntil = validUntil,
                            Thumbprint = columns[3].ToLowerInvariant()
                        };

                    var pos = columns[5].IndexOf("/CN=");

                    if (pos == -1)
                    {
                        continue;
                    }

                    info.Name = columns[5].Substring(pos + "/CN=".Length);

                    certificates.Add(info);
                }
            }

            return certificates;
        }

        /// <summary>
        /// Creates a new hive login.
        /// </summary>
        /// <param name="commandLine">The command line.</param>
        private void UserCreate(CommandLine commandLine)
        {
            DirectNotAllowed();

            var username = commandLine.Arguments.FirstOrDefault();

            if (string.IsNullOrEmpty(username))
            {
                Console.Error.WriteLine("***ERROR: USER argument is required.");
                Program.Exit(1);
            }

            var isUserValid = true;

            foreach (var ch in username)
            {
                if (!(char.IsLetterOrDigit(ch) || ch == '.' || ch == '_' || ch == '-'))
                {
                    isUserValid = false;
                    break;
                }
            }

            if (!isUserValid)
            {
                Console.WriteLine($"***ERROR: USER [{username}] is not valid.  Only letters, digits, periods, underscores, or dashes are allowed.");
                Program.Exit(1);
            }

            switch (username.ToLowerInvariant())
            {
                case "ca":
                case "dhparam":
                case "server":

                    Console.WriteLine($"***ERROR: USER [{username}] is reserved by neonHIVE.  Please choose another name.");
                    Program.Exit(1);
                    break;
            }

            var daysOption = commandLine.GetOption("--days", "365");
            int days       = 365;

            if (string.IsNullOrEmpty(daysOption) || !int.TryParse(daysOption, out days) || days <= 0)
            {
                Console.WriteLine($"***ERROR: [--days={daysOption}] is not valid.  This must be a positive integer.");
                Program.Exit(1);
            }

            var rootPrivileges = commandLine.HasOption("--root");

            RootLogin();

            Directory.CreateDirectory(caFolder);

            try
            {
                // Retrieve the VPN certificate authority ZIP archive from Vault and extract
                // its contents to a temporary folder.

                var caZipBytes = hive.Vault.Client.ReadBytesAsync("neon-secret/vpn/ca.zip.encrypted").Result;
                var vpnCaFiles = VpnCaFiles.LoadZip(caZipBytes, hiveLogin.VpnCredentials.CaZipKey);

                vpnCaFiles.Extract(caFolder);

                // Initialize the file paths.
                //
                // IMPORTANT:
                //
                // Do not change these file names because the [VpnCaFiles] class 
                // depends on this naming convention.

                var indexPath     = Path.Combine(caFolder, "index.txt");
                var caSignCnfPath = Path.Combine(caFolder, "ca-sign.cnf");
                var caCnfPath     = Path.Combine(caFolder, "ca.cnf");
                var caKeyPath     = Path.Combine(caFolder, "ca.key");
                var caReqPath     = Path.Combine(caFolder, "ca.req");
                var caCrtPath     = Path.Combine(caFolder, "ca.crt");
                var dhParamPath   = Path.Combine(caFolder, "dhparam.pem");
                var serverCnfPath = Path.Combine(caFolder, "server.cnf");
                var serverKeyPath = Path.Combine(caFolder, "server.key");
                var serverReqPath = Path.Combine(caFolder, "server.req");
                var serverCrtPath = Path.Combine(caFolder, "server.crt");
                var userCnfPath   = Path.Combine(caFolder, $"{username}.cnf");
                var userReqPath   = Path.Combine(caFolder, $"{username}.req");
                var userKeyPath   = Path.Combine(caFolder, $"{username}.key");
                var userCrtPath   = Path.Combine(caFolder, $"{username}.crt");
                var taKeyPath     = Path.Combine(caFolder, "ta.key");
                var crlnumberPath = Path.Combine(caFolder, "crlnumber");
                var crlPath       = Path.Combine(caFolder, "crl.pem");

                // Build the new user client login.

                File.WriteAllText(userCnfPath, GetClientConfig(hive.Definition, username, rootPrivileges));

                Program.Execute("openssl", "req", "-new",
                    "-config", userCnfPath,
                    "-keyout", userKeyPath,
                    "-out", userReqPath);

                Program.Execute("openssl", "ca", "-batch",
                    "-config", caSignCnfPath,
                    "-days", days,
                    "-out", userCrtPath,
                    "-in", userReqPath);

                // Generate the new hive login file and also write its name 
                // to [new-login.txt] so the outer shim will know what it is.

                var newLogin = hiveLogin.Clone();

                newLogin.Username                = username;
                newLogin.VpnCredentials.UserCert = VpnCaFiles.NormalizePem(File.ReadAllText(userCrtPath));
                newLogin.VpnCredentials.UserKey  = File.ReadAllText(userKeyPath);

                if (!rootPrivileges)
                {
                    newLogin.ClearRootSecrets();
                }

                File.WriteAllText($"{username}@{newLogin.HiveName}.login.json", NeonHelper.JsonSerialize(newLogin, Formatting.Indented));
                File.WriteAllText("new-login.txt", $"{username}@{newLogin.HiveName}.login.json");

                // ZIP the CA files and store them to the hive Vault.

                vpnCaFiles = VpnCaFiles.LoadFolder(caFolder);

                vpnCaFiles.Clean();
                hive.Vault.Client.WriteBytesAsync("neon-secret/vpn/ca.zip.encrypted", vpnCaFiles.ToZipBytes()).Wait();
            }
            finally
            {
                Directory.Delete(caFolder, recursive: true);
                HiveHelper.CloseHive();
            }
        }

        /// <summary>
        /// Pads the string passed with spaces so the string returned has a
        /// minimum number of characters.
        /// </summary>
        /// <param name="value">The string to be padded.</param>
        /// <param name="width">The desired string width.</param>
        /// <returns>The padded string.</returns>
        private string PadRight(string value, int width)
        {
            if (value.Length >= width)
            {
                return value;
            }

            return value + new string(' ', width - value.Length);
        }

        /// <summary>
        /// Lists the VPN user certificates.
        /// </summary>
        private void UserList()
        {
            DirectNotAllowed();
            RootLogin();
            
            var columnWidths = new int[] { "Revoked ".Length, "MM-dd-yyyy HH:mm:ss".Length, "A901C8F59E261D83".Length, "Username".Length };

            Console.WriteLine();
            Console.WriteLine($"{PadRight("Status", columnWidths[0])}   {PadRight("Valid Until", columnWidths[1])}   {PadRight("Thumbprint", columnWidths[2])}   {PadRight("Username", columnWidths[3])}");
            Console.WriteLine($"{new string('-', columnWidths[0])}   {new string('-', columnWidths[1])}   {new string('-', columnWidths[2])}   {new string('-', columnWidths[3])}");

            try
            {
                foreach (var cert in ListCerts(GetVpnCaFiles())
                    .OrderBy(c => c.Name.ToLowerInvariant())
                    .ThenBy(c => c.ValidUntil))
                {
                    if (cert.Name == "ca" || cert.Name == "server")
                    {
                        continue;
                    }

                    var status = cert.IsValid ? "Valid" : "Revoked";

                    Console.WriteLine($"{PadRight(status, columnWidths[0])}   {PadRight(cert.ValidUntil.ToString("MM-dd-yyyy HH:mm:ss"), columnWidths[1])}   {PadRight(cert.Thumbprint, columnWidths[2])}   {cert.Name}");
                }
            }
            finally
            {
                HiveHelper.CloseHive();
            }
        }

        /// <summary>
        /// Revokes a user certificate.
        /// </summary>
        /// <param name="commandLine">The command line.</param>
        private void UserRevoke(CommandLine commandLine)
        {
            DirectNotAllowed();

            var restartVpn = commandLine.HasOption("--restart-vpn");
            var thumbprint = commandLine.Arguments.FirstOrDefault();

            if (string.IsNullOrEmpty(thumbprint))
            {
                Console.Error.WriteLine("*** ERROR: THUMPRINT expected.");
                Program.Exit(1);
            }

            thumbprint = thumbprint.ToLowerInvariant();

            RootLogin();

            try
            {
                var vpnCaFiles = GetVpnCaFiles();
                var certInfo   = ListCerts(vpnCaFiles).Where(c => c.Thumbprint.ToLowerInvariant() == thumbprint).FirstOrDefault();

                if (certInfo == null)
                {
                    Console.Error.WriteLine($"*** ERROR: Certificate with thumbprint [{thumbprint}] is not known.");
                    Program.Exit(1);
                }

                if (!certInfo.IsValid)
                {
                    Console.Error.WriteLine($"*** ERROR: Certificate with thumbprint [{thumbprint}] is already revoked.");
                    Program.Exit(1);
                }

                // Initialize the file paths.
                //
                // IMPORTANT:
                //
                // Do not change these file names because the [VpnCaFiles] class 
                // depends on this naming convention.

                Directory.CreateDirectory(caFolder);

                vpnCaFiles.Extract(caFolder);

                var indexPath     = Path.Combine(caFolder, "index.txt");
                var caSignCnfPath = Path.Combine(caFolder, "ca-sign.cnf");
                var caCnfPath     = Path.Combine(caFolder, "ca.cnf");
                var caKeyPath     = Path.Combine(caFolder, "ca.key");
                var caReqPath     = Path.Combine(caFolder, "ca.req");
                var caCrtPath     = Path.Combine(caFolder, "ca.crt");
                var dhParamPath   = Path.Combine(caFolder, "dhparam.pem");
                var serverCnfPath = Path.Combine(caFolder, "server.cnf");
                var serverKeyPath = Path.Combine(caFolder, "server.key");
                var serverReqPath = Path.Combine(caFolder, "server.req");
                var serverCrtPath = Path.Combine(caFolder, "server.crt");
                var taKeyPath     = Path.Combine(caFolder, "ta.key");
                var crlnumberPath = Path.Combine(caFolder, "crlnumber");
                var crlPath       = Path.Combine(caFolder, "crl.pem");

                // Mark the certificate as revoked.

                Program.Execute("openssl", "ca",
                    "-config", caSignCnfPath,
                    "-crl_reason", "unspecified",
                    "-revoke", $"{Path.Combine(caFolder, thumbprint.ToUpperInvariant())}.pem",
                    "-cert", caCrtPath,
                    "-keyfile", caKeyPath);

                // Generate the new CRL file.

                Program.Execute("openssl", "ca",
                    "-config", caSignCnfPath,
                    "-gencrl",
                    "-out", crlPath);

                // Save the CA files back to the hive Vault.

                vpnCaFiles = VpnCaFiles.LoadFolder(caFolder);

                hive.Vault.Client.WriteBytesAsync("neon-secret/vpn/ca.zip.encrypted", vpnCaFiles.ToZipBytes()).Wait();

                // Write the updated CRL to each manager.

                var crlText = vpnCaFiles.GetFile("crl.pem");

                Console.WriteLine();

                foreach (var manager in hive.Managers)
                {
                    Console.WriteLine($"*** {manager.Name}: Revoking");
                    manager.UploadText("/etc/openvpn/crl.pem", crlText);
                    manager.SudoCommand("chmod 664 /etc/openvpn/crl.pem");
                }

                // Restart OpenVPN on each manager if requested.

                if (restartVpn)
                {
                    Console.WriteLine();

                    foreach (var manager in hive.Managers)
                    {
                        Console.WriteLine($"*** {manager.Name}: Restarting OpenVPN");
                        manager.SudoCommand("systemctl restart openvpn");
                        Thread.Sleep(TimeSpan.FromSeconds(5));
                    }
                }
            }
            finally
            {
                HiveHelper.CloseHive();
            }
        }

        /// <summary>
        /// Copies the current CRL to eack of the OpenVPN servers.
        /// </summary>
        private void UpdateCRL()
        {
            DirectNotAllowed();
            RootLogin();

            try
            {
                var vpnCaFiles = GetVpnCaFiles();
                // Initialize the file paths.
                //
                // IMPORTANT:
                //
                // Do not change these file names because the [VpnCaFiles] class 
                // depends on this naming convention.

                Directory.CreateDirectory(caFolder);

                vpnCaFiles.Extract(caFolder);

                var indexPath     = Path.Combine(caFolder, "index.txt");
                var caSignCnfPath = Path.Combine(caFolder, "ca-sign.cnf");
                var caCnfPath     = Path.Combine(caFolder, "ca.cnf");
                var caKeyPath     = Path.Combine(caFolder, "ca.key");
                var caReqPath     = Path.Combine(caFolder, "ca.req");
                var caCrtPath     = Path.Combine(caFolder, "ca.crt");
                var dhParamPath   = Path.Combine(caFolder, "dhparam.pem");
                var serverCnfPath = Path.Combine(caFolder, "server.cnf");
                var serverKeyPath = Path.Combine(caFolder, "server.key");
                var serverReqPath = Path.Combine(caFolder, "server.req");
                var serverCrtPath = Path.Combine(caFolder, "server.crt");
                var taKeyPath     = Path.Combine(caFolder, "ta.key");
                var crlnumberPath = Path.Combine(caFolder, "crlnumber");
                var crlPath       = Path.Combine(caFolder, "crl.pem");

                // Write the updated CRL to each manager.

                var crlText = vpnCaFiles.GetFile("crl.pem");

                Console.WriteLine();

                foreach (var manager in hive.Managers)
                {
                    Console.WriteLine($"*** {manager.Name}: Updating");
                    manager.UploadText("/etc/openvpn/crl.pem", crlText);
                    manager.SudoCommand("chmod 664 /etc/openvpn/crl.pem");
                }
            }
            finally
            {
                HiveHelper.CloseHive();
            }
        }
    }
}
