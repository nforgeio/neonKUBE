//-----------------------------------------------------------------------------
// FILE:	    AzureHostingManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Azure.Management.Compute.Fluent;
using Microsoft.Azure.Management.Compute.Fluent.Models;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.Network.Fluent;
using Microsoft.Azure.Management.Network.Fluent.LoadBalancer.Definition;
using Microsoft.Azure.Management.Network.Fluent.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;
using Neon.Time;

namespace Neon.Kube
{
    /// <summary>
    /// Manages cluster provisioning on the Google Cloud Platform.
    /// </summary>
    /// <remarks>
    /// </remarks>
    [HostingProvider(HostingEnvironments.Azure)]
    public class AzureHostingManager : HostingManager
    {
        // IMPLEMENTATION NOTE:
        // --------------------
        // Here's the original issue covering Azure provisioning and along with 
        // some discussion about how neonKUBE thinks about cloud deployments:
        // 
        //      https://github.com/nforgeio/neonKUBE/issues/908
        //
        // The remainder of this note will outline how Azure provisioning works.
        //
        // A neonKUBE Azure cluster will require provisioning these things:
        //
        //      * VNET
        //      * VMs & Drives
        //      * Load balancer with public IP
        //
        // In the future, we may relax the public load balancer requirement so
        // that virtual air-gapped clusters can be supported (more on that below).
        //
        // Nodes will be deployed in two Azure availability sets, one set for the
        // masters and the other one for the workers.  We're doing this to ensure
        // that there will always be a quorum of masters available during planned
        // Azure maintenance.
        //
        // By default, we're also going to create an Azure proximity placement group
        // for the cluster and then add both the master and worker availability sets
        // to the proximity group.  This ensures the shortest possible network latency
        // between all of the cluster nodes but with the increased chance that Azure
        // won't be able to satisfy the deployment constraints.  The user can disable
        // this placement groups via [AzureOptions.DisableProximityPlacement].
        //
        // The VNET will be configured using the cluster definitions's [NetworkOptions]
        // and the node IP addresses will be automatically assigned by default
        // but this can be customized via the cluster definition when necessary.
        // The load balancer will be created using a public IP address with
        // NAT rules forwarding network traffic into the cluster.  These rules
        // are controlled by [NetworkOptions.IngressRoutes] in the cluster
        // definition.  The target nodes in the cluster are indicated by the
        // presence of a [neonkube.io/node.ingress=true] label which can be
        // set explicitly for each node or assigned via a [NetworkOptions.IngressNodeSelector]
        // label selector.  neonKUBE will use reasonable defaults when necessary.
        //
        // Azure load balancers will be configured with two security rules:
        // [public] and [private].  By default, these rules will allow traffic
        // from any IP address with the [public] rule being applied to all
        // of the ingress routes and the [private] rules being applied to
        // temporary node-specific SSH rules used for cluster setup and maintainence.
        // You may wish to constrain these to specific IP addresses or subnets
        // for better security.
        //
        // VMs are currently based on the Ubuntu-20.04 Server image provided by 
        // published to the marketplace by Canonical.  They publish Gen1 and Gen2
        // images.  I believe Gen2 images will work on Azure Gen1 & Gen2 instances
        // so our images will be Gen2 based as well.
        //
        // This hosting manager will support creating VMs from the base Canonical
        // image as well as from custom images published to the marketplace by
        // neonFORGE.  The custom images will be preprovisioned with all of the
        // software required, making cluster setup much faster and reliable.  The
        // Canonical based images will need lots of configuration before they can
        // be added to a cluster.  Note that the neonFORGE images are actually
        // created by starting with a Canonical image and doing most of a cluster
        // setup on that image, so we'll continue supporting the raw Canonical
        // images.
        //
        // We're also going to be supporting two different ways of managing the
        // cluster deployment process.  The first approach will be to continue
        // controlling the process from a client application: [neon-cli] or
        // neonDESKTOP using SSH to connect to the nodes via temporary NAT
        // routes through the public load balancer.  neonKUBE clusters reserve
        // 1000 inbound ports (the actual range is configurable in the cluster
        // definition [CloudOptions]) and we'll automatically create NAT rule
        // for each node that routes external SSH traffic to the node.
        //
        // The second approach is to handle cluster setup from within the cloud
        // itself.  We're probably going to defer doing until after we go public
        // with neonCLOUD.  There's two ways of accomplising this: one is to
        // deploy a very small temporary VM within the customer's Azure subscription
        // that lives within the cluster VNET and coordinates things from there.
        // The other way is to is to manage VM setup from a neonCLOUD service,
        // probably using temporary load balancer SSH routes to access specific
        // nodes.  Note that this neonCLOUD service could run anywhere; it is
        // not restricted to running withing the same region as the customer
        // cluster.
        // 
        // Node instance and disk types and sizes are specified by the 
        // [NodeDefinition.Azure] property.  Instance types are specified
        // using standard Azure names, disk type is an enum and disk sizes
        // are specified via strings including optional [ByteUnits].  Provisioning
        // will need to verify that the requested instance and drive types are
        // actually available in the target Azure region and will also need
        // to map the disk size specified by the user to the closest matching
        // Azure disk size.
        //
        // neonKUBE will allow zero or more Azure drives to be attached to
        // a cluster node.  Nodes with zero attached drives will be created
        // will have only a limited amount of disk space available.  The OS
        // drive in this case will actually be backed implicitly by an Azure
        // drive so data there will remain after any VM maintence operations
        // performed by Azure.
        //
        // Azure VMs are also provided with ephemeral disk space local to the VM 
        // itself.  On neonKUBE cluster Linux VMs, the ephemeral block device will
        // be [/dev/sdb] but we don't currently do anything with this (like
        // create and mount a file system).
        //
        // More than one Azure drive can be mounted to a VM and the drives will
        // implicitly have the same size.  neonKUBE will configure these drives
        // as a large RAID0 striped array favoring capacity and performance over
        // reliability.  Azure says that the chance of a drive failure is between
        // 0.1-0.2% per year so for a node with 4 RAID0 drives, there's may be
        // a 1/125 chance per year of losing a one of the drives in the VM 
        // resulting in complete data loss which isn't too bad, especially for
        // situations where a redundant data store is deployed across multiple
        // nodes in the cluster.
        //
        // neonKUBE may support combining multiple Azure drives in to a redundant
        // RAID5 configuration in the future to dramatically lower the possible
        // failure risk.  This happens after provisioning so we'll be able to
        // support this for all clouds.

        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Relates hive node information with Azure VM information.
        /// </summary>
        private class AzureNode
        {
            private AzureHostingManager hostingManager;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="node">The associated node proxy.</param>
            /// <param name="hostingManager">The parent hosting manager.</param>
            public AzureNode(SshProxy<NodeDefinition> node, AzureHostingManager hostingManager)
            {
                Covenant.Requires<ArgumentNullException>(hostingManager != null, nameof(hostingManager));

                this.Node           = node;
                this.hostingManager = hostingManager;
            }

            /// <summary>
            /// Returns the associated node proxy.
            /// </summary>
            public SshProxy<NodeDefinition> Node { get; private set; }

            /// <summary>
            /// Returns the node name.
            /// </summary>
            public string Name => hostingManager.namePrefix + Node.Name;

            /// <summary>
            /// The associated Azure VM.
            /// </summary>
            public IVirtualMachine Vm { get; set; }

            /// <summary>
            /// The node's network interface within the <b>nodes</b> subnet.
            /// </summary>
            public INetworkInterface NodesNic { get; set; }

            /// <summary>
            /// The node's network interface within the <b>vpn</b> subnet (managers only).
            /// </summary>
            public INetworkInterface VpnNic { get; set; }

            /// <summary>
            /// The node's Azure public IP address or FQDN (or <c>null</c>).
            /// </summary>
            public IPublicIPAddress PublicAddress { get; set; }

            /// <summary>
            /// The public FQDN or IP address (as a string) to be used to connect to the
            /// node via SSH while provisioning the hive.  This will be set to the
            /// FQDN of <see cref="PublicAddress"/> if the hive nodes are being
            /// provisioned with addresses or else the FQDN address of the hive 
            /// manager or worker/pet load balancer.
            /// </summary>
            public string PublicSshAddress { get; set; }

            /// <summary>
            /// The SSH port to be used to connect to the node via SSH while provisioning
            /// the hive.  This will be the standard <see cref="NetworkPorts.SSH"/> if the 
            /// hive nodes are being provisioned with addresses or else a temporary NAT port
            /// configured on the appropriate load balancer.
            /// </summary>
            public int PublicSshPort { get; set; } = NetworkPorts.SSH;

            /// <summary>
            /// Returns the Azure name for the temporary NAT rule mapping a 
            /// frontend load balancer port to the SSH port for this node.
            /// </summary>
            public string SshNatRuleName
            {
                get { return $"neon-ssh-tcp-{Node.Name}"; }
            }

            /// <summary>
            /// Returns <c>true</c> if the node is a manager.
            /// </summary>
            public bool IsManager
            {
                get { return Node.Metadata.Role == NodeRole.Master; }
            }

            /// <summary>
            /// Returns <c>true</c> if the node is a worker.
            /// </summary>
            public bool IsWorker
            {
                get { return Node.Metadata.Role == NodeRole.Worker; }
            }
        }

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Ensures that the assembly hosting this hosting manager is loaded.
        /// </summary>
        public static void Load()
        {
            // We don't have to do anything here because the assembly is loaded
            // as a byproduct of calling this method.
        }

        //---------------------------------------------------------------------
        // Instance members

        private ClusterProxy                    cluster;
        private string                          clusterName;
        private string                          resourceGroup;
        private KubeSetupInfo                   setupInfo;
        private HostingOptions                  hostingOptions;
        private AzureOptions                    azureOptions;
        private NetworkOptions                  networkOptions;
        private Dictionary<string, AzureNode>   nodeDictionary;

        // Azure requires that the various components that need to be provisioned
        // for the cluster have names.  We're going to generate these in the constructor.
        // Top level component names will be prefixed by
        //
        //      neon-<cluster-name>-
        //
        // to avoid conflicts with other clusters or things deployed to the same resource
        // group.  For example if there's already a cluster in the same resource group,
        // we wouldn't want to node names like "manager-0" to conflict across multiple 
        // clusters.

        private string                      namePrefix;
        private string                      publicIpName;
        private string                      vnetName;
        private string                      subnetName;
        private string                      masterAvailabilitySetName;
        private string                      workerAvailabilitySetName;
        private string                      proximityPlacementGroupName;
        private string                      loadbalancerName;
        private string                      loadbalancerFrontendName;
        private string                      loadbalancerBackendName;
        private string                      loadbalancerProbeName;
        private string                      publicNetworkSecurityGroupName;
        private string                      privateNetworkSecurityGroupName;
        private string                      outboudNetworkSecurityGroupName;

        // These fields hold various Azure components while provisioning is in progress.

        private IAzure                      azure;
        private IPublicIPAddress            publicIp;
        private INetwork                    vnet;
        private ILoadBalancer               loadBalancer;
        private IAvailabilitySet            masterAvailabilitySet;
        private IAvailabilitySet            workerAvailabilitySet;
        private INetworkSecurityGroup       publicNetworkSecurityGroup;
        private INetworkSecurityGroup       privateNetworkSecurityGroup;
        private INetworkSecurityGroup       outboundNetworkSecurityGroup;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="cluster">The cluster being managed.</param>
        /// <param name="setupInfo">Specifies the cluster setup information.</param>
        /// <param name="logFolder">
        /// The folder where log files are to be written, otherwise or <c>null</c> or 
        /// empty if logging is disabled.
        /// </param>
        public AzureHostingManager(ClusterProxy cluster, KubeSetupInfo setupInfo, string logFolder = null)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null, nameof(cluster));
            Covenant.Requires<ArgumentNullException>(setupInfo != null, nameof(setupInfo));

            cluster.HostingManager = this;

            this.cluster                         = cluster;
            this.clusterName                     = cluster.Name;
            this.resourceGroup                   = cluster.Definition.Hosting.Azure.ResourceGroup ?? $"neon-{clusterName}";
            this.setupInfo                       = setupInfo;
            this.hostingOptions                  = cluster.Definition.Hosting;
            this.azureOptions                    = cluster.Definition.Hosting.Azure;
            this.networkOptions                  = cluster.Definition.Network;

            // Initialize the component names as they will be deployed to Azure.

            this.namePrefix                      = $"neon-{clusterName}-";
            this.publicIpName                    = namePrefix + "public-ip";
            this.vnetName                        = namePrefix + "vnet";
            this.masterAvailabilitySetName       = namePrefix + "master-availability-set";
            this.workerAvailabilitySetName       = namePrefix + "worker-availability-set";
            this.proximityPlacementGroupName     = namePrefix + "proxmity-group";
            this.loadbalancerName                = namePrefix + "load-balancer";

            // These names are relative to another component, so they don't require a prefix.

            this.loadbalancerFrontendName        = "frontend";
            this.loadbalancerBackendName         = "backend;";
            this.loadbalancerProbeName           = "probe";
            this.publicNetworkSecurityGroupName  = "neon-public";
            this.privateNetworkSecurityGroupName = "neon-private";
            this.outboudNetworkSecurityGroupName = "neon-outbound";

            // Initialize the node mapping dictionary and also ensure
            // that each node has Azure reasonable Azure node options.

            this.nodeDictionary = new Dictionary<string, AzureNode>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var node in cluster.Nodes)
            {
                nodeDictionary.Add(node.Name, new AzureNode(node, this));

                if (node.Metadata.Azure == null)
                {
                    // Initialize reasonable defaults.

                    node.Metadata.Azure = new AzureNodeOptions();
                }
            }

            // This identifies the cluster manager instance with the cluster proxy
            // so that the proxy can have the hosting manager perform some operations
            // like managing the SSH port mappings on the load balancer.

            cluster.HostingManager = this;
        }

        /// <inheritdoc/>
        public override void Dispose(bool disposing)
        {
            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        /// <inheritdoc/>
        public override void Validate(ClusterDefinition clusterDefinition)
        {
        }

        /// <inheritdoc/>
        public override bool Provision(bool force, string secureSshPassword, string orgSshPassword = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(secureSshPassword));

            throw new NotImplementedException("$todo(jefflill): Implement this.");
        }

        /// <inheritdoc/>
        public override (string Address, int Port) GetSshEndpoint(string nodeName)
        {
            return (Address: cluster.GetNode(nodeName).PrivateAddress.ToString(), Port: NetworkPorts.SSH);
        }

        /// <inheritdoc/>
        public override bool RequiresAdminPrivileges => false;
    }
}
