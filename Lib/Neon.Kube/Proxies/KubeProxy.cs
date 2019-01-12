//-----------------------------------------------------------------------------
// FILE:	    KubeProxy.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Docker;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

namespace Neon.Kube
{
    /// <summary>
    /// Remotely manages a neonKUBE.
    /// </summary>
    public class KubeProxy : IDisposable
    {
        private RunOptions                                                          defaultRunOptions;
        private Func<string, string, IPAddress, bool, SshProxy<NodeDefinition>>     nodeProxyCreator;
        private bool                                                                appendLog;
        private bool                                                                useBootstrap;

        /// <summary>
        /// Constructs a cluster proxy from a cluster login.
        /// </summary>
        /// <param name="hiveLogin">The cluster login.</param>
        /// <param name="nodeProxyCreator">
        /// The optional application supplied function that creates a node proxy
        /// given the node name, public address or FQDN, private address, and
        /// the node definition.
        /// </param>
        /// <param name="appendLog">Optionally have logs appended to an existing log file rather than creating a new one.</param>
        /// <param name="useBootstrap">
        /// Optionally specifies that the instance should use the HiveMQ client
        /// should directly eference to the HiveMQ cluster nodes for broadcasting
        /// proxy update messages rather than routing traffic through the <b>private</b>
        /// traffic manager.  This is used internally to resolve chicken-and-the-egg
        /// dilemmas for the traffic manager and proxy implementations that rely on
        /// HiveMQ messaging.
        /// </param>
        /// <param name="defaultRunOptions">
        /// Optionally specifies the <see cref="RunOptions"/> to be assigned to the 
        /// <see cref="SshProxy{TMetadata}.DefaultRunOptions"/> property for the
        /// nodes managed by the cluster proxy.  This defaults to <see cref="RunOptions.None"/>.
        /// </param>
        /// <remarks>
        /// The <paramref name="nodeProxyCreator"/> function will be called for each node in
        /// the cluster definition giving the application the chance to create the management
        /// proxy using the node's SSH credentials and also to specify logging.  A default
        /// creator that doesn't initialize SSH credentials and logging is used if a <c>null</c>
        /// argument is passed.
        /// </remarks>
        public KubeProxy(
            KubeContext hiveLogin,
            Func<string, string, IPAddress, bool, SshProxy<NodeDefinition>> nodeProxyCreator  = null,
            bool                                                            appendLog         = false,
            bool                                                            useBootstrap      = false,
            RunOptions                                                      defaultRunOptions = RunOptions.None)

            : this(hiveLogin.Definition, nodeProxyCreator, appendLog: appendLog, useBootstrap: useBootstrap, defaultRunOptions: defaultRunOptions)
        {
            Covenant.Requires<ArgumentNullException>(hiveLogin != null);

            this.HiveLogin = hiveLogin;
        }

        /// <summary>
        /// Constructs a cluster proxy from a cluster definition.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <param name="nodeProxyCreator">
        /// The application supplied function that creates a management proxy
        /// given the node name, public address or FQDN, private address, and
        /// the node definition.
        /// </param>
        /// <param name="appendLog">Optionally have logs appended to an existing log file rather than creating a new one.</param>
        /// <param name="useBootstrap">
        /// Optionally specifies that the instance should use the HiveMQ client
        /// should directly eference to the HiveMQ cluster nodes for broadcasting
        /// proxy update messages rather than routing traffic through the <b>private</b>
        /// traffic manager.  This is used internally to resolve chicken-and-the-egg
        /// dilemmas for the traffic manager and proxy implementations that rely on
        /// HiveMQ messaging.
        /// </param>
        /// <param name="defaultRunOptions">
        /// Optionally specifies the <see cref="RunOptions"/> to be assigned to the 
        /// <see cref="SshProxy{TMetadata}.DefaultRunOptions"/> property for the
        /// nodes managed by the cluster proxy.  This defaults to <see cref="RunOptions.None"/>.
        /// </param>
        /// <remarks>
        /// The <paramref name="nodeProxyCreator"/> function will be called for each node in
        /// the cluster definition giving the application the chance to create the management
        /// proxy using the node's SSH credentials and also to specify logging.  A default
        /// creator that doesn't initialize SSH credentials and logging is used if a <c>null</c>
        /// argument is passed.
        /// </remarks>
        public KubeProxy(
            KubeDefinition clusterDefinition,
            Func<string, string, IPAddress, bool, SshProxy<NodeDefinition>> nodeProxyCreator = null,
            bool                                                            appendLog = false,
            bool                                                            useBootstrap      = false,
            RunOptions                                                      defaultRunOptions = RunOptions.None)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null);

            if (nodeProxyCreator == null)
            {
                nodeProxyCreator =
                    (name, publicAddress, privateAddress, append) =>
                    {
                        var login = KubeHelper.KubeContext;

                        if (login != null)
                        {
                            return new SshProxy<NodeDefinition>(name, publicAddress, privateAddress, login.GetSshCredentials());
                        }
                        else
                        {
                            // Note that the proxy returned won't actually work because we're not 
                            // passing valid SSH credentials.  This is useful for situations where
                            // we need a cluster proxy for global things (like managing a hosting
                            // environment) where we won't need access to specific cluster nodes.

                            // $todo(jeff.lill):
                            //
                            // In the future, I expect that some cluster services (like [neon-cluster-manager])
                            // may need to connect to cluster nodes.  For this to work, we'd need to have
                            // some way to retrieve the SSH (and perhaps other credentials) from Vault
                            // and set them somewhere in the [NeonKube] class (perhaps as the current
                            // login).
                            //
                            // This note is repeated in: HiveLogin.cs

                            return new SshProxy<NodeDefinition>(name, publicAddress, privateAddress, SshCredentials.None);
                        }
                    };
            }

            this.Definition        = clusterDefinition;
            this.HiveLogin         = new KubeContext();
            this.useBootstrap      = useBootstrap;
            this.defaultRunOptions = defaultRunOptions;
            this.nodeProxyCreator  = nodeProxyCreator;
            this.appendLog         = appendLog;

            CreateNodes();
        }

        /// <summary>
        /// Releases all resources associated with the instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Releases all associated resources.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Returns the cluster name.
        /// </summary>
        public string Name
        {
            get { return Definition.Name; }
        }

        /// <summary>
        /// The associated <see cref="IHostingManager"/> or <c>null</c>.
        /// </summary>
        public IHostingManager HostingManager { get; set; }

        /// <summary>
        /// Returns the cluster login information.
        /// </summary>
        public KubeContext HiveLogin { get; set; }

        /// <summary>
        /// Indicates that any <see cref="SshProxy{TMetadata}"/> instances belonging
        /// to this cluster proxy should use public address/DNS names for SSH connections
        /// rather than their private cluster address.  This defaults to <c>false</c>
        /// and must be modified before establising a node connection to have any effect.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When this is <c>false</c>, connections will be established using node
        /// private addresses.  This implies that the current client has direct
        /// access to the cluster LAN via a direct connection or a VPN.
        /// </para>
        /// <para>
        /// Setting this to <c>true</c> is usually limited to cluster setup scenarios
        /// before the VPN is configured.  Exactly which public addresses and ports will
        /// be used when this is <c>true</c> is determined by the <see cref="HostingManager"/> 
        /// implementation for the current environment.
        /// </para>
        /// </remarks>
        public bool UseNodePublicAddress { get; set; } = false;

        /// <summary>
        /// Returns the cluster definition.
        /// </summary>
        public KubeDefinition Definition { get; private set; }

        /// <summary>
        /// Returns the read-only list of cluster node proxies.
        /// </summary>
        public IReadOnlyList<SshProxy<NodeDefinition>> Nodes { get; private set; }

        /// <summary>
        /// Returns the first cluster manager node as sorted by name.
        /// </summary>
        public SshProxy<NodeDefinition> FirstManager { get; private set; }

        /// <summary>
        /// Specifies the <see cref="RunOptions"/> to use when executing commands that 
        /// include secrets.  This defaults to <see cref="RunOptions.Redact"/> for best 
        /// security but may be changed to just <see cref="RunOptions.None"/> when debugging
        /// cluster setup.
        /// </summary>
        public RunOptions SecureRunOptions { get; set; } = RunOptions.Redact | RunOptions.FaultOnError;

        /// <summary>
        /// Enumerates the cluster manager node proxies sorted in ascending order by name.
        /// </summary>
        public IEnumerable<SshProxy<NodeDefinition>> Managers
        {
            get { return Nodes.Where(n => n.Metadata.IsManager).OrderBy(n => n.Name); }
        }

        /// <summary>
        /// Enumerates the cluster worker node proxies sorted in ascending order by name.
        /// </summary>
        public IEnumerable<SshProxy<NodeDefinition>> Workers
        {
            get { return Nodes.Where(n => n.Metadata.IsWorker).OrderBy(n => n.Name); }
        }

        /// <summary>
        /// Ensures that the current login has root privileges.
        /// </summary>
        /// <exception cref="KubeException">Thrown if the current login doesn't have root privileges</exception>
        public void EnsureRootPrivileges()
        {
            if (!HiveLogin.IsRoot)
            {
                throw new KubeException("Access Denied: Login doesn't have root privileges.");
            }
        }

        /// <summary>
        /// Initializes or reinitializes the <see cref="Nodes"/> list.  This is called during
        /// construction and also in rare situations where the node proxies need to be 
        /// recreated (e.g. after configuring node static IP addresses).
        /// </summary>
        public void CreateNodes()
        {
            var nodes = new List<SshProxy<NodeDefinition>>();

            foreach (var nodeDefinition in Definition.SortedNodes)
            {
                var node = nodeProxyCreator(nodeDefinition.Name, nodeDefinition.PublicAddress, IPAddress.Parse(nodeDefinition.PrivateAddress ?? "0.0.0.0"), appendLog);

                node.Hive              = this;
                node.DefaultRunOptions = defaultRunOptions;
                node.Metadata          = nodeDefinition;
                nodes.Add(node);
            }

            this.Nodes        = nodes;
            this.FirstManager = Nodes.Where(n => n.Metadata.IsManager).OrderBy(n => n.Name).First();
        }

        /// <summary>
        /// Returns the <see cref="SshProxy{TMetadata}"/> instance for a named node.
        /// </summary>
        /// <param name="nodeName">The node name.</param>
        /// <returns>The node proxy instance.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the name node is not present in the cluster.</exception>
        public SshProxy<NodeDefinition> GetNode(string nodeName)
        {
            var node = Nodes.SingleOrDefault(n => string.Compare(n.Name, nodeName, StringComparison.OrdinalIgnoreCase) == 0);

            if (node == null)
            {
                throw new KeyNotFoundException($"The node [{nodeName}] is not present in the cluster.");
            }

            return node;
        }

        /// <summary>
        /// Looks for the <see cref="SshProxy{TMetadata}"/> instance for a named node.
        /// </summary>
        /// <param name="nodeName">The node name.</param>
        /// <returns>The node proxy instance or <c>null</c> if the named node does not exist.</returns>
        public SshProxy<NodeDefinition> FindNode(string nodeName)
        {
            return Nodes.SingleOrDefault(n => string.Compare(n.Name, nodeName, StringComparison.OrdinalIgnoreCase) == 0);
        }

        /// <summary>
        /// Returns a manager node that is reachable via the network because it answers a ping.
        /// </summary>
        /// <param name="failureMode">Specifies what should happen when there are no reachable managers.</param>
        /// <returns>The reachable manager node or <c>null</c>.</returns>
        /// <exception cref="KubeException">
        /// Thrown if no managers are reachable and <paramref name="failureMode"/> 
        /// is passed as <see cref="ReachableHostMode.Throw"/>.
        /// </exception>
        public SshProxy<NodeDefinition> GetReachableManager(ReachableHostMode failureMode = ReachableHostMode.ReturnFirst)
        {
            var managerAddresses = Nodes
                .Where(n => n.Metadata.IsManager)
                .Select(n => n.PrivateAddress.ToString())
                .ToList();

            var reachableHost = NetHelper.GetReachableHost(managerAddresses, failureMode);

            if (reachableHost == null)
            {
                return null;
            }

            // Return the node that is assigned the reachable address.

            return Nodes.Where(n => n.PrivateAddress.ToString() == reachableHost.Host).First();
        }

        /// <summary>
        /// Selects a cluster node from the set of nodes that match a predicate that is 
        /// reachable via the network because it answers a ping.
        /// </summary>
        /// <param name="predicate">Predicate used to select the candidate nodes.</param>
        /// <param name="failureMode">Specifies what should happen when there are no reachable managers.</param>
        /// <returns>The reachable node or <c>null</c>.</returns>
        /// <exception cref="KubeException">
        /// Thrown if no nodes matching the predicate are reachable and <paramref name="failureMode"/> 
        /// is passed as <see cref="ReachableHostMode.Throw"/>.
        /// </exception>
        public SshProxy<NodeDefinition> GetReachableNode(Func<SshProxy<NodeDefinition>, bool> predicate, ReachableHostMode failureMode = ReachableHostMode.ReturnFirst)
        {
            var nodeAddresses = Nodes
                .Where(predicate)
                .Select(n => n.PrivateAddress.ToString())
                .ToList();

            var reachableHost = NetHelper.GetReachableHost(nodeAddresses, failureMode);

            if (reachableHost == null)
            {
                return null;
            }

            // Return the node that is assigned the reachable address.

            return Nodes.Where(n => n.PrivateAddress.ToString() == reachableHost.Host).First();
        }

        /// <summary>
        /// Performs cluster configuration steps.
        /// </summary>
        /// <param name="steps">The configuration steps.</param>
        public void Configure(ConfigStepList steps)
        {
            Covenant.Requires<ArgumentNullException>(steps != null);

            foreach (var step in steps)
            {
                step.Run(this);
            }
        }

        /// <summary>
        /// Returns steps that upload a text file to a set of cluster nodes.
        /// </summary>
        /// <param name="nodes">The cluster nodes to receive the upload.</param>
        /// <param name="path">The target path on the Linux node.</param>
        /// <param name="text">The input text.</param>
        /// <param name="tabStop">Optionally expands TABs into spaces when non-zero.</param>
        /// <param name="outputEncoding">Optionally specifies the output text encoding (defaults to UTF-8).</param>
        /// <param name="permissions">Optionally specifies target file permissions (must be <c>chmod</c> compatible).</param>
        /// <returns>The steps.</returns>
        public IEnumerable<ConfigStep> GetFileUploadSteps(IEnumerable<SshProxy<NodeDefinition>> nodes, string path, string text, int tabStop = 0, Encoding outputEncoding = null, string permissions = null)
        {
            Covenant.Requires<ArgumentNullException>(nodes != null);

            var steps = new ConfigStepList();

            foreach (var node in nodes)
            {
                steps.Add(UploadStep.Text(node.Name, path, text, tabStop, outputEncoding, permissions));
            }

            return steps;
        }

        /// <summary>
        /// Returns steps that upload a text file to a cluster node.
        /// </summary>
        /// <param name="node">The cluster node to receive the upload.</param>
        /// <param name="path">The target path on the Linux node.</param>
        /// <param name="text">The input text.</param>
        /// <param name="tabStop">Optionally expands TABs into spaces when non-zero.</param>
        /// <param name="outputEncoding">Optionally specifies the output text encoding (defaults to UTF-8).</param>
        /// <param name="permissions">Optionally specifies target file permissions (must be <c>chmod</c> compatible).</param>
        /// <returns>The steps.</returns>
        public IEnumerable<ConfigStep> GetFileUploadSteps(SshProxy<NodeDefinition> node, string path, string text, int tabStop = 0, Encoding outputEncoding = null, string permissions = null)
        {
            Covenant.Requires<ArgumentNullException>(node != null);

            return GetFileUploadSteps(new List<SshProxy<NodeDefinition>>() { node }, path, text, tabStop, outputEncoding, permissions);
        }

        /// <summary>
        /// Writes a message to the logs associated with all cluster nodes.
        /// </summary>
        /// <param name="message">Optionally specifies the log message.</param>
        public void LogLine(string message = null)
        {
            foreach (var node in Nodes)
            {
                node.LogLine(message);
            }
        }

        /// <summary>
        /// Returns the current time (UTC) for the cluster by fetching the 
        /// time from one of the cluster managers.
        /// </summary>
        /// <returns>The cluster's current <see cref="DateTime"/> (UTC).</returns>
        public DateTime GetTimeUtc()
        {
            var manager = GetReachableManager();

            return manager.GetTimeUtc();
        }
    }
}
