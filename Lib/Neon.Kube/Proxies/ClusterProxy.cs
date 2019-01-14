//-----------------------------------------------------------------------------
// FILE:	    ClusterProxy.cs
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
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

namespace Neon.Kube
{
    /// <summary>
    /// Used to remotely manage a cluster via SSH/SCP.
    /// </summary>
    public class ClusterProxy : IDisposable
    {
        private RunOptions                                                          defaultRunOptions;
        private Func<string, string, IPAddress, bool, SshProxy<NodeDefinition>>     nodeProxyCreator;
        private bool                                                                appendLog;

        /// <summary>
        /// Constructs a cluster proxy from a cluster login.
        /// </summary>
        /// <param name="kubeLogin">The cluster login.</param>
        /// <param name="nodeProxyCreator">
        /// The optional application supplied function that creates a node proxy
        /// given the node name, public address or FQDN, private address, and
        /// the node definition.
        /// </param>
        /// <param name="appendLog">Optionally have logs appended to an existing log file rather than creating a new one.</param>
        /// <param name="defaultRunOptions">
        /// Optionally specifies the <see cref="RunOptions"/> to be assigned to the 
        /// <see cref="SshProxy{TMetadata}.DefaultRunOptions"/> property for the
        /// nodes managed by the cluster proxy.  This defaults to <see cref="RunOptions.None"/>.
        /// </param>
        /// <remarks>
        /// The <paramref name="nodeProxyCreator"/> function will be called for each node in
        /// the cluster definition giving the application the chance to create the management
        /// proxy using the node's SSH credentials and also to specify logging.  A default
        /// creator that doesn't initialize SSH credentials and logging is used if <c>null</c>
        /// is passed.
        /// </remarks>
        public ClusterProxy(
            ClusterLogin kubeLogin,
            Func<string, string, IPAddress, bool, SshProxy<NodeDefinition>> nodeProxyCreator  = null,
            bool                                                            appendLog         = false,
            RunOptions                                                      defaultRunOptions = RunOptions.None)

            : this(kubeLogin.Definition, nodeProxyCreator, appendLog: appendLog, defaultRunOptions: defaultRunOptions)
        {
            Covenant.Requires<ArgumentNullException>(kubeLogin != null);

            this.KubeLogin = kubeLogin;
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
        /// <param name="defaultRunOptions">
        /// Optionally specifies the <see cref="RunOptions"/> to be assigned to the 
        /// <see cref="SshProxy{TMetadata}.DefaultRunOptions"/> property for the
        /// nodes managed by the cluster proxy.  This defaults to <see cref="RunOptions.None"/>.
        /// </param>
        /// <remarks>
        /// The <paramref name="nodeProxyCreator"/> function will be called for each node in
        /// the cluster definition giving the application the chance to create the management
        /// proxy using the node's SSH credentials and also to specify logging.  A default
        /// creator that doesn't initialize SSH credentials and logging is used if <c>null</c>
        /// is passed.
        /// </remarks>
        public ClusterProxy(
            ClusterDefinition clusterDefinition,
            Func<string, string, IPAddress, bool, SshProxy<NodeDefinition>> nodeProxyCreator = null,
            bool                                                            appendLog = false,
            RunOptions                                                      defaultRunOptions = RunOptions.None)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null);

            if (nodeProxyCreator == null)
            {
                nodeProxyCreator =
                    (name, publicAddress, privateAddress, append) =>
                    {
                        var login = KubeHelper.ClusterLogin;

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

                            return new SshProxy<NodeDefinition>(name, publicAddress, privateAddress, SshCredentials.None);
                        }
                    };
            }

            this.Definition        = clusterDefinition;
            this.KubeLogin         = new ClusterLogin();
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
        public ClusterLogin KubeLogin { get; set; }

        /// <summary>
        /// Returns the cluster definition.
        /// </summary>
        public ClusterDefinition Definition { get; private set; }

        /// <summary>
        /// Returns the read-only list of cluster node proxies.
        /// </summary>
        public IReadOnlyList<SshProxy<NodeDefinition>> Nodes { get; private set; }

        /// <summary>
        /// Returns the first cluster master node as sorted by name.
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
        /// Enumerates the cluster master node proxies sorted in ascending order by name.
        /// </summary>
        public IEnumerable<SshProxy<NodeDefinition>> Managers
        {
            get { return Nodes.Where(n => n.Metadata.IsMaster).OrderBy(n => n.Name); }
        }

        /// <summary>
        /// Enumerates the cluster worker node proxies sorted in ascending order by name.
        /// </summary>
        public IEnumerable<SshProxy<NodeDefinition>> Workers
        {
            get { return Nodes.Where(n => n.Metadata.IsWorker).OrderBy(n => n.Name); }
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

                node.Kube              = this;
                node.DefaultRunOptions = defaultRunOptions;
                node.Metadata          = nodeDefinition;
                nodes.Add(node);
            }

            this.Nodes        = nodes;
            this.FirstManager = Nodes.Where(n => n.Metadata.IsMaster).OrderBy(n => n.Name).First();
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
        /// Returns a master node that is reachable via the network because it answers a ping.
        /// </summary>
        /// <param name="failureMode">Specifies what should happen when there are no reachable masters.</param>
        /// <returns>The reachable master node or <c>null</c>.</returns>
        /// <exception cref="KubeException">
        /// Thrown if no managers are reachable and <paramref name="failureMode"/> 
        /// is passed as <see cref="ReachableHostMode.Throw"/>.
        /// </exception>
        public SshProxy<NodeDefinition> GetReachableMaster(ReachableHostMode failureMode = ReachableHostMode.ReturnFirst)
        {
            var masterAddresses = Nodes
                .Where(n => n.Metadata.IsMaster)
                .Select(n => n.PrivateAddress.ToString())
                .ToList();

            var reachableHost = NetHelper.GetReachableHost(masterAddresses, failureMode);

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
            var master = GetReachableMaster();

            return master.GetTimeUtc();
        }
    }
}
