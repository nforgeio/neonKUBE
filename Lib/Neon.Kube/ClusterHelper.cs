//-----------------------------------------------------------------------------
// FILE:	    ClusterHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Couchbase;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Data;
using Neon.Diagnostics;
using Neon.Docker;
using Neon.Net;

// $todo(jeff.lill): 
//
// This class being static doesn't support dependency injection.  
// I'm not sure it's worth changing this now.

namespace Neon.Kube
{
    /// <summary>
    /// neonHIVE related utilties.
    /// </summary>
    public static partial class ClusterHelper
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Low-level Windows APIs.
        /// </summary>
        private static class Windows
        {
            [DllImport("advapi32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool EncryptFile(string filename);
        }

        //---------------------------------------------------------------------
        // Implementation

        private static INeonLogger      log = LogManager.Default.GetLogger(typeof(ClusterHelper));
        private static bool             remoteConnection;

        /// <summary>
        /// Explicitly sets the class <see cref="INeonLogger"/> implementation.  This defaults to
        /// a reasonable value.
        /// </summary>
        /// <param name="log"></param>
        public static void SetLogger(INeonLogger log)
        {
            Covenant.Requires<ArgumentNullException>(log != null);

            ClusterHelper.log = log;
        }

        /// <summary>
        /// Encrypts a file or directory when supported by the underlying operating system
        /// and file system.  Currently, this only works on non-HOME versions of Windows
        /// and NTFS file systems.  This fails silently.
        /// </summary>
        /// <param name="path">The file or directory path.</param>
        /// <returns><c>true</c> if the operation was successful.</returns>
        private static bool EncryptFile(string path)
        {
            try
            {
                return Windows.EncryptFile(path);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the current application is running in the special 
        /// <b>neon-cli</b> container as a shimmed application.
        /// </summary>
        public static bool InToolContainer
        {
            get { return Environment.GetEnvironmentVariable("NEON_TOOL_CONTAINER") == "1"; }
        }

        /// <summary>
        /// Returns the path the folder holding the user specific hive files such
        /// as hive logins, Ansible passwords, etc.
        /// </summary>
        /// <param name="ignoreNeonToolContainerVar">
        /// Optionally ignore the presence of a <b>NEON_TOOL_CONTAINER</b> environment 
        /// variable.  Defaults to <c>false</c>.
        /// </param>
        /// <returns>The folder path.</returns>
        /// <remarks>
        /// The actual path return depends on the presence of the <b>NEON_TOOL_CONTAINER</b>
        /// environment variable.  <b>NEON_TOOL_CONTAINER=1</b> then we're running in a 
        /// shimmed Docker container and we'll expect the hive login information to be mounted
        /// at <b>/neonhive</b>.  Otherwise, we'll return a suitable path within the 
        /// current user's home directory.
        /// </remarks>
        public static string GetHiveUserFolder(bool ignoreNeonToolContainerVar = false)
        {
            if (!ignoreNeonToolContainerVar && InToolContainer)
            {
                return "/neonhive";
            }

            if (NeonHelper.IsWindows)
            {
                var path = Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA"), "neonFORGE", "neonhive");

                Directory.CreateDirectory(path);

                try
                {
                    EncryptFile(path);
                }
                catch
                {
                    // Encryption is not available on all platforms (e.g. Windows Home, or non-NTFS
                    // file systems).  The secrets won't be encrypted for these situations.
                }

                return path;
            }
            else if (NeonHelper.IsLinux || NeonHelper.IsOSX)
            {
                return Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".neonforge", "neonhive");
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Returns the root path where the [neon run CMD ...] will copy secrets and run the command.
        /// </summary>
        /// <returns>The folder path.</returns>
        public static string GetRunFolder()
        {
            var path = Path.Combine(GetHiveUserFolder(), "run");

            Directory.CreateDirectory(path);

            return path;
        }

        /// <summary>
        /// Returns the path the folder containing login information for the known logins, creating
        /// the folder if it doesn't already exist.
        /// </summary>
        /// <returns>The folder path.</returns>
        /// <remarks>
        /// <para>
        /// This folder will exist on developer/operator workstations that have used the <b>neon-cli</b>
        /// to deploy and manage neonHIVEs.  Each known hive will have a JSON file named
        /// <b><i>hive-name</i>.json</b> holding the serialized <see cref="Kube.ClusterLogin"/> 
        /// information for the hive.
        /// </para>
        /// <para>
        /// The <b>.current</b> file (if present) specifies the name of the hive to be considered
        /// to be currently logged in.
        /// </para>
        /// </remarks>
        public static string GetLoginFolder()
        {
            var path = Path.Combine(GetHiveUserFolder(), "logins");

            Directory.CreateDirectory(path);

            return path;
        }

        /// <summary>
        /// Returns the path the neonFORGE temporary folder, creating the folder if it doesn't already exist.
        /// </summary>
        /// <returns>The folder path.</returns>
        /// <remarks>
        /// This folder will exist on developer/operator workstations that have used the <b>neon-cli</b>
        /// to deploy and manage neonHIVEs.  The client will use this to store temporary files that may
        /// include sensitive information because these folders are encrypted on disk.
        /// </remarks>
        public static string GetTempFolder()
        {
            var path = Path.Combine(GetHiveUserFolder(), "temp");

            Directory.CreateDirectory(path);

            return path;
        }

        /// <summary>
        /// Returns the path to the root folder containing the installed Ansible role files.
        /// </summary>
        /// <returns>The folder path.</returns>
        public static string GetAnsibleRolesFolder()
        {
            var path = Path.Combine(GetHiveUserFolder(), "ansible", "roles");

            Directory.CreateDirectory(path);

            return path;
        }

        /// <summary>
        /// Returns the path to the root folder containing the Ansible Vault password files.
        /// </summary>
        /// <returns>The folder path.</returns>
        public static string GetAnsiblePasswordsFolder()
        {
            var path = Path.Combine(GetHiveUserFolder(), "ansible", "passwords");

            Directory.CreateDirectory(path);

            return path;
        }

        /// <summary>
        /// Returns the path to the file indicating which hive is currently logged in.
        /// </summary>
        public static string CurrentPath
        {
            get { return Path.Combine(GetLoginFolder(), ".current"); }
        }

        /// <summary>
        /// Returns the path to the login information for the named hive.
        /// </summary>
        /// <param name="username">The operator's user name.</param>
        /// <param name="hiveName">The hive name.</param>
        /// <returns>The path to the hive's credentials file.</returns>
        public static string GetLoginPath(string username, string hiveName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(hiveName));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(username));

            return Path.Combine(GetLoginFolder(), $"{username}@{hiveName}.login.json");
        }

        /// <summary>
        /// Returns the path to the current user's hive virtual machine templates
        /// folder, creating the directory if it doesn't already exist.
        /// </summary>
        /// <returns>The path to the neonHIVE setup folder.</returns>
        public static string GetVmTemplatesFolder()
        {
            var path = Path.Combine(GetHiveUserFolder(), "vm-templates");

            Directory.CreateDirectory(path);

            return path;
        }

        /// <summary>
        /// Returns the hive login for the currently logged in hive and
        /// establishes a hive connection.
        /// </summary>
        /// <param name="noConnect">
        /// Optionally indicates that the method should not connect to the hive
        /// and should simply return the current login if one is available.
        /// </param>
        /// <param name="clientVersion">
        /// Optionally specifies the current <b>neon-cli</b> version to be checked
        /// against the hive's minimum required client.
        /// </param>
        /// <exception cref="VersionException">Thrown if the client is not capable of managing the hive.</exception>
        /// <returns>The current hive login or <c>null</c>.</returns>
        /// <remarks>
        /// <note>
        /// The tricky thing here is that the hive definition nodes 
        /// within the hive login returned will actually be loaded from
        /// the hive itself or the cached copy.
        /// </note>
        /// </remarks>
        public static ClusterLogin GetLogin(bool noConnect = false, string clientVersion = null)
        {
            if (File.Exists(CurrentPath))
            {
                var current = CurrentClusterLogin.Load();
                var login   = ClusterHelper.SplitLogin(current.Login);

                if (!login.IsOK)
                {
                    File.Delete(CurrentPath);
                    return null;
                }

                var username      = login.Username;
                var hiveName      = login.HiveName;
                var hiveLoginPath = GetLoginPath(username, hiveName);

                if (File.Exists(hiveLoginPath))
                {
                    var hiveLogin = ClusterHelper.LoadHiveLogin(username, hiveName);

                    InitLogin(hiveLogin);

                    if (noConnect)
                    {
                        return hiveLogin;
                    }

                    OpenHive(hiveLogin);

                    return hiveLogin;
                }
                else
                {
                    // The referenced hive file doesn't exist so quietly remove the ".current" file.

                    File.Delete(CurrentPath);
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the path to the cached hive definition for the named hive.
        /// </summary>
        /// <param name="username">The operator's user name.</param>
        /// <param name="hiveName">The hive name.</param>
        /// <returns>The path to the hive's credentials file.</returns>
        public static string GetCachedDefinitionPath(string username, string hiveName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(hiveName));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(username));

            return Path.Combine(GetLoginFolder(), $"{username}@{hiveName}.def.json");
        }

        /// <summary>
        /// Splits a hive login in the form of <b>USER@HIVE</b> into
        /// the operator's username and the hive name.
        /// </summary>
        /// <param name="login">The hive identifier.</param>
        /// <returns>The username and hive name parts.</returns>
        public static (bool IsOK, string Username, string HiveName) SplitLogin(string login)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(login));

            var fields = login.Split(new char[] { '@' }, 2);

            if (fields.Length != 2 || string.IsNullOrEmpty(fields[0]) || string.IsNullOrEmpty(fields[1]))
            {
                return (IsOK: false, Username: null, HiveName: null);
            }

            return (IsOK: true, Username: fields[0], HiveName: fields[1]);
        }

        /// <summary>
        /// Loads the hive login information for the current hive, performing any necessary decryption.
        /// </summary>
        /// <param name="username">The operator's user name.</param>
        /// <param name="hiveName">The name of the target hive.</param>
        /// <returns>The <see cref="Kube.ClusterLogin"/>.</returns>
        public static ClusterLogin LoadHiveLogin(string username, string hiveName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(username));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(hiveName));

            var path      = Path.Combine(GetLoginFolder(), $"{username}@{hiveName}.login.json");
            var hiveLogin = NeonHelper.JsonDeserialize<ClusterLogin>(File.ReadAllText(path));

            hiveLogin.Path = path;

            return hiveLogin;
        }

        /// <summary>
        /// Validates a certificate name and returns the full path of its Vault key.
        /// </summary>
        /// <param name="name">The certificate name.</param>
        /// <returns>The fully qualified certificate key.</returns>
        /// <exception cref="ArgumentException">Thrown if the certificate name is not valid.</exception>
        /// <remarks>
        /// Reports and exits the application for invalid certificate names.
        /// </remarks>
        public static string GetVaultCertificateKey(string name)
        {
            if (!ClusterDefinition.IsValidName(name))
            {
                throw new ArgumentException($"[{name}] is not a valid certificate name.  Only letters, numbers, periods, dashes, and underscores are allowed.");
            }

            return "neon-secret/cert/" + name;
        }

        /// <summary>
        /// Returns the hive's Vault URI.
        /// </summary>
        public static Uri VaultUri
        {
            get
            {
                var vaultUri = Environment.GetEnvironmentVariable("VAULT_ADDR");

                if (string.IsNullOrEmpty(vaultUri))
                {
                    throw new NotSupportedException("Cannot access hive Vault because the [VAULT_ADDR] environment variable is not defined.");
                }

                return new Uri(vaultUri);
            }
        }

        /// <summary>
        /// Returns the hive's Consul URI.
        /// </summary>
        public static Uri ConsulUri
        {
            get
            {
                var consulUri = Environment.GetEnvironmentVariable("CONSUL_HTTP_FULLADDR");

                if (string.IsNullOrEmpty(consulUri))
                {
                    throw new NotSupportedException("Cannot access hive Consul because the [CONSUL_HTTP_FULLADDR] environment variable is not defined.");
                }

                return new Uri(Environment.GetEnvironmentVariable("CONSUL_HTTP_FULLADDR"));
            }
        }

        /// <summary>
        /// Indicates whether the application is connected to thje cluster.
        /// </summary>
        public static bool IsConnected { get; private set; } = false;

        /// <summary>
        /// Returns the <see cref="Kube.ClusterLogin"/> for the opened hive. 
        /// </summary>
        public static ClusterLogin HiveLogin { get; private set; } = null;

        /// <summary>
        /// Returns the <see cref="ClusterProxy"/> for the opened hive.
        /// </summary>
        public static ClusterProxy Hive { get; private set; } = null;

        /// <summary>
        /// Simulates connecting the current application to the hive.
        /// </summary>
        /// <param name="login">The hive login information.</param>
        /// <returns>The <see cref="ClusterProxy"/>.</returns>
        public static ClusterProxy OpenHive(ClusterLogin login)
        {
            if (IsConnected)
            {
                return ClusterHelper.Hive;
            }

            log.LogInfo(() => $"Connecting to [{login.Username}@{login.ClusterName}].");

            HiveLogin        = login;
            remoteConnection = true;

            OpenHive(
                new ClusterProxy(HiveLogin,
                    (name, publicAddress, privateAddress, appendLog) =>
                    {
                        var proxy = new SshProxy<NodeDefinition>(name, publicAddress, privateAddress, HiveLogin.GetSshCredentials(), null);

                        proxy.RemotePath += $":{ClusterHostFolders.Setup}";
                        proxy.RemotePath += $":{ClusterHostFolders.Tools}";

                        return proxy;
                    }));

            return ClusterHelper.Hive;
        }

        /// <summary>
        /// <para>
        /// Connects the current application to the hive.  This only works for applications
        /// actually running in the hive.
        /// </para>
        /// <note>
        /// This should only be called by services that are actually deployed in running 
        /// hive containers that have mapped in the hive node environment variables
        /// and host DNS mappings from <b>/etc/neon/host-env</b>.
        /// </note>
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the current process does not appear to be running as a hive container
        /// with the node environment variables mapped in.
        /// </exception>
        /// <returns>The <see cref="ClusterProxy"/>.</returns>
        /// <remarks>
        /// </remarks>
        public static ClusterProxy OpenHive()
        {
            log.LogInfo(() => "Connecting to hive.");

            if (IsConnected)
            {
                return Hive;
            }

            remoteConnection = false;

            var sshCredentials = SshCredentials.None;

            // Load the hive definition from Consul and initialize the [Hive] property.
            // Note that we need to hack [GetDefinitionAsync()] into believing that the 
            // hive is already connected for this to work.

            ClusterDefinition definition = null;    // $todo(jeff.lill): FIX THIS

            try
            {
                IsConnected = true;
            }
            finally
            {
                IsConnected = false;
            }

            log.LogInfo(() => $"Connecting to [{definition.Name}].");

            HiveLogin = new ClusterLogin()
            {
                Definition = definition
            };

            var hive = OpenHive(
                new ClusterProxy(HiveLogin,
                    (name, publicAddress, privateAddress, appendLog) =>
                    {
                        var proxy = new SshProxy<NodeDefinition>(name, publicAddress, privateAddress, sshCredentials, null);

                        proxy.RemotePath += $":{ClusterHostFolders.Setup}";
                        proxy.RemotePath += $":{ClusterHostFolders.Tools}";

                        return proxy;
                    }));

            log.LogInfo(() => $"Connected to [{definition.Name}].");

            return hive;
        }

        /// <summary>
        /// Looks for a certificate with a friendly name.
        /// </summary>
        /// <param name="store">The certificate store.</param>
        /// <param name="friendlyName">The case insensitive friendly name.</param>
        /// <returns>The certificate or <c>null</c> if one doesn't exist by the name.</returns>
        private static X509Certificate2 FindCertificateByFriendlyName(X509Store store, string friendlyName)
        {
            Covenant.Requires<ArgumentNullException>(store != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(friendlyName));

            foreach (var certificate in store.Certificates)
            {
                if (friendlyName.Equals(certificate.FriendlyName, StringComparison.InvariantCultureIgnoreCase))
                {
                    return certificate;
                }
            }

            return null;
        }

        /// <summary>
        /// Ensures that a hive login is properly initialized on the current machine.
        /// </summary>
        /// <param name="login">The login.</param>
        internal static void InitLogin(ClusterLogin login)
        {
            Covenant.Requires<ArgumentNullException>(login != null);

            if (login.InitMachine)
            {
                return; // Already initialized.
            }

            // Ensure that the required hive certificates are trusted.

            if (NeonHelper.IsWindows)
            {
                // We're going to perist or update these in the Windows certificate store 
                // using the certificate friendly names.

                using (var store = new X509Store(StoreName.AuthRoot, StoreLocation.LocalMachine))
                {
                    store.Open(OpenFlags.ReadWrite);

                    foreach (var certificate in login.ClientCertificates)
                    {
                        if (string.IsNullOrEmpty(certificate.FriendlyName))
                        {
                            // We'll see this happen for old, pre-release logins created before
                            // 08-15-2017 when we added the [TlsCertificate.FriendlyName] parameter.
                            // Those logins are no longer supported but we'll silently ignore
                            // the problems.

                            continue;
                        }

                        var certificateX509 = certificate.ToX509Certificate2();
                        var existingX509    = FindCertificateByFriendlyName(store, certificate.FriendlyName);

                        if (existingX509 == null || existingX509.Thumbprint != certificateX509.Thumbprint)
                        {
                            if (existingX509 != null)
                            {
                                store.Remove(existingX509);
                            }

                            store.Add(certificateX509);
                        }
                    }
                }
            }
            else if (NeonHelper.IsOSX)
            {
                throw new NotImplementedException("$todo(jeff.lill): IMPLEMENT THIS!");
            }
            else if (NeonHelper.IsLinux && InToolContainer)
            {
                // We're probably running as [neon-cli] within a shimmed Docker
                // container so we'll need to trust the hive certificates.
                // We're going to assume that the container provides the
                // [update-ca-certificates] tool.

                var certNumber = 0;

                foreach (var certificate in login.ClientCertificates)
                {
                    File.WriteAllText(Path.Combine("/usr/local/share/ca-certificates", $"hive-cert-{certNumber}.crt"), certificate.CertPemNormalized);
                    certNumber++;
                }

                NeonHelper.ExecuteCapture("update-ca-certificates", (string)null).EnsureSuccess();
            }
            else if (NeonHelper.IsLinux)
            {
                // $todo(jeff.lill):
                //
                // We'll land here if we're actually running on the hive or
                // when/if we support [neon-cli] on Linux workstations.  We'll
                // need to detect which is the case (perhaps with a new parameter
                // or environment variable).
                //
                // For [neon-cli], we'll need to ensure that the hive certificates
                // are trusted.  For Docker containers, we're going to assume 
                // that the host node mounted its certificates into the container
                // and that the container entrypoint script loaded them.
            }

            login.InitMachine = true;
        }

        /// <summary>
        /// Connects to a hive using a <see cref="ClusterProxy"/>.  Note that this version does not
        /// fully initialize the <see cref="HiveLogin"/> property.
        /// </summary>
        /// <param name="hive">The hive proxy.</param>
        /// <returns>The <see cref="ClusterProxy"/>.</returns>
        public static ClusterProxy OpenHive(ClusterProxy hive)
        {
            Covenant.Requires<ArgumentNullException>(hive != null);

            InitLogin(hive.HiveLogin);

            // Make this current login.

            if (IsConnected)
            {
                return ClusterHelper.Hive;
            }

            IsConnected = true;
            Hive        = hive;

           if (HiveLogin == null)
            {
                HiveLogin =
                    new ClusterLogin()
                    {
                        Definition = hive.Definition
                    };
            }

            // Initialize some properties.

            var clusterDefinition = Hive.Definition;
            var healthyManager = Hive.GetReachableManager(ReachableHostMode.ReturnFirst);

            // Simulate the environment variables initialized by a mounted [host-env] script.

            var hostingProvider = string.Empty;

            if (hive.Definition.Hosting != null)
            {
                hostingProvider = hive.Definition.Hosting.Environment.ToString().ToLowerInvariant();
            }

            Environment.SetEnvironmentVariable("NEON_HIVE", clusterDefinition.Name);
            Environment.SetEnvironmentVariable("NEON_DATACENTER", clusterDefinition.Datacenter);
            Environment.SetEnvironmentVariable("NEON_ENVIRONMENT", clusterDefinition.Environment.ToString().ToUpperInvariant());
            Environment.SetEnvironmentVariable("NEON_HOSTING", hostingProvider);
            Environment.SetEnvironmentVariable("NEON_NODE_NAME", healthyManager.Name);
            Environment.SetEnvironmentVariable("NEON_NODE_ROLE", healthyManager.Metadata.Role);
            Environment.SetEnvironmentVariable("NEON_NODE_IP", healthyManager.Metadata.PrivateAddress.ToString());

            return ClusterHelper.Hive;
        }

        /// <summary>
        /// Disconnects the current hive (if there is one).
        /// </summary>
        public static void CloseHive()
        {
            if (!IsConnected)
            {
                return;
            }

            Hive             = null;
            IsConnected      = false;
            remoteConnection = false;
        }

        /// <summary>
        /// Removes any local references to hives that are not referenced by a hive
        /// login for the current user.  This removes things like DNS records in the
        /// local <b>hosts</b> file or any trusted hive certificates.
        /// </summary>
        public static void CleanHiveReferences()
        {
            // Scan the hive login files for the current user and create a hashset
            // with the current hive names.

            var hiveNames = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var loginPath in Directory.GetFiles(ClusterHelper.GetLoginFolder(), "*.login.json", SearchOption.TopDirectoryOnly))
            {
                var login = NeonHelper.JsonDeserialize<ClusterLogin>(File.ReadAllText(loginPath));

                if (!hiveNames.Contains(login.Definition.Name))
                {
                    hiveNames.Add(login.Definition.Name);
                }
            }

            // Scan the platform certificate store for certificates belonging to
            // hives without a local login and remove them.  We're going to depend
            // on being able to identify hive certificates by parsing friendly
            // names like:
            //
            //      neonHIVE: HIVENAME

            if (NeonHelper.IsWindows)
            {
                using (var store = new X509Store(StoreName.AuthRoot, StoreLocation.LocalMachine))
                {
                    store.Open(OpenFlags.ReadWrite);

                    foreach (var certificate in store.Certificates)
                    {
                        if (!string.IsNullOrEmpty(certificate.FriendlyName) && certificate.FriendlyName.StartsWith("neonHIVE:"))
                        {
                            var posColon = certificate.FriendlyName.IndexOf(':');

                            if (posColon != -1)
                            {
                                var certHiveName = certificate.FriendlyName.Substring(posColon + 1).Trim();

                                if (!hiveNames.Contains(certHiveName))
                                {
                                    // There's no login for this certificate, so delete it.

                                    store.Remove(certificate);
                                }
                            }
                        }
                    }
                }
            }
            else if (NeonHelper.IsOSX)
            {
                throw new NotImplementedException("$todo(jeff.lill): Implement this for OSX.");
            }
            else
            {
                throw new NotImplementedException();
            }

            // Scan the [host] sections for section names like:
            //
            //      hive-HIVENAME
            //
            // and then extract the hivenames and then delete any sections
            // for which there is no local hive.  Note that the sections
            // will be listed in uppercase.

            foreach (var section in NetHelper.ListLocalHostsSections())
            {
                if (section.StartsWith("hive-", StringComparison.InvariantCultureIgnoreCase))
                {
                    var hiveName = section.Substring("hive-".Length);

                    if (!hiveNames.Contains(hiveName))
                    {
                        NetHelper.ModifyLocalHosts(section: $"hive-{hiveName}");
                    }
                }
            }
        }

        /// <summary>
        /// Verifies that a hive is connected.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when a hive is not connected.</exception>
        private static void VerifyConnected()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Hive is not connected.");
            }
        }

        /// <summary>
        /// Returns a client that can access Docker on the current machine.
        /// </summary>
        /// <returns>A <see cref="DockerClient"/>.</returns>
        public static DockerClient OpenDocker()
        {
            VerifyConnected();

            DockerSettings  settings; 

            if (remoteConnection)
            {
                settings = new DockerSettings(Hive.GetReachableManager().PrivateAddress);
            }
            else
            {
                settings = new DockerSettings("unix:///var/run/docker.sock");
            }

            return new DockerClient(settings);
        }

        /// <summary>
        /// Determines whether a hostname refers to the Docker public registry.
        /// </summary>
        /// <param name="hostname">The hostname being tested.</param>
        /// <returns>><c>true</c> for the public registry.</returns>
        public static bool IsDockerPublicRegistry(string hostname)
        {
            if (hostname == null)
            {
                return false;
            }

            return hostname.Equals(ClusterConst.DockerPublicRegistry, StringComparison.InvariantCultureIgnoreCase);
        }
    }
}
