//-----------------------------------------------------------------------------
// FILE:        AzureHostingManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Data;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using Azure.ResourceManager.Storage;

using k8s;
using k8s.Models;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Collections;
using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Kube.Clients;
using Neon.Kube.ClusterDef;
using Neon.Kube.Config;
using Neon.Kube.Deployment;
using Neon.Kube.Proxy;
using Neon.Kube.Setup;
using Neon.Kube.SSH;
using Neon.Net;
using Neon.SSH;
using Neon.Tasks;
using Neon.Time;

using PublicIPAddressSku     = Azure.ResourceManager.Network.Models.PublicIPAddressSku;
using PublicIPAddressSkuName = Azure.ResourceManager.Network.Models.PublicIPAddressSkuName;
using PublicIPAddressSkuTier = Azure.ResourceManager.Network.Models.PublicIPAddressSkuTier;

namespace Neon.Kube.Hosting.Azure
{
    /// <summary>
    /// Manages cluster provisioning on the Google Cloud Platform.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Optional capability support:
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><see cref="HostingCapabilities.Pausable"/></term>
    ///     <description><b>NO</b></description>
    /// </item>
    /// <item>
    ///     <term><see cref="HostingCapabilities.Stoppable"/></term>
    ///     <description><b>YES</b></description>
    /// </item>
    /// </list>
    /// </remarks>
    [HostingProvider(HostingEnvironment.Azure)]
    public class AzureHostingManager : HostingManager
    {
        // IMPLEMENTATION NOTE:
        // --------------------
        // Here's the original issue covering Azure provisioning and along with 
        // some discussion about how NEONKUBE thinks about cloud deployments:
        // 
        //      https://github.com/nforgeio/neonKUBE/issues/908
        //
        // The remainder of this note will outline how Azure provisioning works.
        //
        // A NEONKUBE Azure cluster will require provisioning these things:
        //
        //      * VNET
        //      * VMs & Disks
        //      * Load balancer with public IP
        //
        // In the future, we may relax the public load balancer requirement so
        // that virtual air-gapped clusters can be supported (more on that below).
        //
        // Nodes will be deployed in two Azure availability sets, one set for the
        // control-plane nodes and the other one for the workers.  We're doing this to ensure
        // that there will always be a quorum of control-plane nodes available during planned
        // Azure maintenance.
        //
        // By default, we're also going to create an Azure proximity placement group
        // for the cluster and then add both the control-plane and worker availability sets
        // to the proximity group.  This ensures the shortest possible network latency
        // between all of the cluster nodes but with the increased chance that Azure
        // won't be able to satisfy the deployment constraints.  The user can disable
        // placement groups via [AzureOptions.DisableProximityPlacement=true].
        //
        // The VNET will be configured using the cluster definition's [NetworkOptions],
        // with [NetworkOptions.NodeSubnet] used to configure the subnet.
        // Node IP addresses will be automatically assigned by default, but this
        // can be customized via explict node address assignments in the cluster
        // definition when necessary.
        //
        // The load balancer will be created using a public IP address to balance
        // inbound traffic across a backend pool including the VMs designated to
        // accept ingress traffic into the cluster.  These nodes are identified 
        // by the presence of a [neonkube.io/node.ingress=true] label which can be
        // set explicitly.  NEONKUBE will default to reasonable ingress nodes when
        // necessary.
        //
        // External load balancer traffic can be enabled for specific ports via 
        // [NetworkOptions.IngressRules] which specify three ports: 
        // 
        //      * The external load balancer port
        //      * The node port where Istio is listening and will forward traffic
        //        into the Kubernetes cluster
        //      * The target Istio port used by Istio to make routing decisions
        //
        // The [NetworkOptions.IngressRules] can also explicitly allow or deny traffic
        // from specific source IP addresses and/or subnets.
        //
        // NOTE: Only TCP connections are supported at this time because Istio
        //       doesn't support UDP, ICMP,...
        //
        // A network security group will be created and assigned to the subnet.
        // This will include ingress rules constructed from [NetworkOptions.IngressRules],
        // any temporary SSH related rules as well as egress rules constructed from
        // [NetworkOptions.EgressAddressRules].
        //
        // Azure VM NICs will be configured with each node's IP address.  We are not
        // currently assigning network security groups to these NICs.  The provisioner 
        // assigns these addresses automatically.
        //
        // VMs are currently based on the Ubuntu-22.04 Server image provided  
        // published to the marketplace by Canonical.  We use the [neon-image] tool
        // from the NEONCLOUD repo to create Azure Gen2 base and node images used
        // to provision the cluster.  Gen2 images work on most Azure VM sizes and offer
        // larger OS disks, improved performance, more memory and support for premium
        // and ultra storage.  There's a decent chance that Azure will deprecate Gen1
        // VMs at some point, so NEONKUBE is going to only support Gen2 images to
        // simplify things.
        //
        // This hosting manager will support creating VMs from NEONKUBE node
        // images published to Azure .  No9de images are preprovisioned with all of
        // the software required, making cluster setup much faster and reliable.
        //
        // Node virtual machine and disk types and sizes are specified by the 
        // [NodeDefinition.Azure] property.  VM sizes are specified using standard Azure
        // size names, disk type is an enum and disk sizes are specified via strings
        // including optional [ByteUnits].  Provisioning will need to verify that the
        // requested VM and drive sizes are actually available in the target Azure
        // region and will also need to map the disk size specified by the user to the
        // closest matching Azure disk size greater than or equal to the requested size.
        //
        // We'll be managing cluster node setup and maintenance remotely via
        // SSH connections and the cluster reserves 1000 external load balancer
        // ports (by default) to accomplish this.  When we need an external SSH
        // connection to any cluster node, the hosting manager will add one or
        // more rules to allow traffic to the range of external SSH ports assigned to
        // the cluster nodes.  NAT rules will be added to the to the load balancer that
        // route traffic from the external port to SSH port 22 on the target node.
        //
        // Note that we also support source address white/black listing for both
        // ingress and SSH rules and as well as destination address white/black
        // listing for general outbound cluster traffic.
        //
        // Idempotent Implementation
        // -------------------------
        // The Azure hosting manager is designed to be able to be interrupted and restarted
        // for cluster creation as well as management of the cluster afterwards.  This works
        // by reading the current state of the cluster resources from Azure.

        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Defines common resource type names used for configuring a load balancer.
        /// </summary>
        private static class LoadbalancerResourceTypes
        {
            public const string FrontendIPConfigurations = "frontendIPConfigurations";
        }

        /// <summary>
        /// Relates cluster node information to an Azure VM.
        /// </summary>
        private class AzureVm
        {
            private AzureHostingManager hostingManager;

            /// <summary>
            /// Constructs an instance from a <see cref="NodeSshProxy{TMetadata}"/> (used during cluster deployment).
            /// </summary>
            /// <param name="nodeProxy">The associated node proxy.</param>
            /// <param name="hostingManager">The parent hosting manager.</param>
            public AzureVm(NodeSshProxy<NodeDefinition> nodeProxy, AzureHostingManager hostingManager)
            {
                Covenant.Requires<ArgumentNullException>(hostingManager != null, nameof(hostingManager));
                Covenant.Requires<ArgumentNullException>(nodeProxy != null, nameof(nodeProxy));

                this.NodeProxy      = nodeProxy;
                this.hostingManager = hostingManager;
            }

            /// <summary>
            /// Constructs an instance from a <see cref="NodeDeployment"/> (used after cluster deployment).
            /// </summary>
            /// <param name="nodeDeployment">The associated node deployment.</param>
            /// <param name="hostingManager">The parent hosting manager.</param>
            public AzureVm(NodeDeployment nodeDeployment, AzureHostingManager hostingManager)
            {
                Covenant.Requires<ArgumentNullException>(hostingManager != null, nameof(hostingManager));
                Covenant.Requires<ArgumentNullException>(nodeDeployment != null, nameof(nodeDeployment));

                this.NodeDeployment = nodeDeployment;
                this.hostingManager = hostingManager;
            }

            /// <summary>
            /// Returns the associated node proxy when we're deploying the cluster,
            /// <c>null</c> when the cluster has already been deployed.
            /// </summary>
            public NodeSshProxy<NodeDefinition> NodeProxy { get; private set; }

            /// <summary>
            /// Returns the associated node deployment or <c>null</c> when we're deploying the cluster.
            /// </summary>
            public NodeDeployment NodeDeployment { get; private set; }

            /// <summary>
            /// Returns the node metadata (AKA its definition).
            /// </summary>
            public NodeDefinition Metadata => NodeProxy.Metadata;

            /// <summary>
            /// Returns the name of the node as defined in the cluster definition.
            /// </summary>
            public string Name => NodeProxy != null ? NodeProxy.Metadata.Name : NodeDeployment.Name;

            /// <summary>
            /// Returns the name of the Azure VM for this node.
            /// </summary>
            public string VmName => hostingManager.GetResourceName("vm", NodeProxy.Name);

            /// <summary>
            /// Returns the private IP address of the node.
            /// </summary>
            public string Address => NodeProxy.Metadata.Address;

            /// <summary>
            /// Returns the Azure VM size name (AKA SKU) for the node.
            /// </summary>
            public string VmSize => NodeProxy.Metadata.Azure.VmSize;

            /// <summary>
            /// The associated Azure VM.
            /// </summary>
            public VirtualMachineResource Vm { get; set; }

            /// <summary>
            /// References the node's network interface.
            /// </summary>
            public NetworkInterfaceResource Nic { get; set; }

            /// <summary>
            /// Returns <c>true</c> if the node is a control-plane.
            /// </summary>
            public bool IsControlPlane => NodeProxy.Metadata.Role == NodeRole.ControlPlane;

            /// <summary>
            /// Returns <c>true</c> if the node is a worker.
            /// </summary>
            public bool IsWorker => NodeProxy.Metadata.Role == NodeRole.Worker;

            /// <summary>
            /// The Azure availability set hosting this node.
            /// </summary>
            public string AvailabilitySetName { get; set; }

            /// <summary>
            /// The external SSH port assigned to the VM.  This port is
            /// allocated from the range of external SSH ports configured for
            /// the cluster and is persisted as tag for each Azure VM.
            /// </summary>
            public int ExternalSshPort { get; set; }

            /// <summary>
            /// The cluster node provisioning/power state.
            /// </summary>
            public ClusterNodeState State { get; set; }
        }

        /// <summary>
        /// Flags used to control how the cluster network is updated.
        /// </summary>
        [Flags]
        private enum NetworkOperations
        {
            /// <summary>
            /// Update the cluster's ingress/egress rules.
            /// </summary>
            InternetRouting = 0x0001,

            /// <summary>
            /// Enable external SSH to the cluster nodes.
            /// </summary>
            AllowNodeSsh = 0x0002,

            /// <summary>
            /// Disable external SSH to the cluster nodes.
            /// </summary>
            DenyNodeSsh = 0x0004,
        }

        /// <summary>
        /// Enumerates the known Azure CPU virtual machine architectures.
        /// </summary>
        private enum AzureCpuArchitecture
        {
            /// <summary>
            /// Unknown or unexpected architecture.
            /// </summary>
            Unknown = 0,

            /// <summary>
            /// AMD64
            /// </summary>
            [EnumMember(Value = "x64")]
            Amd64,

            /// <summary>
            /// ARM64
            /// </summary>
            [EnumMember(Value = "arm64")]
            Arm64
        }

        /// <summary>
        /// Wraps a <see cref="ComputeResourceSku"/>, adding type-safe properties to access
        /// the SKU capabilities.
        /// </summary>
        private class VmSku
        {
            private ComputeResourceSku sku;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="sku">The <see cref="ComputeResourceSku"/> being wrapped.</param>
            public VmSku(ComputeResourceSku sku)
            {
                Covenant.Requires<ArgumentNullException>(sku != null, nameof(sku));

                this.sku = sku;

                // Extract the capabilities.

                foreach (var capability in sku.Capabilities)
                {
                    switch (capability.Name)
                    {
                        case "CpuArchitectureType":

                            if (!NeonHelper.TryParse<AzureCpuArchitecture>(capability.Value, out var cpuArchitecture))
                            {
                                cpuArchitecture = AzureCpuArchitecture.Unknown;
                            }

                            this.CpuArchitecture = cpuArchitecture;
                            break;

                        case "vCPUs":

                            this.VirtualCpus = int.Parse(capability.Value);
                            break;

                        case "HyperVGenerations":

                            var generations = capability.Value.Split(',', StringSplitOptions.TrimEntries);

                            this.HypervGen1 = generations.Contains("V1");
                            this.HypervGen2 = generations.Contains("V2");
                            break;

                        case "PremiumIO":

                            this.PremiumIO = NeonHelper.ParseBool(capability.Value);
                            break;

                        case "EphemeralOSDiskSupported":

                            this.EphemeralOSDiskSupported = NeonHelper.ParseBool(capability.Value);
                            break;

                        case "MemoryGB":

                            this.MemoryGiB = decimal.Parse(capability.Value);
                            break;

                        case "AcceleratedNetworkingEnabled":

                            this.AcceleratedNetworking = NeonHelper.ParseBool(capability.Value);
                            break;

                        case "MaxDataDiskCount":

                            this.MaxDataDisks = int.Parse(capability.Value);
                            break;

                        case "MaxNetworkInterfaces":

                            this.MaxNetworkInterfaces = int.Parse(capability.Value);
                            break;
                    }
                }
            }

            //-----------------------------------------------------------------
            // Native properties

            /// <summary>
            /// Returns the SKU name.
            /// </summary>
            public string Name => sku.Name;

            /// <summary>
            /// Returns the SKU tier.
            /// </summary>
            public string Tier => sku.Tier;

            /// <summary>
            /// Returns the SKU size.
            /// </summary>
            public string Size => sku.Size;

            /// <summary>
            /// Returns the SKU family.
            /// </summary>
            public string Family => sku.Family;

            /// <summary>
            /// Returns the kind of resources supported by this SKU.
            /// </summary>
            public string Kind => sku.Kind;

            /// <summary>
            /// Specifies the number of virtual machines in the scale set.
            /// </summary>
            public ComputeResourceSkuCapacity Capacity => sku.Capacity;

            /// <summary>
            /// Lists the locations where the SKU is available.
            /// </summary>
            public IReadOnlyList<AzureLocation> Locations => sku.Locations;

            /// <summary>
            /// Lists the locations and availability zones in those locations where
            /// the SKU is available.
            /// </summary>
            public IReadOnlyList<ComputeResourceSkuLocationInfo> LocationInfo => sku.LocationInfo;

            /// <summary>
            /// Lists the API versions that support this SKU.
            /// </summary>
            public IReadOnlyList<string> ApiVersions => sku.ApiVersions;

            /// <summary>
            /// Returns pricing information for the SKU.
            /// </summary>
            public IReadOnlyList<ResourceSkuCosts> Costs => sku.Costs;

            /// <summary>
            /// Returns a dictionary of SKU capabilities.
            /// </summary>
            public IReadOnlyList<ComputeResourceSkuCapabilities> Capabilities => sku.Capabilities;

            /// <summary>
            /// Lists the restrictions preventing this SKU from being used.  This will be
            /// empty when there are no restrictions.
            /// </summary>
            public IReadOnlyList<ComputeResourceSkuRestrictions> Restrictions => sku.Restrictions;

            //-----------------------------------------------------------------
            // Extended properties

            /// <summary>
            /// Indicates whether use of this SKU is restricted.  See <see cref="Restrictions"/> for more information.
            /// </summary>
            public bool IsRestricted => Restrictions.Count > 0;

            /// <summary>
            /// Identifies the CPU architecture.
            /// </summary>
            public AzureCpuArchitecture CpuArchitecture { get; }

            /// <summary>
            /// <para>
            /// Returns the number of virtual CPUs for the SKU.
            /// </para>
            /// <note>
            /// The number of virtual CPUs may be larger than the number of vCPUS for
            /// a virtual machine SKU.  For example, most modern AMD64 based SKUs support
            /// hyper-threading which effectively results in 2 virtual CPUs per core.
            /// </note>
            /// </summary>
            public int VirtualCpus { get; }

            /// <summary>
            /// Indicates whether the SKU supports HyperV Gen1 images.
            /// </summary>
            public bool HypervGen1 { get; }

            /// <summary>
            /// Indicates whether the SKU supports HyperV Gen2 images.
            /// </summary>
            public bool HypervGen2 { get; }

            /// <summary>
            /// Indicates whether the SKU supports premium I/O.
            /// </summary>
            public bool PremiumIO { get; }

            /// <summary>
            /// Indicates whether the SKU supports ephemeral OS disks.
            /// </summary>
            public bool EphemeralOSDiskSupported { get; }

            /// <summary>
            /// Returns the SKU RAM in GiB.
            /// </summary>
            public decimal MemoryGiB { get; }

            /// <summary>
            /// Indicates whether the SKU supports accelerated networking.
            /// </summary>
            public bool AcceleratedNetworking { get; }

            /// <summary>
            /// Returns the maximum number of additional data disks that can be attached to the SKU.
            /// </summary>
            public int MaxDataDisks { get; }

            /// <summary>
            /// Returns the maximum number of network interfaces that can be attached to the SKU.
            /// </summary>
            public int MaxNetworkInterfaces { get; }
        }

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Used to limit how many threads will be created by parallel operations.
        /// </summary>
        private static readonly ParallelOptions parallelOptions = new ParallelOptions() { MaxDegreeOfParallelism = MaxAsyncParallelHostingOperations };

        /// <summary>
        /// The first NSG rule priority to use for ingress rules.
        /// </summary>
        private const int firstIngressNsgRulePriority = 1000;

        /// <summary>
        /// The first NSG rule priority to use for egress rules.
        /// </summary>
        private const int firstEgressNsgRulePriority = 1000;

        /// <summary>
        /// The first NSG rule priority to use for temporary SSH rules.
        /// </summary>
        private const int firstSshNsgRulePriority = 2000;

        /// <summary>
        /// The name prefix for user related defined ingress rules (from the cluster configuration).
        /// </summary>
        private const string ingressRulePrefix = "ingress-";

        /// <summary>
        /// The name prefix for public cluster ingress and NSG rules used for configuring
        /// or managing nodes public SSH access.
        /// </summary>
        private const string publicSshRulePrefix = "public-ssh-";

        /// <summary>
        /// The (namespace) prefix used for NEONKUBE related Azure resource tags.
        /// </summary>
        private const string neonTagKeyPrefix = "neon:";

        /// <summary>
        /// Used to tag resources with the cluster name.
        /// </summary>
        private const string neonClusterTagKey = neonTagKeyPrefix + "cluster";

        /// <summary>
        /// Used to tag VM resources with the cluster node name.
        /// </summary>
        private const string neonNodeNameTagKey = neonTagKeyPrefix + "node.name";

        /// <summary>
        /// Used to tag VM resources with the cluster node role.
        /// </summary>
        private const string neonNodeRoleTagKey = neonTagKeyPrefix + "node.role";

        /// <summary>
        /// Used to tag virtual machines with the external SSH port to be used to 
        /// establish a SSH connection to the instance.
        /// </summary>
        private const string neonNodeSshPortTagKey = neonTagKeyPrefix + "node.ssh-port";

        /// <summary>
        /// Logical unit number for a node's boot disk.
        /// </summary>
        private const int bootDiskLun = 0;

        /// <summary>
        /// Logical unit number for a node's optional OpenEBS cStor disk.
        /// </summary>
        private const int openEbsDiskLun = 1;

        /// <summary>
        /// Minimum Azure supported TCP reset idle timeout in minutes.
        /// </summary>
        private const int minAzureTcpIdleTimeoutMinutes = 4;

        /// <summary>
        /// Maximum Azure supported TCP reset idle timeout in minutes.
        /// </summary>
        private const int maxAzureTcpIdleTimeoutMinutes = 30;

        /// <summary>
        /// Ensures that the assembly hosting this hosting manager is loaded.
        /// </summary>
        public static void Load()
        {
            // This method can't do nothing because the C# compiler may optimize calls
            // out of trimmed executables and we need this type to be discoverable
            // via reflection.
            //
            // This call does almost nothing to prevent C# optimization.

            Load(() => new AzureHostingManager());
        }

        /// <summary>
        /// Converts a <see cref="IngressProtocol"/> to the corresponding <see cref="SecurityRuleProtocol"/>.
        /// </summary>
        /// <param name="protocol">The input protocol.</param>
        /// <returns>The output protocol.</returns>
        private static SecurityRuleProtocol ToSecurityRuleProtocol(IngressProtocol protocol)
        {
            switch (protocol)
            {
                case IngressProtocol.Http: 
                case IngressProtocol.Https:
                case IngressProtocol.Tcp: 
                    
                    return SecurityRuleProtocol.Tcp;

                default: 
                    
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Converts a <see cref="IngressProtocol"/> to the corresponding <see cref="LoadBalancingTransportProtocol"/>.
        /// </summary>
        /// <param name="protocol">The input protocol.</param>
        /// <returns>The output protocol.</returns>
        private static LoadBalancingTransportProtocol ToTransportProtocol(IngressProtocol protocol)
        {
            switch (protocol)
            {
                case IngressProtocol.Http:
                case IngressProtocol.Https:
                case IngressProtocol.Tcp:

                    return LoadBalancingTransportProtocol.Tcp;

                default:

                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Converts a <see cref="AzureStorageType"/> to the underlying Azure storage type.
        /// </summary>
        /// <param name="azureStorageType">The input storage type.</param>
        /// <param name="osDisk">Optionally indicates that the target disk will be used as the operating system boot disk.</param>
        /// <returns>The underlying Azure storage type.</returns>
        /// <remarks>
        /// Azure does not currently support booting the virtual machine operating system from
        /// a <see cref="AzureStorageType.UltraSSD"/> disk.  Pass <paramref name="osDisk"/> as <c>true</c>
        /// for OS disks and then this method will return the next best storage type <see cref="AzureStorageType.PremiumSSD"/>
        /// for this case.
        /// </remarks>
        private static StorageAccountType ToAzureStorageType(AzureStorageType azureStorageType, bool osDisk = false)
        {
            switch (azureStorageType)
            {
                case AzureStorageType.PremiumSSD:   return StorageAccountType.PremiumLrs;
                case AzureStorageType.StandardHDD:  return StorageAccountType.StandardLrs;
                case AzureStorageType.StandardSSD:  return StorageAccountType.StandardSsdLrs;
                case AzureStorageType.PremiumSSDv2: return osDisk ? StorageAccountType.PremiumV2Lrs : StorageAccountType.UltraSsdLrs;
                case AzureStorageType.UltraSSD:     return osDisk ? StorageAccountType.PremiumLrs : StorageAccountType.UltraSsdLrs;
                default:                            throw new NotImplementedException();
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private const string MarketplacePublisher = "neonforge";
        private const string MarketplaceProduct   = "neonkube";
        private const string MarketplaceOffer     = "neonkube";

        private bool                                        cloudMarketplace;
        private ClusterProxy                                cluster;
        private string                                      clusterName;
        private SetupController<NodeDefinition>             controller;
        private HostingOptions                              hostingOptions;
        private CloudOptions                                cloudOptions;
        private bool                                        prefixResourceNames;
        private AzureHostingOptions                         azureOptions;
        private NetworkOptions                              networkOptions;
        private string                                      region;
        private AzureLocation                               azureLocation;
        private ArmClient                                   azure;
        private SubscriptionResource                        subscription;
        private ImageReference                              nodeImageRef;
        private ComputePlan                                 nodeImagePlan;
        private Dictionary<string, AzureVm>                 nodeNameToVm;
        private Dictionary<string, VmSku>                   nodeNameToVmSku;
        private Dictionary<string, LocationExpanded>        regionToLocation;

        // These names will be used to identify the cluster resources.

        private readonly string                             resourceGroupName;
        private readonly string                             publicIngressAddressName;
        private readonly string                             publicEgressAddressName;
        private readonly string                             publicEgressPrefixName;
        private readonly string                             vnetName;
        private readonly string                             subnetName;
        private readonly string                             primaryNicName;
        private readonly string                             proximityPlacementGroupName;
        private readonly string                             loadbalancerName;
        private readonly string                             loadbalancerFrontendName;
        private readonly string                             loadbalancerIngressBackendName;
        private readonly string                             loadbalancerControlPlaneBackendName;
        private readonly string                             subnetNsgName;
        private readonly string                             natGatewayName;

        // These reference the cluster's Azure resources.

        private ResourceGroupResource                       resourceGroup; 
        private PublicIPAddressResource                     publicIngressAddress;
        private PublicIPAddressResource                     publicEgressAddress;
        private PublicIPPrefixResource                      publicEgressPrefix;
        private IPAddress                                   clusterAddress;
        private VirtualNetworkResource                      vnet;
        private SubnetData                                  subnet;
        private LoadBalancerResource                        loadBalancer;
        private ProximityPlacementGroupResource             proximityPlacementGroup;
        private Dictionary<string, AvailabilitySetResource> nameToAvailabilitySet;
        private NetworkSecurityGroupResource                subnetNsg;
        private NatGatewayResource                          natGateway;

        /// <summary>
        /// Creates an instance that is only capable of validating the hosting
        /// related options in the cluster definition.
        /// </summary>
        public AzureHostingManager()
        {
        }

        /// <summary>
        /// Creates an instance that is capable of provisioning a cluster on Azure.
        /// </summary>
        /// <param name="cluster">The cluster being managed.</param>
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
        /// <param name="nodeImageUri">Ignored.</param>
        /// <param name="nodeImagePath">Ignored.</param>
        /// <param name="logFolder">
        /// The folder where log files are to be written, otherwise or <c>null</c> or 
        /// empty if logging is disabled.
        /// </param>
        /// <remarks>
        /// <note>
        /// <b>WARNING!</b> All hosting manager constructors must have the same signature
        /// because these are constructed via reflection by the <c>HostingLoader</c> class
        /// in the <b>Neon.Kube.Hosting</b> assembly.  The parameter must match what the
        /// <c>HostingLoader</c> expects.
        /// </note>
        /// </remarks>
        public AzureHostingManager(ClusterProxy cluster, bool cloudMarketplace, string nodeImageUri = null, string nodeImagePath = null, string logFolder = null)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null, nameof(cluster));

            cluster.HostingManager = this;

            this.cloudMarketplace      = cloudMarketplace;
            this.cluster               = cluster;
            this.clusterName           = cluster.Name;
            this.hostingOptions        = cluster.Hosting;
            this.cloudOptions          = hostingOptions.Cloud;
            this.azureOptions          = hostingOptions.Azure;
            this.cloudOptions          = hostingOptions.Cloud;
            this.networkOptions        = cluster.SetupState?.ClusterDefinition.Network;
            this.nameToAvailabilitySet = new Dictionary<string, AvailabilitySetResource>(StringComparer.InvariantCultureIgnoreCase);
            this.region                = azureOptions.Region;
            this.azureLocation         = new AzureLocation(azureOptions.Region);

            // Determine the resource group name for the cluster and perform any other
            // initialization based on whether the cluster is being deployed or not.

            if (cluster.SetupState != null)
            {
                this.resourceGroupName = cluster.SetupState.ClusterDefinition.Deployment.GetPrefixedName(azureOptions.ResourceGroup);

                nodeNameToVm = new Dictionary<string, AzureVm>(StringComparer.InvariantCultureIgnoreCase);

                foreach (var nodeProxy in cluster.Nodes)
                {
                    nodeNameToVm.Add(nodeProxy.Name, new AzureVm(nodeProxy, this));
                }
            }
            else
            {
                Covenant.Assert(cluster.KubeConfig != null);

                this.resourceGroupName = azureOptions.ResourceGroup.ToLowerInvariant();
            }

            // Determine whether we're going to prefix the cluster resource names.
            // We default to not doing this for Azure because all resources will
            //already be scoped to the cluster resource group and having shorter
            // names is nicer.

            switch (cloudOptions.PrefixResourceNames)
            {
                case TriState.Default:

                    // Default to FALSE for Azure because all resource names
                    // will be scoped to the cluster resource group.

                    this.prefixResourceNames = false;
                    break;

                case TriState.True:

                    this.prefixResourceNames = true;
                    break;

                case TriState.False:

                    this.prefixResourceNames = false;
                    break;
            }

            // Initialize the component names as they will be deployed to Azure.  Note that we're
            // going to prefix each name with the Azure item type convention described here:
            //
            //      https://docs.microsoft.com/en-us/azure/cloud-adoption-framework/ready/azure-best-practices/naming-and-tagging
            //
            // optionally combined with the cluster name.

            this.publicIngressAddressName            = GetResourceName("pip", "cluster-ingress", true);
            this.publicEgressAddressName             = GetResourceName("pip", "cluster-egress", true);
            this.publicEgressPrefixName              = GetResourceName("ippre", "cluster-egress", true);
            this.vnetName                            = GetResourceName("vnet", "cluster", true);
            this.subnetName                          = GetResourceName("snet", "cluster", true);
            this.primaryNicName                      = "primary";
            this.proximityPlacementGroupName         = GetResourceName("ppg", "cluster", true);
            this.loadbalancerName                    = GetResourceName("lbe", "public", true);
            this.subnetNsgName                       = GetResourceName("nsg", "subnet");
            this.natGatewayName                      = GetResourceName("ng", "cluster", true);
            this.loadbalancerFrontendName            = "ingress";
            this.loadbalancerIngressBackendName      = "ingress-nodes";
            this.loadbalancerControlPlaneBackendName = "control-plane";

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

        /// <inheritdoc/>
        public override int NodeMtu => 1400;    // MTU needs to be smaller than 1500 due to VPC using VXLAN. (https://learn.microsoft.com/en-us/azure/virtual-network/virtual-network-tcpip-performance-tuning#azure-and-vm-mtu)

        /// <summary>
        /// Enumerates the cluster nodes in no particular order.
        /// </summary>
        private IEnumerable<AzureVm> Nodes => nodeNameToVm.Values;

        /// <summary>
        /// Enumerates the cluster nodes in ascending order by name.
        /// </summary>
        private IEnumerable<AzureVm> SortedNodes => Nodes.OrderBy(node => node.Name, StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Enumerates the cluster control-plane nodes in no particular order.
        /// </summary>
        private IEnumerable<AzureVm> ControlNodes => Nodes.Where(node => node.IsControlPlane);

        /// <summary>
        /// Enumerates the cluster control-plane nodes in ascending order by name.
        /// </summary>
        private IEnumerable<AzureVm> SortedControlNodes => Nodes.Where(node => node.IsControlPlane).OrderBy(node => node.Name, StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Enumerates the cluster worker nodes in no particular order.
        /// </summary>
        private IEnumerable<AzureVm> WorkerNodes => Nodes.Where(node => node.IsControlPlane);

        /// <summary>
        /// Enumerates the cluster worker nodes in ascending order by name.
        /// </summary>
        private IEnumerable<AzureVm> SorteWorkerNodes => Nodes.Where(node => node.IsWorker).OrderBy(node => node.Name, StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Enumerates the cluster worker nodes in ascending order by name followed by the sorted worker nodes.
        /// </summary>
        private IEnumerable<AzureVm> SortedControlThenWorkerNodes => SortedControlNodes.Union(SorteWorkerNodes);

        /// <summary>
        /// <para>
        /// Returns the name to use for a cluster related resource based on the standard Azure resource type
        /// prefix, the cluster name (if enabled) and the base resource name.  This is based on Azure naming
        /// conventions:
        /// </para>
        /// <para>
        /// <a href="https://docs.microsoft.com/en-us/azure/cloud-adoption-framework/ready/azure-best-practices/naming-and-tagging">Recommended naming and tagging conventions</a>
        /// </para>
        /// </summary>
        /// <param name="resourceTypePrefix">The Azure resource type prefix (like "pip" for public IP address).</param>
        /// <param name="resourceName">The base resource name.</param>
        /// <param name="omitResourceNameWhenPrefixed">Optionally omit <paramref name="resourceName"/> when resource names include the cluster name.</param>
        /// <returns>The fully qualified resource name.</returns>
        private string GetResourceName(string resourceTypePrefix, string resourceName, bool omitResourceNameWhenPrefixed = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(resourceTypePrefix), nameof(resourceTypePrefix));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(resourceName), nameof(resourceName));

            if (prefixResourceNames)
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

        /// <summary>
        /// Adds standard tags to a resource's tag dictionary as well as any optional tags passed.
        /// </summary>
        /// <param name="resource">The resource data being tagged.</param>
        /// <param name="tags">Specifies any optional tags.</param>
        /// <returns>The updated <paramref name="resource"/>.</returns>
        private TResource WithTags<TResource>(TResource resource, params ResourceTag[] tags)
            where TResource : TrackedResourceData
        {
            Covenant.Requires<ArgumentNullException>(resource != null, nameof(resource));

            resource.Tags[neonClusterTagKey] = clusterName;

            foreach (var tag in tags)
            {
                resource.Tags[tag.Key] = tag.Value;
            }

            return resource;
        }

        /// <summary>
        /// Adds standard tags to a network resource's tag dictionary as well as any optional tags passed.
        /// </summary>
        /// <param name="resource">The resource data being tagged.</param>
        /// <param name="tags">Specifies any optional tags.</param>
        /// <returns>The updated <paramref name="resource"/>.</returns>
        private TResource WithNetworkTags<TResource>(TResource resource, params ResourceTag[] tags)
            where TResource : NetworkTrackedResourceData
        {
            Covenant.Requires<ArgumentNullException>(resource != null, nameof(resource));

            resource.Tags[neonClusterTagKey] = clusterName;

            foreach (var tag in tags)
            {
                resource.Tags[tag.Key] = tag.Value;
            }

            return resource;
        }

        /// <inheritdoc/>
        public override HostingEnvironment HostingEnvironment => HostingEnvironment.Azure;

        /// <inheritdoc/>
        public override bool RequiresNodeAddressCheck => false;

        /// <inheritdoc/>
        public override void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));
            Covenant.Assert(clusterDefinition.Hosting.Environment == HostingEnvironment.Azure, $"{nameof(HostingOptions)}.{nameof(HostingOptions.Environment)}] must be set to [{HostingEnvironment.Azure}].");

            if (string.IsNullOrEmpty(clusterDefinition.Hosting.Azure.ClientId))
            {
                throw new ClusterDefinitionException($"{nameof(AzureHostingOptions)}.{nameof(AzureHostingOptions.ClientId)}] is required for Azure clusters.");
            }

            if (string.IsNullOrEmpty(clusterDefinition.Hosting.Azure.ClientSecret))
            {
                throw new ClusterDefinitionException($"{nameof(AzureHostingOptions)}.{nameof(AzureHostingOptions.ClientSecret)}] is required for Azure clusters.");
            }

            AssignNodeAddresses(clusterDefinition);

            // Set the cluster definition datacenter to the target region when the
            // user hasn't explictly specified a datacenter.

            if (string.IsNullOrEmpty(clusterDefinition.Datacenter))
            {
                clusterDefinition.Datacenter = clusterDefinition.Hosting.Azure.Region.ToUpperInvariant();
            }
        }

        /// <inheritdoc/>
        public override async Task CheckDeploymentReadinessAsync(ClusterDefinition clusterDefinition)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            var readiness = new HostingReadiness();

            // Connect to Azure and lookup the node instance types to determine the number
            // of vCPUs and memory available for each and then call a common method to
            // verify that NEONKUBE actually supports those instance types.

            try
            {
                await ConnectAzureAsync();
            }
            catch (Exception e)
            {
                readiness.AddProblem(type: HostingReadinessProblem.AzureType, details: NeonHelper.ExceptionError(e));
                readiness.ThrowIfNotReady();
            }

            var nameToVmSku = new Dictionary<string, VmSku>(StringComparer.InvariantCultureIgnoreCase);

            await foreach (var resourceSku in subscription.GetComputeResourceSkusAsync())
            {
                if (resourceSku.ResourceType == "virtualMachines")
                {
                    nameToVmSku[resourceSku.Name] = new VmSku(resourceSku);
                }
            }

            var hostedNodes = new List<HostedNodeInfo>();

            foreach (var nodeDefinition in clusterDefinition.Nodes)
            {
                if (!nameToVmSku.TryGetValue(nodeDefinition.Azure.VmSize, out var vmSizeInfo))
                {
                    throw new ClusterDefinitionException($"Node [{nodeDefinition.Name}] specifies [VMSize={nodeDefinition.Azure.VmSize}] which does not exist.");
                }

                hostedNodes.Add(new HostedNodeInfo(nodeDefinition.Name, nodeDefinition.Role, vmSizeInfo.VirtualCpus, (long)(vmSizeInfo.MemoryGiB * ByteUnits.GibiBytes)));
            }

            ValidateCluster(clusterDefinition, hostedNodes, readiness);

            readiness.ThrowIfNotReady();
        }

        /// <inheritdoc/>
        public override void AddProvisioningSteps(SetupController<NodeDefinition> controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Assert(cluster != null, $"[{nameof(AzureHostingManager)}] was created with the wrong constructor.");

            this.controller = controller;

            // We need to ensure that the cluster has at least one ingress node.

            KubeHelper.EnsureIngressNodes(cluster.SetupState.ClusterDefinition);

            // Initialize and run the [SetupController].

            var operation = $"Provisioning [{cluster.Name}] on Azure [{region}/{resourceGroupName}]";

            controller.AddGlobalStep("AZURE connect", state => ConnectAzureAsync());
            controller.AddGlobalStep("locate node image", state => LocateNodeImageAsync());
            controller.AddGlobalStep("region check", state => VerifyRegionAndVmSizesAsync());
            controller.AddGlobalStep("resource group", state => GetClusterResourceGroup());

            if (!azureOptions.DisableProximityPlacement)
            {
                controller.AddGlobalStep("proximity placement group", state => CreateProximityPlacementGroupAsync());
            }

            controller.AddGlobalStep("availability sets", state => CreateAvailabilitySetsAsync());
            controller.AddGlobalStep("public addresses/prefixes", state => CreateAddressesAndPrefixAsync());
            controller.AddGlobalStep("network security groups", state => CreateNetworkSecurityGroupAsync());
            controller.AddGlobalStep("nat gateway", state => CreateNatGatewayAsync());
            controller.AddGlobalStep("virtual network", state => CreateVirtualNetworkAsync());
            controller.AddGlobalStep("ssh config", ConfigureNodeSsh, quiet: true);
            controller.AddGlobalStep("load balancer", state => CreateLoadBalancerAsync());
            controller.AddGlobalStep("listing virtual machines",
                async state =>
                {
                    controller.SetGlobalStepStatus("list: virtual machines");

                    // Update [azureNodes] with any existing Azure nodes and their NICs.
                    // Note that it's possible for VMs that are unrelated to the cluster
                    // to be in the resource group, so we'll have to ignore those.

                    var virtualMachineCollection = resourceGroup.GetVirtualMachines();

                    await foreach (var vm in virtualMachineCollection.GetAllAsync())
                    {
                        if (!vm.Data.Tags.TryGetValue(neonNodeNameTagKey, out var nodeName))
                        {
                            break;  // Not a cluster VM
                        }

                        if (!nodeNameToVm.TryGetValue(nodeName, out var azureVm))
                        {
                            // $todo(jefflill):
                            //
                            // This happens when a VM exists for the node but there's no node
                            // defined in the cluster definition.  In the future, we should
                            // remove the node from the cluster when we support adding/removing
                            // nodes in an existing cluster.
                            //
                            // We're going to ignore these for the time being.

                            break;
                        }

                        azureVm.Vm = vm;

                        var nicReference = vm.Data.NetworkProfile.NetworkInterfaces.First();
                        var nicResource  = azure.GetNetworkInterfaceResource(new ResourceIdentifier(nicReference.Id));

                        azureVm.Nic = await nicResource.GetAsync();
                    }
                },
                quiet: true);
            controller.AddNodeStep("credentials",
                (controller, node) =>
                {
                    // Update the node SSH proxies to use the private SSH key.

                    node.UpdateCredentials(SshCredentials.FromPrivateKey(KubeConst.SysAdminUser, cluster.SetupState.SshKey.PrivatePEM));
                },
                quiet: true);
            controller.AddNodeStep("virtual machines", CreateVmAsync);
            controller.AddGlobalStep("internet access", state => UpdateNetworkAsync(NetworkOperations.InternetRouting | NetworkOperations.AllowNodeSsh));
        }

        /// <inheritdoc/>
        public override void AddPostProvisioningSteps(SetupController<NodeDefinition> controller)
        {
            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);

            if (cluster.SetupState.ClusterDefinition.Storage.OpenEbs.Engine == OpenEbsEngine.cStor)
            {
                // We need to add any required OpenEBS cStor disks after the node has been otherwise
                // prepared.  We need to do this here because if we created the data and OpenEBS disks
                // when the VM is initially created, the disk setup scripts executed during prepare
                // won't be able to distinguish between the two disks.
                //
                // At this point, the data disk should be partitioned, formatted, and mounted so
                // the OpenEBS disk will be easy to identify as the only unpartitioned disk.

                controller.AddNodeStep("openebs",
                    async (controller, node) =>
                    {
                        var azureVm            = nodeNameToVm[node.Name];
                        var vm                 = azureVm.Vm;
                        var openEbsStorageType = ToAzureStorageType(azureVm.Metadata.Azure.OpenEbsStorageType);

                        node.Status = "openebs: checking";

                        if (node.Metadata.OpenEbsStorage)    
                        {
                            node.Status = "openebs: cStor disk";

                            var vmPatch = new VirtualMachinePatch()
                            {
                                StorageProfile = vm.Data.StorageProfile
                            };

                            var openEbsDiskSize = ByteUnits.Parse(node.Metadata.Azure.OpenEbsDiskSize);

                            vmPatch.StorageProfile.DataDisks.Add(
                                new VirtualMachineDataDisk(openEbsDiskLun, DiskCreateOptionType.Empty)
                                {
                                    DiskSizeGB   = (int)AzureHelper.GetDiskSizeGiB(azureVm.NodeProxy.Metadata.Azure.StorageType, openEbsDiskSize),
                                    Caching      = CachingType.None,
                                    ManagedDisk  = new VirtualMachineManagedDisk()
                                    {
                                        StorageAccountType = openEbsStorageType
                                    },
                                    DeleteOption = DiskDeleteOptionType.Delete,
                                });

                            azureVm.Vm = (await vm.UpdateAsync(WaitUntil.Completed, vmPatch)).Value;
                        }
                    },
                    (controller, node) => node.Metadata.OpenEbsStorage);
            }
        }

        /// <inheritdoc/>
        public override void AddSetupSteps(SetupController<NodeDefinition> controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            this.controller = controller;

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);

            controller.AddGlobalStep("connect azure",
                async controller =>
                {
                    await ConnectAzureAsync();
                });

            controller.AddGlobalStep("ssh port mappings",
                async controller =>
                {
                    await cluster.HostingManager.EnableInternetSshAsync();

                    // We need to update the cluster node addresses and SSH ports
                    // to match the cluster load balancer port forward rules.

                    foreach (var node in cluster.Nodes)
                    {
                        var endpoint = cluster.HostingManager.GetSshEndpoint(node.Name);

                        node.Address = IPAddress.Parse(endpoint.Address);
                        node.SshPort = endpoint.Port;
                    }
                });
        }

        /// <inheritdoc/>
        public override void AddPostSetupSteps(SetupController<NodeDefinition> controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            this.controller = controller;

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);

            controller.AddGlobalStep("node topology",
                async controller =>
                {
                    controller.LogProgress(verb: "label", message: "nodes (cloud)");

                    var k8s               = controller.Get<IKubernetes>(KubeSetupProperty.K8sClient);
                    var cluster           = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
                    var clusterDefinition = cluster.SetupState.ClusterDefinition;
                    var k8sNodes          = (await k8s.CoreV1.ListNodeAsync()).Items;

                    foreach (var nodeDefinition in clusterDefinition.NodeDefinitions.Values)
                    {
                        controller.ThrowIfCancelled();

                        var k8sNode = k8sNodes.Where(n => n.Metadata.Name == nodeDefinition.Name).Single();

                        var patch = new V1Node()
                        {
                            Metadata = new V1ObjectMeta()
                            {
                                Labels = k8sNode.Labels()
                            }
                        };

                        var vm = nodeNameToVm[nodeDefinition.Name];

                        if (!nodeDefinition.Labels.Custom.ContainsKey("node.kubernetes.io/instance-type"))
                        {
                            patch.Metadata.Labels.Add("node.kubernetes.io/instance-type", vm.VmSize);
                        }

                        if (!nodeDefinition.Labels.Custom.ContainsKey("topology.kubernetes.io/region"))
                        {
                            patch.Metadata.Labels.Add("topology.kubernetes.io/region", vm.Vm.Data.Location.Name);
                        }

                        if (!nodeDefinition.Labels.Custom.ContainsKey("topology.kubernetes.io/zone"))
                        {
                            // For Azure, the [AvailabilitySetId] is a long URI path, longer than the 63 maximum
                            // characters allowed by Kubernetes labels.  It looks like the availablty set name is
                            // the last segment of this path.  We're going to extract ant use that, which will be
                            // cleaner anyway.
                            //
                            // NOTE: We're converting set name to lowercase and stripping off the "avail-" prefix
                            // if present.

                            const string prefix = "avail-";

                            var availabilitySetName = vm.Vm.Data.AvailabilitySetId.ToString().Split('/').Last().ToLowerInvariant();

                            if (availabilitySetName.StartsWith(prefix))
                            {
                                availabilitySetName = availabilitySetName.Substring(prefix.Length);
                            }

                            patch.Metadata.Labels.Add("topology.kubernetes.io/zone", availabilitySetName);
                        }

                        if (patch.Metadata.Labels.Count > 0)
                        {
                            await k8s.CoreV1.PatchNodeAsync(new V1Patch(patch, V1Patch.PatchType.StrategicMergePatch), k8sNode.Metadata.Name);
                        }
                    }
                });

            controller.AddGlobalStep("ssh block ingress",
                async controller =>
                {
                    await cluster.HostingManager.DisableInternetSshAsync();
                });
        }

        /// <inheritdoc/>
        public override bool CanManageRouter => true;

        /// <inheritdoc/>
        public override async Task UpdateInternetRoutingAsync()
        {
            await SyncContext.Clear;

            await ConnectAzureAsync();

            var operations = NetworkOperations.InternetRouting;

            if (loadBalancer.Data.LoadBalancingRules.Any(rule => rule.Name.StartsWith(ingressRulePrefix, StringComparison.InvariantCultureIgnoreCase)))
            {
                // It looks like SSH NAT rules are enabled so we'll update
                // those as well.

                operations |= NetworkOperations.AllowNodeSsh;
            }

            await UpdateNetworkAsync(operations);
        }

        /// <inheritdoc/>
        public override async Task EnableInternetSshAsync()
        {
            await SyncContext.Clear;

            await ConnectAzureAsync();
            await UpdateNetworkAsync(NetworkOperations.AllowNodeSsh);
        }

        /// <inheritdoc/>
        public override async Task DisableInternetSshAsync()
        {
            await SyncContext.Clear;

            await ConnectAzureAsync();
            await UpdateNetworkAsync(NetworkOperations.DenyNodeSsh);
        }

        /// <inheritdoc/>
        public override (string Address, int Port) GetSshEndpoint(string nodeName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nodeName), nameof(nodeName));
            Covenant.Assert(azure != null, "Azure: Not connected.");

            // Get the Azure VM so we can retrieve the assigned external SSH port for the
            // node via the external SSH port tag.

            if (!nodeNameToVm.TryGetValue(nodeName, out var azureVm))
            {
                throw new NeonKubeException($"Cannot locate Azure VM for the [{nodeName}] node.");
            }

            return (Address: publicIngressAddress.Data.IPAddress, Port: azureVm.ExternalSshPort);
        }

        /// <inheritdoc/>
        public override string GetDataDisk(LinuxSshProxy node)
        {
            Covenant.Requires<ArgumentNullException>(node != null, nameof(node));

            var unpartitonedDisks = node.ListUnpartitionedDisks();

            if (unpartitonedDisks.Count() == 0)
            {
                return "PRIMARY";
            }

            Covenant.Assert(unpartitonedDisks.Count() == 1, "VMs are assumed to have no more than one attached data disk.");

            return unpartitonedDisks.Single();
        }

        /// <inheritdoc/>
        public override IEnumerable<string> GetClusterAddresses()
        {
            return new List<string>() { publicIngressAddress.Data.IPAddress };
        }

        /// <inheritdoc/>
        public override async Task<(double? Latitude, double? Longitude)> GetDatacenterCoordinatesAsync()
        {
            await SyncContext.Clear;

            Covenant.Assert(!string.IsNullOrEmpty(region));

            if (regionToLocation.TryGetValue(region, out var location))
            {
                return (Latitude: location.Metadata.Latitude, Longitude: location.Metadata.Longitude);
            }
            else
            {
                throw new NeonKubeException($"Azure region [{region}] does not exist or is not available to your subscription.");
            }
        }

        /// <summary>
        /// <para>
        /// Connects to Azure if we're not already connected.
        /// </para>
        /// <note>
        /// The current state of the deployed resources will always be loaded by this method,
        /// even if an connection has already been established.
        /// </note>
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task ConnectAzureAsync()
        {
            await SyncContext.Clear;

            if (azure != null)
            {
                return;
            }

            controller?.SetGlobalStepStatus("connect: Azure");

            var environment = ArmEnvironment.AzurePublicCloud;

            if (azureOptions.Environment != null)
            {
                switch (azureOptions.Environment.Name)
                {
                    case AzureCloudEnvironments.GlobalCloud:

                        environment = ArmEnvironment.AzurePublicCloud;
                        break;

                    case AzureCloudEnvironments.ChinaCloud:

                        environment = ArmEnvironment.AzureChina;
                        break;

                    case AzureCloudEnvironments.GermanCloud:

                        environment = ArmEnvironment.AzureGermany;
                        break;

                    case AzureCloudEnvironments.USGovernment:

                        environment = ArmEnvironment.AzureGovernment;
                        break;

                    case AzureCloudEnvironments.Custom:

                        environment = new ArmEnvironment(new Uri(azureOptions.Environment.Endpoint), azureOptions.Environment.Audience);
                        break;

                    default:

                        throw new NotImplementedException($"Azure environment [{azureOptions.Environment.Name}] is not currently supported.");
                }
            }

            // We're going to indirectly use the [EnvironmentCredential] to create
            // the default credential by setting the required environment variables.

            Environment.SetEnvironmentVariable("AZURE_TENANT_ID", azureOptions.TenantId);
            Environment.SetEnvironmentVariable("AZURE_CLIENT_ID", azureOptions.ClientId);
            Environment.SetEnvironmentVariable("AZURE_CLIENT_SECRET", azureOptions.ClientSecret);

            azure = new ArmClient(new DefaultAzureCredential(),
                azureOptions.SubscriptionId,
                new ArmClientOptions()
                {
                    Environment = ArmEnvironment.AzurePublicCloud
                });

            subscription = await azure.GetDefaultSubscriptionAsync();

            // Load references to any existing cluster resources.

            await LoadResourcesAsync();
        }

        /// <summary>
        /// Loads references to any existing cluster resources.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task LoadResourcesAsync()
        {
            await SyncContext.Clear;

            if (nodeNameToVm == null)
            {
                nodeNameToVm = new Dictionary<string, AzureVm>(StringComparer.InvariantCultureIgnoreCase);
            }

            //-----------------------------------------------------------------
            // Load information about the Azure regions available to this subscription.

            // Verify that the region exists and is available to the current subscription.

            regionToLocation = new Dictionary<string, LocationExpanded>(StringComparer.InvariantCultureIgnoreCase);

            await foreach (var location in subscription.GetLocationsAsync())
            {
                regionToLocation.Add(location.Name, location);
            }

            //-----------------------------------------------------------------
            // The resource group.

            var resourceGroupCollection = subscription.GetResourceGroups();

            if (!await resourceGroupCollection.ExistsAsync(resourceGroupName))
            {
                // The resource group doesn't exist so it's not possible for any other
                // cluster resources to exist either.

                return;
            }

            resourceGroup = await resourceGroupCollection.GetAsync(resourceGroupName);

            //-----------------------------------------------------------------
            // Network resources.

            var vnetCollection         = resourceGroup.GetVirtualNetworks();
            var nsgCollection          = resourceGroup.GetNetworkSecurityGroups();
            var loadBalancerCollection = resourceGroup.GetLoadBalancers();
            var natGatewayCollection   = resourceGroup.GetNatGateways();
            var publicAddresses        = resourceGroup.GetPublicIPAddresses();
            var publicPrefixes         = resourceGroup.GetPublicIPPrefixes();

            if (await publicAddresses.ExistsAsync(publicIngressAddressName))
            {
                publicIngressAddress = await publicAddresses.GetAsync(publicIngressAddressName);
                clusterAddress       = NetHelper.ParseIPv4Address(publicIngressAddress.Data.IPAddress);
            }

            if (await publicAddresses.ExistsAsync(publicEgressAddressName))
            {
                publicEgressAddress = await publicAddresses.GetAsync(publicEgressAddressName);
            }

            if (await publicPrefixes.ExistsAsync(publicEgressPrefixName))
            {
                publicEgressPrefix = await publicPrefixes.GetAsync(publicEgressPrefixName);
            }

            if (await vnetCollection.ExistsAsync(vnetName))
            {
                vnet   = (await vnetCollection.GetAsync(vnetName)).Value;
                subnet = vnet.Data.Subnets.First();
            }

            if (await nsgCollection.ExistsAsync(subnetNsgName))
            {
                subnetNsg = (await nsgCollection.GetAsync(subnetNsgName)).Value;
            }

            if (await loadBalancerCollection.ExistsAsync(loadbalancerName))
            {
                loadBalancer = (await loadBalancerCollection.GetAsync(loadbalancerName)).Value;
            }

            if (await natGatewayCollection.ExistsAsync(natGatewayName))
            {
                natGateway = (await natGatewayCollection.GetAsync(natGatewayName)).Value;
            }

            //-----------------------------------------------------------------
            // Availability sets

            var availabilitySetCollection = resourceGroup.GetAvailabilitySets();

            nameToAvailabilitySet.Clear();

            await foreach (var availabilitySet in availabilitySetCollection.GetAllAsync())
            {
                nameToAvailabilitySet.Add(availabilitySet.Data.Name, availabilitySet);
            }

            //-----------------------------------------------------------------
            // Virtual machines

            nodeNameToVm.Clear();

            var vmList = new List<VirtualMachineResource>();

            await foreach (var vm in resourceGroup.GetVirtualMachines())
            {
                vmList.Add(vm);
            }

            if (cluster.SetupState != null)
            {
                // Cluster deployment is in progress.
                //
                // We're going to initialize [nodeNameToVm] using node proxies.

                foreach (var node in cluster.Nodes)
                {
                    nodeNameToVm.Add(node.Name, new AzureVm(node, this));

                    if (node.Metadata.Azure == null)
                    {
                        node.Metadata.Azure = new AzureNodeOptions();
                    }
                }

                foreach (var vm in vmList)
                {
                    if (!vm.Data.Tags.TryGetValue(neonNodeNameTagKey, out var nodeName))
                    {
                        throw new NeonKubeException($"Corrupted VM: [{vm.Data.Name}] is missing the [{neonNodeNameTagKey}] tag.");
                    }

                    var node = cluster.FindNode(nodeName);

                    if (node == null)
                    {
                        throw new NeonKubeException($"Unexpected VM: [{vm.Data.Name}] does not correspond to a node in the cluster definition.");
                    }

                    if (!vm.Data.Tags.TryGetValue(neonNodeSshPortTagKey, out var sshPortString))
                    {
                        throw new NeonKubeException($"Corrupted VM: [{vm.Data.Name}] is missing the [{neonNodeSshPortTagKey}] tag.");
                    }

                    if (!int.TryParse(sshPortString, out var sshPort) || !NetHelper.IsValidPort(sshPort))
                    {
                        throw new NeonKubeException($"Corrupted VM: [{vm.Data.Name}] is has invalid [{neonNodeSshPortTagKey}={sshPortString}] tag.");
                    }

                    var azureVm = nodeNameToVm[nodeName];

                    azureVm.Vm                  = vm;
                    azureVm.AvailabilitySetName = (await azure.GetAvailabilitySetResource(vm.Data.AvailabilitySetId).GetAsync()).Value.Data.Name;

                    var nicReference = vm.Data.NetworkProfile.NetworkInterfaces.First();
                    var nicResource  = azure.GetNetworkInterfaceResource(new ResourceIdentifier(nicReference.Id));

                    azureVm.Nic             = await nicResource.GetAsync();
                    azureVm.ExternalSshPort = sshPort;
                }
            }
            else
            {
                // Cluster is already deployed.
                //
                // We're going to initialize [nodeNameToVm] from Azure virtual machine information.

                foreach (var vm in vmList)
                {
                    if (!vm.Data.Tags.TryGetValue(neonNodeNameTagKey, out var nodeName))
                    {
                        throw new NeonKubeException($"Corrupted VM: [{vm.Data.Name}] is missing the [{neonNodeNameTagKey}] tag.");
                    }

                    var nodeDeployment = new NodeDeployment() { Name = nodeName };
                    var azureVm        = new AzureVm(nodeDeployment, this);

                    azureVm.Vm                  = vm;
                    azureVm.AvailabilitySetName = (await azure.GetAvailabilitySetResource(vm.Data.AvailabilitySetId).GetAsync()).Value.Data.Name;

                    var nicReference = vm.Data.NetworkProfile.NetworkInterfaces.First();
                    var nicResource  = azure.GetNetworkInterfaceResource(new ResourceIdentifier(nicReference.Id));

                    azureVm.Nic = await nicResource.GetAsync();

                    nodeNameToVm.Add(nodeName, azureVm);
                }
            }
        }

        /// <summary>
        /// Queries Azure for the provisoning/power status for all known cluster nodes.  This updates
        /// the <see cref="nodeNameToVm"/> dictionary values.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task GetAllClusterVmStatus()
        {
            await SyncContext.Clear;
            Covenant.Assert(azure != null);
            Covenant.Assert(nodeNameToVm != null);

            // Unfortunately, Azure doesn't retrieve VM powerstate when we list the VMs in [LoadResourcesAsync()],
            // so we need to query for each VM state individually.  We'll do this in parallel to speed things up.
            //
            // We'll be querying for each VM for its [InstanceView] and examine it's current status:
            //
            //      https://docs.microsoft.com/en-us/azure/virtual-machines/states-billing
            //
            // and then convert that to our [ClusterNodeState].

            await Parallel.ForEachAsync(nodeNameToVm.Values, parallelOptions,
                async (azureVm, cancellationToken) =>
                {
                    if (azureVm.Vm == null)
                    {
                        // The virtual machine doesn't actually exist.

                        azureVm.State = ClusterNodeState.NotProvisioned;
                        return;
                    }

                    var instanceView = (await azureVm.Vm.InstanceViewAsync()).Value;
                    var latestStatus = instanceView.Statuses
                        .Where(status => status.Code.StartsWith("PowerState/"))
                        .OrderByDescending(status => status.Time)
                        .FirstOrDefault();

                    if (latestStatus == null)
                    {
                        azureVm.State = ClusterNodeState.Off;
                    }
                    else
                    {
                        switch (latestStatus.Code.ToLowerInvariant())
                        {
                            case "powerstate/starting":

                                azureVm.State = ClusterNodeState.Starting;
                                break;

                            case "powerstate/running":

                                azureVm.State = ClusterNodeState.Running;
                                break;

                            case "powerstate/stopping":

                                azureVm.State = ClusterNodeState.Running;
                                break;

                            case "powerstate/stopped":

                                azureVm.State = ClusterNodeState.Off;
                                break;

                            case "powerstate/deallocating":

                                azureVm.State = ClusterNodeState.Running;
                                break;

                            case "powerstate/deallocated":

                                azureVm.State = ClusterNodeState.NotProvisioned;
                                break;

                            default:

                                azureVm.State = ClusterNodeState.Unknown;
                                break;
                        }
                    }
                });
        }

        /// <summary>
        /// Locates the virtual machine node image to be used to provision the cluster.
        /// The <see cref="nodeImageRef"/> and possibly <see cref="nodeImagePlan"/> members
        /// will be set to the correct image reference and plan.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task LocateNodeImageAsync()
        {
            await SyncContext.Clear;

            var neonKubeVersion = SemanticVersion.Parse(KubeVersions.NeonKube);
            var cpuArchitecture = NeonHelper.CpuArchitecture.ToMemberString();

            if (cloudMarketplace)
            {
                // Query the headend to locate the marketplace offer to use.

                var headendClient   = controller.Get<HeadendClient>(KubeSetupProperty.NeonCloudHeadendClient);
                var usePreviewImage = controller.Get<bool>(KubeSetupProperty.UsePreviewImage, false);
                var imageDetails    = await headendClient.ClusterSetup.GetAzureImageDetailsAsync(KubeVersions.NeonKube, CpuArchitecture.amd64, preview: usePreviewImage);

                //###############################
                // $debug(jefflill): DELETE THIS!

                var preview     = false;
                var fullVersion = SemanticVersion.Parse("0.10.0-beta.1");
                var numericPart = fullVersion.Numeric;
                var prerelease  = fullVersion.Prerelease;

                string offer;

                imageDetails  = new AzureImageDetails()
                {
                    ImageReference = new AzureImageReference()
                    {
                        Publisher = "neonforge",
                        Sku       = "neonkube",
                        Offer     = preview ? "neonkube-preview" : "neonkube",
                        Version   = numericPart
                    },
                    ComputePlan = new AzureComputePlan()
                    {
                        Publisher = "neonforge",
                        Name      = "neonkube",
                        Product   = preview ? "neonkube-preview" : "neonkube"
                    }
                };
                //###############################

                nodeImageRef = new ImageReference()
                {
                    Publisher = imageDetails.ImageReference.Publisher,
                    Offer     = imageDetails.ImageReference.Offer,
                    Sku       = imageDetails.ImageReference.Sku,
                    Version   = imageDetails.ImageReference.Version,
                };

                nodeImagePlan = new ComputePlan()
                {
                    Name      = imageDetails.ComputePlan.Name,
                    Publisher = imageDetails.ComputePlan.Publisher,
                    Product   = imageDetails.ComputePlan.Product,
                };
            }
            else
            {
                // This is hardcoded to locate the current node image from our private development image gallery.

                const string galleryResourceGroupName = "neonkube-images";
                const string galleryName              = "neonkube.images";

                var nodeImageName           = neonKubeVersion.IsPrerelease ? $"neonkube-node-{cpuArchitecture}-{neonKubeVersion.Prerelease}{KubeVersions.BranchPart}"
                                                                           : $"neonkube-node-{cpuArchitecture}{KubeVersions.BranchPart}";
                var nodeImageVersionName    = $"{neonKubeVersion.Major}.{neonKubeVersion.Minor}.{neonKubeVersion.Patch}";
                var resourceGroupCollection = subscription.GetResourceGroups();

                if (!await resourceGroupCollection.ExistsAsync(galleryResourceGroupName))
                {
                    throw new NeonKubeException($"Resource group [{galleryResourceGroupName}] not found in subscription.");
                }

                var galleryResourceGroup = (await resourceGroupCollection.GetAsync(galleryResourceGroupName)).Value;
                var galleryCollection    = galleryResourceGroup.GetGalleries();

                if (!await galleryCollection.ExistsAsync(galleryName))
                {
                    throw new NeonKubeException($"Gallery [{galleryName}] not found in resource group: {galleryResourceGroupName}.");
                }

                var gallery                = (await galleryCollection.GetAsync(galleryName)).Value;
                var galleryImageCollection = gallery.GetGalleryImages();

                if (!await galleryImageCollection.ExistsAsync(nodeImageName))
                {
                    throw new NeonKubeException($"Node image [{nodeImageName}] not found in resource group: {galleryResourceGroupName}:{galleryName}");
                }

                var nodeImage                     = (await galleryImageCollection.GetAsync(nodeImageName)).Value;
                var galleryImageVersionCollection = nodeImage.GetGalleryImageVersions();

                if (!await galleryImageVersionCollection.ExistsAsync(nodeImageVersionName))
                {
                    throw new NeonKubeException($"Node image [{nodeImageVersionName}] not found in resource group: {galleryResourceGroupName}:{galleryName}");
                }

                nodeImageRef = new ImageReference()
                {
                    Id = (await galleryImageVersionCollection.GetAsync(nodeImageVersionName)).Value.Id
                };
            }
        }

        /// <summary>
        /// Loads virtual machine size related information for the current region into the
        /// <see cref="nodeNameToVmSku"/> field when not already initialized.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task LoadVmSizeMetadataAsync()
        {
            await SyncContext.Clear;

            if (nodeNameToVmSku == null)
            {
                nodeNameToVmSku = new Dictionary<string, VmSku>(StringComparer.InvariantCultureIgnoreCase);

                await foreach (var resourceSku in subscription.GetComputeResourceSkusAsync(filter: $"location eq '{region.ToLowerInvariant()}'"))
                {
                    if (resourceSku.ResourceType == "virtualMachines")
                    {
                        nodeNameToVmSku[resourceSku.Name] = new VmSku(resourceSku);
                    }
                }
            }
        }

        /// <summary>
        /// <para>
        /// Verifies that the requested Azure region exists, supports the requested VM sizes,
        /// and that VMs for nodes that specify UltraSSD actually support UltraSSD.  We'll also
        /// verify that the requested VMs have the minimum required number or vCPUs and RAM.
        /// </para>
        /// <para>
        /// This also updates the node labels to match the capabilities of their VMs.
        /// </para>
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task VerifyRegionAndVmSizesAsync()
        {
            await SyncContext.Clear;

            controller.SetGlobalStepStatus("verify: Azure region and VM size availability");

            var regionName = cluster.Hosting.Azure.Region;

            await LoadVmSizeMetadataAsync();

            foreach (var node in cluster.Nodes)
            {
                var vmSkuName = node.Metadata.Azure.VmSize;

                if (!nodeNameToVmSku.TryGetValue(vmSkuName, out var vmSku))
                {
                    throw new NeonKubeException($"Node [{node.Name}] requests [{nameof(node.Metadata.Azure.VmSize)}={vmSkuName}].  This is not available in the [{regionName}] Azure region.");
                }

                switch (node.Metadata.Role)
                {
                    case NodeRole.ControlPlane:

                        if (vmSku.VirtualCpus < KubeConst.MinControlNodeVCpus)
                        {
                            throw new NeonKubeException($"Control-plane node [{node.Name}] requests [{nameof(node.Metadata.Azure.VmSize)}={vmSkuName}] with [vcpus={vmSku.VirtualCpus} MiB] which is lower than the required [{KubeConst.MinControlNodeVCpus}] vcpus.]");
                        }

                        if (vmSku.MemoryGiB < KubeConst.MinControlPlaneNodeRamMiB / 1024)
                        {
                            throw new NeonKubeException($"Control-plane node [{node.Name}] requests [{nameof(node.Metadata.Azure.VmSize)}={vmSkuName}] with [memory={vmSku.MemoryGiB} MiB] which is lower than the required [{KubeConst.MinControlPlaneNodeRamMiB * 1024} MiB].]");
                        }

                        if (vmSku.MaxDataDisks < 1)
                        {
                            throw new NeonKubeException($"Control-plane node [{node.Name}] requests [{nameof(node.Metadata.Azure.VmSize)}={vmSkuName}] that supports up to [{vmSku.MaxDataDisks}] disks.  A minimum of [1] drive is required.");
                        }
                        break;

                    case NodeRole.Worker:

                        if (vmSku.VirtualCpus < KubeConst.MinWorkerNodeVCpus)
                        {
                            throw new NeonKubeException($"Worker node [{node.Name}] requests [{nameof(node.Metadata.Azure.VmSize)}={vmSkuName}] with [vcpus={vmSku.VirtualCpus} MiB] which is lower than the required [{KubeConst.MinWorkerNodeVCpus}] vcpus.]");
                        }

                        if (vmSku.MemoryGiB < KubeConst.MinWorkerNodeRamMiB / 1024)
                        {
                            throw new NeonKubeException($"Worker node [{node.Name}] requests [{nameof(node.Metadata.Azure.VmSize)}={vmSkuName}] with [memory={vmSku.MemoryGiB * 1024} MiB] which is lower than the required [{KubeConst.MinWorkerNodeRamMiB} MiB].]");
                        }

                        if (vmSku.MaxDataDisks < 1)
                        {
                            throw new NeonKubeException($"Worker node [{node.Name}] requests [{nameof(node.Metadata.Azure.VmSize)}={vmSkuName}] that supports up to [{vmSku.MaxDataDisks}] disks.  A minimum of [1] drive is required.");
                        }
                        break;

                    default:

                        throw new NotImplementedException();
                }

                // Update the node labels to match the actual VM capabilities.

                node.Metadata.Labels.StorageOSDiskSize      = $"{AzureHelper.GetDiskSizeGiB(node.Metadata.Azure.StorageType, ByteUnits.Parse(node.Metadata.Azure.DiskSize))} GiB";
                node.Metadata.Labels.StorageOSDiskHDD       = node.Metadata.Azure.StorageType == AzureStorageType.StandardHDD;
                node.Metadata.Labels.StorageOSDiskEphemeral = false;
                node.Metadata.Labels.StorageOSDiskLocal     = false;
                node.Metadata.Labels.StorageOSDiskRedundant = true;
            }
        }

        /// <summary>
        /// Creates the cluster's resource group if it doesn't already exist.  This sets
        /// <see cref="resourceGroup"/> field to the cluster resource group.
        /// </summary>
        private async Task GetClusterResourceGroup()
        {
            await SyncContext.Clear;

            if (resourceGroup != null)
            {
                return;
            }

            controller.SetGlobalStepStatus($"create: resource group: {resourceGroupName}");

            resourceGroup = (await subscription.GetResourceGroups().CreateOrUpdateAsync(WaitUntil.Completed, resourceGroupName, WithTags(new ResourceGroupData(azureLocation)))).Value;
        }

        /// <summary>
        /// Creates the proximity placement group when this feature is not disabled.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task CreateProximityPlacementGroupAsync()
        {
            await SyncContext.Clear;

            if (!azureOptions.DisableProximityPlacement)
            {
                var proximityPlacementGroupCollection = resourceGroup.GetProximityPlacementGroups();

                if (await proximityPlacementGroupCollection.ExistsAsync(proximityPlacementGroupName))
                {
                    proximityPlacementGroup = (await proximityPlacementGroupCollection.GetAsync(proximityPlacementGroupName)).Value;
                }
                else
                {
                    controller.SetGlobalStepStatus("create: proximity placement group");

                    proximityPlacementGroup = (await proximityPlacementGroupCollection.CreateOrUpdateAsync(WaitUntil.Completed, proximityPlacementGroupName, WithTags(new ProximityPlacementGroupData(azureLocation)))).Value;
                }
            }
        }

        /// <summary>
        /// Creates an availability set for the control-plane VMs and a separate one for the worker VMs
        /// as well as the cluster's proximity placement group.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task CreateAvailabilitySetsAsync()
        {
            await SyncContext.Clear;

            foreach (var azureVm in nodeNameToVm.Values)
            {
                azureVm.AvailabilitySetName = GetResourceName("avail", azureVm.Metadata.Labels.PhysicalAvailabilitySet);

                if (nameToAvailabilitySet.ContainsKey(azureVm.AvailabilitySetName))
                {
                    continue;   // The availability set already exists.
                }

                // Create the availability set.

                controller.SetGlobalStepStatus($"create: availability set: {azureVm.AvailabilitySetName}");

                var availabilitySetCollection = resourceGroup.GetAvailabilitySets();
                var availabilitySetData       =
                    new AvailabilitySetData(azureLocation)
                    {
                        Sku                       = new ComputeSku() { Name = "Aligned" },
                        PlatformUpdateDomainCount = azureOptions.UpdateDomains,
                        PlatformFaultDomainCount  = azureOptions.FaultDomains
                    };

                if (!azureOptions.DisableProximityPlacement)
                {
                    availabilitySetData.ProximityPlacementGroupId = proximityPlacementGroup.Id;
                }

                var availabilitySet = (await availabilitySetCollection.CreateOrUpdateAsync(WaitUntil.Completed, azureVm.AvailabilitySetName, WithTags(availabilitySetData))).Value;

                nameToAvailabilitySet.Add(azureVm.AvailabilitySetName, availabilitySet);
            }
        }

        /// <summary>
        /// Creates the public IP addresses and outbound NAT prefix as required.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task CreateAddressesAndPrefixAsync()
        {
            await SyncContext.Clear;

            var publicAddressCollection = resourceGroup.GetPublicIPAddresses();
            var publicPrefixCollection  = resourceGroup.GetPublicIPPrefixes();
            var resourceGroupCollection = subscription.GetResourceGroups();

            if (publicIngressAddress == null)
            {
                if (!string.IsNullOrEmpty(azureOptions.Network.IngressPublicIpAddressId))
                {
                    controller.SetGlobalStepStatus("attach: cluster ingress address");

                    ResourceIdentifier ingressPublicIpAddressId;

                    try
                    {
                        ingressPublicIpAddressId = new ResourceIdentifier(azureOptions.Network.IngressPublicIpAddressId);
                    }
                    catch (Exception e)
                    {
                        throw new NeonKubeException($"Cannot parse Azure resource ID [{nameof(azureOptions.Network.IngressPublicIpAddressId)}={azureOptions.Network.IngressPublicIpAddressId}]", e);
                    }

                    if (!await resourceGroupCollection.ExistsAsync(ingressPublicIpAddressId.ResourceGroupName))
                    {
                        throw new NeonKubeException($"Cannot locate resource group [{ingressPublicIpAddressId.ResourceGroupName}] specified by [{nameof(azureOptions.Network.IngressPublicIpAddressId)}={azureOptions.Network.IngressPublicIpAddressId}]");
                    }

                    var refResourceGroup           = (await resourceGroupCollection.GetAsync(ingressPublicIpAddressId.ResourceGroupName)).Value;
                    var refPublicAddressCollection = refResourceGroup.GetPublicIPAddresses();

                    if (!await refPublicAddressCollection.ExistsAsync(ingressPublicIpAddressId.Name))
                    {
                        throw new NeonKubeException($"Cannot locate public IP address [{ingressPublicIpAddressId.ResourceGroupName}/{ingressPublicIpAddressId.Name}] specified by [{nameof(azureOptions.Network.IngressPublicIpAddressId)}={azureOptions.Network.IngressPublicIpAddressId}]");
                    }

                    publicIngressAddress = (await refPublicAddressCollection.GetAsync(ingressPublicIpAddressId.Name)).Value;

                    if (publicIngressAddress.Data.Location != region)
                    {
                        throw new NeonKubeException($"Egress public IP address is located at [{publicIngressAddress.Data.Location}] instead of the cluster region [{region}]: [{nameof(azureOptions.Network.IngressPublicIpAddressId)}={azureOptions.Network.IngressPublicIpAddressId}]");
                    }
                }
                else
                {
                    controller.SetGlobalStepStatus("create: cluster ingress address");

                    var ingressAddressData = new PublicIPAddressData()
                    {
                        Location                 = azureLocation,
                        DnsSettings              = new PublicIPAddressDnsSettings() { DomainNameLabel = azureOptions.DomainLabel },
                        PublicIPAllocationMethod = NetworkIPAllocationMethod.Static,
                        Sku                      = new PublicIPAddressSku() { Name = PublicIPAddressSkuName.Standard, Tier = PublicIPAddressSkuTier.Regional },
                    };

                    publicIngressAddress = (await publicAddressCollection.CreateOrUpdateAsync(WaitUntil.Completed, publicIngressAddressName, WithNetworkTags(ingressAddressData))).Value;
                    clusterAddress       = NetHelper.ParseIPv4Address(publicIngressAddress.Data.IPAddress);

                    cluster.SetupState.PublicAddresses = new List<string>() { publicIngressAddress.Data.IPAddress };
                }
            }

            // We'll favor an existing public egress prefix or address, otherwise
            // we'll create a prefix if a prefix length was specified and if none
            // of those apply, we'll create an public IP.

            if (!string.IsNullOrEmpty(azureOptions.Network.EgressPublicIpPrefixId))
            {
                controller.SetGlobalStepStatus("attach: cluster egress prefix");

                ResourceIdentifier egressPublicIpPrefixId;

                try
                {
                    egressPublicIpPrefixId = new ResourceIdentifier(azureOptions.Network.EgressPublicIpPrefixId);
                }
                catch (Exception e)
                {
                    throw new NeonKubeException($"Cannot parse Azure resource ID [{nameof(azureOptions.Network.EgressPublicIpPrefixId)}={azureOptions.Network.EgressPublicIpPrefixId}]", e);
                }

                if (!await resourceGroupCollection.ExistsAsync(egressPublicIpPrefixId.ResourceGroupName))
                {
                    throw new NeonKubeException($"Cannot locate resource group [{egressPublicIpPrefixId.ResourceGroupName}] specified by [{nameof(azureOptions.Network.EgressPublicIpPrefixId)}={azureOptions.Network.EgressPublicIpPrefixId}]");
                }

                var refResourceGroup          = (await resourceGroupCollection.GetAsync(egressPublicIpPrefixId.ResourceGroupName)).Value;
                var refPublicPrefixCollection = refResourceGroup.GetPublicIPPrefixes();

                if (!await refPublicPrefixCollection.ExistsAsync(egressPublicIpPrefixId.Name))
                {
                    throw new NeonKubeException($"Cannot locate public prefix [{egressPublicIpPrefixId.ResourceGroupName}/{egressPublicIpPrefixId.Name}] specified by [{nameof(azureOptions.Network.EgressPublicIpPrefixId)}={azureOptions.Network.EgressPublicIpPrefixId}]");
                }

                publicEgressPrefix = (await refPublicPrefixCollection.GetAsync(egressPublicIpPrefixId.Name)).Value;

                if (publicEgressPrefix.Data.Location != region)
                {
                    throw new NeonKubeException($"Egress public IP prefix is located at [{publicEgressPrefix.Data.Location}] instead of the cluster region [{region}]: [{nameof(azureOptions.Network.EgressPublicIpPrefixId)}={azureOptions.Network.EgressPublicIpPrefixId}]");
                }
            }
            else if (!string.IsNullOrEmpty(azureOptions.Network.EgressPublicIpAddressId))
            {
                controller.SetGlobalStepStatus("attach: cluster egress address");

                ResourceIdentifier egressPublicIpAddressId;

                try
                {
                    egressPublicIpAddressId = new ResourceIdentifier(azureOptions.Network.EgressPublicIpAddressId);
                }
                catch (Exception e)
                {
                    throw new NeonKubeException($"Cannot parse Azure resource ID [{nameof(azureOptions.Network.EgressPublicIpAddressId)}={azureOptions.Network.EgressPublicIpAddressId}]", e);
                }

                if (!await resourceGroupCollection.ExistsAsync(egressPublicIpAddressId.ResourceGroupName))
                {
                    throw new NeonKubeException($"Cannot locate resource group [{egressPublicIpAddressId.ResourceGroupName}] specified by [{nameof(azureOptions.Network.EgressPublicIpAddressId)}={azureOptions.Network.EgressPublicIpAddressId}]");
                }

                var refResourceGroup           = (await resourceGroupCollection.GetAsync(egressPublicIpAddressId.ResourceGroupName)).Value;
                var refPublicAddressCollection = refResourceGroup.GetPublicIPAddresses();

                if (!await refPublicAddressCollection.ExistsAsync(egressPublicIpAddressId.Name))
                {
                    throw new NeonKubeException($"Cannot locate public IP address [{egressPublicIpAddressId.ResourceGroupName}/{egressPublicIpAddressId.Name}] specified by [{nameof(azureOptions.Network.EgressPublicIpAddressId)}={azureOptions.Network.EgressPublicIpAddressId}]");
                }

                publicEgressAddress = (await refPublicAddressCollection.GetAsync(egressPublicIpAddressId.Name)).Value;

                if (publicEgressAddress.Data.Location != region)
                {
                    throw new NeonKubeException($"Egress public IP address is located at [{publicEgressAddress.Data.Location}] instead of the cluster region [{region}]: [{nameof(azureOptions.Network.EgressPublicIpAddressId)}={azureOptions.Network.EgressPublicIpPrefixId}]");
                }
            }
            else if (azureOptions.Network.EgressPublicIpPrefixLength > 0)
            {
                controller.SetGlobalStepStatus("create: cluster egress prefix");

                var publicIpPrefixData = new PublicIPPrefixData()
                {
                    Location               = azureLocation,
                    PrefixLength           = azureOptions.Network.EgressPublicIpPrefixLength,
                    Sku                    = new PublicIPPrefixSku() { Name = PublicIPPrefixSkuName.Standard, Tier = PublicIPPrefixSkuTier.Regional },
                    PublicIPAddressVersion = global::Azure.ResourceManager.Network.Models.NetworkIPVersion.IPv4
                };

                publicEgressPrefix = (await publicPrefixCollection.CreateOrUpdateAsync(WaitUntil.Completed, publicEgressPrefixName, WithNetworkTags(publicIpPrefixData))).Value;
            }
            else
            {
                controller.SetGlobalStepStatus("create: cluster egress address");

                var publicIpAddressData = new PublicIPAddressData()
                {
                    Location                 = azureLocation,
                    DnsSettings              = new PublicIPAddressDnsSettings() { DomainNameLabel = azureOptions.DomainLabel + "-egress" },
                    PublicIPAllocationMethod = NetworkIPAllocationMethod.Static,
                    Sku                      = new PublicIPAddressSku() { Name = PublicIPAddressSkuName.Standard, Tier = PublicIPAddressSkuTier.Regional }
                };

                publicEgressAddress = (await publicAddressCollection.CreateOrUpdateAsync(WaitUntil.Completed, publicEgressAddressName, publicIpAddressData)).Value;
            }
        }

        /// <summary>
        /// Creates the network security group.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task CreateNetworkSecurityGroupAsync()
        {
            await SyncContext.Clear;

            if (subnetNsg == null)
            {
                controller.SetGlobalStepStatus("create: network security group");

                // Note that we're going to add the actual security rules later.

                var nsgCollection = resourceGroup.GetNetworkSecurityGroups();
                var nsgData       = new NetworkSecurityGroupData()
                {
                    Location = azureLocation
                };

                subnetNsg = (await nsgCollection.CreateOrUpdateAsync(WaitUntil.Completed, subnetNsgName, WithNetworkTags(nsgData))).Value;
            }
        }

        /// <summary>
        /// Creates the cluster's outbound NAT gateway.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task CreateNatGatewayAsync()
        {
            await SyncContext.Clear;

            var natGatewayCollection = resourceGroup.GetNatGateways();

            if (natGateway == null)
            {
                controller.SetGlobalStepStatus("create: NAT gateway");

                var natGatewayData = new NatGatewayData()
                {
                    Location             = azureLocation,
                    SkuName              = NatGatewaySkuName.Standard,
                    IdleTimeoutInMinutes = azureOptions.Network.MaxNatGatewayTcpIdle
                };

                // Attach the public egress IP address or prefix.

                if (publicEgressPrefix != null)
                {
                    natGatewayData.PublicIPPrefixes.Add(new WritableSubResource() { Id = publicEgressPrefix.Id });
                }
                else if (publicEgressAddress != null)
                {
                    natGatewayData.PublicIPAddresses.Add(new WritableSubResource() { Id = publicEgressAddress.Id });
                }
                else
                {
                    Covenant.Assert(false, "Expected a public IP address or prefix.");
                }

                natGateway = (await natGatewayCollection.CreateOrUpdateAsync(WaitUntil.Completed, natGatewayName, WithNetworkTags(natGatewayData))).Value;
            }
        }

        /// <summary>
        /// Creates the cluster's virtual network.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task CreateVirtualNetworkAsync()
        {
            await SyncContext.Clear;

            if (vnet == null)
            {
                controller.SetGlobalStepStatus("create: vnet");

                var networkCollection = resourceGroup.GetVirtualNetworks();
                var networkData       = new VirtualNetworkData() { Location = azureLocation };

                networkData.AddressPrefixes.Add(azureOptions.Network.VnetSubnet);
                networkData.Subnets.Add(
                    new SubnetData()
                    {
                        Name                 = subnetName,
                        AddressPrefix        = azureOptions.Network.NodeSubnet,
                        NatGatewayId         = natGateway.Id,
                        NetworkSecurityGroup = new NetworkSecurityGroupData() { Id = subnetNsg.Id }
                    });

                var nameservers = cluster.SetupState.ClusterDefinition.Network.Nameservers;

                if (nameservers != null)
                {
                    foreach (var nameserver in nameservers)
                    {
                        networkData.DhcpOptionsDnsServers.Add(nameserver);
                    }
                }

                vnet   = (await networkCollection.CreateOrUpdateAsync(WaitUntil.Completed, vnetName, WithNetworkTags(networkData))).Value;
                subnet = vnet.Data.Subnets.First();

                // Reload to pick up any changes.

                var natGatewayCollection = resourceGroup.GetNatGateways();

                natGateway = (await natGatewayCollection.GetAsync(natGatewayName)).Value;
            }
        }

        /// <summary>
        /// Assigns external SSH ports to Azure VM records that don't already have one and updates
        /// the cluster node tags to identify the cluster's public IP and assigned SSH port.  Note
        /// that we're not actually going to write the virtual machine tags here; we'll do that
        /// when we actually create any new VMs.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        private void ConfigureNodeSsh(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            // Create a table with the currently allocated external SSH ports.

            var allocatedPorts = new HashSet<int>();

            foreach (var azureVm in nodeNameToVm.Values.Where(azureVm => azureVm.ExternalSshPort != 0))
            {
                allocatedPorts.Add(azureVm.ExternalSshPort);
            }

            // Create a list of unallocated external SSH ports.

            var unallocatedPorts = new List<int>();

            for (int port = networkOptions.FirstExternalSshPort; port <= networkOptions.LastExternalSshPort; port++)
            {
                if (!allocatedPorts.Contains(port))
                {
                    unallocatedPorts.Add(port);
                }
            }

            // Assign unallocated external SSH ports to nodes that don't already have one.

            var nextUnallocatedPortIndex = 0;

            foreach (var azureVm in SortedControlThenWorkerNodes.Where(vm => vm.ExternalSshPort == 0))
            {
                azureVm.ExternalSshPort = unallocatedPorts[nextUnallocatedPortIndex++];
            }

            // The cluster node proxies were created before we made the external SSH port
            // assignments above or obtained the ingress elastic IP for the load balancer,
            // so the node proxies will be configured with the internal node IP addresses
            // and the standard SSH port 22.
            //
            // These endpoints won't work from outside of the VPC, so we'll need to update
            // the node proxies with the cluster's load balancer address and the unique
            // SSH port assigned to each node.
            //
            // It would have been nicer to construct the node proxies with the correct
            // endpoint but we have a bit of a chicken-and-egg problem so this seems
            // to be the easiest approach.

            Covenant.Assert(publicIngressAddress != null);

            foreach (var node in cluster.Nodes)
            {
                var azureVm = nodeNameToVm[node.Name];

                node.Address = IPAddress.Parse(publicIngressAddress.Data.IPAddress);
                node.SshPort = azureVm.ExternalSshPort;
            }
        }

        /// <summary>
        /// Create the cluster's external load balancer.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task CreateLoadBalancerAsync()
        {
            await SyncContext.Clear;

            if (loadBalancer == null)
            {
                controller.SetGlobalStepStatus("create: load balancer");

                var loadBalancerCollection = resourceGroup.GetLoadBalancers();
                var loadBalancerData       = new LoadBalancerData()
                {
                     Location = azureLocation,
                     Sku      = new LoadBalancerSku() { Name = "Standard", Tier = "Regional" }
                };

                loadBalancerData.FrontendIPConfigurations.Add(
                    new FrontendIPConfigurationData()
                    {
                        Name            = loadbalancerFrontendName,
                        PublicIPAddress = publicIngressAddress.Data,
                    });

                loadBalancerData.BackendAddressPools.Add(
                    new BackendAddressPoolData()
                    {
                         Name = loadbalancerControlPlaneBackendName
                    });

                loadBalancerData.BackendAddressPools.Add(
                    new BackendAddressPoolData()
                    {
                        Name = loadbalancerIngressBackendName
                    });

                loadBalancer = (await loadBalancerCollection.CreateOrUpdateAsync(WaitUntil.Completed, loadbalancerName, WithNetworkTags(loadBalancerData))).Value;
            }
        }

        /// <summary>
        /// Creates the NIC and VM for a cluster node.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="node">The target node.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task CreateVmAsync(ISetupController controller, NodeSshProxy<NodeDefinition> node)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            await LoadVmSizeMetadataAsync();

            var azureVm = nodeNameToVm[node.Name];

            if (azureVm.Vm != null)
            {
                // The VM already exists.

                return;
            }

            if (!nodeNameToVmSku.TryGetValue(azureVm.VmSize, out var vmSku))
            {
                throw new NeonKubeException($"VM size [{azureVm.VmSize}] is not available at [{region}].");
            }

            node.Status = "create: NIC";

            var nicCollection = resourceGroup.GetNetworkInterfaces();
            var nicData       = new NetworkInterfaceData()
            {
                Location                    = azureLocation,
                NicType                     = NetworkInterfaceNicType.Standard,
                NetworkSecurityGroup        = subnetNsg.Data,
                EnableAcceleratedNetworking = vmSku.AcceleratedNetworking
            };

            nicData.IPConfigurations.Add(
                new NetworkInterfaceIPConfigurationData()
                {
                    Name                      = primaryNicName,
                    Primary                   = true,
                    Subnet                    = subnet,
                    PrivateIPAddress          = azureVm.Address,
                    PrivateIPAllocationMethod = NetworkIPAllocationMethod.Static
                });

            azureVm.Nic = (await nicCollection.CreateOrUpdateAsync(WaitUntil.Completed, GetResourceName("nic", azureVm.NodeProxy.Name), WithNetworkTags(nicData))).Value;

            node.Status = "create: virtual machine";

            var azureNodeOptions     = azureVm.NodeProxy.Metadata.Azure;
            var azureOSStorageType   = ToAzureStorageType(azureNodeOptions.StorageType, osDisk: true);
            var azureDataStorageType = ToAzureStorageType(azureNodeOptions.StorageType);
            var diskSize             = ByteUnits.Parse(node.Metadata.Azure.DiskSize);

            //-----------------------------------------------------------------
            // We need deploy a script that runs when the VM boots to: 
            //
            //      1. Install unzip ([LinuxSshProxy] requires this)
            //      2. Enable SSH password authentication (with known good SSH config)
            //      3. Set the secure password for [sysadmin]

            var bootScript =
$@"#cloud-boothook
#!/bin/bash

# To enable logging for this Azure custom-data script, add ""-ex"" to the SHABANG above.
# the SHEBANG above and uncomment the [exec] command below.  Then each command and its
# output to be logged and can be viewable in the Azure portal.
#
# WARNING: Do not leave the ""-ex"" SHABANG option in production builds to avoid 
# leaking the secure SSH password to any logs!
#          
# exec &> >(tee /var/log/user-data.log|logger -t user-data -s 2>/dev/console) 2>&1

#------------------------------------------------------------------------------
# Write a file indicating that this script was executed (for debugging).

mkdir -p /etc/neonkube/cloud-init
echo $0 > /etc/neonkube/cloud-init/node-init
date >> /etc/neonkube/cloud-init/node-init
chmod 644 /etc/neonkube/cloud-init/node-init

# Write this script's path to a file so that cluster setup can remove it.
# This is important because we don't want to expose the SSH password we
# set below.

echo $0 > /etc/neonkube/cloud-init/boot-script-path
chmod 600 /etc/neonkube/cloud-init/boot-script-path

#------------------------------------------------------------------------------
# Update the [sysadmin] user password:

echo 'sysadmin:{cluster.SetupState.SshPassword}' | chpasswd

#------------------------------------------------------------------------------
# Enable the [sysadmin] SSH public key:

echo '{cluster.SetupState.SshKey.PublicPUB}' > /home/sysadmin/.ssh/authorized_keys
";
            var encodedBootScript        = Convert.ToBase64String(Encoding.UTF8.GetBytes(NeonHelper.ToLinuxLineEndings(bootScript)));
            var virtualMachineCollection = resourceGroup.GetVirtualMachines();
            var virtualMachineData       = new VirtualMachineData(azureLocation)
            {
                HardwareProfile = new VirtualMachineHardwareProfile()
                {
                    VmSize = azureVm.VmSize
                },
                OSProfile = new VirtualMachineOSProfile()
                {
                    ComputerName  = "ubuntu",
                    AdminUsername = KubeConst.SysAdminUser,
                    AdminPassword = cluster.SetupState.SshPassword
                },
                NetworkProfile    = new VirtualMachineNetworkProfile(),
                StorageProfile    = new VirtualMachineStorageProfile(),
                AvailabilitySetId = nameToAvailabilitySet[azureVm.AvailabilitySetName].Id,
                UserData          = encodedBootScript
            };

            virtualMachineData.NetworkProfile.NetworkInterfaces.Add(new VirtualMachineNetworkInterfaceReference() { Id = azureVm.Nic.Id });

            if (proximityPlacementGroup != null)
            {
                virtualMachineData.ProximityPlacementGroupId = proximityPlacementGroup.Id;
            }

            virtualMachineData.StorageProfile.ImageReference = nodeImageRef;
            virtualMachineData.Plan                          = nodeImagePlan;

            virtualMachineData.StorageProfile.OSDisk = new VirtualMachineOSDisk(DiskCreateOptionType.FromImage)
            {
                ManagedDisk = new VirtualMachineManagedDisk()
                {
                    StorageAccountType = azureOSStorageType
                },
                OSType       = SupportedOperatingSystemType.Linux,
                DiskSizeGB   = (int)AzureHelper.GetDiskSizeGiB(azureNodeOptions.StorageType, diskSize),
                DeleteOption = DiskDeleteOptionType.Delete,
                Caching      = CachingType.None
            };

            var nodeTags = new ResourceTag[]
            {
                new ResourceTag(neonNodeNameTagKey, node.Name),
                new ResourceTag(neonNodeRoleTagKey, node.Metadata.Role),
                new ResourceTag(neonNodeSshPortTagKey, azureVm.ExternalSshPort.ToString()),
            };

            azureVm.Vm = (await virtualMachineCollection.CreateOrUpdateAsync(WaitUntil.Completed, azureVm.VmName, WithTags(virtualMachineData, nodeTags))).Value;
        }

        /// <summary>
        /// Updates the load balancer and related security rules based on the operation flags passed.
        /// </summary>
        /// <param name="operations">Flags that control how the load balancer and related security rules are updated.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task UpdateNetworkAsync(NetworkOperations operations)
        {
            await SyncContext.Clear;

            if ((operations & NetworkOperations.InternetRouting) != 0)
            {
                controller.SetGlobalStepStatus("update: load balancer ingress/egress rules");
                await UpdateLoadBalancerAsync();
            }

            if ((operations & NetworkOperations.AllowNodeSsh) != 0)
            {
                controller.SetGlobalStepStatus("add ssh rules");
                await AddSshRulesAsync();
            }

            if ((operations & NetworkOperations.DenyNodeSsh) != 0)
            {
                controller.SetGlobalStepStatus("remove ssh rules");
                await RemoveSshRulesAsync();
            }
        }

        /// <summary>
        /// Returns the ID for a load balancer child resource.
        /// </summary>
        /// <param name="childResourceType">Specifies the child resource type.</param>
        /// <param name="childResourceName">Specifies the child resource name.</param>
        /// <returns>The child resource ID.</returns>
        private ResourceIdentifier GetChildLoadBalancerResourceId(string childResourceType, string childResourceName)
        {
            return new ResourceIdentifier($"/subscriptions/{subscription.Id}/resourceGroups/{resourceGroupName}/providers/Microsoft.Network/loadBalancers/{loadbalancerName}/{childResourceType}/{childResourceName}");
        }

        /// <summary>
        /// <para>
        /// Updates the load balancer and network security rules to match the current cluster definition.
        /// This also ensures that some nodes are marked for ingress when the cluster has one or more
        /// ingress rules and that nodes marked for ingress are in the load balancer's backend pool.
        /// </para>
        /// <node>
        /// This method <b>does not change the SSH inbound NAT rules in any way.</b>
        /// </node>
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task UpdateLoadBalancerAsync()
        {
            await SyncContext.Clear;
            Covenant.Assert(loadBalancer != null, "LoadBalancer is not loaded.");

            var networkSecurityGroupCollection = resourceGroup.GetNetworkSecurityGroups();
            var loadBalancerCollection         = resourceGroup.GetLoadBalancers();
            var frontendCollection             = loadBalancer.GetFrontendIPConfigurations();
            var backendCollection              = loadBalancer.GetBackendAddressPools();
            var probeCollection                = loadBalancer.GetProbes();

            // Ensure that we actually have some nodes labeled for ingress when the cluster
            // defines some ingress rules and then ensure that the load balancer's backend
            // pool includes those node VMs.

            KubeHelper.EnsureIngressNodes(cluster.SetupState.ClusterDefinition);

            //-----------------------------------------------------------------
            // Backend pools:
            //
            // Add the virtual machine NICs to the backend pools as required.

            var nicCollection          = resourceGroup.GetNetworkInterfaces();
            var controlBackendPoolData = loadBalancer.Data.BackendAddressPools.Single(pool => pool.Name.Equals(loadbalancerControlPlaneBackendName, StringComparison.InvariantCultureIgnoreCase));
            var ingressBackendPoolData = loadBalancer.Data.BackendAddressPools.Single(pool => pool.Name.Equals(loadbalancerIngressBackendName, StringComparison.InvariantCultureIgnoreCase));

            await Parallel.ForEachAsync(nodeNameToVm.Values, parallelOptions,
                async (azureVm, cancellationToken) =>
                {
                    var ipConfiguration        = azureVm.Nic.Data.IPConfigurations.First();
                    var nicBackendAddressPools = ipConfiguration.LoadBalancerBackendAddressPools;
                    var changed                = false;

                    if (azureVm.IsControlPlane && !nicBackendAddressPools.Any(pool => pool.Id == controlBackendPoolData.Id))
                    {
                        changed = true;

                        nicBackendAddressPools.Add(
                            new BackendAddressPoolData()
                            {
                                Id = controlBackendPoolData.Id
                            });
                    }

                    if (azureVm.Metadata.Ingress && !nicBackendAddressPools.Any(pool => pool.Id == ingressBackendPoolData.Id))
                    {
                        changed = true;

                        nicBackendAddressPools.Add(
                            new BackendAddressPoolData()
                            {
                                Id = ingressBackendPoolData.Id
                            });
                    }

                    if (changed)
                    {
                        azureVm.Nic = (await nicCollection.CreateOrUpdateAsync(WaitUntil.Completed, azureVm.Nic.Data.Name, azureVm.Nic.Data)).Value;
                    }
                });

            // Reload the load balancer to pick up any changes.

            loadBalancer = await loadBalancer.GetAsync();

            //-----------------------------------------------------------------
            // Add the load balancer ingress rules and health probes.

            // We need to add a special ingress rule for the Kubernetes API on its standard
            // port 6443 and load balance this traffic to the control-plane nodes.

            var clusterRules = new IngressRule[]
            {
                new IngressRule()
                {
                    Name                  = "kubernetes-api",
                    Protocol              = IngressProtocol.Tcp,
                    ExternalPort          = NetworkPorts.KubernetesApiServer,
                    NodePort              = NetworkPorts.KubernetesApiServer,
                    Target                = IngressRuleTarget.ControlPlane,
                    AddressRules          = networkOptions.ManagementAddressRules,
                    IdleTcpReset          = true,
                    TcpIdleTimeoutMinutes = IngressRule.DefaultTcpIdleTimeoutMinutes
                }
            };

            var ingressRules       = networkOptions.IngressRules.Union(clusterRules).ToArray();
            var defaultHealthCheck = networkOptions.IngressHealthCheck ?? new HealthCheckOptions();

            // Create a probe for each ingress rule.

            loadBalancer.Data.Probes.Clear();

            foreach (var ingressRule in ingressRules)
            {
                var probeName   = $"{ingressRulePrefix}{ingressRule.Name}";
                var healthCheck = ingressRule.IngressHealthCheck ?? defaultHealthCheck;

                if (!loadBalancer.Data.Probes.Any(probe => probe.Name.Equals(probeName, StringComparison.InvariantCultureIgnoreCase)))
                {
                    loadBalancer.Data.Probes.Add(
                        new ProbeData()
                        {
                            Name              = probeName,
                            Protocol          = ProbeProtocol.Tcp,
                            Port              = ingressRule.NodePort,
                            IntervalInSeconds = healthCheck.IntervalSeconds,
                            NumberOfProbes    = healthCheck.ThresholdCount
                        });
                }
            }

            // We need to update the load balancer so we can obtain the IDs for the frontend, the
            // backend pools, as well as the health probes for each ingress rule.

            var nameToFrontEndId    = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            var nameToBackEndPoolId = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            var nameToProbeId       = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

            loadBalancer = (await loadBalancerCollection.CreateOrUpdateAsync(WaitUntil.Completed, loadbalancerName, loadBalancer.Data)).Value;

            foreach (var frontEnd in loadBalancer.Data.FrontendIPConfigurations)
            {
                nameToFrontEndId.Add(frontEnd.Name, frontEnd.Id);
            }

            foreach (var backEndPool in loadBalancer.Data.BackendAddressPools)
            {
                nameToBackEndPoolId.Add(backEndPool.Name, backEndPool.Id);
            }

            foreach (var probe in loadBalancer.Data.Probes)
            {
                nameToProbeId.Add(probe.Name, probe.Id);
            }

            foreach (var ingressRule in ingressRules)
            {
                var probeName      = $"{ingressRulePrefix}{ingressRule.Name}";
                var ruleName       = $"{ingressRulePrefix}{ingressRule.Name}";
                var tcpIdleTimeout = Math.Min(Math.Max(minAzureTcpIdleTimeoutMinutes, ingressRule.TcpIdleTimeoutMinutes), maxAzureTcpIdleTimeoutMinutes);
                var backendPoolId  = (string)null;

                switch (ingressRule.Target)
                {
                    case IngressRuleTarget.Ingress:

                        backendPoolId = nameToBackEndPoolId[loadbalancerIngressBackendName];
                        break;

                    case IngressRuleTarget.ControlPlane:

                        backendPoolId = nameToBackEndPoolId[loadbalancerControlPlaneBackendName];
                        break;

                    default:

                        throw new NotImplementedException();
                }

                if (!loadBalancer.Data.LoadBalancingRules.Any(rule => rule.Name.Equals(ruleName, StringComparison.InvariantCultureIgnoreCase)))
                {
                    loadBalancer.Data.LoadBalancingRules.Add(
                        new LoadBalancingRuleData()
                        {
                            Name                      = ruleName,
                            FrontendIPConfigurationId = new ResourceIdentifier(nameToFrontEndId.Values.First()),
                            BackendAddressPoolId      = new ResourceIdentifier(backendPoolId),
                            ProbeId                   = new ResourceIdentifier(nameToProbeId[probeName]),
                            Protocol                  = ToTransportProtocol(ingressRule.Protocol),
                            FrontendPort              = ingressRule.ExternalPort,
                            BackendPort               = ingressRule.NodePort,
                            EnableTcpReset            = ingressRule.IdleTcpReset,
                            IdleTimeoutInMinutes      = tcpIdleTimeout,
                            EnableFloatingIP          = false
                        });
                }
            }

            // Add the NSG rules corresponding to the ingress rules from the cluster definition.
            //
            // To keep things simple, we're going to generate a separate rule for each source address
            // restriction.  In theory, we could have tried collecting allow and deny rules 
            // together to reduce the number of rules but that doesn't seem worth the trouble. 
            // This is possible because NSG rules allow a comma separated list of IP addresses
            // or subnets to be specified.
            //
            // We may need to revisit this if we approach Azure rule count limits (currently 1000
            // rules per NSG).  That would also be a good time to support port ranges as well.

            var subnetNsgData = new NetworkSecurityGroupData() { Location = azureLocation };
            var priority      = firstIngressNsgRulePriority;

            foreach (var ingressRule in ingressRules)
            {
                var ruleProtocol = ToSecurityRuleProtocol(ingressRule.Protocol);

                if (ingressRule.AddressRules == null || ingressRule.AddressRules.Count == 0)
                {
                    // Default to allowing all addresses when no address rules are specified.

                    var ruleName = $"{ingressRulePrefix}{ingressRule.Name}";

                    if (!subnetNsgData.SecurityRules.Any(rule => rule.Name.Equals(ruleName, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        subnetNsgData.SecurityRules.Add(
                            new SecurityRuleData()
                            {
                                Name                     = ruleName,
                                Access                   = SecurityRuleAccess.Allow,
                                Direction                = SecurityRuleDirection.Inbound,
                                SourceAddressPrefix      = "0.0.0.0/0",
                                SourcePortRange          = "*",
                                DestinationAddressPrefix = subnet.AddressPrefix,
                                DestinationPortRange     = ingressRule.NodePort.ToString(),
                                Protocol                 = ruleProtocol,
                                Priority                 = priority++
                            });
                    }
                }
                else
                {
                    // We need to generate a separate NSG rule for each address rule.  We're going to 
                    // include an address rule index in the name when there's more than one address
                    // rule to ensure that rule names are unique.

                    var addressRuleIndex = 0;

                    foreach (var addressRule in ingressRule.AddressRules)
                    {
                        var multipleAddresses = ingressRule.AddressRules.Count > 1;
                        var ruleName          = multipleAddresses ? $"{ingressRulePrefix}{ingressRule.Name}-{addressRuleIndex++}" : $"{ingressRulePrefix}{ingressRule.Name}";

                        if (!subnetNsgData.SecurityRules.Any(rule => rule.Name.Equals(ruleName, StringComparison.InvariantCultureIgnoreCase)))
                        {
                            subnetNsgData.SecurityRules.Add(
                                new SecurityRuleData() 
                                { 
                                    Name                     = ruleName, 
                                    Direction                = SecurityRuleDirection.Inbound,
                                    Access                   = addressRule.Action == AddressRuleAction.Allow ? SecurityRuleAccess.Allow : SecurityRuleAccess.Deny,
                                    SourceAddressPrefix      = addressRule.IsAny ? "0.0.0.0/0" : addressRule.AddressOrSubnet,
                                    SourcePortRange          = "*",
                                    DestinationAddressPrefix = subnet.AddressPrefix,
                                    DestinationPortRange     = ingressRule.NodePort.ToString(),
                                    Protocol                 = ruleProtocol,
                                    Priority                 = priority++ 
                                });
                        }
                    }
                }
            }

            // Update the load balancer and subnet security group.

            subnetNsg    = (await networkSecurityGroupCollection.CreateOrUpdateAsync(WaitUntil.Completed, subnetNsgName, WithNetworkTags(subnetNsgData))).Value;
            loadBalancer = (await loadBalancerCollection.CreateOrUpdateAsync(WaitUntil.Completed, loadbalancerName, WithNetworkTags(loadBalancer.Data))).Value;
        }

        /// <summary>
        /// Adds public SSH NAT and security rules for every node in the cluster.
        /// These are used by NEONKUBE tools for provisioning, setting up, and
        /// managing cluster nodes.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task AddSshRulesAsync()
        {
            await SyncContext.Clear;

            var loadBalancerCollection         = resourceGroup.GetLoadBalancers();
            var nicCollection                  = resourceGroup.GetNetworkInterfaces();
            var networkSecurityGroupCollection = resourceGroup.GetNetworkSecurityGroups();

            // Add SSH NAT rules for each node.  We need to do this in two steps:
            //
            //      1. Create the inbound NAT rules on the load balancer (if they don't already exist)
            //
            //      2. Iterate through the virtual machine NICs and add the associated rule ID to
            //         each NIC's frontend configuration.

            // STEP 1: Create the inbound NAT rules on the load balancer (if they don't already exist)

            foreach (var azureVm in nodeNameToVm.Values)
            {
                var ruleName = $"{publicSshRulePrefix}{azureVm.Name}";

                // Add the SSH NAT rule if it doesn't already exist.

                if (!loadBalancer.Data.InboundNatRules.Any(rule => rule.Name.Equals(ruleName, StringComparison.InvariantCultureIgnoreCase)))
                {
                    loadBalancer.Data.InboundNatRules.Add(
                        new InboundNatRuleData()
                        {
                            Name                      = ruleName,
                            FrontendIPConfigurationId = new ResourceIdentifier(loadBalancer.Data.FrontendIPConfigurations.Single().Id),
                            Protocol                  = LoadBalancingTransportProtocol.Tcp,
                            FrontendPort              = azureVm.ExternalSshPort,
                            BackendPort               = NetworkPorts.SSH,
                            EnableTcpReset            = true,
                            IdleTimeoutInMinutes      = maxAzureTcpIdleTimeoutMinutes,
                            EnableFloatingIP          = false
                        });
                }
            }

            loadBalancer = (await loadBalancerCollection.CreateOrUpdateAsync(WaitUntil.Completed, loadbalancerName, loadBalancer.Data)).Value;

            // STEP 2: Add each inbound NAT rule to the associated VM NIC's IP configuration.

            await Parallel.ForEachAsync(nodeNameToVm.Values, parallelOptions,
                async (azureVm, cancellationToken) =>
                {
                    var vmIpConfiguration = azureVm.Nic.Data.IPConfigurations.First();
                    var ruleName = $"{publicSshRulePrefix}{azureVm.Name}";
                    var rule = loadBalancer.Data.InboundNatRules.SingleOrDefault(rule => rule.Name.Equals(ruleName, StringComparison.CurrentCultureIgnoreCase));

                    if (rule != null && !vmIpConfiguration.LoadBalancerInboundNatRules
                        .Any(rule =>
                        {
                            // $note(jefflill):
                            //
                            // Only the [rule.Id] property is set here so we need to extract the
                            // rule name from the ID.

                            var ruleId = new ResourceIdentifier(rule.Id);

                            return ruleId.Name.Equals(ruleName, StringComparison.InvariantCultureIgnoreCase);
                        }))
                    {
                        vmIpConfiguration.LoadBalancerInboundNatRules.Add(rule);

                        azureVm.Nic = (await nicCollection.CreateOrUpdateAsync(WaitUntil.Completed, azureVm.Nic.Data.Name, azureVm.Nic.Data)).Value;
                    }
                });

            loadBalancer = await loadBalancer.GetAsync();   // Fetch any NAT rule updates.

            // Add NSG rules so that the public Kubernetes API and SSH NAT rules can actually route
            // traffic to the nodes.
            //
            // To keep things simple, we're going to generate a separate rule for each source
            // address restriction.  In theory, we could have tried collecting allow and deny rules 
            // together to reduce the number of rules but that doesn't seem worth the trouble. 
            // This would be possible because NSG rules allow a comma separated list of IP addresses
            // or subnets to be specified.

            var priority  = firstSshNsgRulePriority;
            var ruleIndex = 0;

            if (networkOptions.ManagementAddressRules == null || networkOptions.ManagementAddressRules.Count == 0)
            {
                // Default to allowing all source addresses when no address rules are specified.

                var ruleName = $"{publicSshRulePrefix}{ruleIndex++}";

                if (!subnetNsg.Data.SecurityRules.Any(rule => rule.Name.Equals(ruleName, StringComparison.InvariantCultureIgnoreCase)))
                {
                    subnetNsg.Data.SecurityRules.Add(
                        new SecurityRuleData()
                        {
                            Name                     = ruleName,
                            Access                   = SecurityRuleAccess.Allow,
                            Direction                = SecurityRuleDirection.Inbound,
                            SourceAddressPrefix      = "0.0.0.0/0",
                            SourcePortRange          = "*",
                            DestinationAddressPrefix = subnet.AddressPrefix,
                            DestinationPortRange     = "*",
                            Protocol                 = SecurityRuleProtocol.Tcp,
                            Priority                 = priority++
                        });
                }
            }
            else
            {
                // We need to generate a separate NSG rule for each source address rule.  We're going
                // to include an address rule index in the name when there's more than one address
                // rule to ensure that rule names are unique.

                var addressRuleIndex = 0;

                foreach (var addressRule in networkOptions.ManagementAddressRules)
                {
                    var multipleAddresses = networkOptions.ManagementAddressRules.Count > 1;
                    var ruleName          = multipleAddresses ? $"{publicSshRulePrefix}{ruleIndex++}-{addressRuleIndex++}" : $"{publicSshRulePrefix}{ruleIndex++}";

                    if (!subnetNsg.Data.SecurityRules.Any(rule => rule.Name.Equals(ruleName, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        subnetNsg.Data.SecurityRules.Add(
                            new SecurityRuleData()
                            {
                                Name                     = ruleName,
                                Access                   = addressRule.Action == AddressRuleAction.Allow ? SecurityRuleAccess.Allow : SecurityRuleAccess.Deny,
                                Direction                = SecurityRuleDirection.Inbound,
                                SourceAddressPrefix      = addressRule.IsAny ? "0.0.0.0/0" : addressRule.AddressOrSubnet,
                                SourcePortRange          = "*",
                                DestinationAddressPrefix = subnet.AddressPrefix,
                                DestinationPortRange     = "*",
                                Protocol                 = SecurityRuleProtocol.Tcp,
                                Priority                 = priority++
                            });
                    }
                }
            }

            // Apply the updates.

            subnetNsg    = (await networkSecurityGroupCollection.CreateOrUpdateAsync(WaitUntil.Completed, subnetNsgName, WithNetworkTags(subnetNsg.Data))).Value;
            loadBalancer = (await loadBalancerCollection.CreateOrUpdateAsync(WaitUntil.Completed, loadbalancerName, WithNetworkTags(loadBalancer.Data))).Value;
        }

        /// <summary>
        /// Removes public SSH NAT and security rules for every node in the cluster.
        /// These are used by NEONKUBE related tools for provisioning, setting up, and
        /// managing cluster nodes. 
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task RemoveSshRulesAsync()
        {
            await SyncContext.Clear;

            var loadBalancerCollection         = resourceGroup.GetLoadBalancers();
            var networkSecurityGroupCollection = resourceGroup.GetNetworkSecurityGroups();

            // Remove all existing SSH related load balancer NAT and NSG rules.

            var natDeleteRules = loadBalancer.Data.InboundNatRules
                .Where(rule => rule.Name.StartsWith(publicSshRulePrefix, StringComparison.InvariantCultureIgnoreCase))
                .ToArray();

            foreach (var rule in natDeleteRules)
            {
                loadBalancer.Data.InboundNatRules.Remove(rule);
            }

            var nsgDeleteRules = subnetNsg.Data.SecurityRules
                .Where(rule => rule.Name.StartsWith(publicSshRulePrefix, StringComparison.InvariantCultureIgnoreCase))
                .ToArray();

            if (nsgDeleteRules.Length > 0)
            {
                foreach (var rule in nsgDeleteRules)
                {
                    subnetNsg.Data.SecurityRules.Remove(rule);
                }

                // Apply the changes.

                subnetNsg    = (await networkSecurityGroupCollection.CreateOrUpdateAsync(WaitUntil.Completed, subnetNsgName, WithNetworkTags(subnetNsg.Data))).Value;
                loadBalancer = (await loadBalancerCollection.CreateOrUpdateAsync(WaitUntil.Completed, loadbalancerName, WithNetworkTags(loadBalancer.Data))).Value;
            }
        }

        //---------------------------------------------------------------------
        // Cluster life-cycle methods

        /// <inheritdoc/>
        public override HostingCapabilities Capabilities => HostingCapabilities.Stoppable | HostingCapabilities.Removable;

        /// <inheritdoc/>
        public override async Task<HostingResourceAvailability> GetResourceAvailabilityAsync(long reserveMemory = 0, long reserveDisk = 0)
        {
            await SyncContext.Clear;

            await ConnectAzureAsync();

            // $todo(jefflill): We're deferring checking quotas and current utilization
            // for Azure (for the time being):
            //
            //      https://github.com/nforgeio/neonKUBE/issues/1544

            var regionName = azureOptions.Region;

            if (!regionToLocation.ContainsKey(regionName))
            {
                var constraint =
                    new HostingResourceConstraint()
                    {
                        ResourceType = HostingConstrainedResourceType.VmHost,
                        Details      = $"Azure region [{regionName}] does not exist or is not available to your subscription.",
                        Nodes        = cluster.SetupState.ClusterDefinition.NodeDefinitions.Keys.ToList()
                    };

                return new HostingResourceAvailability()
                {
                    Constraints   = 
                        new Dictionary<string, List<HostingResourceConstraint>>()
                        {
                            { $"AZURE/{regionName}", new List<HostingResourceConstraint>() { constraint } }
                        }
                };
            }

            // Verify that the virtual machine sizes required by the cluster are available in the region
            // and also that all of the requested VM sizes are AMD64.

            // $todo(jefflill):
            //
            // We don't currently:
            // 
            //      * Ensure that all VM sizes required by the cluster VM sizes are AMD64 compatible
            //      * VM is compatible with the storage tier specified for each node
            //
            //      https://github.com/nforgeio/neonKUBE/issues/1545

            await LoadVmSizeMetadataAsync();

            var constraints    = new List<HostingResourceConstraint>();
            var clusterVmSizes = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var node in cluster.Nodes)
            {
                var vmSize = node.Metadata.Azure.VmSize;

                if (!clusterVmSizes.Contains(vmSize))
                {
                    clusterVmSizes.Add(vmSize);
                }
            }

            foreach (var vmSize in clusterVmSizes)
            {
                if (!nodeNameToVmSku.TryGetValue(vmSize, out var vmSku))
                {
                    constraints.Add(
                        new HostingResourceConstraint()
                        {
                            ResourceType = HostingConstrainedResourceType.VmHost,
                            Details      = $"VM Size [{vmSize}] is not available in Azure region [{regionName}].",
                            Nodes        = cluster.Nodes
                                               .Where(node => node.Metadata.Azure.VmSize == vmSize)
                                               .Select(node => node.Name)
                                               .ToList()
                        });

                    continue;
                }

                if (vmSku.CpuArchitecture != AzureCpuArchitecture.Amd64)
                {
                    constraints.Add(
                        new HostingResourceConstraint()
                        {
                            ResourceType = HostingConstrainedResourceType.VmHost,
                            Details      = $"VM Size [{vmSize}] [cpu-architecture={vmSku.CpuArchitecture}] is not currently supported by NEONKUBE.",
                            Nodes        = cluster.Nodes
                                               .Where(node => node.Metadata.Azure.VmSize == vmSize)
                                               .Select(node => node.Name)
                                               .ToList()
                        });
                }
            }

            if (constraints.Count == 0)
            {
                return new HostingResourceAvailability();
            }
            else
            {
                var constraintDictionary = new Dictionary<string, List<HostingResourceConstraint>>();

                constraintDictionary.Add($"AZURE/{regionName}", constraints);

                return new HostingResourceAvailability()
                {
                    Constraints = constraintDictionary
                };
            }
        }

        /// <inheritdoc/>
        public override async Task<ClusterHealth> GetClusterHealthAsync(TimeSpan timeout = default)
        {
            await SyncContext.Clear;

            var clusterHealth = new ClusterHealth();

            if (timeout <= TimeSpan.Zero)
            {
                timeout = DefaultStatusTimeout;
            }

            await ConnectAzureAsync();

            // We're going to infer the cluster provisiong status by examining the
            // cluster login and the state of the VMs deployed to Azure.

            if (nodeNameToVm.Count == 0)
            {
                // There are no deployed VMs: looks like the cluster isn't running.

                clusterHealth.State   = ClusterState.NotFound;
                clusterHealth.Summary = "Cluster not found.";

                return clusterHealth;
            }

            // Create a hashset with the names of the nodes that map to deployed AWS
            // machine instances.

            var existingNodes = new HashSet<string>();

            foreach (var item in nodeNameToVm)
            {
                existingNodes.Add(item.Value.Name);
            }

            // We're going to assume that all virtual machines in the cluster's resource group
            // belong to the cluster and we'll map the actual VM states to tracked VM states.

            await GetAllClusterVmStatus();

            foreach (var nodeName in nodeNameToVm.Keys)
            {
                var nodePowerState = ClusterNodeState.NotProvisioned;

                if (existingNodes.Contains(nodeName))
                {
                    if (nodeNameToVm.TryGetValue(nodeName, out var azureVm))
                    {
                        nodePowerState = azureVm.State;
                    }
                }

                clusterHealth.Nodes.Add(nodeName, nodePowerState);
            }

            // We're going to examine the node states from the Azure perspective and
            // short-circuit the Kubernetes level cluster health check when the cluster
            // nodes are not provisioned, are paused or appear to be transitioning
            // between starting, stopping, or paused states.

            var commonNodeState = clusterHealth.Nodes.Values.First();

            foreach (var nodeState in clusterHealth.Nodes.Values)
            {
                if (nodeState != commonNodeState)
                {
                    // Nodes have differing states so we're going to consider the cluster
                    // to be transitioning.

                    clusterHealth.State   = ClusterState.Transitioning;
                    clusterHealth.Summary = "Cluster is transitioning";
                    break;
                }
            }

            if (cluster.SetupState != null && cluster.SetupState.DeploymentStatus != ClusterDeploymentStatus.Ready)
            {
                clusterHealth.State   = ClusterState.Configuring;
                clusterHealth.Summary = "Cluster is partially configured";
            }
            else if (clusterHealth.State != ClusterState.Transitioning)
            {
                // If we get here then all of the nodes have the same state so
                // we'll use that common state to set the overall cluster state.

                switch (commonNodeState)
                {
                    case ClusterNodeState.Starting:

                        clusterHealth.State   = ClusterState.Unhealthy;
                        clusterHealth.Summary = "Cluster is starting";
                        break;

                    case ClusterNodeState.Running:

                        clusterHealth.State   = ClusterState.Healthy;
                        clusterHealth.Summary = "Cluster is running";
                        break;

                    case ClusterNodeState.Paused:
                    case ClusterNodeState.Off:

                        clusterHealth.State   = ClusterState.Off;
                        clusterHealth.Summary = "Cluster is offline";
                        break;

                    case ClusterNodeState.NotProvisioned:

                        clusterHealth.State   = ClusterState.NotFound;
                        clusterHealth.Summary = "Cluster is not found.";
                        break;

                    case ClusterNodeState.Unknown:
                    default:

                        clusterHealth.State   = ClusterState.NotFound;
                        clusterHealth.Summary = "Cluster not found";
                        break;
                }
            }

            if (clusterHealth.State == ClusterState.Off)
            {
                clusterHealth.Summary = "Cluster is offline";

                return clusterHealth;
            }

            return clusterHealth;
        }

        /// <inheritdoc/>
        public override async Task StartClusterAsync()
        {
            await SyncContext.Clear;

            // We're going to signal all cluster VMs to start.

            await ConnectAzureAsync();
            await GetAllClusterVmStatus();

            await Parallel.ForEachAsync(nodeNameToVm.Values, parallelOptions,
                async (azureVm, cancellationToken) =>
                {
                    if (azureVm.State == ClusterNodeState.Off)
                    {
                        await azureVm.Vm.PowerOnAsync(WaitUntil.Started);
                    }
                });
        }

        /// <inheritdoc/>
        public override async Task StopClusterAsync(StopMode stopMode = StopMode.Graceful)
        {
            await SyncContext.Clear;

            // We're going to signal all cluster VMs to stop.

            // $todo(jefflill): Note that the fluent SDK doesn't appear to support forced shutdown:
            //
            //      https://github.com/nforgeio/neonKUBE/issues/1546

            await ConnectAzureAsync();

            await Parallel.ForEachAsync(nodeNameToVm.Values, parallelOptions,
                async (azureVm, cancellationToken) =>
                {
                    await azureVm.Vm.PowerOffAsync(WaitUntil.Completed, skipShutdown: stopMode != StopMode.Graceful);
                });
        }

        /// <inheritdoc/>
        public override async Task DeleteClusterAsync(ClusterDefinition clusterDefinition = null)
        {
            await SyncContext.Clear;

            // We just need to delete the cluster resource group and everything within it.

            await ConnectAzureAsync();
            await resourceGroup.DeleteAsync(WaitUntil.Completed, forceDeletionTypes: "Microsoft.Compute/virtualMachines");
        }
    }
}
