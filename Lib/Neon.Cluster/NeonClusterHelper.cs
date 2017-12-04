//-----------------------------------------------------------------------------
// FILE:	    NeonClusterHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
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
using Neon.Net;

// $todo(jeff.lill): 
//
// This class being static doesn't support dependency injection.  I'm not sure it's
// worth changing this now.  Perhaps when we start doing more cluster unit testing.

namespace Neon.Cluster
{
    /// <summary>
    /// neonCLUSTER related utilties.
    /// </summary>
    public static partial class NeonClusterHelper
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

        private static INeonLogger                  log = LogManager.Default.GetLogger(typeof(NeonClusterHelper));
        private static Dictionary<string, string>   secrets;
        private static bool                         externalConnection;

        /// <summary>
        /// Explicitly sets the class <see cref="INeonLogger"/> implementation.  This defaults to
        /// a reasonable value.
        /// </summary>
        /// <param name="log"></param>
        public static void SetLogger(INeonLogger log)
        {
            Covenant.Requires<ArgumentNullException>(log != null);

            NeonClusterHelper.log = log;
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
        /// Returns the path the folder holding the user specific cluster files.
        /// </summary>
        /// <param name="ignoreNeonToolContainerVar">
        /// Optionally ignore the presence of a <b>NEON_TOOL_CONTAINER</b> environment 
        /// variable.  Defaults to <c>false</c>.
        /// </param>
        /// <returns>The folder path.</returns>
        /// <remarks>
        /// The actual path return depends on the presence of the <b>NEON_TOOL_CONTAINER</b>
        /// environment variable.  <b>NEON_TOOL_CONTAINER=1</b> then we're running in a 
        /// shimmed Docker container and we'll expect the cluster login information to be mounted
        /// at <b>/neoncluster</b>.  Otherwise, we'll return a suitable path within the 
        /// current user's home directory.
        /// </remarks>
        public static string GetRootFolder(bool ignoreNeonToolContainerVar = false)
        {
            if (!ignoreNeonToolContainerVar && InToolContainer)
            {
                return "/neoncluster";
            }

            if (NeonHelper.IsWindows)
            {
                var path = Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA"), "neonFORGE", "neoncluster");

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
                return Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".neonforge", "neoncluster");
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Returns the root path where the [neon shell CMD ...] will copy secrets and run the command.
        /// </summary>
        /// <returns>The folder path.</returns>
        public static string GetShellFolder()
        {
            var path = Path.Combine(GetRootFolder(), "shell");

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
        /// to deploy and manage neonCLUSTERs.  Each known cluster will have a JSON file named
        /// <b><i>cluster-name</i>.json</b> holding the serialized <see cref="Cluster.ClusterLogin"/> 
        /// information for the cluster.
        /// </para>
        /// <para>
        /// The <b>.current</b> file (if present) specifies the name of the cluster to be considered
        /// to be currently logged in.
        /// </para>
        /// </remarks>
        public static string GetLoginFolder()
        {
            var path = Path.Combine(GetRootFolder(), "logins");

            Directory.CreateDirectory(path);

            return path;
        }

        /// <summary>
        /// Returns the path to the root folder containing the Ansible related files.
        /// </summary>
        /// <returns>The folder path.</returns>
        public static string GetAnsibleFolder()
        {
            var path = Path.Combine(GetRootFolder(), "ansible");

            Directory.CreateDirectory(path);

            return path;
        }

        /// <summary>
        /// Returns the path to the root folder containing the Ansible password files.
        /// </summary>
        /// <returns>The folder path.</returns>
        public static string GetAnsiblePasswordFolder()
        {
            var path = Path.Combine(GetRootFolder(), "ansible", "passwords");

            Directory.CreateDirectory(path);

            return path;
        }

        /// <summary>
        /// Returns the path to the file indicating which cluster is currently logged in.
        /// </summary>
        public static string CurrentPath
        {
            get { return Path.Combine(GetLoginFolder(), ".current"); }
        }

        /// <summary>
        /// Returns the path to the login information for the named cluster.
        /// </summary>
        /// <param name="username">The operator's user name.</param>
        /// <param name="clusterName">The cluster name.</param>
        /// <returns>The path to the cluster's credentials file.</returns>
        public static string GetLoginPath(string username, string clusterName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(clusterName));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(username));

            return Path.Combine(GetLoginFolder(), $"{username}@{clusterName}.login.json");
        }

        /// <summary>
        /// Returns the path to the current user's cluster setup folder, creating
        /// the directory if it doesn't already exist.
        /// </summary>
        /// <returns>The path to the nenCLUSTER setup folder.</returns>
        public static string GetSetupFolder()
        {
            var path = Path.Combine(GetRootFolder(), "setup");

            Directory.CreateDirectory(path);

            return path;
        }

        /// <summary>
        /// Returns the cluster login for the currently logged in cluster and
        /// establishes a cluster connection.
        /// </summary>
        /// <param name="noConnect">
        /// Optionally indicates that the method should not connect to the cluster
        /// and should simply return the current login in if one is available.
        /// </param>
        /// <returns>The current cluster login or <c>null</c>.</returns>
        /// <remarks>
        /// <note>
        /// The tricky thing here is that the cluster definition nodes 
        /// within the cluster login returned will actually be loaded from
        /// the cluster itself or the cached copy.
        /// </note>
        /// </remarks>
        public static ClusterLogin GetLogin(bool noConnect = false)
        {
            if (File.Exists(CurrentPath))
            {
                var current = CurrentClusterLogin.Load();
                var login   = NeonClusterHelper.SplitLogin(current.Login);

                if (!login.IsOK)
                {
                    File.Delete(CurrentPath);
                    return null;
                }

                var username         = login.Username;
                var clusterName      = login.ClusterName;
                var clusterLoginPath = GetLoginPath(username, clusterName);

                if (File.Exists(clusterLoginPath))
                {
                    var clusterLogin = NeonClusterHelper.LoadClusterLogin(username, clusterName);

                    clusterLogin.ViaVpn = current.ViaVpn;

                    if (noConnect)
                    {
                        return clusterLogin;
                    }

                    OpenCluster(clusterLogin);

                    var clusterDefinition = GetLiveDefinition(username, clusterName);

                    clusterLogin.Definition.NodeDefinitions = clusterDefinition.NodeDefinitions;

                    return clusterLogin;
                }
                else
                {
                    // The referenced cluster file doesn't exist so quietly remove the ".current" file.

                    File.Delete(CurrentPath);
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the path to the cached cluster definition for the named cluster.
        /// </summary>
        /// <param name="username">The operator's user name.</param>
        /// <param name="clusterName">The cluster name.</param>
        /// <returns>The path to the cluster's credentials file.</returns>
        public static string GetCachedDefinitionPath(string username, string clusterName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(clusterName));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(username));

            return Path.Combine(GetLoginFolder(), $"{username}@{clusterName}.def.json");
        }

        /// <summary>
        /// Returns the current cluster definition from the cluster if we're
        /// currently logged in.
        /// </summary>
        /// <param name="username">The operator's user name.</param>
        /// <param name="clusterName">The cluster name.</param>
        /// <returns>The current cluster definition or <c>null</c>.</returns>
        public static ClusterDefinition GetLiveDefinition(string username, string clusterName)
        {
            var clusterLoginPath = GetLoginPath(username, clusterName);

            if (!File.Exists(clusterLoginPath))
            {
                return null;    // We're not logged in.
            }

            // We're going to optimize the retrieval by attempting to cache
            // a copy of the cluster definition obtained from the cluster and
            // then comparing hashes of the cached definition with the hash
            // saved on the server to determine whether we really need to
            // download the whole definition again.
            //
            // This should make make the [neon-cli] a lot snappier because
            // cluster definitions will change relatively infrequently for
            // many clusters.

            var cachedDefinitionPath = GetCachedDefinitionPath(username, clusterName);
            var cachedDefinition     = (ClusterDefinition)null;

            if (File.Exists(cachedDefinitionPath))
            {
                try
                {
                    cachedDefinition = NeonHelper.JsonDeserialize<ClusterDefinition>(File.ReadAllText(cachedDefinitionPath));
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

            var clusterDefinition = GetDefinitionAsync(cachedDefinition).Result;

            if (!object.ReferenceEquals(clusterDefinition, cachedDefinition))
            {
                // It looks like we received an updated defintion from the cluster
                // so cache it.

                File.WriteAllText(cachedDefinitionPath, NeonHelper.JsonSerialize(clusterDefinition, Formatting.Indented));
            }

            return clusterDefinition;
        }

        /// <summary>
        /// Splits a cluster login in the form of <b>USER@CLUSTER</b> into
        /// the operator's username and the cluster name.
        /// </summary>
        /// <param name="login">The cluster identifier.</param>
        /// <returns>The username and cluster name parts.</returns>
        public static (bool IsOK, string Username, string ClusterName) SplitLogin(string login)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(login));

            var fields = login.Split(new char[] { '@' }, 2);

            if (fields.Length != 2 || string.IsNullOrEmpty(fields[0]) || string.IsNullOrEmpty(fields[1]))
            {
                return (IsOK: false, Username: null, ClusterName: null);
            }

            return (IsOK: true, Username: fields[0], ClusterName: fields[1]);
        }

        /// <summary>
        /// Loads the cluster login information for the current cluster, performing any necessary decryption.
        /// </summary>
        /// <param name="username">The operator's user name.</param>
        /// <param name="clusterName">The name of the target cluster.</param>
        /// <returns>The <see cref="Cluster.ClusterLogin"/>.</returns>
        public static ClusterLogin LoadClusterLogin(string username, string clusterName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(username));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(clusterName));

            var path         = Path.Combine(GetLoginFolder(), $"{username}@{clusterName}.login.json");
            var clusterLogin = NeonHelper.JsonDeserialize<ClusterLogin>(File.ReadAllText(path));

            clusterLogin.Path = path;

            return clusterLogin;
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
        /// Returns the cluster's Vault URI.
        /// </summary>
        public static Uri VaultUri
        {
            get
            {
                var vaultUri = Environment.GetEnvironmentVariable("VAULT_ADDR");

                if (string.IsNullOrEmpty(vaultUri))
                {
                    throw new NotSupportedException("Cannot access cluster Vault because the [VAULT_ADDR] environment variable was not passed to this container.");
                }

                return new Uri(vaultUri);
            }
        }

        /// <summary>
        /// Returns the cluster's Consul URI.
        /// </summary>
        public static Uri ConsulUri
        {
            get
            {
                var consulUri = Environment.GetEnvironmentVariable("CONSUL_HTTP_FULLADDR");

                if (string.IsNullOrEmpty(consulUri))
                {
                    throw new NotSupportedException("Cannot access cluster Consul because the [CONSUL_HTTP_FULLADDR] environment variable was not passed to this container.");
                }

                return new Uri(Environment.GetEnvironmentVariable("CONSUL_HTTP_FULLADDR"));
            }
        }

        /// <summary>
        /// Indicates whether the application is running outside of a Docker container
        /// but we're going to try to simulate the environment such that the application
        /// believe it is running in a container within a Docker cluster.  See 
        /// <see cref="OpenRemoteCluster(DebugSecrets, string)"/> for more information.
        /// </summary>
        public static bool IsConnected { get; private set; } = false;

        /// <summary>
        /// Returns the <see cref="Cluster.ClusterLogin"/> for the current cluster if we're running in debug mode. 
        /// </summary>
        public static ClusterLogin ClusterLogin { get; private set; } = null;

        /// <summary>
        /// Returns the <see cref="ClusterProxy"/> for the current cluster if we're running in debug mode.
        /// </summary>
        public static ClusterProxy Cluster { get; private set; } = null;

        /// <summary>
        /// Simulates running the current application within the currently logged-in
        /// neonCLUSTER for external tools as well as for development and debugging purposes.
        /// </summary>
        /// <param name="secrets">Optional emulated Docker secrets.</param>
        /// <param name="loginPath">Optional path to a specific cluster login to override the current login.</param>
        /// <returns>The <see cref="ClusterProxy"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if a cluster is already connected.</exception>
        /// <remarks>
        /// <note>
        /// This method requres elevated administrative permissions for the local operating
        /// system.
        /// </note>
        /// <note>
        /// Take care to call <see cref="CloseCluster()"/> just before your application
        /// exits to reset any temporary settings like the DNS resolver <b>hosts</b> file.
        /// </note>
        /// <note>
        /// This method currently simulates running the application on a cluster manager node.
        /// In the future, we may provide a way to simulate running on a specific node.
        /// </note>
        /// <para>
        /// In an ideal world, Microsoft/Docker would provide a way to deploy, run and
        /// debug applications into an existing Docker cluster as a container or swarm
        /// mode service.  At this time, there are baby steps in this direction: it's
        /// possible to F5 an application into a standalone container but this method
        /// currently supports running the application directly on Windows while trying
        /// to emulate some of the cluster environment.  Eventually, it will be possible
        /// to do the same in a local Docker container.
        /// </para>
        /// <para>
        /// This method provides a somewhat primitive simulation of running within a
        /// cluster by:
        /// </para>
        /// <list type="bullet">
        ///     <item>
        ///     Simulating the presence of a mounted and executed <b>/etc/neoncluster/env-host</b> script.
        ///     </item>
        ///     <item>
        ///     Simulated mounted Docker secrets.
        ///     </item>
        ///     <item>
        ///     Temporarily modifying the local <b>hosts</b> file to add host entries
        ///     for local services like Vault and Consul.
        ///     </item>
        ///     <item>
        ///     Emulating Docker secret delivery specified using and <paramref name="secrets"/> 
        ///     and using <see cref="GetSecret(string)"/>.
        ///     </item>
        /// </list>
        /// <note>
        /// This is also useful for external tools that are not executed on a cluster node
        /// (such as the <b>neon-cli</b>).  For example, class configures the local <n>hosts</n>
        /// file such that we'll be able to access the Cluster Vault and Consul servers over
        /// TLS.
        /// </note>
        /// <para>
        /// Pass a <see cref="DebugSecrets"/> instance as <paramref name="secrets"/> to emulate 
        /// the Docker secrets feature.  <see cref="DebugSecrets"/> may specify secrets as simple
        /// name/value pairs or may specify more complex Vault or Consul credentials.
        /// </para>
        /// <note>
        /// Applications may wish to use <see cref="NeonHelper.IsDevWorkstation"/> to detect when
        /// the application is running outside of a production environment and call this method
        /// early during application initialization.  <see cref="NeonHelper.IsDevWorkstation"/>
        /// uses the presence of the <b>DEV_WORKSTATION</b> environment variable to determine 
        /// this.
        /// </note>
        /// </remarks>
        public static ClusterProxy OpenRemoteCluster(DebugSecrets secrets = null, string loginPath = null)
        {
            if (IsConnected)
            {
                return NeonClusterHelper.Cluster;
            }

            if (loginPath != null)
            {
                ClusterLogin      = NeonHelper.JsonDeserialize<ClusterLogin>(File.ReadAllText(loginPath));
                ClusterLogin.Path = loginPath;
            }
            else
            {
                ClusterLogin = NeonClusterHelper.GetLogin();

                if (ClusterLogin == null)
                {
                    throw new InvalidOperationException("Connect failed because due to not being logged into a cluster.");
                }
            }

            log.LogInfo(() => $"Connecting to cluster [{ClusterLogin}].");

            OpenCluster(
                new Cluster.ClusterProxy(ClusterLogin,
                    (name, publicAddress, privateAddress) =>
                    {
                        var proxy = new NodeProxy<NodeDefinition>(name, publicAddress, privateAddress, ClusterLogin.GetSshCredentials(), null);

                        proxy.RemotePath += $":{NodeHostFolders.Setup}";
                        proxy.RemotePath += $":{NodeHostFolders.Tools}";

                        return proxy;
                    }));

            NeonClusterHelper.externalConnection = true;

            // Support emulated secrets too.

            secrets?.Realize(Cluster, ClusterLogin);

            NeonClusterHelper.secrets = secrets;

            return NeonClusterHelper.Cluster;
        }

        /// <summary>
        /// Simulates connecting the current application to the to the cluster.
        /// </summary>
        /// <param name="login">The cluster login information.</param>
        /// <returns>The <see cref="ClusterProxy"/>.</returns>
        public static ClusterProxy OpenCluster(ClusterLogin login)
        {
            if (IsConnected)
            {
                return NeonClusterHelper.Cluster;
            }

            log.LogInfo(() => $"Connecting to [{login.Username}@{login.ClusterName}].");

            ClusterLogin       = login;
            externalConnection = true;

            OpenCluster(
                new Cluster.ClusterProxy(ClusterLogin,
                    (name, publicAddress, privateAddress) =>
                    {
                        var proxy = new NodeProxy<NodeDefinition>(name, publicAddress, privateAddress, ClusterLogin.GetSshCredentials(), null);

                        proxy.RemotePath += $":{NodeHostFolders.Setup}";
                        proxy.RemotePath += $":{NodeHostFolders.Tools}";

                        return proxy;
                    }));


            return NeonClusterHelper.Cluster;
        }

        /// <summary>
        /// <para>
        /// Connects the current application to the to the cluster.
        /// </para>
        /// <note>
        /// This should only be called by services that are actually deployed in running 
        /// cluster containers that have mapped in the cluster node environment variables
        /// and host DNS mappings from <b>/etc/neoncluster/env-host</b>.
        /// </note>
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the current process does not appear to be running as a cluster container
        /// with the node environment variables mapped in.
        /// </exception>
        public static void OpenCluster()
        {
            log.LogInfo(() => "Connecting to cluster.");

            if (Environment.GetEnvironmentVariable("NEON_CLUSTER") == null)
            {
                // It looks like the host node's [/etc/neoncluster/env-host] script was not
                // mapped into the current container/process and executed to initialize 
                // important cluster service environment variables.  We'll go ahead and
                // set the important ones here.

                Environment.SetEnvironmentVariable("VAULT_ADDR", $"https://neon-vault.cluster:{NeonHostPorts.ProxyVault}");
                Environment.SetEnvironmentVariable("VAULT_DIRECT_ADDR", $"https://manage-0.neon-vault.cluster:{NetworkPorts.Vault}");
                Environment.SetEnvironmentVariable("CONSUL_HTTP_ADDR", $"neon-consul.cluster:{NetworkPorts.Consul}");
                Environment.SetEnvironmentVariable("CONSUL_HTTP_FULLADDR", $"http://neon-consul.cluster:{NetworkPorts.Consul}");
            }

            IsConnected        = true;
            externalConnection = false;
        }

        /// <summary>
        /// Connects to a cluster using a <see cref="ClusterProxy"/>.  Note that this version does not
        /// fully initialize the <see cref="ClusterLogin"/> property.
        /// </summary>
        /// <param name="cluster">The cluster proxy.</param>
        /// <returns>The <see cref="ClusterProxy"/>.</returns>
        public static ClusterProxy OpenCluster(ClusterProxy cluster)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null);

            if (IsConnected)
            {
                return NeonClusterHelper.Cluster;
            }

            IsConnected = true;
            Cluster     = cluster;

            if (ClusterLogin == null)
            {
                ClusterLogin =
                    new ClusterLogin()
                    {
                        Definition = cluster.Definition
                    };
            }

            // Initialize some properties.

            var clusterDefinition = Cluster.Definition;
            var node              = Cluster.FirstManager;

            // Simulate the environment variables initialized by a mounted [env-host] script.

            var hostingProvider = string.Empty;

            if (cluster.Definition.Hosting != null)
            {
                hostingProvider = cluster.Definition.Hosting.Environment.ToString().ToLowerInvariant();
            }

            Environment.SetEnvironmentVariable("NEON_CLUSTER", clusterDefinition.Name);
            Environment.SetEnvironmentVariable("NEON_DATACENTER", clusterDefinition.Datacenter);
            Environment.SetEnvironmentVariable("NEON_ENVIRONMENT", clusterDefinition.Environment.ToString().ToUpperInvariant());
            Environment.SetEnvironmentVariable("NEON_HOSTING", hostingProvider);
            Environment.SetEnvironmentVariable("NEON_NODE_NAME", node.Name);
            Environment.SetEnvironmentVariable("NEON_NODE_ROLE", node.Metadata.Role);
            Environment.SetEnvironmentVariable("NEON_NODE_IP", node.Metadata.PrivateAddress.ToString());
            Environment.SetEnvironmentVariable("NEON_NODE_SSD", node.Metadata.Labels.StorageSSD ? "true" : "false");
            Environment.SetEnvironmentVariable("NEON_APT_CACHE", clusterDefinition.PackageCache);
            Environment.SetEnvironmentVariable("VAULT_ADDR", $"{clusterDefinition.Vault.GetDirectUri(Cluster.FirstManager.Name)}");
            Environment.SetEnvironmentVariable("VAULT_DIRECT_ADDR", $"{clusterDefinition.Vault.GetDirectUri(Cluster.FirstManager.Name)}");
            Environment.SetEnvironmentVariable("CONSUL_HTTP_ADDR", $"{NeonHosts.Consul}:{clusterDefinition.Consul.Port}");
            Environment.SetEnvironmentVariable("CONSUL_HTTP_FULLADDR", $"http://{NeonHosts.Consul}:{clusterDefinition.Consul.Port}");

            // Temporarily modify the local DNS resolver hosts file so we'll be able
            // resolve common cluster host names.

            var hosts = new Dictionary<string, IPAddress>();

            hosts.Add(NeonHosts.Consul, node.PrivateAddress);
            hosts.Add(NeonHosts.Vault, node.PrivateAddress);
            hosts.Add($"{node.Name}.{NeonHosts.Vault}", node.PrivateAddress);
            hosts.Add(NeonHosts.LogEsData, node.PrivateAddress);

            NeonHelper.ModifyHostsFile(hosts);

            NeonClusterHelper.secrets = new Dictionary<string, string>();

            return NeonClusterHelper.Cluster;
        }

        /// <summary>
        /// Resets any temporary configurations made by <see cref="OpenRemoteCluster(DebugSecrets, string)"/>
        /// such as the modifications to the DNS resolver <b>hosts</b> file.  This should be called just
        /// before the application exits.
        /// </summary>
        public static void CloseCluster()
        {
            if (!IsConnected)
            {
                return;
            }

            IsConnected        = false;
            externalConnection = false;

            log.LogInfo("Emulating cluster close.");

            NeonHelper.ModifyHostsFile();
        }

        /// <summary>
        /// Verifies that a cluster is connected.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if a cluster is not connected.</exception>
        private static void VerifyConnected()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Cluster is not connected.");
            }
        }

        /// <summary>
        /// Returns the value of a named secret.
        /// </summary>
        /// <param name="name">The secret name.</param>
        /// <returns>The secret value or <c>null</c> if the secret doesn't exist.</returns>
        /// <remarks>
        /// <para>
        /// This method can be used to retrieve a secret provisioned to a container via the
        /// Docker secrets feature or a secret provided to <see cref="OpenRemoteCluster(DebugSecrets, string)"/> 
        /// when we're emulating running the application as a cluster container.
        /// </para>
        /// <para>
        /// Docker provisions secrets by mounting a <b>tmpfs</b> file system at <b>/var/run/secrets</b>
        /// and writing the secrets there as text files with the file name identifying the secret.
        /// When the application is not running in debug mode, this method simply attempts to read
        /// the requested secret from the named file in this folder.
        /// </para>
        /// </remarks>
        public static string GetSecret(string name)
        {
            var secretPath = Path.Combine(NodeHostFolders.DockerSecrets, name);

            try
            {
                if (File.Exists(secretPath))
                {
                    return File.ReadAllText(Path.Combine(NodeHostFolders.DockerSecrets, name));
                }
            }
            catch (IOException)
            {
                return null;
            }

            if (secrets == null)
            {
                return null;
            }

            string secret;

            if (secrets.TryGetValue(name, out secret))
            {
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
        /// This method can be used to retrieve a secret provisioned to a container via the
        /// Docker secrets feature or a secret provided to <see cref="OpenRemoteCluster(DebugSecrets, string)"/> 
        /// when we're emulating running the application as a cluster container.
        /// </para>
        /// <para>
        /// Docker provisions secrets by mounting a <b>tmpfs</b> file system at <b>/var/run/secrets</b>
        /// and writing the secrets there as text files with the file name identifying the secret.
        /// When the application is not running in debug mode, this method simply attempts to read
        /// the requested secret from the named file in this folder.
        /// </para>
        /// </remarks>
        public static T GetSecret<T>(string name)
            where T : class, new()
        {
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
        /// Returns a client that can access the cluster Consul service.
        /// </summary>
        /// <returns>A <see cref="ConsulClient"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if no cluster is connected.</exception>
        public static ConsulClient OpenConsul()
        {
            VerifyConnected();

            return new ConsulClient(
                config =>
                {
                    config.Address = ConsulUri;
                });
        }

        /// <summary>
        /// Returns an open cluster Consul client.
        /// </summary>
        /// <remarks>
        /// <note>
        /// The instance returned by the property is intended to be shared across
        /// the application and should <b>not be disposed</b>.  Use <see cref="OpenConsul"/>
        /// if you wish a private instance.
        /// </note>
        /// </remarks>
        public static ConsulClient Consul
        {
            get
            {
                VerifyConnected();

                return OpenConsul();
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

            if (externalConnection && false)
            {
                settings = new DockerSettings(Cluster.FirstManager.PrivateAddress);
            }
            else
            {
                settings = new DockerSettings("unix:///var/run/docker.sock");
            }

            return new DockerClient(settings);
        }

        /// <summary>
        /// Retrieves the current cluster definition from Consul, optionally comparing the
        /// the hashes of the persisted cluster with the cluster passed as a performance 
        /// improvement.
        /// </summary>
        /// <param name="cachedDefinition">The optional cached definition to be compared.</param>
        /// <param name="cancellationToken">The optional cancellation token.</param>
        /// <returns>The cluster definition.</returns>
        /// <exception cref="InvalidOperationException">Thrown if no cluster is connected.</exception>
        /// <exception cref="KeyNotFoundException">Thrown if the cluster definition has not been persisted to the cluster.</exception>
        /// <remarks>
        /// <para>
        /// If <paramref name="cachedDefinition"/> is passed as non-null then the method will first compute
        /// its hash and then retrieve the hash for current cluster definition from Consul.  Then the method
        /// compares the hash returned with the hash for the definition passed.  If the two hashes 
        /// match, then the definition passed is returned (avoiding a potentially large download from Consul).
        /// Otherwise, we'll retrieve the entire definition.
        /// </para>
        /// <para>
        /// This optimizes the common case where the <b>neon-cli</b> is caching the cluster definition
        /// locally within the cluster login and where the cluster definition changes infrequently.
        /// </para>
        /// </remarks>
        public static async Task<ClusterDefinition> GetDefinitionAsync(ClusterDefinition cachedDefinition = null, CancellationToken cancellationToken = default)
        {
            VerifyConnected();

            if (cachedDefinition != null && cachedDefinition.BareDocker)
            {
                // For bare clusters, just return the local definition because there
                // is no Consul service running.

                return cachedDefinition;
            }

            using (var consul = OpenConsul())
            {
                if (cachedDefinition != null)
                {
                    cachedDefinition.ComputeHash();

                    try
                    {
                        var hash = await consul.KV.GetString("neon/cluster/definition.hash");

                        if (hash == cachedDefinition.Hash)
                        {
                            return cachedDefinition;
                        }
                    }
                    catch (KeyNotFoundException)
                    {
                        // It's possible (but super rare) that the cluster definition might
                        // exist without the hash.  In this case, we'll just drop through
                        // and try reading the full definition below.
                    }
                    catch (Exception e)
                    {
                        // This is probably an [HttpRequestException] indicating that we 
                        // could not contact the cluster Consul.

                        throw new NeonClusterException("Unable to connect cluster.", e);
                    }
                }

                var deflated = await consul.KV.GetBytes("neon/cluster/definition.deflate");
                var json     = NeonHelper.DecompressString(deflated);

                return NeonHelper.JsonDeserialize<ClusterDefinition>(json);
            }
        }

        /// <summary>
        /// Persists the cluster definition to Consul.
        /// </summary>
        /// <param name="definition">The cluster definition.</param>
        /// <param name="cancellationToken">The optional cancellation token.</param>
        /// <exception cref="InvalidOperationException">Thrown if no cluster is connected.</exception>
        public async static Task PutDefinitionAsync(ClusterDefinition definition, CancellationToken cancellationToken = default)
        {
            Covenant.Requires<ArgumentNullException>(definition != null);

            VerifyConnected();

            definition.ComputeHash();   // Always recomute the hash

            var json     = NeonHelper.JsonSerialize(definition);
            var deflated = NeonHelper.CompressString(json);

            using (var consul = OpenConsul())
            {
                // We're going to persist the compressed definition and its
                // MD5 hash together in a transaction so they'll always be 
                // in sync.

                var operations = new List<KVTxnOp>()
                    {
                        new KVTxnOp("neon/cluster/definition.deflate", KVTxnVerb.Set) { Value = deflated },
                        new KVTxnOp("neon/cluster/definition.hash", KVTxnVerb.Set) { Value = Encoding.UTF8.GetBytes(definition.Hash) }
                    };

                await consul.KV.Txn(operations);
            }
        }

        /// <summary>
        /// Returns a client that can access the cluster Vault secret management service using a Vault token.
        /// </summary>
        /// <param name="token">The Vault token.</param>
        /// <returns>A <see cref="VaultClient"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if no cluster is connected.</exception>
        public static VaultClient OpenVault(string token)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(token));

            VerifyConnected();

            return VaultClient.OpenWithToken(VaultUri, token);
        }

        /// <summary>
        /// Returns a client that can access the cluster Vault secret management service specified credentials.
        /// </summary>
        /// <param name="credentials">The credentials.</param>
        /// <returns>A <see cref="VaultClient"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if no cluster is connected.</exception>
        public static VaultClient OpenVault(ClusterCredentials credentials)
        {
            Covenant.Requires<ArgumentNullException>(credentials != null);

            VerifyConnected();

            credentials.Validate();

            switch (credentials.Type)
            {
                case ClusterCredentialsType.VaultAppRole:

                    return VaultClient.OpenWithAppRole(VaultUri, credentials.VaultRoleId, credentials.VaultSecretId);

                case ClusterCredentialsType.VaultToken:

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
        ///     The globally unique request activity ID (from the <b>X-Activity-ID header</b>).
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
        ///     (|) characters.  Currently, only the the <b>Host</b> and <b>User-Agent</b> 
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
                throw new ArgumentException($"Credentials at [secret:{secretName}] do not include a username and password.");
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
        /// <returns>The <see cref="RabbitMQ.Client.IConnection"/>.</returns>
        public static async Task<RabbitMQ.Client.IConnection> OpenRabbitMQAsync(string connectionKey, string secretName, CancellationToken cancellationToken = default)
        {
            VerifyConnected();

            var connectionSettings = await Consul.KV.GetObject<RabbitMQSettings>(connectionKey, cancellationToken);
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

            return connectionSettings.OpenBroker(credentials);
        }
    }
}
