//-----------------------------------------------------------------------------
// FILE:	    HiveHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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
using Consul;
using Newtonsoft.Json;
using RabbitMQ.Client;

using Neon.Common;
using Neon.Data;
using Neon.Diagnostics;
using Neon.Docker;
using Neon.HiveMQ;
using Neon.Net;

// $todo(jeff.lill): 
//
// This class being static doesn't support dependency injection.  
// I'm not sure it's worth changing this now.

namespace Neon.Hive
{
    /// <summary>
    /// neonHIVE related utilties.
    /// </summary>
    public static partial class HiveHelper
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

        private static INeonLogger                  log = LogManager.Default.GetLogger(typeof(HiveHelper));
        private static Dictionary<string, string>   secrets;
        private static Dictionary<string, string>   configs;
        private static bool                         remoteConnection;
        private static bool                         enableSecretRetrival;
        private static ConsulClient                 sharedConsul;

        /// <summary>
        /// Explicitly sets the class <see cref="INeonLogger"/> implementation.  This defaults to
        /// a reasonable value.
        /// </summary>
        /// <param name="log"></param>
        public static void SetLogger(INeonLogger log)
        {
            Covenant.Requires<ArgumentNullException>(log != null);

            HiveHelper.log = log;
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
        /// <b><i>hive-name</i>.json</b> holding the serialized <see cref="Hive.HiveLogin"/> 
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
        /// Ensures that the client version specified is capable of managing a hive.
        /// </summary>
        /// <param name="login">The target hive login.</param>
        /// <param name="clientVersion">The optional client version string.</param>
        /// <exception cref="VersionException">Thrown if the client is not capable of managing the hive.</exception>
        /// <remarks>
        /// <note>
        /// No compatiblility check is performed if <paramref name="clientVersion"/> is passed
        /// as <c>null</c> or empty.
        /// </note>
        /// </remarks>
        public static void ValidateClientVersion(HiveLogin login, string clientVersion = null)
        {
            Covenant.Requires<ArgumentNullException>(login != null);

            // Ensure that the current version of the client is compatible with
            // the connected hive.

            if (!string.IsNullOrEmpty(clientVersion) && login.Definition.Summary != null)
            {
                // Client versions may include suffixes like: "-rc0", "-beta" or "-preview", etc.
                // We're going to ignore these, strip off the dash and anything after and just 
                // compare the version numbers.

                var curVersion = clientVersion;
                var minVersion = login.Definition.Summary.NeonCliVersion;
                var dashPos    = curVersion.IndexOf('-');

                if (dashPos != -1)
                {
                    curVersion = curVersion.Substring(0, dashPos);
                }

                dashPos = minVersion.IndexOf('-');

                if (dashPos != -1)
                {
                    minVersion = minVersion.Substring(0, dashPos);
                }

                if (new Version(curVersion) < new Version(minVersion))
                {
                    throw new HiveException($"neon-cli v{clientVersion} cannot manage hive [{login.Definition.Name}].  Use neon-cli v{login.Definition.Summary.NeonCliVersion} or greater.");
                }
            }
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
        public static HiveLogin GetLogin(bool noConnect = false, string clientVersion = null)
        {
            if (File.Exists(CurrentPath))
            {
                var current = CurrentHiveLogin.Load();
                var login   = HiveHelper.SplitLogin(current.Login);

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
                    var hiveLogin = HiveHelper.LoadHiveLogin(username, hiveName);

                    hiveLogin.ViaVpn = current.ViaVpn && hiveLogin.VpnCredentials != null;

                    InitLogin(hiveLogin);

                    if (noConnect)
                    {
                        return hiveLogin;
                    }

                    OpenHive(hiveLogin);

                    var liveDefinition = GetLiveDefinition(username, hiveName);

                    hiveLogin.Definition.NodeDefinitions = liveDefinition.NodeDefinitions;
                    hiveLogin.Definition.Summary         = liveDefinition.Summary;

                    ValidateClientVersion(hiveLogin, clientVersion);

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
        /// Returns the current hive definition from the hive if we're
        /// currently logged in.
        /// </summary>
        /// <param name="username">The operator's user name.</param>
        /// <param name="hiveName">The hive name.</param>
        /// <returns>The current hive definition or <c>null</c>.</returns>
        public static HiveDefinition GetLiveDefinition(string username, string hiveName)
        {
            var hiveLoginPath = GetLoginPath(username, hiveName);

            if (!File.Exists(hiveLoginPath))
            {
                return null;    // We're not logged in.
            }

            // We're going to optimize the retrieval by attempting to cache
            // a copy of the hive definition obtained from the hive and
            // then comparing hashes of the cached definition with the hash
            // saved on the server to determine whether we really need to
            // download the whole definition again.
            //
            // This should make make the [neon-cli] a lot snappier because
            // hive definitions will change relatively infrequently for
            // many hives.

            var cachedDefinitionPath = GetCachedDefinitionPath(username, hiveName);
            var cachedDefinition     = (HiveDefinition)null;

            if (File.Exists(cachedDefinitionPath))
            {
                try
                {
                    cachedDefinition = NeonHelper.JsonDeserialize<HiveDefinition>(File.ReadAllText(cachedDefinitionPath));
                }
                catch
                {
                    // It appears that the cached definition may be corrupted so delete it.

                    try
                    {
                        File.Delete(cachedDefinitionPath);
                    }
                    catch
                    {
                        // Intentionally ignoring this.
                    }
                }
            }

            var hiveDefinition = GetDefinitionAsync(cachedDefinition).Result;

            if (!object.ReferenceEquals(hiveDefinition, cachedDefinition))
            {
                // It looks like we received an updated defintion from the hive
                // so cache it.

                File.WriteAllText(cachedDefinitionPath, NeonHelper.JsonSerialize(hiveDefinition, Formatting.Indented));
            }

            return hiveDefinition;
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
        /// <returns>The <see cref="Hive.HiveLogin"/>.</returns>
        public static HiveLogin LoadHiveLogin(string username, string hiveName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(username));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(hiveName));

            var path      = Path.Combine(GetLoginFolder(), $"{username}@{hiveName}.login.json");
            var hiveLogin = NeonHelper.JsonDeserialize<HiveLogin>(File.ReadAllText(path));

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
            if (!HiveDefinition.IsValidName(name))
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
        /// Indicates whether the application is running outside of a Docker container
        /// but we're going to try to simulate the environment such that the application
        /// believe it is running in a container within a Docker hive.  See 
        /// <see cref="OpenHiveRemote(DebugSecrets, DebugConfigs, string, bool)"/> 
        /// for more information.
        /// </summary>
        public static bool IsConnected { get; private set; } = false;

        /// <summary>
        /// Returns the <see cref="Hive.HiveLogin"/> for the opened hive. 
        /// </summary>
        public static HiveLogin HiveLogin { get; private set; } = null;

        /// <summary>
        /// Returns the <see cref="HiveProxy"/> for the opened hive.
        /// </summary>
        public static HiveProxy Hive { get; private set; } = null;

        /// <summary>
        /// Simulates running the current application within the currently logged-in
        /// neonHIVE for external tools as well as for development and debugging purposes.
        /// </summary>
        /// <param name="secrets">Optional emulated Docker secrets.</param>
        /// <param name="configs">Optional emulated Docker configs.</param>
        /// <param name="loginPath">
        /// Optional path to a specific hive login to override the current login.  This can
        /// specify the path to the login file or be just identify the login like <b>root@myhive</b>.
        /// </param>
        /// <param name="noVpn">Optionally specifies that the hive VPN should be ignored.</param>
        /// <returns>The <see cref="HiveProxy"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if a hive is already connected.</exception>
        /// <remarks>
        /// <note>
        /// This method requires elevated administrative permissions for the local operating
        /// system.
        /// </note>
        /// <note>
        /// Take care to call <see cref="CloseHive()"/> just before your application
        /// exits to reset any temporary settings.
        /// </note>
        /// <note>
        /// This method currently simulates running the application on a hive manager node.
        /// In the future, we may provide a way to simulate running on a specific node.
        /// </note>
        /// <para>
        /// In an ideal world, Microsoft/Docker would provide a way to deploy, run and
        /// debug applications into an existing Docker hive as a container or swarm
        /// mode service.  At this time, there are baby steps in this direction: it's
        /// possible to F5 an application into a standalone container but this method
        /// currently supports running the application directly on Windows while trying
        /// to emulate some of the hive environment.  Eventually, it will be possible
        /// to do the same in a local Docker container.
        /// </para>
        /// <para>
        /// This method provides a somewhat primitive simulation of running within a
        /// hive by:
        /// </para>
        /// <list type="bullet">
        ///     <item>
        ///     Simulating the presence of a mounted and executed <b>/etc/neon/host-env</b> script.
        ///     </item>
        ///     <item>
        ///     Emulated mounted Docker secrets via <paramref name="secrets"/> 
        ///     and using <see cref="GetSecret(string)"/>.
        ///     </item>
        ///     <item>
        ///     Emulated mounted Docker secrets via <paramref name="configs"/> 
        ///     and using <see cref="GetConfig(string)"/>.
        ///     </item>
        ///     <item>
        ///     Temporarily modifying the local <b>hosts</b> file to add host entries
        ///     for local services like Vault and Consul.
        ///     </item>
        /// </list>
        /// <note>
        /// This is also useful for external tools that are not executed on a hive node
        /// (such as the <b>neon-cli</b>).  For example, class configures the local <n>hosts</n>
        /// file such that we'll be able to access the hive Vault and Consul servers over
        /// TLS.
        /// </note>
        /// <para>
        /// Optionally pass a <see cref="DebugSecrets"/> instance as <paramref name="secrets"/> to emulate 
        /// the Docker secrets feature.  <see cref="DebugSecrets"/> may specify secrets as simple
        /// name/value pairs or may specify more complex Vault or Consul credentials.
        /// </para>
        /// <para>
        /// Optionally pass a <see cref="DebugConfigs"/> instance as <paramref name="configs"/> to emulate 
        /// the Docker configs feature.  <see cref="DebugConfigs"/> specifies Docker configs as simple
        /// name/valie pairs.
        /// </para>
        /// <note>
        /// Applications may wish to use <see cref="NeonHelper.IsDevWorkstation"/> to detect when
        /// the application is running outside of a production environment and call this method
        /// early during application initialization.  <see cref="NeonHelper.IsDevWorkstation"/>
        /// uses the presence of the <b>DEV_WORKSTATION</b> environment variable to determine 
        /// this.
        /// </note>
        /// </remarks>
        public static HiveProxy OpenHiveRemote(DebugSecrets secrets = null, DebugConfigs configs = null, string loginPath = null, bool noVpn = false)
        {
            if (IsConnected)
            {
                return HiveHelper.Hive;
            }

            if (loginPath != null)
            {
                if (!File.Exists(loginPath))
                {
                    // Look for a user login instead.

                    var login = SplitLogin(loginPath);

                    if (login.IsOK)
                    {
                        var path = GetLoginPath(login.Username, login.HiveName);

                        if (File.Exists(path))
                        {
                            loginPath = path;
                        }
                    }
                }

                if (!File.Exists(loginPath))
                {
                    throw new HiveException($"Login [{loginPath}] not found.");
                }

                HiveLogin      = NeonHelper.JsonDeserialize<HiveLogin>(File.ReadAllText(loginPath));
                HiveLogin.Path = loginPath;
            }
            else
            {
                HiveLogin = HiveHelper.GetLogin();

                if (HiveLogin == null)
                {
                    throw new InvalidOperationException("Connect failed due to not being logged into a hive.");
                }
            }

            log.LogInfo(() => $"Connecting to hive [{HiveLogin}].");

            if (!noVpn)
            {
                // It is possible for the VPN to be disconnected even if there's a current
                // hive login.  We need to ensure that we're connected if the hive was 
                // deployed with a VPN and wr're not explicitly ignoring that.

                var useVpn = false;

                if (HiveLogin.Definition.Hosting.IsOnPremiseProvider)
                {
                    useVpn = HiveLogin.Definition.Vpn.Enabled;
                }
                else
                {
                    useVpn = true; // Always TRUE for cloud environments.
                }

                if (useVpn)
                {
                    var errorMessage = (string)null;

                    HiveHelper.VpnOpen(HiveLogin, onError: message => errorMessage = message);

                    if (errorMessage != null)
                    {
                        throw new HiveException($"Hive VPN connection failed: {errorMessage}");
                    }
                }
            }

            OpenHive(
                new HiveProxy(HiveLogin,
                    (name, publicAddress, privateAddress, appendLog) =>
                    {
                        var proxy = new SshProxy<NodeDefinition>(name, publicAddress, privateAddress, HiveLogin.GetSshCredentials(), null);

                        proxy.RemotePath += $":{HiveHostFolders.Setup}";
                        proxy.RemotePath += $":{HiveHostFolders.Tools}";

                        return proxy;
                    }));

            HiveHelper.IsConnected      = true;
            HiveHelper.remoteConnection = true;

            // Support emulated secrets and configs too.

            secrets?.Realize(Hive, HiveLogin);
            configs?.Realize(Hive, HiveLogin);

            HiveHelper.secrets = secrets ?? new DebugSecrets();
            HiveHelper.configs = configs ?? new DebugConfigs();

            return HiveHelper.Hive;
        }

        /// <summary>
        /// Simulates connecting the current application to the hive.
        /// </summary>
        /// <param name="login">The hive login information.</param>
        /// <returns>The <see cref="HiveProxy"/>.</returns>
        public static HiveProxy OpenHive(HiveLogin login)
        {
            if (IsConnected)
            {
                return HiveHelper.Hive;
            }

            log.LogInfo(() => $"Connecting to [{login.Username}@{login.HiveName}].");

            HiveLogin        = login;
            remoteConnection = true;

            OpenHive(
                new HiveProxy(HiveLogin,
                    (name, publicAddress, privateAddress, appendLog) =>
                    {
                        var proxy = new SshProxy<NodeDefinition>(name, publicAddress, privateAddress, HiveLogin.GetSshCredentials(), null);

                        proxy.RemotePath += $":{HiveHostFolders.Setup}";
                        proxy.RemotePath += $":{HiveHostFolders.Tools}";

                        return proxy;
                    }));

            return HiveHelper.Hive;
        }

        /// <summary>
        /// <para>
        /// Connects the current application to the hive.  This only works for applications
        /// actually running in the hive.  Use <see cref="OpenHiveRemote(DebugSecrets, DebugConfigs, string, bool)"/>
        /// when running applications remotely.
        /// </para>
        /// <note>
        /// This should only be called by services that are actually deployed in running 
        /// hive containers that have mapped in the hive node environment variables
        /// and host DNS mappings from <b>/etc/neon/host-env</b>.
        /// </note>
        /// </summary>
        /// <param name="sshCredentialsSecret">Optionally identifies the Docker secret the hive SSH credentials.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the current process does not appear to be running as a hive container
        /// with the node environment variables mapped in.
        /// </exception>
        /// <returns>The <see cref="HiveProxy"/>.</returns>
        /// <remarks>
        /// <para>
        /// The <paramref name="sshCredentialsSecret"/> parameter optionally specifies the name
        /// of the Docker secret containing the hive's SSH credentials formatted as: <b>username/password</b>.  
        /// These credentials are required to be able to open SSH/SCP connections to hive nodes.
        /// </para>
        /// </remarks>
        public static HiveProxy OpenHive(string sshCredentialsSecret = null)
        {
            log.LogInfo(() => "Connecting to hive.");

            if (IsConnected)
            {
                return Hive;
            }

            remoteConnection = false;

            var sshCredentials = SshCredentials.None;

            if (!string.IsNullOrEmpty(sshCredentialsSecret))
            {
                var credentials = GetSecret(sshCredentialsSecret);

                if (!string.IsNullOrEmpty(credentials))
                {
                    var fields = credentials.Trim().Split(new char[] { '/' }, 2);

                    sshCredentials = SshCredentials.FromUserPassword(fields[0], fields[1]);
                }
            }

            // Load the hive definition from Consul and initialize the [Hive] property.
            // Note that we need to hack [GetDefinitionAsync()] into believing that the 
            // hive is already connected for this to work.

            HiveDefinition definition;

            try
            {
                IsConnected = true;
                definition  = GetDefinitionAsync().Result;
            }
            finally
            {
                IsConnected = false;
            }

            log.LogInfo(() => $"Connecting to [{definition.Name}].");

            HiveLogin = new HiveLogin()
            {
                Definition = definition
            };

            var hive = OpenHive(
                new HiveProxy(HiveLogin,
                    (name, publicAddress, privateAddress, appendLog) =>
                    {
                        var proxy = new SshProxy<NodeDefinition>(name, publicAddress, privateAddress, sshCredentials, null);

                        proxy.RemotePath += $":{HiveHostFolders.Setup}";
                        proxy.RemotePath += $":{HiveHostFolders.Tools}";

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
        internal static void InitLogin(HiveLogin login)
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
        /// Connects to a hive using a <see cref="HiveProxy"/>.  Note that this version does not
        /// fully initialize the <see cref="HiveLogin"/> property.
        /// </summary>
        /// <param name="hive">The hive proxy.</param>
        /// <returns>The <see cref="HiveProxy"/>.</returns>
        public static HiveProxy OpenHive(HiveProxy hive)
        {
            Covenant.Requires<ArgumentNullException>(hive != null);

            InitLogin(hive.HiveLogin);

            // Make this current login.

            if (IsConnected)
            {
                return HiveHelper.Hive;
            }

            IsConnected = true;
            Hive        = hive;

           if (HiveLogin == null)
            {
                HiveLogin =
                    new HiveLogin()
                    {
                        Definition = hive.Definition
                    };
            }

            // Initialize some properties.

            var hiveDefinition = Hive.Definition;
            var healthyManager = Hive.GetReachableManager(ReachableHostMode.ReturnFirst);

            // Simulate the environment variables initialized by a mounted [host-env] script.

            var hostingProvider = string.Empty;

            if (hive.Definition.Hosting != null)
            {
                hostingProvider = hive.Definition.Hosting.Environment.ToString().ToLowerInvariant();
            }

            Environment.SetEnvironmentVariable("NEON_HIVE", hiveDefinition.Name);
            Environment.SetEnvironmentVariable("NEON_DATACENTER", hiveDefinition.Datacenter);
            Environment.SetEnvironmentVariable("NEON_ENVIRONMENT", hiveDefinition.Environment.ToString().ToUpperInvariant());
            Environment.SetEnvironmentVariable("NEON_HOSTING", hostingProvider);
            Environment.SetEnvironmentVariable("NEON_NODE_NAME", healthyManager.Name);
            Environment.SetEnvironmentVariable("NEON_NODE_ROLE", healthyManager.Metadata.Role);
            Environment.SetEnvironmentVariable("NEON_NODE_IP", healthyManager.Metadata.PrivateAddress.ToString());
            Environment.SetEnvironmentVariable("NEON_NODE_SSD", healthyManager.Metadata.Labels.StorageSSD ? "true" : "false");
            Environment.SetEnvironmentVariable("VAULT_ADDR", $"{hiveDefinition.GetVaultDirectUri(healthyManager.Name)}");
            Environment.SetEnvironmentVariable("VAULT_DIRECT_ADDR", $"{hiveDefinition.GetVaultDirectUri(healthyManager.Name)}");

            if (hiveDefinition.Consul.Tls)
            {
                Environment.SetEnvironmentVariable("CONSUL_HTTP_SSL", "true");
                Environment.SetEnvironmentVariable("CONSUL_HTTP_ADDR", $"{hiveDefinition.Hostnames.Consul}:{hiveDefinition.Consul.Port}");
                Environment.SetEnvironmentVariable("CONSUL_HTTP_FULLADDR", $"https://{hiveDefinition.Hostnames.Consul}:{hiveDefinition.Consul.Port}");
            }
            else
            {
                Environment.SetEnvironmentVariable("CONSUL_HTTP_SSL", "false");
                Environment.SetEnvironmentVariable("CONSUL_HTTP_ADDR", $"{hiveDefinition.Hostnames.Consul}:{hiveDefinition.Consul.Port}");
                Environment.SetEnvironmentVariable("CONSUL_HTTP_FULLADDR", $"{hiveDefinition.Consul.Scheme}://{hiveDefinition.Hostnames.Consul}:{hiveDefinition.Consul.Port}");
            }

            Environment.SetEnvironmentVariable("NEON_APT_PROXY", GetPackageProxyReferences(hiveDefinition));

            // Update the local DNS resolver hosts file so we'll be able resolve
            // common hive hostnames.

            var hosts = new Dictionary<string, IPAddress>();

            hosts.Add(hiveDefinition.Hostnames.Consul, healthyManager.PrivateAddress);
            hosts.Add(hiveDefinition.Hostnames.Vault, healthyManager.PrivateAddress);

            foreach (var manager in hiveDefinition.Managers)
            {
                hosts.Add($"{manager.Name}.{hiveDefinition.Hostnames.Vault}", IPAddress.Parse(manager.PrivateAddress));
            }

            foreach (var node in hiveDefinition.Nodes)
            {
                hosts.Add($"{node.Name}.{hiveDefinition.Hostnames.Base}", IPAddress.Parse(node.PrivateAddress));
            }

            // NOTE:
            //
            // Non-bootstrap HiveMQ settings select up to 10 hive host nodes to be
            // the targets for HiveMQ related requests relying on a private load
            // balancer rule to forward traffic from the Docker ingress network
            // through to the actual HiveMQ cluster nodes.
            //
            // Note that these settings reference the fully qualidied HiveMQ host
            // name so we'll be prepared for TLS support in the future.  This means
            // that we need to generate a local HiveMQ DNS definition for every node
            // in the cluster (rather than just for the nodes actually hosting HiveMQ).

            foreach (var node in hiveDefinition.Nodes)
            {
                hosts.Add($"{node.Name}.{hiveDefinition.Hostnames.HiveMQ}", IPAddress.Parse(node.PrivateAddress));
            }

            hosts.Add(hiveDefinition.Hostnames.LogEsData, healthyManager.PrivateAddress);

            NetHelper.ModifyLocalHosts(hosts, section: $"hive-{hiveDefinition.Name}");

            HiveHelper.secrets = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            HiveHelper.configs = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

            return HiveHelper.Hive;
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

            Hive                 = null;
            IsConnected          = false;
            remoteConnection     = false;
            enableSecretRetrival = false;
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

            foreach (var loginPath in Directory.GetFiles(HiveHelper.GetLoginFolder(), "*.login.json", SearchOption.TopDirectoryOnly))
            {
                var login = NeonHelper.JsonDeserialize<HiveLogin>(File.ReadAllText(loginPath));

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
        /// Returns the APT package proxy references for a hive definition as a space separated list.
        /// </summary>
        /// <returns>The space separated list of package proxy references formatted as HOST_OR_IP:PORT.</returns>
        public static string GetPackageProxyReferences(HiveDefinition hiveDefinition)
        {
            // Convert the package cache URIs from a list of comma separated HTTP URIs to
            // a space separated list of hostname/ports.  Note that we'll use the proxy
            // caches on the manager nodes if no cache URIs are specified.

            var packageProxy     = hiveDefinition.PackageProxy ?? string.Empty;
            var packageCacheRefs = string.Empty;

            foreach (var uriString in packageProxy.Split(','))
            {
                if (!string.IsNullOrEmpty(uriString))
                {
                    if (packageCacheRefs.Length > 0)
                    {
                        packageCacheRefs += "\\ ";  // Bash needs us to escape the space
                    }

                    var uri = new Uri(uriString, UriKind.Absolute);

                    packageCacheRefs += $"{uri.Host}:{uri.Port}";
                }
            }

            if (string.IsNullOrEmpty(packageCacheRefs))
            {
                // Configure the managers as proxy caches if no other
                // proxies are specified.

                foreach (var manager in hiveDefinition.Managers)
                {
                    if (packageCacheRefs.Length > 0)
                    {
                        packageCacheRefs += "\\ ";  // Bash needs us to escape the space
                    }

                    packageCacheRefs += $"{manager.PrivateAddress}:{NetworkPorts.AppCacherNg}";
                }
            }

            return packageCacheRefs;
        }

        /// <summary>
        /// <para>
        /// Enables <see cref="GetSecret(string)"/> and <see cref="GetSecret{T}(string)"/> to retrieve
        /// Docker secrets from Docker itself.  This works only if the current user is logged into
        /// the cluster, which will almost never be the case for services or containers actually
        /// running in the hive. 
        /// </para>
        /// <para>
        /// This is typically used for unit tests (e.g. by the <c>HiveFixture</c> test fixture class)
        /// so that tests can obtain secrets.
        /// </para>
        /// </summary>
        /// <exception cref="HiveException">Thrown if the current user is not remotely logged into a hive.</exception>
        public static void EnableSecretRetrival()
        {
            if (!remoteConnection)
            {
                throw new HiveException($"Cannot enable secret retrieval when not remotely logged into the hive.");
            }

            enableSecretRetrival = true;
        }

        /// <summary>
        /// Returns the value of a named secret.
        /// </summary>
        /// <param name="name">The secret name.</param>
        /// <returns>The secret value or <c>null</c> if the secret doesn't exist.</returns>
        /// <remarks>
        /// <para>
        /// This method can be used to retrieve a secret provisioned to a service via the
        /// Docker secrets feature or a secret provided to <see cref="OpenHiveRemote(DebugSecrets, DebugConfigs, string, bool)"/> 
        /// when we're emulating running the application as a hive container.
        /// </para>
        /// <para>
        /// Docker provisions secrets by mounting a <b>tmpfs</b> file system at <b>/var/run/secrets</b>
        /// and writing the secrets there as text files with the file name identifying the secret.
        /// When the application is not running in debug mode, this method simply attempts to read
        /// the requested secret from the named file in this folder.
        /// </para>
        /// <note>
        /// Unit test fixtures like <c>HiveFixture</c> call <see cref="EnableSecretRetrival"/>
        /// so that this method will attempt to retrieve and cache the requested secret directly
        /// from Docker.
        /// </note>
        /// </remarks>
        public static string GetSecret(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            var secretPath = Path.Combine(HiveConst.ContainerSecretsFolder, name);

            try
            {
                if (File.Exists(secretPath))
                {
                    return File.ReadAllText(secretPath);
                }
            }
            catch (IOException)
            {
                return null;
            }

            string secret;

            if (secrets.TryGetValue(name, out secret))
            {
                return secret;
            }
            else if (enableSecretRetrival && Hive != null)
            {
                secret = Hive.Docker.Secret.GetString(name);

                if (secret == null)
                {
                    return null;
                }

                // This effectively caches the secret so we won't need
                // to retrieve it again.

                secrets.Add(name, secret);
                return secret;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Returns the value of a named secret for a specific type parsed from JSON.
        /// </summary>
        /// <typeparam name="T">The desired output type.</typeparam>
        /// <param name="name">The secret name.</param>
        /// <returns>The secret of type <typeparamref name="T"/> or <c>null</c> if the secret doesn't exist.</returns>
        /// <remarks>
        /// <para>
        /// This method can be used to retrieve a secret provisioned to a service via the
        /// Docker secrets feature or a secret provided to <see cref="OpenHiveRemote(DebugSecrets, DebugConfigs, string, bool)"/> 
        /// when we're emulating running the application as a hive container.
        /// </para>
        /// <para>
        /// Docker provisions secrets by mounting a <b>tmpfs</b> file system at <b>/var/run/secrets</b>
        /// and writing the secrets there as text files with the file name identifying the secret.
        /// When the application is not running in debug mode, this method simply attempts to read
        /// the requested secret from the named file in this folder.
        /// </para>
        /// <note>
        /// Unit test fixtures like <c>HiveFixture</c> call <see cref="EnableSecretRetrival"/>
        /// so that this method will attempt to retrieve and cache the requested secret directly
        /// from Docker.
        /// </note>
        /// </remarks>
        public static T GetSecret<T>(string name)
            where T : class, new()
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            var secretJson = GetSecret(name);

            if (secretJson == null)
            {
                return null;
            }

            try
            {
                return NeonHelper.JsonDeserialize<T>(secretJson);
            }
            catch (Exception e)
            {
                throw new ArgumentException($"Unable to parse local secret [{name}] as a [{typeof(T).FullName}].", e);
            }
        }

        /// <summary>
        /// Returns the value of a named config.
        /// </summary>
        /// <param name="name">The config name.</param>
        /// <returns>The config value or <c>null</c> if the config doesn't exist.</returns>
        /// <remarks>
        /// <para>
        /// This method can be used to retrieve a config provisioned to a service via the
        /// Docker configs feature or a config provided to <see cref="OpenHiveRemote(DebugSecrets, DebugConfigs, string, bool)"/> 
        /// when we're emulating running the application as a hive container.
        /// </para>
        /// <para>
        /// Docker provisions configs by mounting the file at <b>/CONFIG-NAME</b> by default.
        /// When the application is not running in debug mode, this method simply attempts to read
        /// the requested config file from the root folder.
        /// </para>
        /// </remarks>
        public static string GetConfig(string name)
        {
            var configPath = $"/{name}";

            try
            {
                if (File.Exists(configPath))
                {
                    return File.ReadAllText(configPath);
                }
            }
            catch (IOException)
            {
                return null;
            }

            if (configs == null)
            {
                return null;
            }

            string config;

            if (configs.TryGetValue(name, out config))
            {
                return config;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Returns the value of a named config for a specific type parsed from JSON.
        /// </summary>
        /// <typeparam name="T">The desired output type.</typeparam>
        /// <param name="name">The config name.</param>
        /// <returns>The config of type <typeparamref name="T"/> or <c>null</c> if the config doesn't exist.</returns>
        /// <remarks>
        /// <para>
        /// This method can be used to retrieve a config provisioned to a container via the
        /// Docker configs feature or a config provided to <see cref="OpenHiveRemote(DebugSecrets, DebugConfigs, string, bool)"/> 
        /// when we're emulating running the application as a hive container.
        /// </para>
        /// <para>
        /// Docker provisions configs by mounting the file at <b>/CONFIG-NAME</b> by default.
        /// When the application is not running in debug mode, this method simply attempts to read
        /// the requested config file from the root folder.
        /// </para>
        /// </remarks>
        public static T GetConfig<T>(string name)
            where T : class, new()
        {
            var configJson = GetConfig(name);

            if (configJson == null)
            {
                return null;
            }

            try
            {
                return NeonHelper.JsonDeserialize<T>(configJson);
            }
            catch (Exception e)
            {
                throw new ArgumentException($"Unable to parse local config [{name}] as a [{typeof(T).FullName}].", e);
            }
        }

        /// <summary>
        /// Returns a client that can access the hive Consul service.
        /// </summary>
        /// <returns>A <see cref="ConsulClient"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if no hive is connected.</exception>
        public static ConsulClient OpenConsul()
        {
            VerifyConnected();

            return new ConsulClient(
                config =>
                {
                    config.Address  = ConsulUri;
                    config.WaitTime = TimeSpan.FromSeconds(60);
                });
        }

        /// <summary>
        /// Returns an open hive Consul client.
        /// </summary>
        /// <remarks>
        /// <note>
        /// The instance returned by the property is intended to be shared across
        /// the application and <b>must not be disposed</b>.  Use <see cref="OpenConsul"/>
        /// if you wish a private instance.
        /// </note>
        /// </remarks>
        public static ConsulClient Consul
        {
            get
            {
                VerifyConnected();

                var consul = sharedConsul;

                if (consul != null)
                {
                    return consul;
                }

                return sharedConsul = OpenConsul();
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
        /// Retrieves the current hive definition from Consul, optionally comparing the
        /// the hashes of the persisted hive with the hive passed as a performance 
        /// improvement.
        /// </summary>
        /// <param name="cachedDefinition">The optional cached definition to be compared.</param>
        /// <param name="cancellationToken">The optional cancellation token.</param>
        /// <returns>The hive definition.</returns>
        /// <exception cref="InvalidOperationException">Thrown if no hive is connected.</exception>
        /// <exception cref="KeyNotFoundException">Thrown if the hive definition has not been persisted to the hive.</exception>
        /// <remarks>
        /// <para>
        /// If <paramref name="cachedDefinition"/> is passed as non-null then the method will first compute
        /// its hash and then retrieve the hash for current hive definition from Consul.  Then the method
        /// compares the hash returned with the hash for the definition passed.  If the two hashes 
        /// match, then the definition passed is returned (avoiding a potentially large download from Consul).
        /// Otherwise, we'll retrieve the entire definition.
        /// </para>
        /// <para>
        /// This optimizes the common case where the <b>neon-cli</b> is caching the hive definition
        /// locally within the hive login and where the hive definition changes infrequently.
        /// </para>
        /// </remarks>
        public static async Task<HiveDefinition> GetDefinitionAsync(HiveDefinition cachedDefinition = null, CancellationToken cancellationToken = default)
        {
            VerifyConnected();

            // For bare metal hives, just return the cached definition because there
            // is no Consul service running.

            if (cachedDefinition != null && cachedDefinition.BareDocker)
            {
                return cachedDefinition;
            }

            // If we're not running inside the hive, we'll do a quick check to see 
            // if we have any healthy manager nodes.  If not, then we'll return the 
            // cached definition (if there is one).
            //
            // We'll query Consul directly if we're running in the hive.

            if (remoteConnection && cachedDefinition != null)
            {
                var hive    = new HiveProxy(cachedDefinition);
                var manager = hive.GetReachableManager(ReachableHostMode.ReturnNull);

                if (manager == null)
                {
                    return cachedDefinition;
                }
            }

            // Otherwise, we'll attempt to download this from Consul.

            using (var consul = OpenConsul())
            {
                if (cachedDefinition != null)
                {
                    cachedDefinition.ComputeHash();

                    try
                    {
                        var hash = await consul.KV.GetStringOrDefault($"{HiveConst.GlobalKey}/{HiveGlobals.DefinitionHash}");

                        if (hash == null)
                        {
                            // It's possible (but super rare) that the hive definition might
                            // exist without the hash.  In this case, we'll just drop through
                            // and try reading the full definition below.
                        }
                        else if (hash == cachedDefinition.Hash)
                        {
                            return cachedDefinition;
                        }
                    }
                    catch (Exception e)
                    {
                        // This is probably an [HttpRequestException] or [SocketException]
                        // indicating that we could not contact the hive Consul.  We'll 
                        // just returned the cached hive definition in this situation 
                        // (if we have one).

                        if (cachedDefinition != null)
                        {
                            return cachedDefinition;
                        }
                        else
                        {
                            throw new HiveException(NeonHelper.ExceptionError(e));
                        }
                    }
                }

                try
                {
                    var deflated = await consul.KV.GetBytes($"{HiveConst.GlobalKey}/{HiveGlobals.DefinitionDeflate}");
                    var json     = NeonHelper.DecompressString(deflated);

                    try
                    {
                        return NeonHelper.JsonDeserialize<HiveDefinition>(json);
                    }
                    catch (Exception e2)
                    {
                        throw new HiveException($"[{e2.GetType().Name}]: Cannot deserialize remote hive definition.", e2);
                    }
                }
                catch (HiveException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    // This is probably an [HttpRequestException] or [SocketException]
                    // indicating that we could not contact the hive Consul.  We'll 
                    // just returned the cached hive definition in this situation 
                    // (if we have one).

                    if (cachedDefinition != null)
                    {
                        return cachedDefinition;
                    }
                    else
                    {
                        throw new HiveException(NeonHelper.ExceptionError(e));
                    }
                }
            }
        }

        /// <summary>
        /// Persists the hive definition to Consul.
        /// </summary>
        /// <param name="definition">The hive definition.</param>
        /// <param name="savePets">Optionally persists the pet definitions to Consul.</param>
        /// <param name="cancellationToken">The optional cancellation token.</param>
        /// <exception cref="InvalidOperationException">Thrown if no hive is connected.</exception>
        public async static Task PutDefinitionAsync(HiveDefinition definition, bool savePets = false, CancellationToken cancellationToken = default)
        {
            Covenant.Requires<ArgumentNullException>(definition != null);

            VerifyConnected();

            definition.ComputeHash();   // Always recomute the hash

            var fullJson         = NeonHelper.JsonSerialize(definition);
            var deflatedFullJson = NeonHelper.CompressString(fullJson);

            // [neon-hive-manager] expects the pet node definitions to be persisted to
            // Consul at [neon/global/pets-definition] so that it can include any pets
            // in the hive definition file consumed by [neon-cli] before it executes any 
            // hive related commands.

            var petDefinitions = new Dictionary<string, NodeDefinition>();

            foreach (var petDefinition in definition.SortedPets)
            {
                petDefinitions.Add(petDefinition.Name, petDefinition);
            }

            var petsJson      = NeonHelper.JsonSerialize(petDefinitions, Formatting.Indented);
            var petsJsonBytes = Encoding.UTF8.GetBytes(petsJson);

            using (var consul = OpenConsul())
            {
                // We're going to persist the compressed definition and its
                // MD5 hash together in a transaction so they'll always be 
                // in sync.

                var operations = new List<KVTxnOp>()
                {
                    new KVTxnOp($"{HiveConst.GlobalKey}/{HiveGlobals.DefinitionDeflate}", KVTxnVerb.Set) { Value = deflatedFullJson },
                    new KVTxnOp($"{HiveConst.GlobalKey}/{HiveGlobals.DefinitionHash}", KVTxnVerb.Set) { Value = Encoding.UTF8.GetBytes(definition.Hash) }
                };

                // Add any pets to the transaction if enabled.

                if (savePets)
                {
                    operations.Add(new KVTxnOp($"{HiveConst.GlobalKey}/{HiveGlobals.PetsDefinition}", KVTxnVerb.Set) { Value = petsJsonBytes });
                }

                await consul.KV.Txn(operations);
            }
        }

        /// <summary>
        /// Returns a client that can access the hive Vault secret management service using a Vault token.
        /// </summary>
        /// <param name="token">The Vault token.</param>
        /// <returns>A <see cref="VaultClient"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if no hive is connected.</exception>
        public static VaultClient OpenVault(string token)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(token));

            VerifyConnected();

            var client = VaultClient.OpenWithToken(VaultUri, token);

            // $todo(jeff.lill):
            //
            // This should be set from config.  See issue:
            //
            //      https://github.com/jefflill/NeonForge/issues/253

            client.AllowSelfSignedCertificates = true;

            return client;
        }

        /// <summary>
        /// Returns a client that can access the hive Vault secret management service specified credentials.
        /// </summary>
        /// <param name="credentials">The credentials.</param>
        /// <returns>A <see cref="VaultClient"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if no hive is connected.</exception>
        public static VaultClient OpenVault(HiveCredentials credentials)
        {
            Covenant.Requires<ArgumentNullException>(credentials != null);

            VerifyConnected();

            credentials.Validate();

            switch (credentials.Type)
            {
                case HiveCredentialsType.VaultAppRole:

                    return VaultClient.OpenWithAppRole(VaultUri, credentials.VaultRoleId, credentials.VaultSecretId);

                case HiveCredentialsType.VaultToken:

                    return VaultClient.OpenWithToken(VaultUri, credentials.VaultToken);

                default:

                    throw new NotSupportedException($"Vault cannot be opened using [{credentials.Type}] credentisls.");
            }
        }

        /// <summary>
        /// Returns the HAProxy log format string for the named proxy.
        /// </summary>
        /// <param name="proxyName">The proxy Docker service name.</param>
        /// <param name="tcp">Pass <c>true</c> for TCP proxies, <c>false</c> for HTTP.</param>
        /// <returns>The HAProxy log format string.</returns>
        /// <remarks>
        /// <para>
        /// The log format consists fields separated by the caret (<b>^</b>) character.  None of the values 
        /// should include this so quoting or escaping are not required.  The tables below describe the
        /// fields and include the HAProxy log format codes.  See the
        /// <a href="http://cbonte.github.io/haproxy-dconv/1.7/configuration.html#8.2.4">HAPoxy Documentation</a> 
        /// for more information.
        /// </para>
        /// <para>
        /// The first field is hardcoded to be <b>traffic</b> to so the log pipeline can distinguish between
        /// proxy traffic and status messages.  The second field specifies the type of record and the format
        /// version number.  This will be <b>tcp-v1</b> for TCP traffic and <b>http-v1</b> for HTTP traffic.
        /// </para>
        /// <para>
        /// The traffic specific fields follow the <b>traffic</b> and <b>type/version</b> fields.  Note that
        /// the HTTP format is a strict superset of the TCP format, with the additional HTTP related fields 
        /// appearing after the common TCP fields.
        /// </para>
        /// <para><b>Common TCP and HTTP Fields</b></para>
        /// <para>
        /// Here are the TCP log fields in order they appear in the message.
        /// </para>
        /// <list type="table">
        /// <item>
        ///     <term><b>Service</b></term>
        ///     <description>
        ///     The proxy service name (e.g. <b>neon-proxy-public</b>).
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>Timestamp (TCP=%t, HTTP=%tr)</b></term>
        ///     <description>
        ///     Event time, for TCP this is usually when the connection is closed.  For HTTP, this
        ///     is the request time.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>Client IP (%ci)</b></term>
        ///     <description>
        ///     Client IP address.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>Backend Name (%b)</b></term>
        ///     <description>
        ///     Identifies the proxy backend.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>Server Name (%s)</b></term>
        ///     <description>
        ///     Identifies the backend server name.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>Server IP (%si)</b></term>
        ///     <description>
        ///     The backend server IP address
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>Server Port (%sp)</b></term>
        ///     <description>
        ///     The backend server port.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>TLS/SSL Version (%sslv)</b></term>
        ///     <description>
        ///     The TLS/SSL version or a dash (<b>-</b>) if the connection is not secured.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>TLS/SSL Cypher (%sslc)</b></term>
        ///     <description>
        ///     The TLS/SSL cypher (<b>-</b>) if the connection is not secured.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>Bytes Received (%U)</b></term>
        ///     <description>
        ///     Bytes send from client to proxy.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>Bytes Sent (%B)</b></term>
        ///     <description>
        ///     Bytes sent from proxy to client.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>Queue Time (%Tw)</b></term>
        ///     <description>
        ///     Milliseconds waiting in queues for a connection slot or -1 if the connection
        ///     was terminated before reaching a queue.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>Connect Time (%Tc)</b></term>
        ///     <description>
        ///     Milliseconds waiting to establish a connection between the backend and the server 
        ///     or -1 if a connection was never established.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>Session Time (%Tt)</b></term>
        ///     <description>
        ///    Total milliseconds between the time the proxy accepted the connection and the
        ///    time the connection was closed.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>Termination Flags (%ts)</b></term>
        ///     <description>
        ///     Describes how the connection was terminated.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>Proxy Connections (%ac)</b></term>
        ///     <description>
        ///     Number of active connections currently managed by the proxy.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>Frontend Connections (%fc)</b></term>
        ///     <description>
        ///     Number of active proxy frontend connections.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>Backend Connections (%bc)</b></term>
        ///     <description>
        ///     Number of active proxy backend connections.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>Server Connections (%sc)</b></term>
        ///     <description>
        ///     Number of active proxy server connections
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>Retries (%rc)</b></term>
        ///     <description>
        ///     Number of retries.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>Server Queue (%sq)</b></term>
        ///     <description>
        ///     Server queue length.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>Backend Queue (%bq)</b></term>
        ///     <description>
        ///     Backend queue length.
        ///     </description>
        /// </item>
        /// </list>
        /// <para><b>The Extended HTTP Related Fields</b></para>
        /// <para>
        /// Here are the HTTP log fields in the order they appear in the message.
        /// </para>
        /// <list type="table">
        /// <item>
        ///     <term><b>Activity ID (%ID)</b></term>
        ///     <description>
        ///     The globally unique request activity ID (from the <b>X-Request-ID</b> header).
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>Idle Time (%Ti)</b></term>
        ///     <description>
        ///     Milliseconds waiting idle before the first byte of the HTTP request was received.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>Request Time (%TR)</b></term>
        ///     <description>
        ///     Milliseconds to receive the full HTTP request from the first byte.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>Response Time (%Tr)</b></term>
        ///     <description>
        ///     Milliseconds the server took to process the request and return the full status
        ///     line and HTTP headers.  This does not include the network overhead for delivering
        ///     the data.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>Active Time (%Ta)</b></term>
        ///     <description>
        ///     Milliseconds from the <b>Request Time</b> until the response was transmitted.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>HTTP Method (%HM)</b></term>
        ///     <description>
        ///     HTTP request method.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>URI (%HP)</b></term>
        ///     <description>
        ///     Partial HTTP relative URI that excludes query strings.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>URI Query string (%HU)</b></term>
        ///     <description>
        ///     HTTP URI query string.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>HTTP Version (%HV)</b></term>
        ///     <description>
        ///     The HTTP version (e.g. <b>HTTP/1.1</b>).
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>HTTP Status (%ST)</b></term>
        ///     <description>
        ///     The HTTP response status code.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>Request Headers (%hr)</b></term>
        ///     <description>
        ///     The captured HTTP request headers within <b>{...}</b> and separated by pipe 
        ///     (|) characters.  Currently, only the <b>Host</b> and <b>User-Agent</b> 
        ///     headers are captured, in that order.
        ///     </description>
        /// </item>
        /// </list>
        /// </remarks>
        public static string GetProxyLogFormat(string proxyName, bool tcp)
        {
            if (tcp)
            {
                return $"traffic^tcp-v1^{proxyName}^%t^%ci^%b^%s^%si^%sp^%sslv^%sslc^%U^%B^%Tw^%Tc^%Tt^%ts^%ac^%fc^%bc^%sc^%rc^%sq^%bq";
            }
            else
            {
                return $"traffic^http-v1^{proxyName}^%tr^%ci^%b^%s^%si^%sp^%sslv^%sslc^%U^%B^%Tw^%Tc^%Tt^%ts^%ac^%fc^%bc^%sc^%rc^%sq^%bq^%ID^%Ti^%TR^%Tr^%Ta^%HM^%HP^%HQ^%HV^%ST^%hr";
            }
        }

        /// <summary>
        /// Connects to a Couchbase cluster whose connection settings are referenced in Consul 
        /// using the credentials provisioned in a Docker secret.
        /// </summary>
        /// <param name="connectionKey">The Consul key for the connection settings.</param>
        /// <param name="secretName">The local container name for the Docker secret.</param>
        /// <param name="cancellationToken">The optional cancellation token.</param>
        /// <returns>The <see cref="Couchbase.Cluster"/>.</returns>
        public static async Task<Couchbase.Cluster> OpenCouchbaseAsync(string connectionKey, string secretName, CancellationToken cancellationToken = default)
        {
            VerifyConnected();

            var connectionSettings = await Consul.KV.GetObject<CouchbaseSettings>(connectionKey, cancellationToken);
            var credentials        = GetSecret<Credentials>(secretName);

            if (credentials == null)
            {
                throw new ArgumentException($"Secret [name={secretName}] is not present in the container.");
            }

            if (!connectionSettings.IsValid)
            {
                throw new ArgumentException($"Connection settings at [consul:{connectionKey}] are not valid for Couchbase.");
            }

            if (!credentials.HasUsernamePassword)
            {
                throw new ArgumentException($"Credentials at [secret:{secretName}] do not include a username and password.");
            }

            return connectionSettings.OpenCluster(credentials);
        }

        /// <summary>
        /// Connects to a Couchbase bucket whose connection settings are referenced in Consul 
        /// using the credentials provisioned in a Docker secret.
        /// </summary>
        /// <param name="connectionKey">The Consul key for the connection settings.</param>
        /// <param name="secretName">The local container name for the Docker secret.</param>
        /// <param name="cancellationToken">The optional cancellation token.</param>
        /// <returns>The <see cref="Couchbase.Core.IBucket"/>.</returns>
        public static async Task<Couchbase.Core.IBucket> OpenCouchbaseBucketAsync(string connectionKey, string secretName, CancellationToken cancellationToken = default)
        {
            VerifyConnected();

            var connectionSettings = await Consul.KV.GetObject<CouchbaseSettings>(connectionKey, cancellationToken);
            var credentials        = GetSecret<Credentials>(secretName);

            if (credentials == null)
            {
                throw new ArgumentException($"Secret [name={secretName}] is not present in the container.");
            }

            if (!connectionSettings.IsValid)
            {
                throw new ArgumentException($"Connection settings at [consul:{connectionKey}] are not valid for Couchbase.");
            }

            if (!credentials.HasUsernamePassword)
            {
                throw new ArgumentException($"Credentials at [secret:{secretName}] does not include a username and password.");
            }

            return connectionSettings.OpenBucket(credentials);
        }

        /// <summary>
        /// Connects to the RabbitMQ broker whose connection settings are referenced in Consul 
        /// using the credentials provisioned in a Docker secret.
        /// </summary>
        /// <param name="connectionKey">The Consul key for the connection settings.</param>
        /// <param name="secretName">The local container name for the Docker secret.</param>
        /// <param name="cancellationToken">The optional cancellation token.</param>
        /// <returns>The <see cref="global::RabbitMQ.Client.IConnection"/>.</returns>
        public static async Task<global::RabbitMQ.Client.IConnection> OpenRabbitMQAsync(string connectionKey, string secretName, CancellationToken cancellationToken = default)
        {
            VerifyConnected();

            var connectionSettings = await Consul.KV.GetObject<HiveMQSettings>(connectionKey, cancellationToken);
            var credentials        = GetSecret<Credentials>(secretName);

            if (credentials == null)
            {
                throw new ArgumentException($"Secret [name={secretName}] is not present in the container.");
            }

            if (!connectionSettings.IsValid)
            {
                throw new ArgumentException($"Connection settings at [consul:{connectionKey}] are not valid for RabbitMQ.");
            }

            if (!credentials.HasUsernamePassword)
            {
                throw new ArgumentException($"Credentials at [secret:{secretName}] do not include a username and password.");
            }

            return connectionSettings.ConnectRabbitMQ(credentials);
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

            return hostname.Equals(HiveConst.DockerPublicRegistry, StringComparison.InvariantCultureIgnoreCase);
        }
    }
}
