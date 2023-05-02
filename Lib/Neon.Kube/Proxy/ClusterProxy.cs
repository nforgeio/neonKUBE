//-----------------------------------------------------------------------------
// FILE:	    ClusterProxy.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using k8s;
using k8s.Models;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.ClusterDef;
using Neon.Kube.Config;
using Neon.Kube.Deployment;
using Neon.Kube.Hosting;
using Neon.Kube.Resources;
using Neon.Kube.Resources.Cluster;
using Neon.Kube.Setup;
using Neon.Kube.SSH;
using Neon.Net;
using Neon.Retry;
using Neon.SSH;
using Neon.Tasks;

namespace Neon.Kube.Proxy
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
    /// Used to manage a neonKUBE cluster.
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
            /// Only cluster lifecycle operations like <see cref="StartAsync()"/>, <see cref="StopAsync(StopMode)"/>,
            /// amd <see cref="DeleteClusterAsync(bool)"/> will be enabled.
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
        // Static members

        /// <summary>
        /// Creates a cluster proxy from a <see cref="KubeConfig"/> that will
        /// typically be used to manage an already deployed cluster.
        /// </summary>
        /// <param name="kubeConfig">The Kubernetes config with the current context set to the target cluster.</param>
        /// <param name="hostingManagerFactory">The hosting manager factory,</param>
        /// <param name="cloudMarketplace">
        /// <para>
        /// For cloud environments, this specifies whether the cluster should be provisioned
        /// using a VM image from the public cloud marketplace when <c>true</c> or from the
        /// private NEONFORGE image gallery for testing when <c>false</c>.  This is ignored
        /// for on-premise environments.
        /// </para>
        /// <note>
        /// Only NEONFORGE maintainers will have permission to use the private image.
        /// </note>
        /// </param>
        /// <param name="operation">Optionally identifies the operations that will be performed using the proxy.  This defaults to <see cref="Operation.LifeCycle"/>.</param>
        /// <param name="setupState">Optionally specifies cluster setup state.</param>
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
        /// <returns>The <see cref="ClusterProxy"/>.</returns>
        public static async Task<ClusterProxy> CreateAsync(
            KubeConfig              kubeConfig,
            IHostingManagerFactory  hostingManagerFactory,
            bool                    cloudMarketplace,
            Operation               operation         = Operation.LifeCycle,
            KubeSetupState          setupState        = null,
            string                  nodeImageUri      = null,
            string                  nodeImagePath     = null,
            NodeProxyCreator        nodeProxyCreator  = null,
            RunOptions              defaultRunOptions = RunOptions.None)
        {
            Covenant.Requires<ArgumentNullException>(kubeConfig != null, nameof(kubeConfig));
            Covenant.Requires<ArgumentNullException>(hostingManagerFactory != null, nameof(hostingManagerFactory));
            kubeConfig.Validate(needsCurrentCluster: true);

            var cluster = new ClusterProxy(
                kubeConfig:            kubeConfig,
                hostingManagerFactory: hostingManagerFactory,
                cloudMarketplace:      cloudMarketplace,
                operation:             operation,
                setupState:            setupState,
                nodeImageUri:          nodeImageUri,
                nodeImagePath:         nodeImagePath,
                nodeProxyCreator:      nodeProxyCreator,
                defaultRunOptions:     defaultRunOptions);

            cluster.KubeConfig = kubeConfig;
            cluster.Hosting    = new HostingOptions()
            {
                Environment = kubeConfig.Cluster.HostingEnvironment
            };

            await cluster.InitializeAsync();

            cluster.HostingManager = cluster.GetHostingManager(hostingManagerFactory, cloudMarketplace, operation, KubeHelper.LogFolder);

            return cluster;
        }

        /// <summary>
        /// Constructs a cluster proxy that will typically be used for deploying a new cluster.
        /// </summary>
        /// <param name="hostingManagerFactory">The hosting manager factory,</param>
        /// <param name="cloudMarketplace">
        /// <para>
        /// For cloud environments, this specifies whether the cluster should be provisioned
        /// using a VM image from the public cloud marketplace when <c>true</c> or from the
        /// private NEONFORGE image gallery for testing when <c>false</c>.  This is ignored
        /// for on-premise environments.
        /// </para>
        /// <note>
        /// Only NEONFORGE maintainers will have permission to use the private image.
        /// </note>
        /// </param>
        /// <param name="operation">Optionally identifies the operations that will be performed using the proxy.  This defaults to <see cref="Operation.LifeCycle"/>.</param>
        /// <param name="setupState">Optionally specifies cluster setup state.</param>
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
        /// <returns>The <see cref="ClusterProxy"/>.</returns>
        /// <remarks>
        /// <para>
        /// At least one of <paramref name="nodeImageUri"/> or <paramref name="nodeImagePath"/> must be passed
        /// for <see cref="GetHostingManager(IHostingManagerFactory, bool, Operation, string)"/> to work.
        /// </para>
        /// <para>set
        /// The <paramref name="nodeProxyCreator"/> function will be called for each node in
        /// the cluster definition giving the application the chance to create the node
        /// proxy using the node's SSH credentials and also to specify logging.  A default
        /// creator that doesn't initialize SSH credentials and logging is used if <c>null</c>
        /// is passed.
        /// </para>
        /// </remarks>
        public static async Task<ClusterProxy> CreateAsync(
            IHostingManagerFactory  hostingManagerFactory,
            bool                    cloudMarketplace,
            Operation               operation         = Operation.LifeCycle,
            KubeSetupState          setupState        = null,
            string                  nodeImageUri      = null,
            string                  nodeImagePath     = null,
            NodeProxyCreator        nodeProxyCreator  = null,
            RunOptions              defaultRunOptions = RunOptions.None)
        {
            Covenant.Requires<ArgumentNullException>(hostingManagerFactory != null, nameof(hostingManagerFactory));

            var cluster = new ClusterProxy(
                hostingManagerFactory: hostingManagerFactory,
                cloudMarketplace:      cloudMarketplace,
                operation:             operation,
                setupState:            setupState,
                nodeImageUri:          nodeImageUri,
                nodeImagePath:         nodeImagePath,
                nodeProxyCreator:      nodeProxyCreator,
                defaultRunOptions:     defaultRunOptions);

            await cluster.InitializeAsync();

            cluster.HostingManager = cluster.GetHostingManager(hostingManagerFactory, cloudMarketplace, operation, KubeHelper.LogFolder);

            return cluster;
        }

        //---------------------------------------------------------------------
        // Instance members

        private object                          syncLock = new object();
        private RunOptions                      defaultRunOptions;
        private NodeProxyCreator                nodeProxyCreator;
        private string                          nodeImageUri;
        private string                          nodeImagePath;
        private ClusterDeployment               clusterDeployment;
        private NodeSshProxy<NodeDefinition>    deploymentControlNode;
        private IKubernetes                     cachedK8s;

        /// <summary>
        /// Constructs a cluster proxy from a <see cref="KubeConfigContext"/> that will
        /// typically be used to manage an already deployed cluster.
        /// </summary>
        /// <param name="kubeConfig">The Kubernetes config.</param>
        /// <param name="hostingManagerFactory">The hosting manager factory,</param>
        /// <param name="cloudMarketplace">
        /// <para>
        /// For cloud environments, this specifies whether the cluster should be provisioned
        /// using a VM image from the public cloud marketplace when <c>true</c> or from the
        /// private NEONFORGE image gallery for testing when <c>false</c>.  This is ignored
        /// for on-premise environments.
        /// </para>
        /// <note>
        /// Only NEONFORGE maintainers will have permission to use the private image.
        /// </note>
        /// </param>
        /// <param name="operation">Optionally identifies the operations that will be performed using the proxy.  This defaults to <see cref="Operation.LifeCycle"/>.</param>
        /// <param name="setupState">Optionally specifies cluster setup state.</param>
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
        private ClusterProxy(
            KubeConfig              kubeConfig,
            IHostingManagerFactory  hostingManagerFactory,
            bool                    cloudMarketplace,
            Operation               operation         = Operation.LifeCycle,
            KubeSetupState          setupState        = null,
            string                  nodeImageUri      = null,
            string                  nodeImagePath     = null,
            NodeProxyCreator        nodeProxyCreator  = null,
            RunOptions              defaultRunOptions = RunOptions.None)
            
            : this(
                hostingManagerFactory: hostingManagerFactory, 
                cloudMarketplace:      cloudMarketplace,
                operation:             operation,
                setupState:            setupState,
                nodeImageUri:          nodeImageUri, 
                nodeImagePath:         nodeImagePath, 
                nodeProxyCreator:      nodeProxyCreator, 
                defaultRunOptions:     defaultRunOptions)
        {
            KubeConfig = kubeConfig;
        }

        /// <summary>
        /// Constructs a cluster proxy that will typically be used for deploying a new cluster.
        /// </summary>
        /// <param name="hostingManagerFactory">The hosting manager factory,</param>
        /// <param name="cloudMarketplace">
        /// <para>
        /// For cloud environments, this specifies whether the cluster should be provisioned
        /// using a VM image from the public cloud marketplace when <c>true</c> or from the
        /// private NEONFORGE image gallery for testing when <c>false</c>.  This is ignored
        /// for on-premise environments.
        /// </para>
        /// <note>
        /// Only NEONFORGE maintainers will have permission to use the private image.
        /// </note>
        /// </param>
        /// <param name="operation">Optionally identifies the operations that will be performed using the proxy.  This defaults to <see cref="Operation.LifeCycle"/>.</param>
        /// <param name="setupState">Optionally specifies cluster setup state.</param>
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
        /// for <see cref="GetHostingManager(IHostingManagerFactory, bool, Operation, string)"/> to work.
        /// </para>
        /// <para>
        /// The <paramref name="nodeProxyCreator"/> function will be called for each node in
        /// the cluster definition giving the application the chance to create the node
        /// proxy using the node's SSH credentials and also to specify logging.  A default
        /// creator that doesn't initialize SSH credentials and logging is used if <c>null</c>
        /// is passed.
        /// </para>
        /// </remarks>
        private ClusterProxy(
            IHostingManagerFactory  hostingManagerFactory,
            bool                    cloudMarketplace,
            Operation               operation         = Operation.LifeCycle,
            KubeSetupState          setupState        = null,
            string                  nodeImageUri      = null,
            string                  nodeImagePath     = null,
            NodeProxyCreator        nodeProxyCreator  = null,
            RunOptions              defaultRunOptions = RunOptions.None)
        {
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
                        if (KubeHelper.CurrentCluster != null && KubeHelper.CurrentCluster.IsNeonDesktop)
                        {
                            return new NodeSshProxy<NodeDefinition>(name, address, KubeHelper.CurrentCluster.ClusterInfo.SshCredentials);
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

            this.SetupState        = setupState;
            this.KubeConfig        = KubeHelper.KubeConfig.Clone();
            this.defaultRunOptions = defaultRunOptions;
            this.nodeProxyCreator  = nodeProxyCreator;

            if (setupState != null)
            {
                this.Hosting = setupState.ClusterDefinition.Hosting;
            }
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

            cachedK8s?.Dispose();
            cachedK8s = null;

            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Initializes the cluster proxy from setup state when available otherwise from the
        /// cluster's deployment details..
        /// </summary>
        /// <returns></returns>
        private async Task InitializeAsync()
        {
            // Initialize the cluster nodes.  There are two scenarios here:
            //
            //      * The [SetupState] property is set, indicating that the proxy is being
            //        used to deploy a cluster, so we'll obtain deployment details from the
            //        cluster definition.
            //
            //      * The [SetupState] operator is NULL, indicating that the proxy references
            //        an already deployed cluster.  In this case, we'll fetch the deployment
            //        details from the cluster itself.

            var nodes = new List<NodeSshProxy<NodeDefinition>>();

            void AddNode(NodeSshProxy<NodeDefinition> node, NodeDefinition metadata = null)
            {
                node.Cluster           = this;
                node.DefaultRunOptions = defaultRunOptions;
                node.Metadata          = metadata;

                nodes.Add(node);
            }

            this.Nodes = nodes;

            if (SetupState != null)
            {
                foreach (var nodeDefinition in SetupState.ClusterDefinition.SortedNodes)
                {
                    AddNode(nodeProxyCreator(nodeDefinition.Name, NetHelper.ParseIPv4Address(nodeDefinition.Address ?? "0.0.0.0")), nodeDefinition);
                }

                this.DeploymentControlNode = Nodes.Where(n => n.Metadata.IsControlPane).OrderBy(n => n.Name).First();
            }
            else
            {
                var configMap = await K8s.CoreV1.ReadNamespacedTypedConfigMapAsync<ClusterDeployment>(KubeConfigMapName.ClusterDeployment, KubeNamespace.NeonStatus);

                clusterDeployment = configMap.Data;
                Hosting           = clusterDeployment.Hosting.ToOptions();

                foreach (var nodeDeployment in clusterDeployment.Nodes)
                {
                    AddNode(nodeProxyCreator(nodeDeployment.Name, NetHelper.ParseIPv4Address(nodeDeployment.Address ?? "0.0.0.0")));
                }
            }
        }

        /// <summary>
        /// Returns the cluster name.
        /// </summary>
        public string Name => KubeConfig?.Cluster != null ? KubeConfig.Cluster.Name : SetupState.ClusterDefinition.Name;

        /// <summary>
        /// Returns the cluster ID.
        /// </summary>
        public string Id => SetupState != null ? SetupState.ClusterId : clusterDeployment.ClusterId;

        /// <summary>
        /// Returns the cluster domain.
        /// </summary>
        public string Domain => SetupState != null ? SetupState.ClusterDomain : clusterDeployment.ClusterDomain;

        /// <summary>
        /// Returns the cluster hosting options.
        /// </summary>
        public HostingOptions Hosting { get; private set; }

        /// <summary>
        /// Returns associated <see cref="IHostingManager"/>.
        /// </summary>
        public IHostingManager HostingManager { get; set; }

        /// <summary>
        /// Returns the Kubernetes config holding the current cluster.
        /// </summary>
        public KubeConfig KubeConfig { get; set; }

        /// <summary>
        /// Returns a read-only list of cluster node proxies.
        /// </summary>
        public IReadOnlyList<NodeSshProxy<NodeDefinition>> Nodes { get; private set; }

        /// <summary>
        /// Set to the setup state while the cluster is being provisioned,
        /// <c>null</c> otherwise.
        /// </summary>
        public KubeSetupState SetupState { get; set; }

        /// <summary>
        /// Returns the list of node host proxies for hosting managers that
        /// need to manipulate host machines. 
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is initialized by hosting managers such as XenServer and probably Hyper-V
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
        /// <para>
        /// Returns the first cluster control-plane node as sorted by name.
        /// </para>
        /// <note>
        /// This property works only for cluster proxies constructed for cluster setup from a
        /// <see cref="KubeSetupState"/>.  Use <see cref="GetReachableControlNode(ReachableHostMode)"/>
        /// for other scenarios.
        /// </note>
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the proxy was not created for deploying a cluster.</exception>
        public NodeSshProxy<NodeDefinition> DeploymentControlNode
        {
            get
            {
                if (deploymentControlNode == null)
                {
                    throw new InvalidOperationException($"[{nameof(DeploymentControlNode)}] is available only for [{nameof(ClusterProxy)}] instances created for deploying a cluster.");
                }

                return deploymentControlNode;
            }

            private set => deploymentControlNode = value;
        }

        /// <summary>
        /// Specifies the <see cref="RunOptions"/> to use when executing commands that 
        /// include secrets.  This defaults to <see cref="RunOptions.Redact"/> for best 
        /// security but may be changed to just <see cref="RunOptions.None"/> when debugging
        /// cluster setup.
        /// </summary>
        public RunOptions SecureRunOptions { get; set; } = RunOptions.Redact | RunOptions.FaultOnError;

        /// <summary>
        /// Enumerates the cluster control-plane node proxies sorted in ascending order by name.
        /// </summary>
        public IEnumerable<NodeSshProxy<NodeDefinition>> ControlNodes
        {
            get { return Nodes.Where(n => n.Metadata.IsControlPane).OrderBy(n => n.Name); }
        }

        /// <summary>
        /// Enumerates the cluster worker node proxies sorted in ascending order by name.
        /// </summary>
        public IEnumerable<NodeSshProxy<NodeDefinition>> Workers
        {
            get { return Nodes.Where(n => n.Metadata.IsWorker).OrderBy(n => n.Name); }
        }

        /// <summary>
        /// Ensures that the proxy is configured for provisioning the cluster.
        /// </summary>
        /// <exception cref="AssertException">Thrown when the proxy is not configured to provision the cluster.</exception>
        public void EnsureSetupMode()
        {
            Covenant.Assert(SetupState != null, $"[{nameof(ClusterProxy)}] is not configured for provisioning the cluster.");
        }

        /// <summary>
        /// Returns the hosting manager to use for provisioning and deploying the cluster as well
        /// as setting the <see cref="HostingManager"/> property.
        /// </summary>
        /// <param name="hostingManagerFactory">Specifies a custom hosting manager factory to override <see cref="HostingManagerFactory"/>.</param>
        /// <param name="cloudMarketplace">
        /// <para>
        /// For cloud environments, this specifies whether the cluster should be provisioned
        /// using a VM image from the public cloud marketplace when <c>true</c> or from the
        /// private NEONFORGE image gallery for testing when <c>false</c>.  This is ignored
        /// for on-premise environments.
        /// </para>
        /// <note>
        /// Only NEONFORGE maintainers will have permission to use the private image.
        /// </note>
        /// </param>
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
        private IHostingManager GetHostingManager(IHostingManagerFactory hostingManagerFactory, bool cloudMarketplace, Operation operation = Operation.LifeCycle, string logFolder = null)
        {
            Covenant.Assert(Hosting.Environment != HostingEnvironment.Unknown);

            hostingManagerFactory ??= new HostingManagerFactory();

            HostingManager hostingManager;

            if (KubeHelper.IsOnPremiseHypervisorEnvironment(Hosting.Environment))
            {
                if (!string.IsNullOrEmpty(nodeImageUri))
                {
                    hostingManager = hostingManagerFactory.GetManagerWithNodeImageUri(this, cloudMarketplace, nodeImageUri, logFolder: logFolder);
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

                            hostingManager = hostingManagerFactory.GetManager(this, cloudMarketplace);
                            break;

                        default:

                            throw new NotImplementedException();
                    }
                }
            }
            else
            {
                hostingManager = hostingManagerFactory.GetManager(this, cloudMarketplace, logFolder: logFolder);
            }

            if (hostingManager == null)
            {
                throw new NeonKubeException($"No hosting manager for the [{Hosting.Environment}] environment could be located.");
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
        /// Returns a control-plane node that appears to be reachable because it answers a ping.
        /// </summary>
        /// <param name="failureMode">Specifies what should happen when there are no reachable control-plane nodes.</param>
        /// <returns>The reachable control-plane node or <c>null</c>.</returns>
        /// <exception cref="NeonKubeException">
        /// Thrown if no control-plane nodes are reachable and <paramref name="failureMode"/> 
        /// is passed as <see cref="ReachableHostMode.Throw"/>.
        /// </exception>
        public NodeSshProxy<NodeDefinition> GetReachableControlNode(ReachableHostMode failureMode = ReachableHostMode.ReturnFirst)
        {
            var controlNodeAddresses = ControlNodes
                .Select(n => n.Address.ToString())
                .ToList();

            var reachableHost = NetHelper.GetReachableHost(controlNodeAddresses, failureMode);

            if (reachableHost == null)
            {
                return null;
            }

            // Return the node that is assigned the reachable address.

            return ControlNodes.Where(n => n.Address.ToString() == reachableHost.Host).First();
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
        /// Returns the <see cref="IKubernetes"/> client for the cluster.
        /// </summary>
        public IKubernetes K8s
        {
            get
            {
                // $note(jefflill):
                //
                // The lock here may be a bit excessive, but there's a slight chance that
                // multiple clients could be created without it.  [ClusterProxy] isn't really
                // intended for super high transaction volumes and even for applications 
                // doing that, they can mitigate this by save the client instance to a
                // local variable (or something) and using that instead.
                //
                // I thought briefly about adding a [ConnectK8s()] method that would need
                // to be called explicitly first, but that would make the class harder to
                // use and probably break things.

                lock (syncLock)
                {
                    if (cachedK8s != null)
                    {
                        return cachedK8s;
                    }

                    cachedK8s = KubeHelper.GetKubernetesClient(KubeConfig);

                    return cachedK8s;
                }
            }
        }

        /// <summary>
        /// Fetches the cluster deployment details for the proxied cluster.
        /// </summary>
        /// <returns>The <see cref="ClusterDeployment"/> details.</returns>
        public async Task<ClusterDeployment> GetDeploymentAsync()
        {
            if (SetupState != null)
            {
                return new ClusterDeployment(SetupState.ClusterDefinition, SetupState.ClusterId, SetupState.ClusterDomain);
            }
            else
            {
                var configMap = await K8s.CoreV1.ReadNamespacedTypedConfigMapAsync<ClusterDeployment>(KubeConfigMapName.ClusterDeployment, KubeNamespace.NeonStatus);

                return configMap.Data;
            }
        }

        //---------------------------------------------------------------------
        // Handy cluster utility methods.

        /// <summary>
        /// Executes a command on a Minio node using the <b>mc</b> Minio Client.
        /// </summary>
        /// <param name="mcCommand">The Minio Client command.</param>
        /// <param name="noSuccessCheck">Optionally disables the <see cref="ExecuteResponse.EnsureSuccess"/> check.</param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        /// <param name="retryPolicy">Optionally specifies a <see cref="IRetryPolicy"/>.</param>
        /// <returns>The <see cref="ExecuteResponse"/>.</returns>
        public async Task<ExecuteResponse> ExecMinioCommandAsync(
            string              mcCommand, 
            bool                noSuccessCheck    = false,
            IRetryPolicy        retryPolicy       = null,
            CancellationToken   cancellationToken = default)
        {
            await SyncContext.Clear;

            var minioPod = await K8s.CoreV1.GetNamespacedRunningPodAsync(KubeNamespace.NeonSystem, labelSelector: "app.kubernetes.io/name=minio-operator");
            var command  = new string[]
            {
                "/bin/bash",
                "-c",
                $"/mc --debug {mcCommand}"
            };

            if (retryPolicy != null)
            {
                return await K8s.NamespacedPodExecWithRetryAsync(
                    retryPolicy:        retryPolicy,
                    name:               minioPod.Name(),
                    namespaceParameter: minioPod.Namespace(),
                    container:          "minio-operator",
                    command:            command,
                    cancellationToken:  cancellationToken);
            }
            else
            {
                return await K8s.NamespacedPodExecAsync(
                    name:               minioPod.Name(),
                    namespaceParameter: minioPod.Namespace(),
                    container:          "minio-operator",
                    command:            command,
                    noSuccessCheck:     noSuccessCheck,
                    cancellationToken:  cancellationToken);
            }
        }

        /// <summary>
        /// Executes a PSQL command on one of the system database pods using the <b>pgsql</b>
        /// and returns the response.  The database command is executed in the context of the
        /// <see cref="KubeConst.NeonSystemDbAdminUser"/>.
        /// </summary>
        /// <param name="database">Identifies the target database.</param>
        /// <param name="psqlCommand">The PSQL command text.</param>
        /// <param name="noSuccessCheck">Optionally disables the <see cref="ExecuteResponse.EnsureSuccess"/> check.</param>
        /// <param name="retryPolicy">Optionally specifies a <see cref="IRetryPolicy"/>.</param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        /// <returns>The <see cref="ExecuteResponse"/>.</returns>
        public async Task<ExecuteResponse> ExecSystemDbCommandAsync(
            string              database, 
            string              psqlCommand, 
            bool                noSuccessCheck    = false,
            IRetryPolicy        retryPolicy       = null,
            CancellationToken   cancellationToken = default)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(database), nameof(database));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(psqlCommand), nameof(psqlCommand));

            psqlCommand = psqlCommand.Trim();

            if (!psqlCommand.EndsWith(";"))
            {
                psqlCommand += ';';
            }

            var sysDbPod = await K8s.CoreV1.GetNamespacedRunningPodAsync(KubeNamespace.NeonSystem, labelSelector: "app=neon-system-db");
            var command  = new string[]
            {
                "/bin/bash",
                "-c",
                $@"psql -U {KubeConst.NeonSystemDbAdminUser} {database} -t -c ""{psqlCommand};"""
            };

            if (retryPolicy != null)
            {
                return await K8s.NamespacedPodExecWithRetryAsync(
                    retryPolicy:        retryPolicy,
                    name:               sysDbPod.Name(),
                    namespaceParameter: sysDbPod.Namespace(),
                    container:          "postgres",
                    command:            command,
                    cancellationToken:  cancellationToken);
            }
            else
            {
                return await K8s.NamespacedPodExecAsync(
                    name:               sysDbPod.Name(),
                    namespaceParameter: sysDbPod.Namespace(),
                    container:          "postgres",
                    command:            command,
                    noSuccessCheck:     noSuccessCheck,
                    cancellationToken:  cancellationToken);
            }
        }

        /// <summary>
        /// Adds custom <see cref="V1NeonContainerRegistry"/> resources defined in the cluster definition to
        /// the cluster.  <b>neon-node-agent</b> will pick these up and regenerate the CRI-O configuration.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task AddContainerRegistryResourcesAsync()
        {
            await SyncContext.Clear;
            EnsureSetupMode();

            // We need to add the implict local cluster Harbor registry.

            var localRegistries = new List<Registry>();
            var harborCrioUser  = await KubeHelper.GetClusterLdapUserAsync(K8s, KubeConst.HarborCrioUser);

            // Add registries from the cluster definition.

            foreach (var registry in SetupState.ClusterDefinition.Container.Registries)
            {
                localRegistries.Add(registry);
            }

            // Write the custom resources to the cluster.

            foreach (var registry in localRegistries)
            {
                var clusterRegistry              = new V1NeonContainerRegistry();
                clusterRegistry.Metadata         = new V1ObjectMeta();
                clusterRegistry.Metadata.Name    = registry.Name;
                clusterRegistry.Spec             = new V1NeonContainerRegistry.RegistrySpec();
                clusterRegistry.Spec.SearchOrder = SetupState.ClusterDefinition.Container.SearchRegistries.IndexOf(registry.Location);
                clusterRegistry.Spec.Prefix      = registry.Prefix;
                clusterRegistry.Spec.Location    = registry.Location;
                clusterRegistry.Spec.Blocked     = registry.Blocked;
                clusterRegistry.Spec.Insecure    = registry.Insecure;
                clusterRegistry.Spec.Username    = registry.Username;
                clusterRegistry.Spec.Password    = registry.Password;

                await K8s.CustomObjects.CreateClusterCustomObjectAsync(clusterRegistry, registry.Name);
            }
        }

        /// <summary>
        /// Returns a dictionary mapping case-insensitive dashboard names to the
        /// dashboard information.
        /// </summary>
        /// <returns>The dashboard dictionary.</returns>
        public async Task<Dictionary<string, V1NeonDashboard>> ListClusterDashboardsAsync()
        {
            await SyncContext.Clear;

            var nameToDashboard = new Dictionary<string, V1NeonDashboard>(StringComparer.InvariantCultureIgnoreCase);
            var dashboards      = await K8s.CustomObjects.ListClusterCustomObjectAsync<V1NeonDashboard>();

            foreach (var dashboard in dashboards)
            {
                nameToDashboard.Add(dashboard.Metadata.Name, dashboard);
            }

            return nameToDashboard;
        }

        /// <summary>
        /// Persists the setup state for the cluster.
        /// </summary>
        public void SaveSetupState()
        {
            EnsureSetupMode();

            SetupState?.Save();
        }

        //---------------------------------------------------------------------
        // Cluster life cycle methods.

        /// <summary>
        /// Returns flags describing any optional capabilities supported by the cluster's hosting manager.
        /// </summary>
        public HostingCapabilities Capabilities
        {
            get
            {
                Covenant.Assert(HostingManager != null);

                return HostingManager.Capabilities;
            }
        }

        /// <summary>
        /// Determines whether the cluster is considered to be locked to prevent potentially distructive operations
        /// such as <b>Pause</b>, <b>Remove</b>, <b>Reset</b>, <b>Resume</b>, or <b>Stop</b>.  This is used
        /// to help prevent impacting production clusters by accident.
        /// </summary>
        /// /// <param name="cancellationToken">Optionally specifies the cancellation token.</param>
        /// <returns>
        /// <c>true</c> when the cluster is locked, <c>false</c> when it's unlocked or <c>null</c> when
        /// the lock status cannot be determined.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown then the proxy was created with the wrong constructor.</exception>
        public async Task<bool?> IsLockedAsync(CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;

            try
            {
                return (await K8s.CoreV1.ReadNamespacedTypedConfigMapAsync<ClusterLock>(KubeConfigMapName.ClusterLock, KubeNamespace.NeonStatus)).Data.IsLocked;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Locks the cluster by modifying the <see cref="KubeConfigMapName.ClusterLock"/> configmap
        /// in the <see cref="KubeNamespace.NeonStatus"/> namespace.  Potentially distructive
        /// operations like <b>Pause</b>, <b>Remove</b>, <b>Reset</b>, <b>Resume</b>, or <b>Stop</b>
        /// are not allowed on locked clusters.
        /// </summary>
        /// <param name="cancellationToken">Optionally specifies the cancellation token.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown then the proxy was created with the wrong constructor.</exception>
        public async Task LockAsync(CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;

            // We need to check and the potentially modify the existing lock configmap
            // so that Kubernetes can check for write conflicts.

            var clusterLock = await K8s.CoreV1.ReadNamespacedTypedConfigMapAsync<ClusterLock>(KubeConfigMapName.ClusterLock, KubeNamespace.NeonStatus);

            if (!clusterLock.Data.IsLocked)
            {
                clusterLock.Data.IsLocked = true;
                clusterLock.Update();

                await K8s.CoreV1.ReplaceNamespacedTypedConfigMapAsync(clusterLock);
            }
        }

        /// <summary>
        /// Unlocks the cluster by modifying the <see cref="KubeConfigMapName.ClusterLock"/> configmap
        /// in the <see cref="KubeNamespace.NeonStatus"/> namespace.  Potentially distructive
        /// operations like <b>Pause</b>, <b>Remove</b>, <b>Reset</b>, <b>Resume</b>, or <b>Stop</b>
        /// are not allowed on locked clusters.
        /// </summary>
        /// <param name="cancellationToken">Optionally specifies the cancellation token.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown then the proxy was created with the wrong constructor.</exception>
        public async Task UnlockAsync(CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;

            // We need to check and the potentially modify the existing lock configmap
            // so that Kubernetes can check for write conflicts.

            var lockStatusConfig = await K8s.CoreV1.ReadNamespacedTypedConfigMapAsync<ClusterLock>(
                name:               KubeConfigMapName.ClusterLock,
                namespaceParameter: KubeNamespace.NeonStatus,
                cancellationToken:  cancellationToken);

            if (lockStatusConfig.Data.IsLocked)
            {
                lockStatusConfig.Data.IsLocked = false;
                lockStatusConfig.Update();

                await K8s.CoreV1.ReplaceNamespacedTypedConfigMapAsync(lockStatusConfig);
            }
        }

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
            await SyncContext.Clear;
            Covenant.Assert(HostingManager != null);

            return await HostingManager.GetResourceAvailabilityAsync(reserveMemory: reserveMemory, reserveDisk: reserveDisk);
        }

        /// <summary>
        /// Creates a <see cref="ClusterInfo"/> instance from information held by the cluster proxy.
        /// </summary>
        public ClusterInfo CreateClusterInfo()
        {
            EnsureSetupMode();

            return new ClusterInfo()
            {
                CreationTimestamp = DateTime.UtcNow,

                ClusterVersion    = SetupState.ClusterDefinition.ClusterVersion,
                Name              = SetupState.ClusterDefinition.Name,
                Description       = SetupState.ClusterDefinition.Description,
                Environment       = SetupState.ClusterDefinition.Hosting.Environment,
                Purpose           = SetupState.ClusterDefinition.Purpose,
                Datacenter        = SetupState.ClusterDefinition.Datacenter,
                IsDesktop         = SetupState.ClusterDefinition.IsDesktop,
                Latitude          = SetupState.ClusterDefinition.Latitude,
                Longitude         = SetupState.ClusterDefinition.Longitude,
                FeatureOptions    = SetupState.ClusterDefinition.Features,

                ClusterId         = SetupState.ClusterId,
                Domain            = SetupState.ClusterDomain,
                PublicAddresses   = SetupState.PublicAddresses
            };
        }

        /// <summary>
        /// Returns information about a cluster.
        /// </summary>
        /// <returns>The <see cref="ClusterHealth"/>.</returns>
        public async Task<ClusterInfo> GetClusterInfoAsync()
        {
            await SyncContext.Clear;

            var clusterInfoConfig = await K8s.CoreV1.ReadNamespacedTypedConfigMapAsync<ClusterInfo>(
                name:               KubeConfigMapName.ClusterInfo,
                namespaceParameter: KubeNamespace.NeonStatus);

            return clusterInfoConfig.Data;
        }

        /// <summary>
        /// Set information about a cluster.
        /// </summary>
        /// <param name="clusterInfo">The information being set.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task SetClusterInfo(ClusterInfo clusterInfo)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(clusterInfo != null);

            var clusterInfoMap = new TypedConfigMap<ClusterInfo>(KubeConfigMapName.ClusterInfo, KubeNamespace.NeonStatus, clusterInfo);

            await K8s.CoreV1.ReplaceNamespacedTypedConfigMapAsync(clusterInfoMap);
        }

        /// <summary>
        /// Returns the health status of a cluster.
        /// </summary>
        /// <param name="timeout">Optionally specifies the maximum time to wait for the result.  This defaults to <b>15 seconds</b>.</param>
        /// <returns>The <see cref="ClusterHealth"/>.</returns>
        public async Task<ClusterHealth> GetClusterHealthAsync(TimeSpan timeout = default)
        {
            await SyncContext.Clear;
            Covenant.Assert(HostingManager != null);

            var clusterHealth = await HostingManager.GetClusterHealthAsync(timeout);

            switch (clusterHealth.State)
            {
                case ClusterState.Unknown:

                    clusterHealth.State   = ClusterState.Unhealthy;
                    clusterHealth.Summary = clusterHealth.Summary;
                    break;

                case ClusterState.Unhealthy:

                    clusterHealth.State   = ClusterState.Unhealthy;
                    clusterHealth.Summary = clusterHealth.Summary;
                    break;

                case ClusterState.Transitioning:
                case ClusterState.Healthy:

                    clusterHealth.State   = ClusterState.Healthy;
                    clusterHealth.Summary = "Cluster is healthy";
                    break;

                case ClusterState.Paused:

                    clusterHealth.State   = ClusterState.Paused;
                    clusterHealth.Summary = "Cluster is paused";
                    break;
            }

            return clusterHealth;
        }

        /// <summary>
        /// <para>
        /// Starts a cluster if it's not already running.
        /// </para>
        /// <note>
        /// This operation may not be supported for all environments.
        /// </note>
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="NotSupportedException">Thrown if the hosting environment doesn't support this operation.</exception>
        public async Task StartAsync()
        {
            await SyncContext.Clear;
            Covenant.Assert(HostingManager != null);

            await HostingManager.StartClusterAsync();

            // Wait for the cluster to report being healthy.

            await NeonHelper.WaitForAsync(
                async () =>
                {
                    try
                    {
                        var status = await GetClusterHealthAsync();

                        return status.State == ClusterState.Healthy;
                    }
                    catch (TimeoutException)
                    {
                        return false;
                    }
                },
                pollInterval: TimeSpan.FromSeconds(2.5),
                timeout:      TimeSpan.FromMinutes(5));
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
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="NotSupportedException">Thrown if the hosting environment doesn't support this operation.</exception>
        public async Task StopAsync(StopMode stopMode = StopMode.Graceful)
        {
            await SyncContext.Clear;
            Covenant.Assert(HostingManager != null);

            await HostingManager.StopClusterAsync(stopMode);

            // Wait for the cluster to report being stopped.

            await NeonHelper.WaitForAsync(
                async () =>
                {
                    try
                    {
                        var status = await GetClusterHealthAsync();

                        return status.State == ClusterState.Off || status.State == ClusterState.Paused;
                    }
                    catch (TimeoutException)
                    {
                        return false;
                    }
                },
                pollInterval: TimeSpan.FromSeconds(2.5),
                timeout:      TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// Resets the cluster to factory defaults by removing all non <b>neon-*</b> namespaces including
        /// <b>default</b> (which will be recreated to be empty) as well as restoring custom resources
        /// as required.
        /// </summary>
        /// <param name="options">
        /// Optionally specifies details about components to be reset.  This defaults to resetting 
        /// everything that makes sense.
        /// </param>
        /// <param name="progress">Optionally specified a callback to be called with human readable progress messages.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task ResetAsync(ClusterResetOptions options = null, Action<string> progress = null)
        {
            await SyncContext.Clear;
            Covenant.Assert(HostingManager != null);

            options ??= new ClusterResetOptions();

            //-----------------------------------------------------------------
            // Reset namespaces.

            if (!options.KeepNamespaces.Contains("*"))  // An ["*"] namespace indicates that all namespaces should be retained
            {
                progress?.Invoke("Resetting namespaces...");

                // Build a set of the namespaces to be retained.  This includes the internal
                // neonKUBE namespaces as well as any explicitly requested to be excluded
                // by the user.

                // List all of the existing cluster namespaces and then delete the contents
                // of all of those not being retained, including the [default] namespace.  Note
                // that we're going to perform these deletions in parallel to speed things up.
                //
                // We're going to SSH into the first control-plane and execute this via [kubectl] to
                // remove the contents of each namespace:
                //
                //      kubectl delete all --all --cascade --namespace NAMESPACE
                //
                // We're using [kubectl] here instead of using the API server because I believe
                // [kubectl] will be smarter about deleting resources in the correct order and
                // we don't want to have to implement that logic right now.

                var resetNamespaces = (await K8s.CoreV1.ListNamespaceAsync()).Items
                    .Where(item => !KubeNamespace.InternalNamespacesWithoutDefault.Contains(item.Name()))
                    .Where(item => !options.KeepNamespaces.Contains(item.Name()))
                    .Select(item => item.Metadata.Name)
                    .ToArray();

                var controlNode = GetReachableControlNode(ReachableHostMode.Throw);

                try
                {
                    controlNode.Connect();

                    // Note that we're going to limit the number commands in-flight so that
                    // we don't consume too much RAM (for thread stacks) here on the client
                    // as well as not overloading the control-plane node.

                    var parallelOptions = new ParallelOptions() { MaxDegreeOfParallelism = 10 };

                    Parallel.ForEach(resetNamespaces, parallelOptions,
                        @namespace =>
                        {
                            controlNode.SudoCommand("kubectl", new object[] { "delete", "all", "--all", "--cascade", "--namespace", @namespace });
                        });

                    // Delete all of the cleared namespaces other than [default].

                    Parallel.ForEach(resetNamespaces.Where(@namespace => @namespace != "default"), parallelOptions,
                        @namespace =>
                        {
                            controlNode.SudoCommand("kubectl", new object[] { "delete", "namespace", @namespace });
                        });

                    // The [kubectl] command doesn't actually delete everything in a namespace.  This isn't
                    // a problem for the non-default namespaces because we were able to delete them, but
                    // we'll need to explicitly remove any remaining resources in the [default] namespace.
                    //
                    // We're going to use the API server to list listing all namespaced resources
                    // available in the cluster, filter them to include only delete-able resources
                    // and resources without a "/" in their name.  Then we'll use [kubectl] to delete
                    // them all:
                    //
                    //      kubectl delete type0,type1,type2 --all --cascade --namespace default
                    //
                    // We're doing it this way because the API server isn't structured to make this easy.

                    var namespacedResourceTypes = (await K8s.CoreV1.GetAPIResourcesAsync())
                        .Resources
                        .Where(resource => resource.Namespaced && !resource.Name.Contains("/") && resource.Verbs.Contains("delete"))
                        .ToArray();

                    var sbResourceTypes = new StringBuilder();

                    foreach (var resourceType in namespacedResourceTypes)
                    {
                        sbResourceTypes.AppendWithSeparator(resourceType.Name, ",");
                    }

                    if (sbResourceTypes.Length > 0)
                    {
                        controlNode.SudoCommand("kubectl", new object[] { "delete", sbResourceTypes, "--all", "--cascade", "--namespace", "default" }).EnsureSuccess();
                    }
                }
                finally
                {
                    controlNode.Disconnect();
                }
            }

            //-----------------------------------------------------------------
            // Remove all neonKUBE custom resources.

            var neonKubeCrds = (await K8s.ApiextensionsV1.ListCustomResourceDefinitionAsync()).Items
                .Where(crd => KubeHelper.IsNeonKubeCustomResource(crd))
                .ToArray();

            // Remove any cluster scoped neonKUBE resources that are labeled indicating that
            // they should be removed on cluster reset.

            foreach (var crd in neonKubeCrds)
            {
                foreach (var version in crd.Spec.Versions.Select(ver => ver.Name))
                {
                    foreach (var resource in (await K8s.CustomObjects.ListClusterCustomObjectMetadataAsync(crd.Spec.Group, crd.Spec.Versions.First().Name, crd.Spec.Names.Plural, labelSelector: $"{NeonLabel.RemoveOnClusterReset}")).Items)
                    {
                        await K8s.CustomObjects.DeleteClusterCustomObjectAsync(crd.Spec.Group, crd.Spec.Versions.First().Name, crd.Spec.Names.Plural, resource.Name());
                    }
                }
            }

            //-----------------------------------------------------------------
            // Reset CRI-O
            //
            // Currently all we need to do is replace any existing [ContainerRegistry]
            // custom resources with fresh ones built from the original cluster definition.

            if (options.ResetCrio)
            {
                await Parallel.ForEachAsync((await K8s.CustomObjects.ListClusterCustomObjectAsync<V1NeonContainerRegistry>()).Items,
                    async (item, cancellationToken) =>
                    {
                        var metadata = item.GetKubernetesTypeMetadata();

                        await K8s.CustomObjects.DeleteClusterCustomObjectWithHttpMessagesAsync(metadata.Group, metadata.ApiVersion, metadata.PluralName, item.Name());
                    });

                await AddContainerRegistryResourcesAsync();
            }

            // $todo(jefflill):
            //
            // Create NodeTask custom resources to have node agents remove non-standard
            // container images from the node local CRI-O instance.

            //-----------------------------------------------------------------
            // Reset Auth (Dex/Glauth)

            // $todo(marcusbooyah): https://github.com/nforgeio/neonKUBE/issues/1480

            //-----------------------------------------------------------------
            // Reset Harbor

            // $todo(marcusbooyah): https://github.com/nforgeio/neonKUBE/issues/1480

            //-----------------------------------------------------------------
            // Reset Monitoring

            // $todo(marcusbooyah): https://github.com/nforgeio/neonKUBE/issues/1480
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
        /// <param name="deleteOrphans">
        /// Optionally specifies that VMs or clusters with the same VM or resource group prefix
        /// will be tewrminated and removed.  See the remarks for more information.
        /// </param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="NotSupportedException">Thrown if the hosting environment doesn't support this operation.</exception>
        /// <remarks>
        /// <para>
        /// The <paramref name="deleteOrphans"/> parameter is typically enabled when running unit tests
        /// via the <b>ClusterFixture</b> to ensure that clusters and VMs orphaned by previous interrupted
        /// test runs are removed in addition to removing the cluster specified by the cluster definition.
        /// </para>
        /// </remarks>
        public async Task DeleteClusterAsync(bool deleteOrphans = false)
        {
            await SyncContext.Clear;
            Covenant.Assert(HostingManager != null);

            var contextName = KubeContextName.Parse($"{KubeConst.RootUser}@{Name}");
            var context     = KubeHelper.KubeConfig.GetContext(contextName);

            await HostingManager.DeleteClusterAsync(deleteOrphans);

            if (context != null)
            {
                KubeHelper.KubeConfig.RemoveContext(context);
            }
        }
    }
}
