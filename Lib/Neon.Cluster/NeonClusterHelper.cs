//-----------------------------------------------------------------------------
// FILE:	    NeonClusterHelpers.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

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

using Consul;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Docker;
using Neon.Net;

namespace Neon.Cluster
{
    /// <summary>
    /// NeonCluster related utilties.
    /// </summary>
    public static class NeonClusterHelper
    {
        private static ILog                         log = LogManager.GetLogger(typeof(NeonClusterHelper));
        private static Dictionary<string, string>   secrets;
        private static bool                         externalConnection;

        private static class Windows
        {
            [DllImport("advapi32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool EncryptFile(string filename);
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
        public static string GetClusterRootFolder(bool ignoreNeonToolContainerVar = false)
        {
            if (!ignoreNeonToolContainerVar && InToolContainer)
            {
                return "/neoncluster";
            }

            if (NeonHelper.IsWindows)
            {
                var path = Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA"), "NeonForge", "neoncluster");

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
        /// Returns the path the folder containing login information for the known logins, creating
        /// the folder if it doesn't already exist.
        /// </summary>
        /// <returns>The folder path.</returns>
        /// <remarks>
        /// <para>
        /// This folder will exist on developer/operator workstations that have used the <b>neon-cli</b>
        /// to deploy and manage NeonClusters.  Each known cluster will have a JSON file named
        /// <b><i>cluster-name</i>.json</b> holding the serialized <see cref="Cluster.ClusterLogin"/> 
        /// information for the cluster.
        /// </para>
        /// <para>
        /// The <b>.current</b> file (if present) specifies the name of the cluster to be considered
        /// to be currently logged in.
        /// </para>
        /// </remarks>
        public static string GetClusterLoginFolder()
        {
            var path = Path.Combine(GetClusterRootFolder(), "logins");

            Directory.CreateDirectory(path);

            return path;
        }

        /// <summary>
        /// Returns the path to the file indicating which cluster is currently logged in.
        /// </summary>
        public static string CurrentClusterPath
        {
            get { return Path.Combine(GetClusterLoginFolder(), ".current"); }
        }

        /// <summary>
        /// Returns the path to the login information for the named cluster.
        /// </summary>
        /// <param name="userName">The operator's user name.</param>
        /// <param name="clusterName">The cluster name.</param>
        /// <returns>The path to the cluster's credentials file.</returns>
        public static string GetClusterLoginPath(string userName, string clusterName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(clusterName));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(userName));

            return Path.Combine(GetClusterLoginFolder(), $"{userName}@{clusterName}.login.json");
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
        public static ClusterLogin GetClusterLogin(bool noConnect = false)
        {
            if (File.Exists(CurrentClusterPath))
            {
                var current = CurrentClusterLogin.Load();
                var login   = NeonClusterHelper.SplitLogin(current.Login);

                if (!login.IsOK)
                {
                    File.Delete(CurrentClusterPath);
                    return null;
                }

                var userName         = login.UserName;
                var clusterName      = login.ClusterName;
                var clusterLoginPath = GetClusterLoginPath(userName, clusterName);

                if (File.Exists(clusterLoginPath))
                {
                    var clusterLogin = NeonClusterHelper.LoadClusterLogin(userName, clusterName);

                    clusterLogin.ViaVpn = current.ViaVpn;

                    if (noConnect)
                    {
                        return clusterLogin;
                    }

                    ConnectCluster(clusterLogin);

                    var clusterDefinition = GetLiveClusterDefinition(userName, clusterName);

                    clusterLogin.Definition.NodeDefinitions = clusterDefinition.NodeDefinitions;

                    return clusterLogin;
                }
                else
                {
                    // The referenced cluster file doesn't exist so quietly remove the ".current" file.

                    File.Delete(CurrentClusterPath);
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the path to the cached cluster definition for the named cluster.
        /// </summary>
        /// <param name="userName">The operator's user name.</param>
        /// <param name="clusterName">The cluster name.</param>
        /// <returns>The path to the cluster's credentials file.</returns>
        public static string GetCachedDefinitionPath(string userName, string clusterName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(clusterName));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(userName));

            return Path.Combine(GetClusterLoginFolder(), $"{userName}@{clusterName}.def.json");
        }

        /// <summary>
        /// Returns the current cluster definition from the cluster if we're
        /// currently logged in.
        /// </summary>
        /// <param name="userName">The operator's user name.</param>
        /// <param name="clusterName">The cluster name.</param>
        /// <returns>The current cluster definition or <c>null</c>.</returns>
        public static ClusterDefinition GetLiveClusterDefinition(string userName, string clusterName)
        {
            var clusterLoginPath = GetClusterLoginPath(userName, clusterName);

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

            var cachedDefinitionPath = GetCachedDefinitionPath(userName, clusterName);
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

            var clusterDefinition = GetClusterDefinitionAsync(cachedDefinition).Result;

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
        public static (bool IsOK, string UserName, string ClusterName) SplitLogin(string login)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(login));

            var fields = login.Split(new char[] { '@' }, 2);

            if (fields.Length != 2 || string.IsNullOrEmpty(fields[0]) || string.IsNullOrEmpty(fields[1]))
            {
                return (IsOK: false, UserName: null, ClusterName: null);
            }

            return (IsOK: true, UserName: fields[0], ClusterName: fields[1]);
        }

        /// <summary>
        /// Loads the cluster login information for the current cluster, performing any necessary decryption.
        /// </summary>
        /// <param name="userName">The operator's user name.</param>
        /// <param name="clusterName">The name of the target cluster.</param>
        /// <returns>The <see cref="Cluster.ClusterLogin"/>.</returns>
        public static ClusterLogin LoadClusterLogin(string userName, string clusterName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(userName));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(clusterName));

            var path         = Path.Combine(GetClusterLoginFolder(), $"{userName}@{clusterName}.login.json");
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
            get { return new Uri(Environment.GetEnvironmentVariable("VAULT_ADDR")); }
        }

        /// <summary>
        /// Returns the cluster's Consul URI.
        /// </summary>
        public static Uri ConsulUri
        {
            get { return new Uri(Environment.GetEnvironmentVariable("CONSUL_HTTP_FULLADDR")); }
        }

        /// <summary>
        /// Indicates whether the application is running outside of a Docker container
        /// but we're going to try to simulate the environment such that the application
        /// believe it is running in a container within a Docker cluster.  See 
        /// <see cref="ConnectCluster(DebugSecrets, string)"/> for more information.
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
        /// Attempts to simulate running the current application within the currently logged-in
        /// NeonCluster cluster for external tools as well as for development and debugging purposes.
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
        /// Take care to call <see cref="DisconnectCluster()"/> just before your application
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
        public static ClusterProxy ConnectCluster(DebugSecrets secrets = null, string loginPath = null)
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
                ClusterLogin = NeonClusterHelper.GetClusterLogin();

                if (ClusterLogin == null)
                {
                    throw new InvalidOperationException("Connect failed because due to not being logged into a cluster.");
                }
            }

            log.Info(() => $"Connecting to cluster [{ClusterLogin}].");

            ConnectCluster(
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
        /// Simulates running the current application within the cluster.
        /// </summary>
        /// <param name="login">The cluster login information.</param>
        /// <returns>The <see cref="ClusterProxy"/>.</returns>
        public static ClusterProxy ConnectCluster(ClusterLogin login)
        {
            if (IsConnected)
            {
                return NeonClusterHelper.Cluster;
            }

            log.Info(() => $"Connecting to [{login.Username}@{login.ClusterName}].");

            ClusterLogin       = login;
            externalConnection = true;

            ConnectCluster(
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
        /// Indicates that <see cref="NeonClusterHelper"/> should consider the current
        /// application to be connected to the cluster.
        /// </para>
        /// <note>
        /// This should only be called by services that are actually deployed in running 
        /// cluster containers that have mapped in the cluster node environment variables
        /// (such as <b>NEON_CLUSTER</b>).
        /// </note>
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the current process does not appear to be running as a cluster container
        /// with the node environment variables mapped in.
        /// </exception>
        public static void ConnectClusterService()
        {
            log.Info(() => "Connecting to cluster as a service.");

            if (Environment.GetEnvironmentVariable("NEON_CLUSTER") == null)
            {
                throw new InvalidOperationException("Current process does not appear to be running as a cluster container with the node environment variables mapped in.");
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
        public static ClusterProxy ConnectCluster(ClusterProxy cluster)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null);

            if (IsConnected)
            {
                return NeonClusterHelper.Cluster;
            }

            IsConnected = true;

            Cluster = cluster;

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
                hostingProvider = cluster.Definition.Hosting.Provider.ToString().ToLowerInvariant();
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

            // Modify the DNS resolver hosts file.

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
        /// Resets any temporary configurations made by <see cref="ConnectCluster(DebugSecrets, string)"/>
        /// such as the modifications to the DNS resolver <b>hosts</b> file.  This should be called just
        /// before the application exits.
        /// </summary>
        public static void DisconnectCluster()
        {
            if (!IsConnected)
            {
                return;
            }

            IsConnected        = false;
            externalConnection = false;

            log.Info("Emulating cluster disconnect.");

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
        /// Docker secrets feature or a secret provided to <see cref="ConnectCluster(DebugSecrets, string)"/> 
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
        public static async Task<ClusterDefinition> GetClusterDefinitionAsync(ClusterDefinition cachedDefinition = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            VerifyConnected();

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
        public async static Task PutClusterDefinitionAsync(ClusterDefinition definition, CancellationToken cancellationToken = default(CancellationToken))
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
        /// The log format consists fields separated by the <b>» (0xbb)</b>character.  None of the values 
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
                return $"traffic»tcp-v1»{proxyName}»%t»%ci»%b»%s»%si»%sp»%sslv»%sslc»%U»%B»%Tw»%Tc»%Tt»%ts»%ac»%fc»%bc»%sc»%rc»%sq»%bq";
            }
            else
            {
               return $"traffic»http-v1»{proxyName}»%tr»%ci»%b»%s»%si»%sp»%sslv»%sslc»%U»%B»%Tw»%Tc»%Tt»%ts»%ac»%fc»%bc»%sc»%rc»%sq»%bq»%ID»%Ti»%TR»%Tr»%Ta»%HM»%HP»%HQ»%HV»%ST»%hr";
            }
        }

        //---------------------------------------------------------------------
        // We manage VPN connections by launching OpenVPN specifying a configuration
        // file for the target cluster.  These configuration files and keys will be 
        // located in a (hopefully) encrypted or transient [tmpfs] folder.  Each
        // cluster configuration file path will look something like:
        //
        //      .../vpn/CLUSTER/client.conf     *** on Linux
        //      ...\vpn\CLUSTER\client.conf     *** on Windows
        //
        // where CLUSTER is the cluster name and [...] will vary based on the environment:
        //
        //      1. Windows:         located in the user's [AppData] folder.
        //      2. Linux/OSX:       located in the user's home folder.
        //      3. Tool Container:  located in [/dev/shm].
        //
        // Each connection folder includes the following files:
        //
        //      ca.crt          - Certificate authority's certificate
        //      client.conf     - OpenVPN client configuration
        //      client.crt      - Client certificate
        //      client.key      - Client private key
        //      open.cmd/.sh    - Script to manually start the client (for debugging)
        //      pid             - Client process ID
        //      status.txt      - Status file updated every [VpnStatusSeconds]
        //                        when a connection is established.
        //      ta.key          - Shared TLS HMAC key
        //
        // The code below determines VPN connection status by:
        //
        //      1. List all processes who's name start with "openvpn".
        //
        //      2. Comparing each process ID to the PID files in the VPN
        //         client folders.
        //
        //      3. Processes that match one of the folder PIDs are considered
        //         to be client VPN connections.
        //
        //      4. Connection status is determined by looking at the [status.txt]
        //         file.  This is updated every [VpnStatusInterval] when the
        //         connection is healthy.  The timestamp on the second line is 
        //         is compared to the current time to determine detection health.
        //         If no status file exists, we'll assume that OpenVPN is connecting.
        //
        //      5. VPN client folders with PIDs that don't match one of the 
        //         OpenVPN processes scanned above will be considered closed
        //         and the folder will be deleted.

        private static readonly int VpnStatusSeconds = 10;

        /// <summary>
        /// Enumerates the possible VPN states.
        /// </summary>
        public enum VpnState
        {
            /// <summary>
            /// The VPN client is in the process of connecting to the server.
            /// </summary>
            Connecting,

            /// <summary>
            /// The VPN connection is healthy.
            /// </summary>
            Healthy,

            /// <summary>
            /// The VPN connection is unhealthy.
            /// </summary>
            Unhealthy,
        }

        /// <summary>
        /// Holds information about a VPN client.
        /// </summary>
        public class VpnClient
        {
            /// <summary>
            /// Fully qualified path to the client folder.
            /// </summary>
            public string FolderPath { get; internal set; }

            /// <summary>
            /// The cluster name.
            /// </summary>
            public string ClusterName { get; internal set; }

            /// <summary>
            /// The connection state.
            /// </summary>
            public VpnState State { get; internal set; }

            /// <summary>
            /// The OpenVPN process ID.
            /// </summary>
            public int Pid { get; internal set; }
        }

        /// <summary>
        /// Returns the folder path where the VPN cluster client folders will
        /// be located.
        /// </summary>
        /// <returns>The folder path.</returns>
        private static string GetVpnFolder()
        {
            if (NeonClusterHelper.InToolContainer)
            {
                return "/dev/shm/vpn";
            }
            else
            {
                return Path.Combine(NeonClusterHelper.GetClusterRootFolder(), "vpn");
            }
        }

        /// <summary>
        /// Returns current NeonCluster VPN clients.
        /// </summary>
        /// <returns>The <see cref="VpnClient"/> instances.</returns>
        public static List<VpnClient> VpnListClients()
        {
            // Build a hashset of the IDs of the processes that could conceivably
            // be a cluster VPN client.

            var openVpnProcessIds = new HashSet<int>();

            foreach (var process in Process.GetProcesses())
            {
                if (process.ProcessName.StartsWith("openvpn", StringComparison.OrdinalIgnoreCase))
                {
                    openVpnProcessIds.Add(process.Id);
                }

                process.Dispose();
            }

            // Scan the VPN client folders.

            var vpnFolder = GetVpnFolder();
            var clients   = new List<VpnClient>();

            Directory.CreateDirectory(vpnFolder);

            foreach (var clientFolder in Directory.GetDirectories(vpnFolder))
            {
                var pidPath    = Path.Combine(clientFolder, "pid");
                var statusPath = Path.Combine(clientFolder, "status.txt");

                // Folders without a [pid] file will be ignored (but left alone).
                // This can happen if the OpenVPN client is in the process of
                // being started.  (This is a bit fragile).

                if (!File.Exists(pidPath))
                {
                    continue;
                }

                // Folders with [pid] files with IDs that are not in the hash
                // set map to VPN clients that are no longer running.  These
                // folders will be deleted and ignored.

                if (!int.TryParse(File.ReadAllText(pidPath), out var pid) || !openVpnProcessIds.Contains(pid))
                {
                    NeonHelper.DeleteFolderContents(clientFolder);
                    continue;
                }

                // We'll extract the cluster name from the last directory segment 
                // of the client folder.

                var clusterName = clientFolder.Split('/', '\\').Last();
                
                // Folders that map to a running process but without [status.txt]
                // will have [Connecting] status.

                var state = VpnState.Connecting;

                if (File.Exists(statusPath))
                {
                    // Folders with the [status.txt] timestamp greater or equal to 
                    // [currentTime - 2 * VpnStatusSeconds] are considered healthy.
                    // Older timestamps are considered unhealthy.
                    //
                    // The timestamp is on the second line of [status.txt] which
                    // will look something like:
                    //
                    //      Updated,Wed Mar 22 10:01:30 2017
                    //
                    // Note that it's possible for this operation to fail when
                    // OpenVPN just happens to try to update the status file at
                    // the exact moment we're reading it.  To mitegate this, we're
                    // going to try this up to three times, with a small delay 
                    // between attempts.

                    string timestampLine;

                    for (int tryCount = 1; tryCount <= 3; tryCount++)
                    {
                        try
                        {
                            using (var statusFile = new FileStream(statusPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            {
                                using (var statusReader = new StreamReader(statusFile))
                                {
                                    statusReader.ReadLine();
                                    timestampLine = statusReader.ReadLine();
                                }
                            }

                            if (timestampLine == null)
                            {
                                state = VpnState.Connecting;
                                break;
                            }

                            var fields = timestampLine.Split(new char[] { ',' }, 2);

                            if (fields.Length == 2)
                            {
                                var timestamp = DateTime.ParseExact(fields[1], "ddd MMM d HH:mm:ss yyyy", CultureInfo.InvariantCulture);

                                if (timestamp >= DateTime.Now - TimeSpan.FromSeconds(2 * VpnStatusSeconds))
                                {
                                    state = VpnState.Healthy;
                                }
                                else
                                {
                                    state = VpnState.Unhealthy;
                                }
                            }

                            break;
                        }
                        catch
                        {
                            if (tryCount == 3)
                            {
                                throw;
                            }

                            Task.Delay(TimeSpan.FromMilliseconds(50));
                        }
                    }
                }

                clients.Add(
                    new VpnClient()
                    {
                        ClusterName = clusterName,
                        FolderPath  = clientFolder,
                        Pid         = pid,
                        State       = state
                    });
            }

            return clients;
        }

        /// <summary>
        /// Returns the path to the client folder a named cluster.
        /// </summary>
        /// <param name="clusterName">The cluster name.</param>
        /// <returns>The folder path.</returns>
        private static string GetVpnClientFolder(string clusterName)
        {
            return Path.Combine(GetVpnFolder(), clusterName);
        }

        /// <summary>
        /// Determines if a VPN client is running for a cluster and returns it.
        /// </summary>
        /// <param name="clusterName">The cluster name.</param>
        /// <returns>The <see cref="VpnClient"/> or <c>null</c>.</returns>
        public static VpnClient VpnGetClient(string clusterName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(clusterName));

            return VpnListClients().FirstOrDefault(p => p.ClusterName.Equals(clusterName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Escapes backslash (\) characters on Windows by adding a second
        /// backslash to each.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <returns>The escaped output.</returns>
        private static string EscapeWinBackslash(string input)
        {
            if (NeonHelper.IsWindows)
            {
                return input.Replace("\\", "\\\\");
            }
            else
            {
                return input;
            }
        }

        /// <summary>
        /// Ensures that a VPN connection to a cluster is open and healthy.
        /// </summary>
        /// <param name="clusterLogin">The cluster login.</param>
        /// <param name="timeoutSeconds">Maximum seconds to wait for the VPN connection (defaults to 120 seconds).</param>
        /// <param name="onStatus">Optional callback that will be passed a status string.</param>
        /// <param name="onError">Optional callback that will be passed a error string.</param>
        /// <returns><c>true</c> if the connection was established (or has already been established).</returns>
        /// <exception cref="TimeoutException">
        /// Thrown if the VPN connection could not be established before the timeout expired.
        /// </exception>
        /// <exception cref="Exception">
        /// Thrown if the VPN connection is unhealthy.
        /// </exception>
        public static void VpnOpen(ClusterLogin clusterLogin, int timeoutSeconds = 120, Action<string> onStatus = null, Action<string> onError = null)
        {
            Covenant.Requires<ArgumentNullException>(clusterLogin != null);

            var    vpnClient = VpnGetClient(clusterLogin.ClusterName);
            string message;

            if (vpnClient != null)
            {
                switch (vpnClient.State)
                {
                    case VpnState.Healthy:

                        return;

                    case VpnState.Unhealthy:

                        message = $"[{clusterLogin.ClusterName}] VPN connection is unhealthy.";

                        onError?.Invoke(message);
                        throw new Exception(message);

                    case VpnState.Connecting:

                        onStatus?.Invoke($"Connecting [{clusterLogin.ClusterName}] VPN...");

                        try
                        {
                            NeonHelper.WaitFor(
                                () =>
                                {
                                    vpnClient = VpnGetClient(clusterLogin.ClusterName);

                                    if (vpnClient != null)
                                    {
                                        return vpnClient.State == VpnState.Healthy;
                                    }
                                    else
                                    {
                                        return false;
                                    }
                                },
                                TimeSpan.FromSeconds(timeoutSeconds));
                        }
                        catch (TimeoutException)
                        {
                            throw new TimeoutException($"VPN connection could not be established within [{timeoutSeconds}] seconds.");
                        }

                        return;

                    default:

                        throw new NotImplementedException();
                }
            }

            // Initialize the VPN folder for the cluster (deleting any
            // existing folder).

            var clientFolder = GetVpnClientFolder(clusterLogin.ClusterName);

            NeonHelper.DeleteFolderContents(clientFolder);
            Directory.CreateDirectory(clientFolder);

            File.WriteAllText(Path.Combine(clientFolder, "ca.crt"), clusterLogin.VpnCredentials.CaCert);
            File.WriteAllText(Path.Combine(clientFolder, "client.crt"), clusterLogin.VpnCredentials.UserCert);
            File.WriteAllText(Path.Combine(clientFolder, "client.key"), clusterLogin.VpnCredentials.UserKey);
            File.WriteAllText(Path.Combine(clientFolder, "ta.key"), clusterLogin.VpnCredentials.TaKey);

            // VPN servers are reached via the manager load balancer or router
            // using the forwarding port rule assigned to each manager node.

            Covenant.Assert(clusterLogin.Definition.Hosting.ManagerRouterAddress != null, "Manager load balancer address is required.");

            var servers     = string.Empty;
            var firstServer = true;

            foreach (var manager in clusterLogin.Definition.Managers)
            {
                if (firstServer)
                {
                    firstServer = false;
                }
                else
                {
                    servers += "\r\n";
                }

                servers += $"remote {clusterLogin.Definition.Hosting.ManagerRouterAddress} {manager.VpnFrontendPort}";
            }

            // Generate the configuration.

            var config =
$@"##############################################
# Sample client-side OpenVPN 2.0 config file #
# for connecting to multi-client server.     #
#                                            #
# This configuration can be used by multiple #
# clients, however each client should have   #
# its own cert and key files.                #
#                                            #
# On Windows, you might want to rename this  #
# file so it has a .ovpn extension           #
##############################################

# Specify that we are a client and that we
# will be pulling certain config file directives
# from the server.
client

# Use the same setting as you are using on
# the server.
# On most systems, the VPN will not function
# unless you partially or fully disable
# the firewall for the TUN/TAP interface.
;dev tap
dev tun

# Windows needs the TAP-Windows adapter name
# from the Network Connections panel
# if you have more than one.  On XP SP2,
# you may need to disable the firewall
# for the TAP adapter.
;dev-node MyTap

# Are we connecting to a TCP or
# UDP server?  Use the same setting as
# on the server.
proto tcp
;proto udp

# The hostname/IP and port of the server.
# You can have multiple remote entries
# to load balance between the servers.
{servers}

# Choose a random host from the remote
# list for load-balancing.  Otherwise
# try hosts in the order specified.
remote-random

# Keep trying indefinitely to resolve the
# host name of the OpenVPN server.  Very useful
# on machines which are not permanently connected
# to the internet such as laptops.
resolv-retry infinite

# Most clients don't need to bind to
# a specific local port number.
nobind

# Downgrade privileges after initialization (non-Windows only)
;user nobody
;group nobody

# Try to preserve some state across restarts.
;persist-key
;persist-tun

# If you are connecting through an
# HTTP proxy to reach the actual OpenVPN
# server, put the proxy server/IP and
# port number here.  See the man page
# if your proxy server requires
# authentication.
;http-proxy-retry # retry on connection failures
;http-proxy [proxy server] [proxy port #]

# Wireless networks often produce a lot
# of duplicate packets.  Set this flag
# to silence duplicate packet warnings.
;mute-replay-warnings

# SSL/TLS parms.
# See the server config file for more
# description.  It's best to use
# a separate .crt/.key file pair
# for each client.  A single ca
# file can be used for all clients.
ca ""{EscapeWinBackslash(Path.Combine(clientFolder, "ca.crt"))}""
cert ""{EscapeWinBackslash(Path.Combine(clientFolder, "client.crt"))}""
key ""{EscapeWinBackslash(Path.Combine(clientFolder, "client.key"))}""

# Verify server certificate by checking
# that the certicate has the nsCertType
# field set to ""server"".  This is an
# important precaution to protect against
# a potential attack discussed here:
#  http://openvpn.net/howto.html#mitm
#
# To use this feature, you will need to generate
# your server certificates with the nsCertType
# field set to ""server"".  The build-key-server
# script in the easy-rsa folder will do this.
ns-cert-type server

# If a tls-auth key is used on the server
# then every client must also have the key.
tls-auth ""{EscapeWinBackslash(Path.Combine(clientFolder, "ta.key"))}"" 1

# Select a cryptographic cipher.
# If the cipher option is used on the server
# then you must also specify it here.
cipher AES-256-CBC

# Enable compression on the VPN link.
# Don't enable this unless it is also
# enabled in the server config file.
comp-lzo

# Set log file verbosity.
verb 3

# Silence repeating messages
; mute 20
";
            var configPath = Path.Combine(clientFolder, "client.conf");
            var statusPath = Path.Combine(clientFolder, "status.txt");
            var pidPath    = Path.Combine(clientFolder, "pid");

            File.WriteAllText(configPath, config.Replace("\r", string.Empty));  // Linux-style line endings

            // Launch OpenVPN to establish a connection.

            var startInfo = new ProcessStartInfo("openvpn")
            {
                Arguments      = $"--config \"{configPath}\" --status \"{statusPath}\" {VpnStatusSeconds}",
                CreateNoWindow = true
            };

            var scriptPath = Path.Combine(clientFolder, NeonHelper.IsWindows ? "open.cmd" : "open.sh");

            File.WriteAllText(scriptPath, $"openvpn {startInfo.Arguments}");

            try
            {
                var process = Process.Start(startInfo);

                File.WriteAllText(pidPath, $"{process.Id}");

                // This detaches the OpenVPN process from this process so OpenVPN
                // will continue running after this process terminates.

                process.Dispose();
            }
            catch (Exception e)
            {
                NeonHelper.DeleteFolderContents(clientFolder);
                throw new Exception($"*** ERROR: Cannot launch [OpenVPN].  Make sure OpenVPN is installed and isl on the PATH.", e);
            }

            // Wait for the VPN connection.

            onStatus?.Invoke($"Connecting [{clusterLogin.ClusterName}] VPN...");

            vpnClient = VpnGetClient(clusterLogin.ClusterName);

            if (vpnClient != null)
            {
                if (vpnClient.State == VpnState.Healthy)
                {
                    onStatus?.Invoke($"VPN is connected");
                    return;
                }
                else if (vpnClient.State == VpnState.Unhealthy)
                {
                    message = $"[{clusterLogin.ClusterName}] VPN connection is unhealthy";

                    if (onError != null)
                    {
                        onError(message);
                    }

                    throw new Exception(message);
                }
            }

            try
            {
                NeonHelper.WaitFor(
                    () =>
                    {
                        vpnClient = VpnGetClient(clusterLogin.ClusterName);

                        if (vpnClient != null)
                        {
                            return vpnClient.State == VpnState.Healthy;
                        }
                        else
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(timeoutSeconds));

                // Wait an extra bit to give the VPN connection a chance to settle in.
                // This can help avoid some additional delays higher up the stack.

                Thread.Sleep(5000);

                onStatus?.Invoke($"Connected to [{clusterLogin.ClusterName}] VPN");
                return;
            }
            catch (TimeoutException)
            {
                throw new TimeoutException($"VPN connection could not be established within [{timeoutSeconds}] seconds.");
            }
        }

        /// <summary>
        /// Disconnects a VPN client.
        /// </summary>
        /// <param name="vpnClient">The VPN client.</param>
        private static void VpnDisconnect(VpnClient vpnClient)
        {
            if (vpnClient == null)
            {
                return;
            }

            try
            {
                var process = Process.GetProcessById(vpnClient.Pid);

                process.Kill();
            }
            catch
            {
                // Intentionally ignoring errors.
            }
            finally
            {
                // Remove the VPN files for somewhat better security.

                var clientFolder = GetVpnClientFolder(vpnClient.ClusterName);

                NeonHelper.DeleteFolderContents(clientFolder);
            }
        }

        /// <summary>
        /// Disconnects a VPN from a cluster or the VPNs for all clusters.
        /// </summary>
        /// <param name="clusterName">The target cluster name or <c>null</c> if all clusters are to be disconnected.</param>
        public static void VpnClose(string clusterName)
        {
            if (clusterName == null)
            {
                foreach (var vpnClient in VpnListClients())
                {
                    VpnDisconnect(vpnClient);
                }
            }
            else
            {
                VpnDisconnect(VpnGetClient(clusterName));
            }
        }
    }
}
