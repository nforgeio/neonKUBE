//-----------------------------------------------------------------------------
// FILE:	    ClusterProxy.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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
using Neon.SSH;
using Neon.Time;

namespace Neon.Kube
{
    /// <summary>
    /// Creates a <see cref="NodeSshProxy{TMetadata}"/> for the specified host and server name,
    /// configuring logging and the credentials as specified by the global command
    /// line options.
    /// </summary>
    /// <param name="name">The node name.</param>
    /// <param name="address">The node's private IP address.</param>
    /// <returns>The <see cref="NodeSshProxy{TMetadata}"/>.</returns>
    public delegate NodeSshProxy<NodeDefinition> NodeProxyCreator(string name, IPAddress address);

    /// <summary>
    /// Used to remotely manage a cluster via SSH/SCP.
    /// </summary>
    public class ClusterProxy : IDisposable
    {
        //---------------------------------------------------------------------
        // Local types

        /// <summary>
        /// Enumerates the possible operations that can be performed with an
        /// <see cref="IHostingManager"/> managed by the <see cref="ClusterProxy"/>
        /// class.
        /// </summary>
        public enum Operation
        {
            /// <summary>
            /// <para>
            /// Only cluster lifecycle operations like <see cref="StartAsync(bool)"/>, <see cref="StopAsync(StopMode, bool)"/>,
            /// <see cref="RemoveAsync(bool, bool)"/>, and <see cref="GetNodeImageAsync(string, string)"/> will be enabled.
            /// </para>
            /// <note>
            /// These life cycle methods do not required a URI or file reference to a node image.
            /// </note>
            /// </summary>
            LifeCycle,

            /// <summary>
            /// A cluster will be prepared.
            /// </summary>
            Prepare,

            /// <summary>
            /// A cluster will be setup.
            /// </summary>
            Setup
        }

        //---------------------------------------------------------------------
        // Implementation

        private RunOptions              defaultRunOptions;
        private NodeProxyCreator        nodeProxyCreator;
        private string                  nodeImageUri;
        private string                  nodeImagePath;

        /// <summary>
        /// Constructs a cluster proxy from a <see cref="KubeConfigContext"/>.
        /// </summary>
        /// <param name="context">The Kubernetes confug context.</param>
        /// <param name="hostingManagerFactory">The hosting manager factory,</param>
        /// <param name="operation">Optionally identifies the operations that will be performed using the proxy.  This defaults to <see cref="Operation.LifeCycle"/>.</param>
        /// <param name="nodeImageUri">Optionally passed as the URI to the (GZIP compressed) node image.</param>
        /// <param name="nodeImagePath">Optionally passed as the local path to the (GZIP compressed) node image file.</param>
        /// <param name="nodeProxyCreator">
        /// The application supplied function that creates a management proxy
        /// given the node name, public address or FQDN, private address, and
        /// the node definition.
        /// </param>
        /// <param name="defaultRunOptions">
        /// Optionally specifies the <see cref="RunOptions"/> to be assigned to the 
        /// <see cref="LinuxSshProxy.DefaultRunOptions"/> property for the nodes managed
        /// by the cluster proxy.  This defaults to <see cref="RunOptions.None"/>.
        /// </param>
        public ClusterProxy(
            KubeConfigContext       context,
            IHostingManagerFactory  hostingManagerFactory,
            Operation               operation         = Operation.LifeCycle,
            string                  nodeImageUri      = null,
            string                  nodeImagePath     = null,
            NodeProxyCreator        nodeProxyCreator  = null,
            RunOptions              defaultRunOptions = RunOptions.None)
            
            : this(
                  clusterDefinition:        context.Extension.ClusterDefinition,
                  hostingManagerFactory:    hostingManagerFactory, 
                  operation:                operation, 
                  nodeImageUri:             nodeImageUri, 
                  nodeImagePath:            nodeImagePath, 
                  nodeProxyCreator:         nodeProxyCreator, 
                  defaultRunOptions:        defaultRunOptions)
        {
        }

        /// <summary>
        /// Constructs a cluster proxy from a cluster definition.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <param name="hostingManagerFactory">The hosting manager factory,</param>
        /// <param name="operation">Optionally identifies the operations that will be performed using the proxy.  This defaults to <see cref="Operation.LifeCycle"/>.</param>
        /// <param name="nodeImageUri">Optionally passed as the URI to the (GZIP compressed) node image.</param>
        /// <param name="nodeImagePath">Optionally passed as the local path to the (GZIP compressed) node image file.</param>
        /// <param name="nodeProxyCreator">
        /// The application supplied function that creates a management proxy
        /// given the node name, public address or FQDN, private address, and
        /// the node definition.
        /// </param>
        /// <param name="defaultRunOptions">
        /// Optionally specifies the <see cref="RunOptions"/> to be assigned to the 
        /// <see cref="LinuxSshProxy.DefaultRunOptions"/> property for the nodes managed
        /// by the cluster proxy.  This defaults to <see cref="RunOptions.None"/>.
        /// </param>
        /// <remarks>
        /// <para>
        /// At least one of <paramref name="nodeImageUri"/> or <paramref name="nodeImagePath"/> must be passed
        /// for <see cref="GetHostingManager(IHostingManagerFactory, Operation, string)"/> to work.
        /// </para>
        /// <para>
        /// The <paramref name="nodeProxyCreator"/> function will be called for each node in
        /// the cluster definition giving the application the chance to create the node
        /// proxy using the node's SSH credentials and also to specify logging.  A default
        /// creator that doesn't initialize SSH credentials and logging is used if <c>null</c>
        /// is passed.
        /// </para>
        /// </remarks>
        public ClusterProxy(
            ClusterDefinition       clusterDefinition,
            IHostingManagerFactory  hostingManagerFactory,
            Operation               operation         = Operation.LifeCycle,
            string                  nodeImageUri      = null,
            string                  nodeImagePath     = null,
            NodeProxyCreator        nodeProxyCreator  = null,
            RunOptions              defaultRunOptions = RunOptions.None)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));
            Covenant.Requires<ArgumentNullException>(hostingManagerFactory != null, nameof(hostingManagerFactory));

            if (!string.IsNullOrEmpty(nodeImageUri))
            {
                this.nodeImageUri = nodeImageUri;
            }
            else
            {
                this.nodeImagePath = nodeImagePath;
            }

            if (nodeProxyCreator == null)
            {
                nodeProxyCreator =
                    (name, address) =>
                    {
                        var context = KubeHelper.CurrentContext;

                        if (context != null && context.Extension != null)
                        {
                            return new NodeSshProxy<NodeDefinition>(name, address, context.Extension.SshCredentials);
                        }
                        else
                        {
                            // Note that the proxy returned won't actually work because we're not 
                            // passing valid SSH credentials.  This is useful for situations where
                            // we need a cluster proxy for global things (like managing a hosting
                            // environment) where we won't need access to specific cluster nodes.

                            return new NodeSshProxy<NodeDefinition>(name, address, SshCredentials.None);
                        }
                    };
            }

            this.Definition        = clusterDefinition;
            this.KubeContext       = KubeHelper.CurrentContext;
            this.defaultRunOptions = defaultRunOptions;
            this.nodeProxyCreator  = nodeProxyCreator;

            // Create the hosting manager.

            this.HostingManager = GetHostingManager(hostingManagerFactory, operation, KubeHelper.LogFolder);

            // Initialize the cluster nodes.

            var nodes = new List<NodeSshProxy<NodeDefinition>>();

            foreach (var nodeDefinition in Definition.SortedNodes)
            {
                var node = nodeProxyCreator(nodeDefinition.Name, NetHelper.ParseIPv4Address(nodeDefinition.Address ?? "0.0.0.0"));

                node.Cluster           = this;
                node.DefaultRunOptions = defaultRunOptions;
                node.Metadata          = nodeDefinition;

                nodes.Add(node);
            }

            this.Nodes       = nodes;
            this.FirstMaster = Nodes.Where(n => n.Metadata.IsMaster).OrderBy(n => n.Name).First();
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
            foreach (var node in Nodes)
            {
                node.Dispose();
            }

            HostingManager?.Dispose();
            HostingManager = null;

            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Returns the cluster name.
        /// </summary>
        public string Name => Definition.Name;

        /// <summary>
        /// The associated <see cref="IHostingManager"/>.
        /// </summary>
        public IHostingManager HostingManager { get; set; }

        /// <summary>
        /// Returns the cluster context.
        /// </summary>
        public KubeConfigContext KubeContext { get; set; }

        /// <summary>
        /// Returns the cluster definition.
        /// </summary>
        public ClusterDefinition Definition { get; private set; }

        /// <summary>
        /// Returns a read-only list of cluster node proxies.
        /// </summary>
        public IReadOnlyList<NodeSshProxy<NodeDefinition>> Nodes { get; private set; }

        /// <summary>
        /// Returns the list of node host proxies for hosting managers that
        /// need to manipulate host machines. 
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is initialized by hosting manages such as XenServer and probably Hyper-V
        /// in the future so that status changes for host machines will be included in 
        /// <see cref="SetupController{NodeMetadata}"/> UX status updates properly.
        /// </para>
        /// <para>
        /// Hosting managers should add any hosts to this list when the manager is constructed
        /// and then leave this list alone during provisioning.
        /// </para>
        /// </remarks>
        public List<LinuxSshProxy> Hosts { get; private set; } = new List<LinuxSshProxy>();

        /// <summary>
        /// Returns the first cluster master node as sorted by name.
        /// </summary>
        public NodeSshProxy<NodeDefinition> FirstMaster { get; private set; }

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
        public IEnumerable<NodeSshProxy<NodeDefinition>> Masters
        {
            get { return Nodes.Where(n => n.Metadata.IsMaster).OrderBy(n => n.Name); }
        }

        /// <summary>
        /// Enumerates the cluster worker node proxies sorted in ascending order by name.
        /// </summary>
        public IEnumerable<NodeSshProxy<NodeDefinition>> Workers
        {
            get { return Nodes.Where(n => n.Metadata.IsWorker).OrderBy(n => n.Name); }
        }

        /// <summary>
        /// Returns the hosting manager to use for provisioning and deploying the cluster as well
        /// as setting the <see cref="HostingManager"/> property.
        /// </summary>
        /// <param name="hostingManagerFactory">Specifies a custom hosting manager factory to override <see cref="HostingManagerFactory"/>.</param>
        /// <param name="operation">
        /// Specifies the operation(s) that will be performed using the <see cref="IHostingManager"/> returned.
        /// This is used to ensure that this instance already has the information required to complete the
        /// operation.  This defaults to <see cref="Operation.LifeCycle"/>.
        /// </param>
        /// <param name="logFolder">
        /// The folder where log files are to be written, otherwise or <c>null</c> or 
        /// empty if logging is disabled.
        /// </param>
        /// <returns>The <see cref="IHostingManager"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if no valid node image URI or path were passed to the constructor when required for
        /// the specified <paramref name="operation"/>.
        /// </exception>
        /// <remarks>
        /// <note>
        /// A valid node image URI or path must have been passed to the constructor for
        /// this to work.
        /// </note>
        /// </remarks>
        private IHostingManager GetHostingManager(IHostingManagerFactory hostingManagerFactory, Operation operation = Operation.LifeCycle, string logFolder = null)
        {
            hostingManagerFactory ??= new HostingManagerFactory();

            HostingManager hostingManager;

            if (!string.IsNullOrEmpty(nodeImageUri))
            {
                hostingManager = hostingManagerFactory.GetManagerWithNodeImageUri(this, nodeImageUri, logFolder: logFolder);
            }
            else if (!string.IsNullOrEmpty(nodeImagePath))
            {
                hostingManager = hostingManagerFactory.GetManagerWithNodeImageFile(this, nodeImagePath, logFolder: logFolder);
            }
            else
            {
                switch (operation)
                {
                    case Operation.Prepare:

                        throw new InvalidOperationException($"One of [{nameof(nodeImageUri)}] or [{nameof(nodeImagePath)}] needed to have been passed as non-NULL to the [{nameof(ClusterProxy)}] constructor for [{nameof(GetHostingManager)}] to support [{operation}].");

                    case Operation.LifeCycle:
                    case Operation.Setup:

                        hostingManager = hostingManagerFactory.GetManager(this);
                        break;

                    default:

                        throw new NotImplementedException();
                }
            }

            if (hostingManager == null)
            {
                throw new NeonKubeException($"No hosting manager for the [{this.Definition.Hosting.Environment}] environment could be located.");
            }

            return hostingManager;
        }

        /// <summary>
        /// Returns the <see cref="NodeSshProxy{TMetadata}"/> instance for a named node.
        /// </summary>
        /// <param name="nodeName">The node name.</param>
        /// <returns>The node definition.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the name node is not present in the cluster.</exception>
        public NodeSshProxy<NodeDefinition> GetNode(string nodeName)
        {
            var node = Nodes.SingleOrDefault(n => string.Compare(n.Name, nodeName, StringComparison.OrdinalIgnoreCase) == 0);

            if (node == null)
            {
                throw new KeyNotFoundException($"The node [{nodeName}] is not present in the cluster.");
            }

            return node;
        }

        /// <summary>
        /// Looks for the <see cref="NodeSshProxy{TMetadata}"/> instance for a named node.
        /// </summary>
        /// <param name="nodeName">The node name.</param>
        /// <returns>The node proxy instance or <c>null</c> if the named node does not exist.</returns>
        public NodeSshProxy<NodeDefinition> FindNode(string nodeName)
        {
            return Nodes.SingleOrDefault(n => string.Compare(n.Name, nodeName, StringComparison.OrdinalIgnoreCase) == 0);
        }

        /// <summary>
        /// Clears the status for the all of the cluster nodes.
        /// </summary>
        public void ClearNodeStatus()
        {
            foreach (var node in Nodes)
            {
                node.Status = string.Empty;
            }
        }

        /// <summary>
        /// Returns a master node that is reachable via the network because it answers a ping.
        /// </summary>
        /// <param name="failureMode">Specifies what should happen when there are no reachable masters.</param>
        /// <returns>The reachable master node or <c>null</c>.</returns>
        /// <exception cref="NeonKubeException">
        /// Thrown if no masters are reachable and <paramref name="failureMode"/> 
        /// is passed as <see cref="ReachableHostMode.Throw"/>.
        /// </exception>
        public NodeSshProxy<NodeDefinition> GetReachableMaster(ReachableHostMode failureMode = ReachableHostMode.ReturnFirst)
        {
            var masterAddresses = Masters
                .Select(n => n.Address.ToString())
                .ToList();

            var reachableHost = NetHelper.GetReachableHost(masterAddresses, failureMode);

            if (reachableHost == null)
            {
                return null;
            }

            // Return the node that is assigned the reachable address.

            return Masters.Where(n => n.Address.ToString() == reachableHost.Host).First();
        }

        /// <summary>
        /// Selects a cluster node from the set of nodes that match a predicate that is 
        /// reachable via the network because it answers a ping.
        /// </summary>
        /// <param name="predicate">Predicate used to select the candidate nodes.</param>
        /// <param name="failureMode">Specifies what should happen when there are no reachable nodes.</param>
        /// <returns>The reachable node or <c>null</c>.</returns>
        /// <exception cref="NeonKubeException">
        /// Thrown if no nodes matching the predicate are reachable and <paramref name="failureMode"/> 
        /// is passed as <see cref="ReachableHostMode.Throw"/>.
        /// </exception>
        public NodeSshProxy<NodeDefinition> GetReachableNode(Func<NodeSshProxy<NodeDefinition>, bool> predicate, ReachableHostMode failureMode = ReachableHostMode.ReturnFirst)
        {
            var nodeAddresses = Nodes
                .Where(predicate)
                .Select(n => n.Address.ToString())
                .ToList();

            var reachableHost = NetHelper.GetReachableHost(nodeAddresses, failureMode);

            if (reachableHost == null)
            {
                return null;
            }

            // Return the node that is assigned the reachable address.

            return Nodes.Where(n => n.Address.ToString() == reachableHost.Host).First();
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
        /// time from one of the cluster masters.
        /// </summary>
        /// <returns>The cluster's current <see cref="DateTime"/> (UTC).</returns>
        public DateTime GetTimeUtc()
        {
            var master = GetReachableMaster();

            return master.GetTimeUtc();
        }

        //---------------------------------------------------------------------
        // Cluster life cycle methods.

        /// <summary>
        /// Returns the availability of resources required to deploy a cluster.
        /// </summary>
        /// <param name="reserveMemory">Optionally specifies the amount of host memory (in bytes) to be reserved for host operations.</param>
        /// <param name="reserveDisk">Optionally specifies the amount of host disk disk (in bytes) to be reserved for host operations.</param>
        /// <returns>Details about whether cluster deployment can proceed.</returns>
        /// <remarks>
        /// <para>
        /// The optional <paramref name="reserveMemory"/> and <paramref name="reserveDisk"/> parameters
        /// can be used to specify memory and disk that are to be reserved for the host environment.  Hosting 
        /// manager implementations are free to ignore this when they don't really makse sense.
        /// </para>
        /// <para>
        /// This is currently used for Hyper-V based clusters running on a user workstation or laptop to ensure
        /// that deployed clusters don't adverserly impact the host machine too badly.
        /// </para>
        /// <para>
        /// These parameters don't really make sense for cloud or dedicated hypervisor hosting environments because
        /// those environemnts will still work well when all available resources are consumed.
        /// </para>
        /// </remarks>
        public async Task<HostingResourceAvailability> GetResourceAvailabilityAsync(long reserveMemory = 0, long reserveDisk = 0)
        {
            Covenant.Assert(HostingManager != null);

            return await HostingManager.GetResourceAvailabilityAsync(reserveMemory: reserveMemory, reserveDisk: reserveDisk);
        }

        /// <summary>
        /// Determines the status of a cluster.
        /// </summary>
        /// <param name="timeout">Optionally specifies the maximum time to wait for the result.  This defaults to <b>15 seconds</b>.</param>
        /// <returns>The <see cref="ClusterStatus"/>.</returns>
        public async Task<ClusterStatus> GetClusterStatusAsync(TimeSpan timeout = default)
        {
            Covenant.Assert(HostingManager != null);

            return await HostingManager.GetClusterStatusAsync(timeout);
        }

        /// <summary>
        /// <para>
        /// Starts a cluster if it's not already running.
        /// </para>
        /// <note>
        /// This operation may not be supported for all environments.
        /// </note>
        /// </summary>
        /// <param name="noWait">Optionally specifies that the method should not wait until the operation has completed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="NotSupportedException">Thrown if the hosting environment doesn't support this operation.</exception>
        public async Task StartAsync(bool noWait = false)
        {
            Covenant.Assert(HostingManager != null);

            await HostingManager.StartClusterAsync(noWait);
        }

        /// <summary>
        /// <para>
        /// Stops a cluster if it's running.
        /// </para>
        /// <note>
        /// This operation may not be supported for all environments.
        /// </note>
        /// </summary>
        /// <param name="stopMode">Optionally specifies how the cluster nodes are stopped.  This defaults to <see cref="StopMode.Graceful"/>.</param>
        /// <param name="noWait">Optionally specifies that the method should not wait until the operation has completed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="NotSupportedException">Thrown if the hosting environment doesn't support this operation.</exception>
        public async Task StopAsync(StopMode stopMode = StopMode.Graceful, bool noWait = false)
        {
            Covenant.Assert(HostingManager != null);

            await HostingManager.StopClusterAsync(stopMode, noWait);
        }

        /// <summary>
        /// <para>
        /// Removes an existing cluster by terminating any nodes and then removing node VMs
        /// and any related resources as well as the related local cluster login by default.  
        /// The cluster does not need to be running.  This method can optionally remove clusters
        /// or VMs potentially orphaned by interrupted unit tests as identified by a resource 
        /// group or VM name prefix.
        /// </para>
        /// <note>
        /// This operation may not be supported for all environments.
        /// </note>
        /// </summary>
        /// <param name="noWait">Optionally specifies that the method should not wait until the operation has completed.</param>
        /// <param name="removeOrphansByPrefix">
        /// Optionally specifies that VMs or clusters with the same resource group prefix or VM name
        /// prefix will be removed as well.  See the remarks for more information.
        /// </param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="NotSupportedException">Thrown if the hosting environment doesn't support this operation.</exception>
        /// <remarks>
        /// <para>
        /// The <paramref name="removeOrphansByPrefix"/> parameter is typically enabled when running unit tests
        /// via the <b>KubernetesFixture</b> to ensure that clusters and VMs orphaned by previous interrupted
        /// test runs are removed in addition to removing the cluster specified by the cluster definition.
        /// </para>
        /// </remarks>
        public async Task RemoveAsync(bool noWait = false, bool removeOrphansByPrefix = false)
        {
            Covenant.Assert(HostingManager != null);

            await HostingManager.RemoveClusterAsync(noWait, removeOrphansByPrefix);

            var contextName = KubeContextName.Parse($"{KubeConst.RootUser}@{Definition.Name}");
            var context     = KubeHelper.Config.GetContext(contextName);

            if (context != null)
            {
                KubeHelper.Config.RemoveContext(context);
            }

            var login = KubeHelper.GetClusterLogin(contextName);

            if (login != null)
            {
                login.Delete();
            }
        }

        /// <summary>
        /// <para>
        /// Starts a specific cluster node when it's not already running.
        /// </para>
        /// <note>
        /// This operation may not be supported for all environments.
        /// </note>
        /// </summary>
        /// <param name="nodeName">Identifies the target node.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task StartNodeAsync(string nodeName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nodeName));
            Covenant.Assert(HostingManager != null);

            await HostingManager.StartNodeAsync(nodeName);
        }

        /// <summary>
        /// <para>
        /// Stops a specific cluster node down when it's not already stopped or sleeping.
        /// </para>
        /// <note>
        /// This operation may not be supported for all environments.
        /// </note>
        /// </summary>
        /// <param name="nodeName">Identifies the target node.</param>
        /// <param name="stopMode">Optionally specifies how the node is stopped.  This defaults to <see cref="StopMode.Graceful"/>.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task StopNodeAsync(string nodeName, StopMode stopMode = StopMode.Graceful)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nodeName));
            Covenant.Assert(HostingManager != null);

            await HostingManager.StopNodeAsync(nodeName, stopMode);
        }

        /// <summary>
        /// <para>
        /// Retrieves the node image for a specified node in a cluster to a folder.  The node
        /// must already be stopped.  The node image file name will look like <b>NODE-NAME.EXTENSION</b>
        /// where <b>NODE-NAME</b> is the name of the node and <b>EXTENSION</b> will be the native
        /// extension for the hosting environment (e.g. <b>.vhdx</b> for Hyper-V, <b>.xva</b> for
        /// XenServer.
        /// </para>
        /// <note>
        /// This operation may not be supported for all environments.
        /// </note>
        /// </summary>
        /// <param name="nodeName">Identifies the node being captured.</param>
        /// <param name="folder">Path to the output folder.</param>
        /// <returns>The fully qualified path to the downloaded image file.</returns>
        /// <exception cref="NotSupportedException">Thrown if the hosting environment doesn't support this operation.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the node is not stopped or the node has multiple drives.</exception>
        public async Task<string> GetNodeImageAsync(string nodeName, string folder)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nodeName));
            Covenant.Assert(HostingManager != null);

            return await HostingManager.GetNodeImageAsync(nodeName, folder); 
        }
    }
}
