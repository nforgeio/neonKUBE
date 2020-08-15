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
using Microsoft.Azure.Management.Network.Fluent.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;
using Neon.Time;
using Microsoft.Azure.Management.Monitor.Fluent.Models;
using Microsoft.Azure.Management.ContainerService.Fluent.Models;
using Microsoft.Azure.Management.AppService.Fluent;
using Microsoft.Azure.Management.CosmosDB.Fluent.Models;
using Microsoft.Azure.Management.Network.Models;

using INetworkSecurityGroup = Microsoft.Azure.Management.Network.Fluent.INetworkSecurityGroup;
using SecurityRuleProtocol  = Microsoft.Azure.Management.Network.Fluent.Models.SecurityRuleProtocol;
using TransportProtocol     = Microsoft.Azure.Management.Network.Fluent.Models.TransportProtocol;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;

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
            public string Name => hostingManager.GetResourceName("vm", Node.Name);

            /// <summary>
            /// The associated Azure VM.
            /// </summary>
            public IVirtualMachine Vm { get; set; }

            /// <summary>
            /// The node's network interface.
            /// </summary>
            public INetworkInterface Nic { get; set; }

            /// <summary>
            /// The SSH port to be used to connect to the node via SSH while provisioning
            /// or managing the cluster.
            /// </summary>
            public int PublicSshPort { get; set; } = NetworkPorts.SSH;

            /// <summary>
            /// Returns the Azure name for the temporary NAT rule mapping the
            /// cluster's frontend load balancer port to the SSH port for this 
            /// node.
            /// </summary>
            public string SshNatRuleName
            {
                get { return $"neon-ssh-tcp-{Node.Name}"; }
            }

            /// <summary>
            /// Returns <c>true</c> if the node is a master.
            /// </summary>
            public bool IsMaster
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

        /// <summary>
        /// Flags used to customize how the cluster load balancer is updated.
        /// </summary>
        [Flags]
        private enum LoadBalancerUpdate
        {
            /// <summary>
            /// Update the cluster's ingress rules.
            /// </summary>
            IngressRules = 0x0001,

            /// <summary>
            /// Add SSH management NAT rules for every node in the cluster. 
            /// </summary>
            AddSshRules = 0x0002,
            
            /// <summary>
            /// Remove SSH management NAT rules for every node in the cluster.
            /// </summary>
            RemoveSshRules = 0x0004,

            /// <summary>
            /// SSH management rule names include an encoded UTC timestamp.  This
            /// flag specifies that any SSH management rules with a timestamp older
            /// than 24 hours will be removed.
            /// </summary>
            PurgeSshRules = 0x0008
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
        private string                          nodeUsername;
        private string                          nodePassword;
        private AzureOptions                    azureOptions;
        private string                          region;
        private AzureCredentials                azureCredentials;
        private string                          resourceGroup;
        private KubeSetupInfo                   setupInfo;
        private HostingOptions                  hostingOptions;
        private CloudOptions                    cloudOptions;
        private NetworkOptions                  networkOptions;
        private Dictionary<string, AzureNode>   azureNodes;
        private IAzure                          azure;

        // Azure requires that the various components that need to be provisioned
        // for the cluster have names.  We're going to generate these in the constructor.
        // Top level component names will be prefixed by
        //
        //      neon-<cluster-name>-
        //
        // to avoid conflicts with other clusters or things deployed to the same resource
        // group.  For example if there's already a cluster in the same resource group,
        // we wouldn't want to node names like "master-0" to conflict across multiple 
        // clusters.

        private string                          publicAddressName;
        private string                          vnetName;
        private string                          subnetName;
        private string                          masterAvailabilitySetName;
        private string                          workerAvailabilitySetName;
        private string                          proximityPlacementGroupName;
        private string                          loadbalancerName;
        private string                          loadbalancerFrontendName;
        private string                          loadbalancerBackendName;
        private string                          loadbalancerProbeName;
        private string                          ingressNsgName;
        private string                          egressNsgName;
        private string                          manageNsgName;

        // These fields hold various Azure components while provisioning is in progress.

        private IPublicIPAddress                publicAddress;
        private INetwork                        vnet;
        private ILoadBalancer                   loadBalancer;
        private IAvailabilitySet                masterAvailabilitySet;
        private IAvailabilitySet                workerAvailabilitySet;
        private INetworkSecurityGroup           ingressNsg;
        private INetworkSecurityGroup           egressNsg;
        private INetworkSecurityGroup           manageNsg;

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

            this.cluster                     = cluster;
            this.clusterName                 = cluster.Name;
            this.azureOptions                = cluster.Definition.Hosting.Azure;
            this.region                      = azureOptions.Region;
            this.resourceGroup               = azureOptions.ResourceGroup ?? $"neon-{clusterName}";
            this.setupInfo                   = setupInfo;
            this.hostingOptions              = cluster.Definition.Hosting;
            this.cloudOptions                = hostingOptions.Cloud;
            this.networkOptions              = cluster.Definition.Network;

            // Initialize the component names as they will be deployed to Azure.  Note that we're
            // going to prefix each name with the Azure item type convention described here:
            //
            //      https://docs.microsoft.com/en-us/azure/cloud-adoption-framework/ready/azure-best-practices/naming-and-tagging
            //
            // optionally combined with the cluster name.

            this.publicAddressName           = GetResourceName("pip", "cluster", true);
            this.vnetName                    = GetResourceName("vnet", "cluster", true);
            this.subnetName                  = GetResourceName("snet", "cluster", true);
            this.masterAvailabilitySetName   = GetResourceName("avail", "master");
            this.workerAvailabilitySetName   = GetResourceName("avail", "worker");
            this.proximityPlacementGroupName = GetResourceName("ppg", "cluster", true);
            this.loadbalancerName            = GetResourceName("lbe", "cluster", true);
            this.ingressNsgName              = "nsg-ingress";
            this.egressNsgName               = "nsg-egress";
            this.manageNsgName               = "nsg-manage";
            this.loadbalancerFrontendName    = "frontend";
            this.loadbalancerBackendName     = "backend";
            this.loadbalancerProbeName       = "probe";

            // Initialize the node mapping dictionary and also ensure
            // that each node has Azure reasonable Azure node options.

            this.azureNodes = new Dictionary<string, AzureNode>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var node in cluster.Nodes)
            {
                azureNodes.Add(node.Name, new AzureNode(node, this));

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
            // The Azure connection class doesn't implement [IDispose]
            // so we don't have much to do here.

            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Returns the name to use for a cluster related resource based on the standard Azure resource type
        /// prefix, the cluster name (if enabled) and the base resource name.
        /// </summary>
        /// <param name="resourceTypePrefix">The Azure resource type prefix (like "pip" for public IP address).</param>
        /// <param name="resourceName">The base resource name.</param>
        /// <param name="omitResourceNameWhenPrefixed">Optionall omit <paramref name="resourceName"/> when resource names include the cluster name.</param>
        /// <returns>The full resource name.</returns>
        private string GetResourceName(string resourceTypePrefix, string resourceName, bool omitResourceNameWhenPrefixed = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(resourceTypePrefix), nameof(resourceTypePrefix));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(resourceName), nameof(resourceName));

            if (cloudOptions.PrefixResourceNames)
            {
                if (omitResourceNameWhenPrefixed)
                {
                    return $"{resourceTypePrefix}-{clusterName}";
                }
                else
                {
                    return $"{resourceTypePrefix}-{clusterName}-{resourceName}";
                }
            }
            else
            {
                return $"{resourceTypePrefix}-{resourceName}";
            }
        }

        /// <inheritdoc/>
        public override void Validate(ClusterDefinition clusterDefinition)
        {
            // Ensure that any explicit node IP address assignments are located
            // within the nodes subnet and do not conflict with any of the addresses
            // reserved by the cloud provider or neonKUBE.  We're also going to 
            // require that the nodes subnet have at least 256 addresses.

            var nodeSubnet = NetworkCidr.Parse(networkOptions.NodeSubnet);

            if (nodeSubnet.AddressCount < 256)
            {
                throw new ClusterDefinitionException($"[{nameof(networkOptions.NodeSubnet)}={networkOptions.NodeSubnet}] is too small.  Cloud subnets must include at least 256 addresses (at least a /24 network).");
            }

            const int reservedCount = KubeConst.CloudVNetStartReservedIPs + KubeConst.CloudVNetEndReservedIPs;

            if (clusterDefinition.Nodes.Count() > nodeSubnet.AddressCount - reservedCount)
            {
                throw new ClusterDefinitionException($"The cluster includes [{clusterDefinition.Nodes.Count()}] which will not fit within the [{nameof(networkOptions.NodeSubnet)}={networkOptions.NodeSubnet}] after accounting for [{reservedCount}] reserved addresses.");
            }

            var firstValidAddressUint = NetHelper.AddressToUint(nodeSubnet.FirstAddress) + KubeConst.CloudVNetStartReservedIPs;
            var firstValidAddress     = NetHelper.UintToAddress(firstValidAddressUint);
            var lastValidAddressUint  = NetHelper.AddressToUint(nodeSubnet.LastAddress) - KubeConst.CloudVNetEndReservedIPs;
            var lastValidAddress      = NetHelper.UintToAddress(lastValidAddressUint);

            foreach (var node in clusterDefinition.SortedNodes.OrderBy(node => node.Name))
            {
                if (string.IsNullOrEmpty(node.Address))
                {
                    // Ignore nodes with unassigned addresses.

                    continue;
                }

                var address = IPAddress.Parse(node.Address);

                if (!nodeSubnet.Contains(address))
                {
                    throw new ClusterDefinitionException($"Node [{node.Name}] is assigned [{node.Address}={node.Address}] which is outside of [{nameof(networkOptions.NodeSubnet)}={networkOptions.NodeSubnet}].");
                }

                var addressUint = NetHelper.AddressToUint(address);

                if (addressUint < firstValidAddressUint)
                {
                    throw new ClusterDefinitionException($"Node [{node.Name}] defines IP address [{node.Address}={node.Address}] which is reserved.  The first valid node address [{nameof(networkOptions.NodeSubnet)}={networkOptions.NodeSubnet}] is [{firstValidAddress}].");
                }

                if (addressUint > lastValidAddressUint)
                {
                    throw new ClusterDefinitionException($"Node [{node.Name}] defines IP address [{node.Address}={node.Address}] which is reserved.  The last valid node address [{nameof(networkOptions.NodeSubnet)}={networkOptions.NodeSubnet}] is [{lastValidAddress}].");
                }
            }

            //-----------------------------------------------------------------
            // Automatically assign IP unused IP addresses within the subnet to nodes that 
            // were not explicitly assigned an address in the cluster definition.

            // Add any explicitly assigned addresses to a HashSet so we won't reuse any.

            var assignedAddresses = new HashSet<uint>();

            foreach (var node in clusterDefinition.SortedNodes)
            {
                if (string.IsNullOrEmpty(node.Address))
                {
                    continue;
                }

                var address     = IPAddress.Parse(node.Address);
                var addressUint = NetHelper.AddressToUint(address);

                if (!assignedAddresses.Contains(addressUint))
                {
                    assignedAddresses.Add(addressUint);
                }
            }

            // Assign master node addresses first so these will tend to appear first
            // in the subnet.

            foreach (var node in clusterDefinition.SortedMasters)
            {
                if (!string.IsNullOrEmpty(node.Address))
                {
                    continue;
                }

                for (var addressUint = firstValidAddressUint; addressUint <= lastValidAddressUint; addressUint++)
                {
                    if (!assignedAddresses.Contains(addressUint))
                    {
                        node.Address = NetHelper.UintToAddress(addressUint).ToString();

                        assignedAddresses.Add(addressUint);
                        break;
                    }
                }
            }

            // Now assign the worker node addresses, so these will tend to appear
            // after the masters in the subnet.

            foreach (var node in clusterDefinition.SortedWorkers)
            {
                if (!string.IsNullOrEmpty(node.Address))
                {
                    continue;
                }

                for (var addressUint = firstValidAddressUint; addressUint <= lastValidAddressUint; addressUint++)
                {
                    if (!assignedAddresses.Contains(addressUint))
                    {
                        node.Address = NetHelper.UintToAddress(addressUint).ToString();

                        assignedAddresses.Add(addressUint);
                        break;
                    }
                }
            }
        }

        /// <inheritdoc/>
        public override bool Provision(bool force, string secureSshPassword, string orgSshPassword = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(secureSshPassword));

            this.nodeUsername = KubeConst.SysAdminUsername;
            this.nodePassword = secureSshPassword;

            var operation  = $"Provisioning [{cluster.Definition.Name}] on Azure [{region}/{resourceGroup}]";
            var controller = new SetupController<NodeDefinition>(operation, cluster.Nodes)
            {
                ShowStatus     = this.ShowStatus,
                ShowNodeStatus = true,
                MaxParallel    = int.MaxValue       // There's no reason to constrain this
            };

            controller.AddGlobalStep("connecting Azure", () => ConnectAzure());
            controller.AddGlobalStep("region check", () => VerifyRegionAndVmSizes());
            controller.AddGlobalStep("resource group", () => CreateResourceGroup());
            controller.AddGlobalStep("availability sets", () => CreateAvailabilitySets());
            controller.AddGlobalStep("virtual network", () => CreateVirtualNetwork());
            controller.AddGlobalStep("network security groups", () => CreateNetworkSecurityGroups());
            controller.AddGlobalStep("public address", () => CreatePublicAddress());
            controller.AddGlobalStep("load balancer", () => CreateLoadBalancer());
            controller.AddStep("virtual machines", CreateVm);

            if (!controller.Run(leaveNodesConnected: false))
            {
                Console.WriteLine("*** One or more Azure provisioning steps failed.");
                return false;
            }

            return true;
        }

        /// <inheritdoc/>
        public override (string Address, int Port) GetSshEndpoint(string nodeName)
        {
            return (Address: cluster.GetNode(nodeName).Address.ToString(), Port: NetworkPorts.SSH);
        }

        /// <inheritdoc/>
        public override bool RequiresAdminPrivileges => false;

        /// <summary>
        /// Connects to Azure if we're not already connected.
        /// </summary>
        private void ConnectAzure()
        {
            if (azure != null)
            {
                return; // Already connected.
            }

            var environment = AzureEnvironment.AzureGlobalCloud;

            if (azureOptions.Environment != null)
            {
                switch (azureOptions.Environment.Name)
                {
                    case AzureCloudEnvironments.GlobalCloud:

                        environment = AzureEnvironment.AzureGlobalCloud;
                        break;

                    case AzureCloudEnvironments.ChinaCloud:

                        environment = AzureEnvironment.AzureChinaCloud;
                        break;

                    case AzureCloudEnvironments.GermanCloud:

                        environment = AzureEnvironment.AzureGermanCloud;
                        break;

                    case AzureCloudEnvironments.USGovernment:

                        environment = AzureEnvironment.AzureUSGovernment;
                        break;

                    case AzureCloudEnvironments.Custom:

                        environment = new AzureEnvironment()
                        {
                            AuthenticationEndpoint  = azureOptions.Environment.AuthenticationEndpoint,
                            GraphEndpoint           = azureOptions.Environment.GraphEndpoint,
                            ManagementEndpoint      = azureOptions.Environment.ManagementEnpoint,
                            ResourceManagerEndpoint = azureOptions.Environment.ResourceManagerEndpoint
                        };
                        break;

                    default:

                        throw new NotImplementedException($"Azure environment [{azureOptions.Environment.Name}] is not currently supported.");
                }
            }

            azureCredentials =
                new AzureCredentials(
                    new ServicePrincipalLoginInformation()
                    {
                        ClientId     = azureOptions.AppId,
                        ClientSecret = azureOptions.AppPassword
                    },
                azureOptions.TenantId,
                environment);

            azure = Azure.Configure()
                .Authenticate(azureCredentials)
                .WithSubscription(azureOptions.SubscriptionId);
        }

        /// <summary>
        /// <para>
        /// Verify that the requested Azure region exists, supports the requested VM sizes,
        /// and that VMs for nodes that specify UltraSSD actually support UltraSSD.  We'll also
        /// verify that the requested VMs have the minimum required number or cores and RAM.
        /// </para>
        /// <para>
        /// This also updates the node labels to match the capabilities of their VMs.
        /// </para>
        /// </summary>
        private void VerifyRegionAndVmSizes()
        {
            var region       = cluster.Definition.Hosting.Azure.Region;
            var vmSizes      = azure.VirtualMachines.Sizes.ListByRegion(region);
            var nameToVmSize = new Dictionary<string, IVirtualMachineSize>(StringComparer.InvariantCultureIgnoreCase);
            var nameToVmSku  = new Dictionary<string, IComputeSku>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var vmSize in azure.VirtualMachines.Sizes.ListByRegion(region))
            {
                nameToVmSize[vmSize.Name] = vmSize;
            }

            foreach (var vmSku in azure.ComputeSkus.ListByRegion(region))
            {
                nameToVmSku[vmSku.Name.Value] = vmSku;
            }

            foreach (var node in cluster.Nodes)
            {
                var vmSizeName = node.Metadata.Azure.VmSize;

                if (!nameToVmSize.TryGetValue(vmSizeName, out var vmSize))
                {
                    throw new KubeException($"Node [{node.Name}] requests [{nameof(node.Metadata.Azure.VmSize)}={vmSizeName}].  This is not available in the [{region}] Azure region.");
                }

                if (!nameToVmSku.TryGetValue(vmSizeName, out var vmSku))
                {
                    // This should never happen, right?

                    throw new KubeException($"Node [{node.Name}] requests [{nameof(node.Metadata.Azure.VmSize)}={vmSizeName}].  This is not available in the [{region}] Azure region.");
                }

                switch (node.Metadata.Role)
                {
                    case NodeRole.Master:

                        if (vmSize.NumberOfCores < KubeConst.MinMasterCores)
                        {
                            throw new KubeException($"Master node [{node.Name}] requests [{nameof(node.Metadata.Azure.VmSize)}={vmSizeName}] with [Cores={vmSize.NumberOfCores} MiB] which is lower than the required [{KubeConst.MinMasterCores}] cores.]");
                        }

                        if (vmSize.MemoryInMB < KubeConst.MinMasterRamMiB)
                        {
                            throw new KubeException($"Master node [{node.Name}] requests [{nameof(node.Metadata.Azure.VmSize)}={vmSizeName}] with [RAM={vmSize.MemoryInMB} MiB] which is lower than the required [{KubeConst.MinMasterRamMiB} MiB].]");
                        }

                        if (vmSize.MaxDataDiskCount < 1)
                        {
                            throw new KubeException($"Master node [{node.Name}] requests [{nameof(node.Metadata.Azure.VmSize)}={vmSizeName}] that supports up to [{vmSize.MaxDataDiskCount}] disks.  A minimum of [1] drive is required.");
                        }
                        break;

                    case NodeRole.Worker:

                        if (vmSize.NumberOfCores < KubeConst.MinWorkerCores)
                        {
                            throw new KubeException($"Worker node [{node.Name}] requests [{nameof(node.Metadata.Azure.VmSize)}={vmSizeName}] with [Cores={vmSize.NumberOfCores} MiB] which is lower than the required [{KubeConst.MinWorkerCores}] cores.]");
                        }

                        if (vmSize.MemoryInMB < KubeConst.MinWorkerRamMiB)
                        {
                            throw new KubeException($"Worker node [{node.Name}] requests [{nameof(node.Metadata.Azure.VmSize)}={vmSizeName}] with [RAM={vmSize.MemoryInMB} MiB] which is lower than the required [{KubeConst.MinWorkerRamMiB} MiB].]");
                        }

                        if (vmSize.MaxDataDiskCount < 1)
                        {
                            throw new KubeException($"Worker node [{node.Name}] requests [{nameof(node.Metadata.Azure.VmSize)}={vmSizeName}] that supports up to [{vmSize.MaxDataDiskCount}] disks.  A minimum of [1] drive is required.");
                        }
                        break;

                    default:

                        throw new NotImplementedException();
                }

                if (node.Metadata.Azure.StorageType == AzureStorageTypes.UltraSSD)
                {
                    if (!vmSku.Capabilities.Any(Capability => Capability.Name == "UltraSSDAvailable" && Capability.Value == "False"))
                    {
                        throw new KubeException($"Node [{node.Name}] requests an UltraSSD disk.  This is not available in the [{region}] Azure region and/or the [{vmSize}] VM Size.");
                    }
                }

                // Update the node labels to match the actual VM capabilities.

                node.Metadata.Labels.ComputeCores     = vmSize.NumberOfCores;
                node.Metadata.Labels.ComputeRam       = vmSize.MemoryInMB;

                node.Metadata.Labels.StorageSize      = $"{AzureHelper.GetDiskSizeGiB(node.Metadata.Azure.StorageType, ByteUnits.Parse(node.Metadata.Azure.DiskSize))} GiB";
                node.Metadata.Labels.StorageHDD       = node.Metadata.Azure.StorageType == AzureStorageTypes.StandardHDD;
                node.Metadata.Labels.StorageEphemeral = false;
                node.Metadata.Labels.StorageLocal     = false;
                node.Metadata.Labels.StorageRedundant = true;
            }
        }

        /// <summary>
        /// Creates the cluster's resource group if it doesn't already exist.
        /// </summary>
        private void CreateResourceGroup()
        {
            if (azure.ResourceGroups.Contain(resourceGroup))
            {
                return;
            }

            azure.ResourceGroups
                .Define(resourceGroup)
                .WithRegion(region)
                .Create();
        }

        /// <summary>
        /// Creates an avilablity set for the master VMs and a separate one for the worker VMs
        /// as well as the cluster's proximity placement group.
        /// </summary>
        private void CreateAvailabilitySets()
        {
            // Availability sets

            var existingAvailabilitySets = azure.AvailabilitySets.ListByResourceGroup(resourceGroup);

            masterAvailabilitySet = existingAvailabilitySets.FirstOrDefault(aset => aset.Name.Equals(masterAvailabilitySetName, StringComparison.InvariantCultureIgnoreCase));
            workerAvailabilitySet = existingAvailabilitySets.FirstOrDefault(aset => aset.Name.Equals(workerAvailabilitySetName, StringComparison.InvariantCultureIgnoreCase));

            if (azureOptions.DisableProximityPlacement)
            {
                if (masterAvailabilitySet == null)
                {
                    masterAvailabilitySet = (IAvailabilitySet)azure.AvailabilitySets
                        .Define(masterAvailabilitySetName)
                        .WithRegion(region)
                        .WithExistingResourceGroup(resourceGroup)
                        .WithUpdateDomainCount(azureOptions.UpdateDomains)
                        .WithFaultDomainCount(azureOptions.FaultDomains)
                        .Create();
                }

                if (workerAvailabilitySet == null)
                {
                    workerAvailabilitySet = (IAvailabilitySet)azure.AvailabilitySets
                        .Define(workerAvailabilitySetName)
                        .WithRegion(region)
                        .WithExistingResourceGroup(resourceGroup)
                        .WithUpdateDomainCount(azureOptions.UpdateDomains)
                        .WithFaultDomainCount(azureOptions.FaultDomains)
                        .Create();
                }
            }
            else
            {
                if (masterAvailabilitySet == null)
                {
                    masterAvailabilitySet = (IAvailabilitySet)azure.AvailabilitySets
                        .Define(masterAvailabilitySetName)
                        .WithRegion(region)
                        .WithExistingResourceGroup(resourceGroup)
                        .WithNewProximityPlacementGroup(proximityPlacementGroupName, ProximityPlacementGroupType.Standard)
                        .WithUpdateDomainCount(azureOptions.UpdateDomains)
                        .WithFaultDomainCount(azureOptions.FaultDomains)
                        .Create();
                }

                if (workerAvailabilitySet == null)
                {
                    workerAvailabilitySet = (IAvailabilitySet)azure.AvailabilitySets
                        .Define(workerAvailabilitySetName)
                        .WithRegion(region)
                        .WithExistingResourceGroup(resourceGroup)
                        .WithNewProximityPlacementGroup(proximityPlacementGroupName, ProximityPlacementGroupType.Standard)
                        .WithUpdateDomainCount(azureOptions.UpdateDomains)
                        .WithFaultDomainCount(azureOptions.FaultDomains)
                        .Create();
                }
            }
        }

        /// <summary>
        /// Creates the cluster's virtual network if it doesn't already exist.
        /// </summary>
        private void CreateVirtualNetwork()
        {
            vnet = azure.Networks.ListByResourceGroup(resourceGroup).FirstOrDefault(vnet => vnet.Name.Equals(vnetName, StringComparison.InvariantCultureIgnoreCase));

            if (vnet != null)
            {
                return;
            }

            vnet = azure.Networks
                .Define(vnetName)
                .WithRegion(region)
                .WithExistingResourceGroup(resourceGroup)
                .WithAddressSpace(networkOptions.NodeSubnet)
                .DefineSubnet(subnetName)
                    .WithAddressPrefix(networkOptions.NodeSubnet)
                    .Attach()
                .Create();
        }

        /// <summary>
        /// Create or updates the network security groups if they don't already exist.
        /// </summary>
        private void CreateNetworkSecurityGroups()
        {
            var nsgList = azure.NetworkSecurityGroups.ListByResourceGroup(resourceGroup);

            ingressNsg = nsgList.FirstOrDefault(nsg => nsg.Name.Equals(ingressNsgName, StringComparison.InvariantCultureIgnoreCase));
            egressNsg  = nsgList.FirstOrDefault(nsg => nsg.Name.Equals(egressNsgName, StringComparison.InvariantCultureIgnoreCase));

            if (ingressNsg != null)
            {
                // Ingress: Honor the cluster definition ingress rules, if there are any.

                var ingressNsgCreator = azure.NetworkSecurityGroups
                    .Define(ingressNsgName)
                    .WithRegion(region)
                    .WithExistingResourceGroup(resourceGroup);

                var priority = 3000;

                foreach (var rule in networkOptions.IngressRules)
                {
                    var allowedInboundAddresses = rule.AllowedAddresses.ToArray();

                    if (allowedInboundAddresses.Length == 0)
                    {
                        allowedInboundAddresses = new string[] { "0.0.0.0/0" };    // Allow traffic from everywhere
                    }

                    ingressNsgCreator
                        .DefineRule($"{rule.Name}")
                            .AllowInbound()
                            .FromAddresses(allowedInboundAddresses)
                            .FromAnyPort()
                            .ToAnyAddress()
                            .ToPort(rule.NodePort)
                            .WithProtocol(SecurityRuleProtocol.Tcp)                     // Only TCP is supported now
                            .WithPriority(priority++)
                            .Attach();
                }

                ingressNsg = ingressNsgCreator.Create();
            }

            // Egress:

            if (egressNsg != null)
            {
                var priority = 3000;

                var allowedOutboundAddresses = networkOptions.EgressAddresses.ToArray();

                if (allowedOutboundAddresses.Length == 0)
                {
                    allowedOutboundAddresses = new string[] { "0.0.0.0/0" };        // Allow outbound traffic to everywhere
                }

                egressNsg = azure.NetworkSecurityGroups
                    .Define(egressNsgName)
                    .WithRegion(region)
                    .WithExistingResourceGroup(resourceGroup)
                    .DefineRule("outbound-allow-all")
                        .AllowOutbound()
                        .FromAnyAddress()
                        .FromAnyPort()
                        .ToAddresses(allowedOutboundAddresses)
                        .ToAnyPort()
                        .WithAnyProtocol()
                        .WithPriority(priority++)
                        .Attach()
                    .Create();
            }

            // Manage:

            // $todo(jefflill): Implement this!
        }

        /// <summary>
        /// Creates the public IP address for the cluster's load balancer.
        /// </summary>
        private void CreatePublicAddress()
        {
            publicAddress = azure.PublicIPAddresses
                .Define(publicAddressName)
                    .WithRegion(azureOptions.Region)
                    .WithExistingResourceGroup(resourceGroup)
                    .WithStaticIP()
                    .WithLeafDomainLabel(azureOptions.DomainLabel)
                    .WithSku(PublicIPSkuType.Standard)
                    .Create();
        }

        /// <summary>
        /// Creates the cluster's external load balancer if it doesn't already exist.
        /// </summary>
        private void CreateLoadBalancer()
        {
            loadBalancer = azure.LoadBalancers.ListByResourceGroup(resourceGroup).FirstOrDefault(lb => lb.Name.Equals(loadbalancerName, StringComparison.InvariantCultureIgnoreCase));

            if (loadBalancer != null)
            {
                return;
            }

            // The Azure fluent API does not support creating a load balancer without
            // any rules.  So we're going to create the load balancer with a dummy rule
            // and then delete the rule straight away.

            loadBalancer = azure.LoadBalancers
                .Define(loadbalancerName)
                .WithRegion(region)
                .WithExistingResourceGroup(resourceGroup)
                .DefineLoadBalancingRule("dummy")
                    .WithProtocol(TransportProtocol.Tcp)
                    .FromFrontend(loadbalancerFrontendName)
                    .FromFrontendPort(10000)
                    .ToBackend(loadbalancerBackendName)
                    .ToBackendPort(10000)
                    .Attach()
                .DefinePublicFrontend(loadbalancerFrontendName)
                    .WithExistingPublicIPAddress(publicAddress)
                    .Attach()
                .DefineBackend(loadbalancerBackendName)
                    .Attach()
                .WithSku(LoadBalancerSkuType.Standard)
                .Create();

            loadBalancer.Update()
                .WithoutLoadBalancingRule("dummy")
                .Apply();
        }

        /// <summary>
        /// Creates the NIC and VM for a cluster node.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="stepDelay">The step delay.</param>
        private void CreateVm(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            var azureNode = azureNodes[node.Name];

            node.Status = "create NIC";

            azureNode.Nic = azure.NetworkInterfaces
                .Define(GetResourceName("nic",azureNode.Node.Name))
                .WithRegion(azureOptions.Region)
                .WithExistingResourceGroup(resourceGroup)
                .WithExistingPrimaryNetwork(vnet)
                .WithSubnet(subnetName)
                .WithPrimaryPrivateIPAddressStatic(azureNode.Node.Metadata.Address)
                .Create();

            node.Status = "creating...";

            var azureNodeOptions = azureNode.Node.Metadata.Azure;
            var azureStorageType = StorageAccountTypes.StandardSSDLRS;

            switch (azureNodeOptions.StorageType)
            {
                case AzureStorageTypes.PremiumSSD:

                    azureStorageType = StorageAccountTypes.PremiumLRS;
                    break;

                case AzureStorageTypes.StandardHDD:

                    azureStorageType = StorageAccountTypes.StandardLRS;
                    break;

                case AzureStorageTypes.StandardSSD:

                    azureStorageType = StorageAccountTypes.StandardSSDLRS;
                    break;

                case AzureStorageTypes.UltraSSD:

                    azureStorageType = StorageAccountTypes.UltraSSDLRS;
                    break;

                default:

                    throw new NotImplementedException();
            }

            // $todo(jefflill): We need to use the Gen2 image when supported by the VM size

            var imageRef = new ImageReference()
            {
                Publisher = "Canonical",
                Offer     = "0001-com-ubuntu-server-focal",
                Sku       = "20_04-lts",
                Version   = "20.04.202007290"
            };

            azureNode.Vm = azure.VirtualMachines
                .Define(azureNode.Name)
                .WithRegion(azureOptions.Region)
                .WithExistingResourceGroup(resourceGroup)
                .WithExistingPrimaryNetworkInterface(azureNode.Nic)
                .WithSpecificLinuxImageVersion(imageRef)
                .WithRootUsername(nodeUsername)
                .WithRootPassword(nodePassword)
                .WithComputerName("ubuntu")
                .WithDataDiskDefaultStorageAccountType(azureStorageType)
                .WithNewDataDisk((int)(ByteUnits.Parse(node.Metadata.Azure.DiskSize) / ByteUnits.GibiBytes))
                .WithSize(node.Metadata.Azure.VmSize)
                .WithExistingAvailabilitySet(azureNode.IsMaster ? masterAvailabilitySet : workerAvailabilitySet)
                .Create();
        }

        /// <summary>
        /// Updates the load balancer configuration based on the flags passed.
        /// </summary>
        /// <param name="updateFlags">Flags that control how the load balancer is updated.</param>
        private void UpdateLoadBalancer(LoadBalancerUpdate updateFlags)
        {
        }
    }
}
