//-----------------------------------------------------------------------------
// FILE:	    AzureHostingManager.cs
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

// $note(jefflill):
//
// The fluent SDK is hard to use for programatically determined configuration
// but I think we can drop all of that and simply manipulate the [inner] property
// of these resources directly and then apply the changes.  The current code
// is a bit of a mess, but it'll be OK for cluster deployment.
//
// This would dramatically simplify things and also allow us to perform some
// operations in one go instead of multiple operations.  This will be more
// important when we support dynamically adding and removing nodes in the
// cluster.
//
// That being said, we should probably just upgrade to the new API.

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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Azure.Management.Compute.Fluent;
using Microsoft.Azure.Management.Compute.Fluent.Models;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.Network.Fluent;
using Microsoft.Azure.Management.Network.Fluent.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Collections;
using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;
using Neon.SSH;
using Neon.Tasks;
using Neon.Time;

using INetworkSecurityGroup = Microsoft.Azure.Management.Network.Fluent.INetworkSecurityGroup;
using SecurityRuleProtocol  = Microsoft.Azure.Management.Network.Fluent.Models.SecurityRuleProtocol;
using TransportProtocol     = Microsoft.Azure.Management.Network.Fluent.Models.TransportProtocol;

namespace Neon.Kube
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
        // some discussion about how neonKUBE thinks about cloud deployments:
        // 
        //      https://github.com/nforgeio/neonKUBE/issues/908
        //
        // The remainder of this note will outline how Azure provisioning works.
        //
        // A neonKUBE Azure cluster will require provisioning these things:
        //
        //      * VNET
        //      * VMs & Disks
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
        // set explicitly.  neonKUBE will default to reasonable ingress nodes when
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
        // VMs are currently based on the Ubuntu-20.04 Server image provided  
        // published to the marketplace by Canonical.  We use the [neon-image] tool
        // from the neonCLOUD repo to create Azure Gen2 base and node images used
        // to provision the cluster.  Gen2 images work on most Azure VM sizes and offer
        // larger OS disks, improved performance, more memory and support for premium
        // and ultra storage.  There's a decent chance that Azure will deprecate Gen1
        // VMs at some point, so neonKUBE is going to only support Gen2 images to
        // simplify things.
        //
        // This hosting manager will support creating VMs from neonKUBE node
        // images published to Azure .  No9de images are preprovisioned with all of
        // the software required, making cluster setup much faster and reliable.
        //
        // Node instance and disk types and sizes are specified by the 
        // [NodeDefinition.Azure] property.  VM sizes are specified using standard Azure
        // size names, disk type is an enum and disk sizes are specified via strings
        // including optional [ByteUnits].  Provisioning will need to verify that the
        // requested instance and drive types are actually available in the target Azure
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
        // by reading the current state of the cluster resources.

        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Relates cluster node information to an Azure VM.
        /// </summary>
        private class AzureVm
        {
            private AzureHostingManager hostingManager;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="node">The associated node proxy.</param>
            /// <param name="hostingManager">The parent hosting manager.</param>
            public AzureVm(NodeSshProxy<NodeDefinition> node, AzureHostingManager hostingManager)
            {
                Covenant.Requires<ArgumentNullException>(hostingManager != null, nameof(hostingManager));

                this.Node           = node;
                this.hostingManager = hostingManager;
            }

            /// <summary>
            /// Returns the associated node proxy.
            /// </summary>
            public NodeSshProxy<NodeDefinition> Node { get; private set; }

            /// <summary>
            /// Returns the node metadata (AKA its definition).
            /// </summary>
            public NodeDefinition Metadata => Node.Metadata;

            /// <summary>
            /// Returns the name of the node as defined in the cluster definition.
            /// </summary>
            public string Name => Node.Metadata.Name;

            /// <summary>
            /// Returns the name of the Azure VM for this node.
            /// </summary>
            public string VmName => hostingManager.GetResourceName("vm", Node.Name);

            /// <summary>
            /// Returns the private IP address of the node.
            /// </summary>
            public string Address => Node.Metadata.Address;

            /// <summary>
            /// The associated Azure VM.
            /// </summary>
            public IVirtualMachine Vm { get; set; }

            /// <summary>
            /// The node's network interface.
            /// </summary>
            public INetworkInterface Nic { get; set; }

            /// <summary>
            /// Returns <c>true</c> if the node is a master.
            /// </summary>
            public bool IsMaster => Node.Metadata.Role == NodeRole.Master;

            /// <summary>
            /// Returns <c>true</c> if the node is a worker.
            /// </summary>
            public bool IsWorker => Node.Metadata.Role == NodeRole.Worker;

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
            EnableSsh = 0x0002,

            /// <summary>
            /// Disable external SSH to the cluster nodes.
            /// </summary>
            DisableSsh = 0x0004,
        }

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Used to limit how many threads will be created by parallel operations.
        /// </summary>
        private static readonly ParallelOptions parallelOptions = new ParallelOptions() { MaxDegreeOfParallelism = 10 };

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
        /// The (namespace) prefix used for neonKUBE related Azure resource tags.
        /// </summary>
        private const string neonTagKeyPrefix = "neon:";

        /// <summary>
        /// Used to tag resources with the cluster name.
        /// </summary>
        private const string neonClusterTagKey = neonTagKeyPrefix + "cluster";

        /// <summary>
        /// Used to tag resources with the cluster environment.
        /// </summary>
        private const string neonEnvironmentTagKey = neonTagKeyPrefix + "environment";

        /// <summary>
        /// Used to tag VM resources with the cluster node name.
        /// </summary>
        private const string neonNodeNameTagKey = neonTagKeyPrefix + "node.name";

        /// <summary>
        /// Used to tag instances resources with the external SSH port to be used to 
        /// establish a SSH connection to the instance.
        /// </summary>
        private const string neonNodeSshPortTagKey = neonTagKeyPrefix + "node.ssh-port";

        /// <summary>
        /// Returns the list of Azure VM size name <see cref="Regex"/> patterns
        /// for VMs that are known to be <b>compatible</b> with Gen2 VM images.
        /// </summary>
        private static IReadOnlyList<Regex> gen2VmSizeAllowedRegex;

        /// <summary>
        /// Logical unit number for a node's boot disk.
        /// </summary>
        private const int bootDiskLun = 0;

        /// <summary>
        /// Logical unit number for a node's data disk.
        /// </summary>
        private const int dataDiskLun = 1;

        /// <summary>
        /// Logical unit number for a node's optional OpenEBS cStor disk.
        /// </summary>
        private const int openEBSDiskLun = 2;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static AzureHostingManager()
        {
            // IMPORTANT:
            //
            // This needs to be updated periodically as Azure adds new VM sizes
            // that support Gen2 images.
            //
            //      https://docs.microsoft.com/en-us/azure/virtual-machines/windows/generation-2#generation-2-vm-sizes

            gen2VmSizeAllowedRegex = new List<Regex>
            {
                new Regex(@"^Standard_B\d+", RegexOptions.IgnoreCase),                  // B
                new Regex(@"^Standard_DC\d+s_v2$", RegexOptions.IgnoreCase),            // DCsv2
                new Regex(@"^Standard_D\d*_v2$", RegexOptions.IgnoreCase),              // Dv2
                new Regex(@"^Standard_DS\d+_v2$", RegexOptions.IgnoreCase),             // Dsv2
                new Regex(@"^Standard_D\d+_v3$", RegexOptions.IgnoreCase),              // Dv3
                new Regex(@"^Standard_D\d+s_v3$", RegexOptions.IgnoreCase),             // Dsv3
                new Regex(@"^Standard_D\d+_v4$", RegexOptions.IgnoreCase),              // Dav4
                new Regex(@"^Standard_D\d+s_v4$", RegexOptions.IgnoreCase),             // Dasv4
                new Regex(@"^Standard_D\d+d_v4$", RegexOptions.IgnoreCase),             // Ddv4
                new Regex(@"^Standard_D\dds_v4$", RegexOptions.IgnoreCase),             // Ddsv4
                new Regex(@"^Standard_D\d+d_v4$", RegexOptions.IgnoreCase),             // Ddv4
                new Regex(@"^Standard_D\d+ds_v4$", RegexOptions.IgnoreCase),            // Ddsv4
                new Regex(@"^Standard_D\d+as_v5$", RegexOptions.IgnoreCase),            // Dasv5
                new Regex(@"^Standard_D\d+ads_v5$", RegexOptions.IgnoreCase),           // Dadsv5
                new Regex(@"^Standard_D\d+as_v5$", RegexOptions.IgnoreCase),            // Dasv5
                new Regex(@"^Standard_D\d+ads_v5$", RegexOptions.IgnoreCase),           // Dadsv5
                new Regex(@"^Standard_DC\d+as_v5$", RegexOptions.IgnoreCase),           // DCasv5
                new Regex(@"^Standard_DC\d+ads_v5$", RegexOptions.IgnoreCase),          // DCadsv5
                new Regex(@"^Standard_DC\d+as_v5$", RegexOptions.IgnoreCase),           // DCasv5
                new Regex(@"^Standard_DC\d+ads_v5$", RegexOptions.IgnoreCase),          // DCadsv5
                new Regex(@"^Standard_D\d+_v5$", RegexOptions.IgnoreCase),              // Dv5
                new Regex(@"^Standard_D\d+s_v5$", RegexOptions.IgnoreCase),             // Dsv5
                new Regex(@"^Standard_D\d+d_v5$", RegexOptions.IgnoreCase),             // Ddv5
                new Regex(@"^Standard_D\d+ds_v5$", RegexOptions.IgnoreCase),            // Ddsv5
                new Regex(@"^Standard_E\d+_v3$", RegexOptions.IgnoreCase),              // Ev3
                new Regex(@"^Standard_E\d+s_v3$", RegexOptions.IgnoreCase),             // Esv3
                new Regex(@"^Standard_E\d+_v4$", RegexOptions.IgnoreCase),              // Ev4
                new Regex(@"^Standard_E\d+s_v4$", RegexOptions.IgnoreCase),             // Esv4
                new Regex(@"^Standard_E\d+_v5$", RegexOptions.IgnoreCase),              // Ev5
                new Regex(@"^Standard_E\d+s_v5$", RegexOptions.IgnoreCase),             // Esv5
                new Regex(@"^Standard_E\d+a_v4$", RegexOptions.IgnoreCase),             // Eav4
                new Regex(@"^Standard_E\d+as_v4$", RegexOptions.IgnoreCase),            // Easv4
                new Regex(@"^Standard_E\d+d_v4$", RegexOptions.IgnoreCase),             // Edv4
                new Regex(@"^Standard_E\d+ds_v4$", RegexOptions.IgnoreCase),            // Edsv4
                new Regex(@"^Standard_E\d+as_v5$", RegexOptions.IgnoreCase),            // Easv5
                new Regex(@"^Standard_E\d+ads_v5$", RegexOptions.IgnoreCase),           // Eadsv5
                new Regex(@"^Standard_EC\d+as_v5$", RegexOptions.IgnoreCase),           // ECasv5
                new Regex(@"^Standard_E\d+ads_v5$", RegexOptions.IgnoreCase),           // ECadsv5
                new Regex(@"^Standard_E\d+d_v5$", RegexOptions.IgnoreCase),             // Edv5
                new Regex(@"^Standard_E\d+ds_v5$", RegexOptions.IgnoreCase),            // Edsv5
                new Regex(@"^Standard_E\d+_v5$", RegexOptions.IgnoreCase),              // Ev5
                new Regex(@"^Standard_E\d+s_v5$", RegexOptions.IgnoreCase),             // Esv5
                new Regex(@"^Standard_F\d+s_v2$", RegexOptions.IgnoreCase),             // Fsv2
                new Regex(@"^Standard_FX\d+mds$", RegexOptions.IgnoreCase),             // FX
                new Regex(@"^Standard_GS\d+$", RegexOptions.IgnoreCase),                // GS
                new Regex(@"^Standard_HB\d+rs$", RegexOptions.IgnoreCase),              // HB
                new Regex(@"^Standard_HB\d+rs_v2$", RegexOptions.IgnoreCase),           // HBv2
                new Regex(@"^Standard_HB\d+rs_v3$", RegexOptions.IgnoreCase),           // HBv3
                new Regex(@"^Standard_HC\d+rs$", RegexOptions.IgnoreCase),              // HC
                new Regex(@"^Standard_L\d+s$", RegexOptions.IgnoreCase),                // Ls
                new Regex(@"^Standard_L\d+s_v2$", RegexOptions.IgnoreCase),             // Lsv2
                new Regex(@"^Standard_M\d+$", RegexOptions.IgnoreCase),                 // M
                new Regex(@"^Standard_M\d+s_v2$", RegexOptions.IgnoreCase),             // Mv2 (s)
                new Regex(@"^Standard_M\d+ms_v2$", RegexOptions.IgnoreCase),            // Mv2 (ms)
                new Regex(@"^Standard_M\d+s_v2$", RegexOptions.IgnoreCase),             // Msv2 (s)
                new Regex(@"^Standard_M\d+ms_v2$", RegexOptions.IgnoreCase),            // Msv2 (ms)
                new Regex(@"^Standard_M\d+is_v2$", RegexOptions.IgnoreCase),            // Msv2 (is)
                new Regex(@"^Standard_M\d+ims_v2$", RegexOptions.IgnoreCase),           // Msv2 (ms)
                new Regex(@"^Standard_M\d+dms_v2$", RegexOptions.IgnoreCase),           // Mdsv2 (dms)
                new Regex(@"^Standard_M\d+ds_v2$", RegexOptions.IgnoreCase),            // Mdsv2 (ds)
                new Regex(@"^Standard_M\d+ids_v2$", RegexOptions.IgnoreCase),           // Mdsv2 (ids)
                new Regex(@"^Standard_M\d+idms_v2$", RegexOptions.IgnoreCase),          // Mdsv2 (idms)
                new Regex(@"^Standard_NC\d+s_v2$", RegexOptions.IgnoreCase),            // NCv2 (s)
                new Regex(@"^Standard_NC\d+rs_v2$", RegexOptions.IgnoreCase),           // NCv2 (rs)
                new Regex(@"^Standard_NC\d+s_v3$", RegexOptions.IgnoreCase),            // NCv3 (s)
                new Regex(@"^Standard_NC\d+rs_v3$", RegexOptions.IgnoreCase),           // NCv3 (rs)
                new Regex(@"Standard_NC\d+as_T4_v3$", RegexOptions.IgnoreCase),         // NCasT4_v3
                new Regex(@"^Standard_ND\d+s$", RegexOptions.IgnoreCase),               // ND (s)
                new Regex(@"^Standard_ND\d+rs$", RegexOptions.IgnoreCase),              // ND (rs)
                new Regex(@"^Standard_ND\d+asr_v4$", RegexOptions.IgnoreCase),          // ND A100 v4
                new Regex(@"^Standard_ND\d+rs_v2$", RegexOptions.IgnoreCase),           // NDv2
                new Regex(@"^Standard_NV\d+s_v3$", RegexOptions.IgnoreCase),            // NDv3
                new Regex(@"^Standard_NV\d+as_v4$", RegexOptions.IgnoreCase),           // NDv4
                new Regex(@"^Standard_NV\d+ads_A10_v5$", RegexOptions.IgnoreCase),      // NVadsA10 v5 (ads)
                new Regex(@"^Standard_NV\d+adms_A10_v5$", RegexOptions.IgnoreCase),     // NVadsA10 v5 (adms)
                new Regex(@"^Standard_ND\d+amsr_A100_v4$", RegexOptions.IgnoreCase),    // NDm A100 v4
            }
            .AsReadOnly();
        }

        /// <summary>
        /// Ensures that the assembly hosting this hosting manager is loaded.
        /// </summary>
        public static void Load()
        {
            // We don't have to do anything here because the assembly is loaded
            // as a byproduct of calling this method.
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
        /// Converts a <see cref="IngressProtocol"/> to the corresponding <see cref="TransportProtocol"/>.
        /// </summary>
        /// <param name="protocol">The input protocol.</param>
        /// <returns>The output protocol.</returns>
        private static TransportProtocol ToTransportProtocol(IngressProtocol protocol)
        {
            switch (protocol)
            {
                case IngressProtocol.Http:
                case IngressProtocol.Https:
                case IngressProtocol.Tcp:

                    return TransportProtocol.Tcp;

                default:

                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Converts a <see cref="AzureStorageType"/> to the underlying Azure storage type.
        /// </summary>
        /// <param name="azureStorageType">The input storage type.</param>
        /// <returns>The underlying Azure storage type.</returns>
        private static StorageAccountTypes ToAzureStorageType(AzureStorageType azureStorageType)
        {
            switch (azureStorageType)
            {
                case AzureStorageType.PremiumSSD:   return StorageAccountTypes.PremiumLRS;
                case AzureStorageType.StandardHDD:  return StorageAccountTypes.StandardLRS;
                case AzureStorageType.StandardSSD:  return StorageAccountTypes.StandardSSDLRS;
                case AzureStorageType.UltraSSD:     return StorageAccountTypes.UltraSSDLRS;
                default:                            throw new NotImplementedException();
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private ClusterProxy                            cluster;
        private string                                  clusterName;
        private SetupController<NodeDefinition>         controller;
        private string                                  clusterEnvironment;
        private HostingOptions                          hostingOptions;
        private CloudOptions                            cloudOptions;
        private bool                                    prefixResourceNames;
        private AzureHostingOptions                     azureOptions;
        private AzureCredentials                        azureCredentials;
        private NetworkOptions                          networkOptions;
        private string                                  region;
        private IAzure                                  azure;
        private readonly Dictionary<string, AzureVm>    nameToVm;
        private IGalleryImageVersion                    nodeImageVersion;

        // These names will be used to identify the cluster resources.

        private readonly string                         resourceGroupName;
        private readonly string                         publicAddressName;
        private readonly string                         vnetName;
        private readonly string                         subnetName;
        private readonly string                         proximityPlacementGroupName;
        private readonly string                         loadbalancerName;
        private readonly string                         loadbalancerFrontendName;
        private readonly string                         loadbalancerIngressBackendName;
        private readonly string                         loadbalancerMasterBackendName;
        private readonly string                         subnetNsgName;

        // These reference the Azure resources.

        private bool                                    resourceGroupExists; 
        private IPublicIPAddress                        publicAddress;
        private IPAddress                               clusterAddress;
        private INetwork                                vnet;
        private ILoadBalancer                           loadBalancer;
        private Dictionary<string, IAvailabilitySet>    nameToAvailabilitySet;
        private INetworkSecurityGroup                   subnetNsg;

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
        /// <param name="nodeImageUri">Ignored: must be <c>null</c>.</param>
        /// <param name="nodeImagePath">Ignored: must be <c>null</c>.</param>
        /// <param name="logFolder">
        /// The folder where log files are to be written, otherwise or <c>null</c> or 
        /// empty if logging is disabled.
        /// </param>
        public AzureHostingManager(ClusterProxy cluster, string nodeImageUri = null, string nodeImagePath = null, string logFolder = null)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null, nameof(cluster));
            Covenant.Requires<ArgumentException>(nodeImageUri == null, nameof(nodeImageUri));
            Covenant.Requires<ArgumentException>(nodeImagePath == null, nameof(nodeImagePath));

            cluster.HostingManager = this;

            this.cluster               = cluster;
            this.clusterName           = cluster.Name;
            this.clusterEnvironment    = NeonHelper.EnumToString(cluster.Definition.Environment);
            this.hostingOptions        = cluster.Definition.Hosting;
            this.cloudOptions          = hostingOptions.Cloud;
            this.azureOptions          = hostingOptions.Azure;
            this.cloudOptions          = hostingOptions.Cloud;
            this.networkOptions        = cluster.Definition.Network;
            this.nameToAvailabilitySet = new Dictionary<string, IAvailabilitySet>(StringComparer.InvariantCultureIgnoreCase);
            this.region                = azureOptions.Region;
            this.resourceGroupName     = cluster.Definition.Deployment.GetPrefixedName(azureOptions.ResourceGroup ?? $"neon-{clusterName}");

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

            this.publicAddressName              = GetResourceName("pip", "cluster", true);
            this.vnetName                       = GetResourceName("vnet", "cluster", true);
            this.subnetName                     = GetResourceName("snet", "cluster", true);
            this.proximityPlacementGroupName    = GetResourceName("ppg", "cluster", true);
            this.loadbalancerName               = GetResourceName("lbe", "cluster", true);
            this.subnetNsgName                  = GetResourceName("nsg", "subnet");
            this.loadbalancerFrontendName       = "ingress";
            this.loadbalancerIngressBackendName = "ingress-nodes";
            this.loadbalancerMasterBackendName  = "master-nodes";

            // Initialize the vm/node mapping dictionary and also ensure
            // that each node has reasonable Azure node options.

            this.nameToVm = new Dictionary<string, AzureVm>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var node in cluster.Nodes)
            {
                nameToVm.Add(node.Name, new AzureVm(node, this));

                if (node.Metadata.Azure == null)
                {
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
        /// Enumerates the cluster nodes in no particular order.
        /// </summary>
        private IEnumerable<AzureVm> Nodes => nameToVm.Values;

        /// <summary>
        /// Enumerates the cluster nodes in ascending order by name.
        /// </summary>
        private IEnumerable<AzureVm> SortedNodes => Nodes.OrderBy(node => node.Name, StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Enumerates the cluster master nodes in no particular order.
        /// </summary>
        private IEnumerable<AzureVm> MasterNodes => Nodes.Where(node => node.IsMaster);

        /// <summary>
        /// Enumerates the cluster master nodes in ascending order by name.
        /// </summary>
        private IEnumerable<AzureVm> SortedMasterNodes => Nodes.Where(node => node.IsMaster).OrderBy(node => node.Name, StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Enumerates the cluster worker nodes in no particular order.
        /// </summary>
        private IEnumerable<AzureVm> WorkerNodes => Nodes.Where(node => node.IsMaster);

        /// <summary>
        /// Enumerates the cluster worker nodes in ascending order by name.
        /// </summary>
        private IEnumerable<AzureVm> SorteWorkerNodes => Nodes.Where(node => node.IsWorker).OrderBy(node => node.Name, StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Enumerates the cluster worker nodes in ascending order by name followed by the sorted worker nodes.
        /// </summary>
        private IEnumerable<AzureVm> SortedMasterThenWorkerNodes => SortedMasterNodes.Union(SorteWorkerNodes);

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
        /// Determines whether a resource belongs to the cluster by comparing its NEON cluster
        /// tag value to the cluster name.
        /// </summary>
        /// <param name="resource">The resource being checked.</param>
        /// <returns><c>true</c> if the resource belongs to the cluster.</returns>
        private bool IsClusterResource(IResource resource)
        {
            if (resource == null)
            {
                return false;
            }

            return resource.Tags.Any(tag => tag.Key == neonClusterTagKey && tag.Value == clusterName);
        }

        /// <summary>
        /// Creates the tags for a resource including cluster details, any custom
        /// user resource tags, as well as any optional tags passed.
        /// </summary>
        /// <param name="tags">Any optional tags.</param>
        /// <returns>The dictionary.</returns>
        private Dictionary<string, string> GetTags(params ResourceTag[] tags)
        {
            var tagDictionary = new Dictionary<string, string>();

            tagDictionary[neonClusterTagKey]     = clusterName;
            tagDictionary[neonEnvironmentTagKey] = NeonHelper.EnumToString(cluster.Definition.Environment);

            foreach (var tag in tags)
            {
                tagDictionary[tag.Key] = tag.Value;
            }

            return tagDictionary;
        }

        /// <inheritdoc/>
        public override HostingEnvironment HostingEnvironment => HostingEnvironment.Azure;

        /// <inheritdoc/>
        public override bool RequiresNodeAddressCheck => false;

        /// <inheritdoc/>
        public override void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            if (clusterDefinition.Hosting.Environment != HostingEnvironment.Azure)
            {
                throw new ClusterDefinitionException($"{nameof(HostingOptions)}.{nameof(HostingOptions.Environment)}] must be set to [{HostingEnvironment.Azure}].");
            }

            if (string.IsNullOrEmpty(clusterDefinition.Hosting.Azure.ClientId))
            {
                throw new ClusterDefinitionException($"{nameof(AzureHostingOptions)}.{nameof(AzureHostingOptions.ClientId)}] must be specified for Azure clusters.");
            }

            if (string.IsNullOrEmpty(clusterDefinition.Hosting.Azure.ClientSecret))
            {
                throw new ClusterDefinitionException($"{nameof(AzureHostingOptions)}.{nameof(AzureHostingOptions.ClientSecret)}] must be specified for Azure clusters.");
            }

            AssignNodeAddresses(clusterDefinition);

            // Set the cluster definition datacenter to the target region when the
            // user hasn't explictly specified a datacenter.

            if (string.IsNullOrEmpty(clusterDefinition.Datacenter))
            {
                clusterDefinition.Datacenter = region.ToUpperInvariant();
            }
        }

        /// <inheritdoc/>
        public override void AddProvisioningSteps(SetupController<NodeDefinition> controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Assert(cluster != null, $"[{nameof(AzureHostingManager)}] was created with the wrong constructor.");

            this.controller = controller;

            // We need to ensure that the cluster has at least one ingress node.

            KubeHelper.EnsureIngressNodes(cluster.Definition);

            // Initialize and run the [SetupController].

            var operation = $"Provisioning [{cluster.Definition.Name}] on Azure [{region}/{resourceGroupName}]";

            controller.AddGlobalStep("AZURE connect", state => ConnectAzureAsync());
            controller.AddGlobalStep("locate node image", state => LocateNodeImageAsync());
            controller.AddGlobalStep("region check", state => VerifyRegionAndVmSizesAsync());
            controller.AddGlobalStep("resource group", state => CreateResourceGroup());
            controller.AddGlobalStep("availability sets", state => CreateAvailabilitySetsAsync());
            controller.AddGlobalStep("network security groups", state => CreateNetworkSecurityGroupsAsync());
            controller.AddGlobalStep("virtual network", state => CreateVirtualNetworkAsync());
            controller.AddGlobalStep("public address", state => CreatePublicAddressAsync());
            controller.AddGlobalStep("ssh config", ConfigureNodeSsh, quiet: true);
            controller.AddGlobalStep("load balancer", state => CreateLoadBalancerAsync());
            controller.AddGlobalStep("listing virtual machines",
                state =>
                {
                    controller.SetGlobalStepStatus("list: virtual machines");

                    // Update [azureNodes] with any existing Azure nodes and their NICs.
                    // Note that it's possible for VMs that are unrelated to the cluster
                    // to be in the resource group, so we'll have to ignore those.

                    foreach (var vm in azure.VirtualMachines.ListByResourceGroup(resourceGroupName))
                    {
                        if (!vm.Tags.TryGetValue(neonNodeNameTagKey, out var nodeName))
                        {
                            break;  // Not a cluster VM
                        }

                        if (!nameToVm.TryGetValue(nodeName, out var azureNode))
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

                        azureNode.Vm  = vm;
                        azureNode.Nic = vm.GetPrimaryNetworkInterface();
                    }
                },
                quiet: true);
            controller.AddNodeStep("credentials",
                (controller, node) =>
                {
                    // Update the node SSH proxies to use the secure SSH password.

                    var clusterLogin = controller?.Get<ClusterLogin>(KubeSetupProperty.ClusterLogin);

                    node.UpdateCredentials(SshCredentials.FromUserPassword(KubeConst.SysAdminUser, clusterLogin.SshPassword));
                },
                quiet: true);
            controller.AddNodeStep("virtual machines", CreateVmAsync);
            controller.AddGlobalStep("internet access", state => UpdateNetworkAsync(NetworkOperations.InternetRouting | NetworkOperations.EnableSsh));
        }

        /// <inheritdoc/>
        public override void AddPostProvisioningSteps(SetupController<NodeDefinition> controller)
        {
            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);

            if (cluster.Definition.Storage.OpenEbs.Engine == OpenEbsEngine.cStor)
            {
                // We need to add any required OpenEBS cStor disks after the node has been otherwise
                // prepared.  We need to do this here because if we created the data and OpenEBS disks
                // when the VM is initially created, the disk setup scripts executed during prepare
                // won't be able to distinguish between the two disks.
                //
                // At this point, the data disk should be partitioned, formatted, and mounted so
                // the OpenEBS disk will be easy to identify as the only unpartitioned disks.

                controller.AddNodeStep("openebs",
                    async (controller, node) =>
                    {
                        var azureNode          = nameToVm[node.Name];
                        var openEBSStorageType = ToAzureStorageType(azureNode.Metadata.Azure.OpenEBSStorageType);

                        node.Status = "openebs: checking";

                        if (azureNode.Vm.DataDisks.Count < 1)   // Note that the OS disk doesn't count.
                        {
                            node.Status = "openebs: cStor disk";

                            await azureNode.Vm
                                .Update()
                                .WithNewDataDisk((int)(ByteUnits.Parse(node.Metadata.Azure.OpenEBSDiskSize) / ByteUnits.GibiBytes), openEBSDiskLun, CachingTypes.ReadOnly, openEBSStorageType)
                                .WithTags(GetTags())
                                .ApplyAsync();
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

            controller.AddGlobalStep("AZURE connect",
                async controller =>
                {
                    await ConnectAzureAsync();
                });

            controller.AddGlobalStep("ssh: port mappings",
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

            controller.AddGlobalStep("ssh: block ingress",
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
            await ConnectAzureAsync();

            var operations = NetworkOperations.InternetRouting;

            if (loadBalancer.InboundNatRules.Values.Any(rule => rule.Name.StartsWith(ingressRulePrefix, StringComparison.InvariantCultureIgnoreCase)))
            {
                // It looks like SSH NAT rules are enabled so we'll update
                // those as well.

                operations |= NetworkOperations.EnableSsh;
            }

            await UpdateNetworkAsync(operations);
        }

        /// <inheritdoc/>
        public override async Task EnableInternetSshAsync()
        {
            await SyncContext.Clear;

            await ConnectAzureAsync();
            await UpdateNetworkAsync(NetworkOperations.EnableSsh);
        }

        /// <inheritdoc/>
        public override async Task DisableInternetSshAsync()
        {
            await SyncContext.Clear;

            await ConnectAzureAsync();
            await UpdateNetworkAsync(NetworkOperations.DisableSsh);
        }

        /// <inheritdoc/>
        public override (string Address, int Port) GetSshEndpoint(string nodeName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nodeName), nameof(nodeName));
            Covenant.Assert(azure != null, "Azure: Not connected.");

            // Get the Azure VM so we can retrieve the assigned external SSH port for the
            // node via the external SSH port tag.

            if (!nameToVm.TryGetValue(nodeName, out var azureVm))
            {
                throw new NeonKubeException($"Cannot locate Azure VM for the [{nodeName}] node.");
            }

            return (Address: publicAddress.IPAddress, Port: azureVm.ExternalSshPort);
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
        public override string GetClusterAddress(bool nullWhenNoLoadbalancer = false)
        {
            return publicAddress.IPAddress;
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
            if (azure != null)
            {
                return;
            }

            controller?.SetGlobalStepStatus("connect: Azure");

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
                        ClientId     = azureOptions.ClientId,
                        ClientSecret = azureOptions.ClientSecret
                    },
                    tenantId:    azureOptions.TenantId,
                    environment: environment);

            azure = Azure.Configure()
                .Authenticate(azureCredentials)
                .WithSubscription(azureOptions.SubscriptionId);

            // Load references to any existing cluster resources.

           await GetResourcesAsync();
        }

        /// <summary>
        /// Retrieves references to any cluster resources.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task GetResourcesAsync()
        {
            // The resource group.

            if (!(await azure.ResourceGroups.ListAsync()).Any(resourceGroupItem => resourceGroupItem.Name == resourceGroupName && resourceGroupItem.RegionName == region))
            {
                // The resource group doesn't exist so it's not possible for any other
                // cluster resources to exist either.

                resourceGroupExists = false;
                return;
            }

            resourceGroupExists = true;

            // Network stuff.

            publicAddress = (await azure.PublicIPAddresses.ListByResourceGroupAsync(resourceGroupName)).SingleOrDefault(address => address.Name == publicAddressName);

            if (publicAddress != null)
            {
                clusterAddress = NetHelper.ParseIPv4Address(publicAddress.IPAddress);
            }

            vnet         = (await azure.Networks.ListByResourceGroupAsync(resourceGroupName)).SingleOrDefault(vnet => vnet.Name == vnetName);
            subnetNsg    = (await azure.NetworkSecurityGroups.ListByResourceGroupAsync(resourceGroupName)).SingleOrDefault(nsg => nsg.Name == subnetNsgName);
            loadBalancer = (await azure.LoadBalancers.ListByResourceGroupAsync(resourceGroupName)).SingleOrDefault(loadBalancer => loadBalancer.Name == loadbalancerName);

            // Availability sets

            var existingAvailabilitySets = azure.AvailabilitySets.ListByResourceGroup(resourceGroupName)
                .Where(set => IsClusterResource(set));

            nameToAvailabilitySet.Clear();

            foreach (var set in existingAvailabilitySets)
            {
                nameToAvailabilitySet.Add(set.Name, set);
            }

            // VM information

            var existingVms = (await azure.VirtualMachines.ListByResourceGroupAsync(resourceGroupName))
                .Where(vm => IsClusterResource(vm));

            foreach (var vm in existingVms)
            {
                if (!vm.Tags.TryGetValue(neonNodeNameTagKey, out var nodeName))
                {
                    throw new NeonKubeException($"Corrupted VM: [{vm.Name}] is missing the [{neonNodeNameTagKey}] tag.");
                }

                var node = cluster.FindNode(nodeName);

                if (node == null)
                {
                    throw new NeonKubeException($"Unexpected VM: [{vm.Name}] does not correspond to a node in the cluster definition.");
                }

                if (!vm.Tags.TryGetValue(neonNodeSshPortTagKey, out var sshPortString))
                {
                    throw new NeonKubeException($"Corrupted VM: [{vm.Name}] is missing the [{neonNodeSshPortTagKey}] tag.");
                }

                if (!int.TryParse(sshPortString, out var sshPort) || !NetHelper.IsValidPort(sshPort))
                {
                    throw new NeonKubeException($"Corrupted VM: [{vm.Name}] is has invalid [{neonNodeSshPortTagKey}={sshPortString}] tag.");
                }

                var azureVm = nameToVm[nodeName];

                azureVm.AvailabilitySetName = (await azure.AvailabilitySets.GetByIdAsync(vm.AvailabilitySetId)).Name;
                azureVm.Nic                 = await azure.NetworkInterfaces.GetByIdAsync(vm.NetworkInterfaceIds.First());
                azureVm.Vm                  = vm;
                azureVm.ExternalSshPort     = sshPort;
            }
        }

        /// <summary>
        /// Locates the node virtual machine image to be used to provision the cluster.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task LocateNodeImageAsync()
        {
            // $todo(jefflill):
            //
            // This is currently hardcoded to locate the current node image from our
            // private development image gallery.  We'll need to modify this to reference
            // our marketplace image and perhaps optionally use the development gallery.

            const string galleryResourceGroupName = "neonkube-images";
            const string galleryName              = "neonkube.images";

            var gallery = (await azure.Galleries.ListByResourceGroupAsync(galleryResourceGroupName)).SingleOrDefault(gallery => gallery.Name == galleryName);

            if (gallery == null)
            {
                throw new NeonKubeException($"Gallery [{galleryName}] not found in resource group: {galleryResourceGroupName}");
            }

            var neonKubeVersion = SemanticVersion.Parse(KubeVersions.NeonKube);
            var nodeImageName   = "neonkube-node";

            if (neonKubeVersion.Prerelease != null)
            {
                nodeImageName += $"-{neonKubeVersion.Prerelease}";
            }

            var nodeImage = (await azure.GalleryImages.ListByGalleryAsync(gallery.ResourceGroupName, gallery.Name)).SingleOrDefault(image => image.Name == nodeImageName);

            if (nodeImage == null)
            {
                throw new NeonKubeException($"Node image [{nodeImageName}] not found in image gallery: {galleryResourceGroupName}:{galleryName}");
            }

            var nodeImageVersionName = $"{neonKubeVersion.Major}.{neonKubeVersion.Minor}.{neonKubeVersion.Patch}";

            nodeImageVersion = (await azure.GalleryImageVersions.ListByGalleryImageAsync(gallery.ResourceGroupName, gallery.Name, nodeImage.Name))
                .SingleOrDefault(imageVersion => imageVersion.Name == nodeImageVersionName);

            if (nodeImageVersion == null)
            {
                throw new NeonKubeException($"Node image version [{nodeImageVersionName}] not found in image: {galleryResourceGroupName}:{galleryName}/{nodeImageName}");
            }
        }

        /// <summary>
        /// <para>
        /// Verifies that the requested Azure region exists, supports the requested VM sizes,
        /// and that VMs for nodes that specify UltraSSD actually support UltraSSD.  We'll also
        /// verify that the requested VMs have the minimum required number or cores and RAM.
        /// </para>
        /// <para>
        /// This also updates the node labels to match the capabilities of their VMs.
        /// </para>
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task VerifyRegionAndVmSizesAsync()
        {
            controller.SetGlobalStepStatus("verify: Azure region and VM size availability");

            var regionName   = cluster.Definition.Hosting.Azure.Region;
            var nameToVmSize = new Dictionary<string, IVirtualMachineSize>(StringComparer.InvariantCultureIgnoreCase);
            var nameToVmSku  = new Dictionary<string, IComputeSku>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var vmSize in await azure.VirtualMachines.Sizes.ListByRegionAsync(regionName))
            {
                nameToVmSize[vmSize.Name] = vmSize;
            }

            foreach (var vmSku in await azure.ComputeSkus.ListByRegionAsync(regionName))
            {
                nameToVmSku[vmSku.Name.Value] = vmSku;
            }

            foreach (var node in cluster.Nodes)
            {
                var vmSizeName = node.Metadata.Azure.VmSize;

                if (!nameToVmSize.TryGetValue(vmSizeName, out var vmSize))
                {
                    throw new NeonKubeException($"Node [{node.Name}] requests [{nameof(node.Metadata.Azure.VmSize)}={vmSizeName}].  This is not available in the [{regionName}] Azure region.");
                }

                if (!nameToVmSku.TryGetValue(vmSizeName, out var vmSku))
                {
                    // This should never happen, right?

                    throw new NeonKubeException($"Node [{node.Name}] requests [{nameof(node.Metadata.Azure.VmSize)}={vmSizeName}].  This is not available in the [{regionName}] Azure region.");
                }

                // $todo(jefflill):
                //
                // We don't currently ensure that all VM sizes required by the cluster are AMD64 compatible.
                //
                //      https://github.com/nforgeio/neonKUBE/issues/1545

                switch (node.Metadata.Role)
                {
                    case NodeRole.Master:

                        if (vmSize.NumberOfCores < KubeConst.MinMasterCores)
                        {
                            throw new NeonKubeException($"Master node [{node.Name}] requests [{nameof(node.Metadata.Azure.VmSize)}={vmSizeName}] with [Cores={vmSize.NumberOfCores} MiB] which is lower than the required [{KubeConst.MinMasterCores}] cores.]");
                        }

                        if (vmSize.MemoryInMB < KubeConst.MinMasterRamMiB)
                        {
                            throw new NeonKubeException($"Master node [{node.Name}] requests [{nameof(node.Metadata.Azure.VmSize)}={vmSizeName}] with [RAM={vmSize.MemoryInMB} MiB] which is lower than the required [{KubeConst.MinMasterRamMiB} MiB].]");
                        }

                        if (vmSize.MaxDataDiskCount < 1)
                        {
                            throw new NeonKubeException($"Master node [{node.Name}] requests [{nameof(node.Metadata.Azure.VmSize)}={vmSizeName}] that supports up to [{vmSize.MaxDataDiskCount}] disks.  A minimum of [1] drive is required.");
                        }
                        break;

                    case NodeRole.Worker:

                        if (vmSize.NumberOfCores < KubeConst.MinWorkerCores)
                        {
                            throw new NeonKubeException($"Worker node [{node.Name}] requests [{nameof(node.Metadata.Azure.VmSize)}={vmSizeName}] with [Cores={vmSize.NumberOfCores} MiB] which is lower than the required [{KubeConst.MinWorkerCores}] cores.]");
                        }

                        if (vmSize.MemoryInMB < KubeConst.MinWorkerRamMiB)
                        {
                            throw new NeonKubeException($"Worker node [{node.Name}] requests [{nameof(node.Metadata.Azure.VmSize)}={vmSizeName}] with [RAM={vmSize.MemoryInMB} MiB] which is lower than the required [{KubeConst.MinWorkerRamMiB} MiB].]");
                        }

                        if (vmSize.MaxDataDiskCount < 1)
                        {
                            throw new NeonKubeException($"Worker node [{node.Name}] requests [{nameof(node.Metadata.Azure.VmSize)}={vmSizeName}] that supports up to [{vmSize.MaxDataDiskCount}] disks.  A minimum of [1] drive is required.");
                        }
                        break;

                    default:

                        throw new NotImplementedException();
                }

                if (node.Metadata.Azure.StorageType == AzureStorageType.UltraSSD)
                {
                    if (!vmSku.Capabilities.Any(Capability => Capability.Name == "UltraSSDAvailable" && Capability.Value == "False"))
                    {
                        throw new NeonKubeException($"Node [{node.Name}] requests an UltraSSD disk.  This is not available in the [{regionName}] Azure region and/or the [{vmSize}] VM Size.");
                    }
                }

                // Update the node labels to match the actual VM capabilities.

                node.Metadata.Labels.ComputeCores     = vmSize.NumberOfCores;
                node.Metadata.Labels.ComputeRam       = vmSize.MemoryInMB;
                node.Metadata.Labels.StorageSize      = $"{AzureHelper.GetDiskSizeGiB(node.Metadata.Azure.StorageType, ByteUnits.Parse(node.Metadata.Azure.DiskSize))} GiB";
                node.Metadata.Labels.StorageHDD       = node.Metadata.Azure.StorageType == AzureStorageType.StandardHDD;
                node.Metadata.Labels.StorageEphemeral = false;
                node.Metadata.Labels.StorageLocal     = false;
                node.Metadata.Labels.StorageRedundant = true;
            }
        }

        /// <summary>
        /// Creates the cluster's resource group if it doesn't already exist.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task CreateResourceGroup()
        {
            if (!resourceGroupExists)
            {
                controller.SetGlobalStepStatus("create: resource group");

                await azure.ResourceGroups
                    .Define(resourceGroupName)
                    .WithRegion(region)
                    .WithTags(GetTags())
                    .CreateAsync();

                resourceGroupExists = true;
            }
        }

        /// <summary>
        /// Creates an avilablity set for the master VMs and a separate one for the worker VMs
        /// as well as the cluster's proximity placement group.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task CreateAvailabilitySetsAsync()
        {
            // Create the availability sets defined for the cluster nodes.

            controller.SetGlobalStepStatus("create: availability sets");

            foreach (var azureNode in nameToVm.Values)
            {
                azureNode.AvailabilitySetName = GetResourceName("avail", azureNode.Metadata.Labels.PhysicalAvailabilitySet);

                if (nameToAvailabilitySet.ContainsKey(azureNode.AvailabilitySetName))
                {
                    continue;   // The availability set already exists.
                }

                // Create the availability set.

                IAvailabilitySet newSet;

                if (azureOptions.DisableProximityPlacement)
                {
                    newSet = await azure.AvailabilitySets
                        .Define(azureNode.AvailabilitySetName)
                        .WithRegion(region)
                        .WithExistingResourceGroup(resourceGroupName)
                        .WithUpdateDomainCount(azureOptions.UpdateDomains)
                        .WithFaultDomainCount(azureOptions.FaultDomains)
                        .WithTags(GetTags())
                        .CreateAsync();
                }
                else
                {
                    newSet = await azure.AvailabilitySets
                        .Define(azureNode.AvailabilitySetName)
                        .WithRegion(region)
                        .WithExistingResourceGroup(resourceGroupName)
                        .WithNewProximityPlacementGroup(proximityPlacementGroupName, ProximityPlacementGroupType.Standard)
                        .WithUpdateDomainCount(azureOptions.UpdateDomains)
                        .WithFaultDomainCount(azureOptions.FaultDomains)
                        .WithTags(GetTags())
                        .CreateAsync();
                }

                nameToAvailabilitySet.Add(azureNode.AvailabilitySetName, newSet);
            }
        }

        /// <summary>
        /// Creates the network security groups.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task CreateNetworkSecurityGroupsAsync()
        {
            if (subnetNsg == null)
            {
                controller.SetGlobalStepStatus("create: network security groups");

                // Note that we're going to add rules later.

                subnetNsg = await azure.NetworkSecurityGroups
                    .Define(subnetNsgName)
                    .WithRegion(region)
                    .WithExistingResourceGroup(resourceGroupName)
                    .WithTags(GetTags())
                    .CreateAsync();
            }
        }

        /// <summary>
        /// Creates the cluster's virtual network.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task CreateVirtualNetworkAsync()
        {
            if (vnet == null)
            {
                controller.SetGlobalStepStatus("create: vnet");

                var vnetCreator = azure.Networks
                    .Define(vnetName)
                    .WithRegion(region)
                    .WithExistingResourceGroup(resourceGroupName)
                    .WithAddressSpace(azureOptions.VnetSubnet)
                    .DefineSubnet(subnetName)
                        .WithAddressPrefix(azureOptions.NodeSubnet)
                        .WithExistingNetworkSecurityGroup(subnetNsg.Id)
                        .Attach();

                var nameservers = cluster.Definition.Network.Nameservers;

                if (nameservers != null)
                {
                    foreach (var nameserver in nameservers)
                    {
                        vnetCreator.WithDnsServer(nameserver);
                    }
                }

                vnet = await vnetCreator
                    .WithTags(GetTags())
                    .CreateAsync();
            }
        }

        /// <summary>
        /// Creates the public IP address for the cluster's load balancer.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task CreatePublicAddressAsync()
        {
            if (publicAddress == null)
            {
                controller.SetGlobalStepStatus("create: public IPv4 address");

                publicAddress = await azure.PublicIPAddresses
                    .Define(publicAddressName)
                        .WithRegion(azureOptions.Region)
                        .WithExistingResourceGroup(resourceGroupName)
                        .WithStaticIP()
                        .WithLeafDomainLabel(azureOptions.DomainLabel)
                        .WithSku(PublicIPSkuType.Standard)
                        .WithTags(GetTags())
                        .CreateAsync();

                clusterAddress = NetHelper.ParseIPv4Address(publicAddress.IPAddress);

                // Set [ClusterDefinition.PublicAddresses] to the public IP if the
                // user hasn't specified any addresses.

                if (cluster.Definition.PublicAddresses.Count == 0)
                {
                    cluster.Definition.PublicAddresses.Add(publicAddress.IPAddress);
                }
            }
        }

        /// <summary>
        /// Assigns external SSH ports to AWS instance records that don't already have one and update
        /// the cluster nodes to reference the cluster's public IP and assigned SSH port.  Note
        /// that we're not actually going to write the instance tags here; we'll do that when we
        /// actually create any new instances.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        private void ConfigureNodeSsh(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            // Create a table with the currently allocated external SSH ports.

            var allocatedPorts = new HashSet<int>();

            foreach (var azureVm in nameToVm.Values.Where(azureVm => azureVm.ExternalSshPort != 0))
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

            foreach (var azureVm in SortedMasterThenWorkerNodes.Where(awsInstance => awsInstance.ExternalSshPort == 0))
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

            Covenant.Assert(publicAddress != null);

            foreach (var node in cluster.Nodes)
            {
                var azureVm = nameToVm[node.Name];

                node.Address = IPAddress.Parse(publicAddress.IPAddress);
                node.SshPort = azureVm.ExternalSshPort;
            }
        }

        /// <summary>
        /// Create the cluster's external load balancer.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task CreateLoadBalancerAsync()
        {
            if (loadBalancer == null)
            {
                controller.SetGlobalStepStatus("create: load balancer");

                // The Azure fluent API does not support creating a load balancer without
                // any rules.  So we're going to create the load balancer with a dummy rule
                // and then delete that rule straight away.

                loadBalancer = await azure.LoadBalancers
                    .Define(loadbalancerName)
                    .WithRegion(region)
                    .WithExistingResourceGroup(resourceGroupName)
                    .DefineLoadBalancingRule("dummy")
                        .WithProtocol(TransportProtocol.Tcp)
                        .FromFrontend(loadbalancerFrontendName)
                        .FromFrontendPort(10000)
                        .ToBackend(loadbalancerIngressBackendName)
                        .ToBackendPort(10000)
                        .Attach()
                    .DefinePublicFrontend(loadbalancerFrontendName)
                        .WithExistingPublicIPAddress(publicAddress)
                        .Attach()
                    .DefineBackend(loadbalancerMasterBackendName)
                        .Attach()
                    .DefineBackend(loadbalancerIngressBackendName)
                        .Attach()
                    .WithSku(LoadBalancerSkuType.Standard)
                    .WithTags(GetTags())
                    .CreateAsync();

                loadBalancer = await loadBalancer.Update()
                    .WithoutLoadBalancingRule("dummy")
                    .WithTags(GetTags())
                    .ApplyAsync();
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
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            var azureNode = nameToVm[node.Name];

            if (azureNode.Vm != null)
            {
                // The VM already exists.

                return;
            }

            node.Status = "create: NIC";

            azureNode.Nic = await azure.NetworkInterfaces
                .Define(GetResourceName("nic", azureNode.Node.Name))
                .WithRegion(azureOptions.Region)
                .WithExistingResourceGroup(resourceGroupName)
                .WithExistingPrimaryNetwork(vnet)
                .WithSubnet(subnetName)
                .WithPrimaryPrivateIPAddressStatic(azureNode.Address)
                .WithTags(GetTags())
                .CreateAsync();

            node.Status = "create: virtual machine";

            var clusterLogin     = controller.Get<ClusterLogin>(KubeSetupProperty.ClusterLogin);
            var azureNodeOptions = azureNode.Node.Metadata.Azure;
            var azureStorageType = ToAzureStorageType(azureNodeOptions.StorageType);
            var diskSize         = (int)(ByteUnits.Parse(node.Metadata.Azure.DiskSize) / ByteUnits.GibiBytes);
            var dataDiskSize     = (int)(ByteUnits.Parse(node.Metadata.Azure.OpenEBSDiskSize) / ByteUnits.GibiBytes);

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
# output to be logged and can be viewable in the AWS portal.
#
#   https://aws.amazon.com/premiumsupport/knowledge-center/ec2-linux-log-user-data/
#
# WARNING: Do not leave the ""-ex"" SHABANG option in production builds to avoid 
#          leaking the secure SSH password to any logs!
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

echo 'sysadmin:{clusterLogin.SshPassword}' | chpasswd
";
            var encodedBootScript = Convert.ToBase64String(Encoding.UTF8.GetBytes(NeonHelper.ToLinuxLineEndings(bootScript)));

            if (dataDiskSize > 0)
            {
                azureNode.Vm = await azure.VirtualMachines
                    .Define(azureNode.VmName)
                    .WithRegion(azureOptions.Region)
                    .WithExistingResourceGroup(resourceGroupName)
                    .WithExistingPrimaryNetworkInterface(azureNode.Nic)
                    .WithLinuxGalleryImageVersion(nodeImageVersion.Id)
                    .WithRootUsername(KubeConst.SysAdminUser)
                    .WithRootPassword(clusterLogin.SshPassword)
                    .WithComputerName("ubuntu")
                    .WithCustomData(encodedBootScript)
                    .WithNewDataDisk(dataDiskSize, dataDiskLun, CachingTypes.ReadOnly, azureStorageType)    // <-- adding the optional data disk here
                    .WithOSDiskStorageAccountType(azureStorageType)
                    .WithOSDiskSizeInGB((int)AzureHelper.GetDiskSizeGiB(azureNodeOptions.StorageType, diskSize))
                    .WithSize(node.Metadata.Azure.VmSize)
                    .WithExistingAvailabilitySet(nameToAvailabilitySet[azureNode.AvailabilitySetName])
                    .WithTags(GetTags(new ResourceTag(neonNodeNameTagKey, node.Name), new ResourceTag(neonNodeSshPortTagKey, azureNode.ExternalSshPort.ToString())))
                    .CreateAsync();
            }
            else
            {
                azureNode.Vm = await azure.VirtualMachines
                    .Define(azureNode.VmName)
                    .WithRegion(azureOptions.Region)
                    .WithExistingResourceGroup(resourceGroupName)
                    .WithExistingPrimaryNetworkInterface(azureNode.Nic)
                    .WithLinuxGalleryImageVersion(nodeImageVersion.Id)
                    .WithRootUsername(KubeConst.SysAdminUser)
                    .WithRootPassword(clusterLogin.SshPassword)
                    .WithComputerName("ubuntu")
                    .WithCustomData(encodedBootScript)
                    .WithOSDiskStorageAccountType(azureStorageType)
                    .WithOSDiskSizeInGB((int)AzureHelper.GetDiskSizeGiB(azureNodeOptions.StorageType, diskSize))
                    .WithSize(node.Metadata.Azure.VmSize)
                    .WithExistingAvailabilitySet(nameToAvailabilitySet[azureNode.AvailabilitySetName])
                    .WithTags(GetTags(new ResourceTag(neonNodeNameTagKey, node.Name), new ResourceTag(neonNodeSshPortTagKey, azureNode.ExternalSshPort.ToString())))
                    .CreateAsync();
            }
        }

        /// <summary>
        /// Updates the load balancer and related security rules based on the operation flags passed.
        /// </summary>
        /// <param name="operations">Flags that control how the load balancer and related security rules are updated.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task UpdateNetworkAsync(NetworkOperations operations)
        {
            if ((operations & NetworkOperations.InternetRouting) != 0)
            {
                controller.SetGlobalStepStatus("update: load balancer ingress/egress rules");
                await UpdateIngressEgressRulesAsync();
            }

            if ((operations & NetworkOperations.EnableSsh) != 0)
            {
                controller.SetGlobalStepStatus("add: SSH rules");
                await AddSshRulesAsync();
            }

            if ((operations & NetworkOperations.DisableSsh) != 0)
            {
                controller.SetGlobalStepStatus("remove: SSH rules");
                await RemoveSshRulesAsync();
            }
        }

        /// <summary>
        /// Updates the load balancer and network security rules to match the current cluster definition.
        /// This also ensures that some nodes are marked for ingress when the cluster has one or more
        /// ingress rules and that nodes marked for ingress are in the load balancer's backend pool.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task UpdateIngressEgressRulesAsync()
        {
            // $note(jefflill):
            //
            // I really wanted to apply changes to the load balancer backend configuration
            // in one go so these changes would be atomic, but this doesn't seem to work.
            // Only one of the master or ingress backends appear to be able to be updated
            // simultaneously.
            //
            // This isn't really an issue for cluster setup but it may be a problem when
            // we support adding and removing nodes from an existing cluster (although
            // I suspect that will be OK too).
            //
            // We may be able to address this by editing the inner load balancer update
            // properties directly (like we do when enabling TCP reset) or when we switch
            // from the Azure Fluent SDK to the current SDK.

            var loadBalancerUpdater = loadBalancer.Update();
            var subnetNsgUpdater    = subnetNsg.Update();

            // We need to add a special ingress rule for the Kubernetes API on port 6443 and
            // load balance this traffic to the master nodes.

            var clusterRules = new IngressRule[]
                {
                    new IngressRule()
                    {
                        Name                  = "kubernetes-api",
                        Protocol              = IngressProtocol.Tcp,
                        ExternalPort          = NetworkPorts.KubernetesApiServer,
                        NodePort              = NetworkPorts.KubernetesApiServer,
                        Target                = IngressRuleTarget.Neon,
                        AddressRules          = networkOptions.ManagementAddressRules,
                        IdleTcpReset          = true,
                        TcpIdleTimeoutMinutes = 5
                    }
                };

            var ingressRules = networkOptions.IngressRules.Union(clusterRules).ToArray();

            //-----------------------------------------------------------------
            // Backend pools:

            // Ensure that we actually have some nodes marked for ingress when the cluster
            // defines some ingress rules and then ensure that the load balancer's backend
            // pool includes those node VMs.

            KubeHelper.EnsureIngressNodes(cluster.Definition);

            // Rebuild the [ingress-nodes] backend pool. 
            //
            // Note that we're going to add these VMs to the backend set one at a time
            // because the API only works when the VMs being added in a batch are all
            // in the same availability set.  Masters and workers are located within
            // different sets by default.

            var ingressBackendUpdater = loadBalancerUpdater.UpdateBackend(loadbalancerIngressBackendName);

            if (loadBalancer.Backends.TryGetValue(loadbalancerIngressBackendName, out var ingressBackend))
            {
                // Remove any existing VMs for nodes that no longer exist or are not
                // currently labeled for ingress from the pool.

                var vms = new List<IVirtualMachine>();

                await Parallel.ForEachAsync(ingressBackend.GetVirtualMachineIds(), parallelOptions,
                    async (vmId, cancellationToken) =>
                    {
                        var vm   = await azure.VirtualMachines.GetByIdAsync(vmId);
                        var node = cluster.FindNode(vm.Tags[neonNodeNameTagKey]);

                        if (node == null || !node.Metadata.Ingress)
                        {
                            lock (vms)
                            {
                                vms.Add(vm);
                            }
                        }
                    });

                if (vms.Count > 0)
                {
                    ingressBackendUpdater.WithoutExistingVirtualMachines(vms.ToArray());
                }
            }

            foreach (var ingressNode in nameToVm.Values.Where(node => node.Metadata.Ingress))
            {
                ingressBackendUpdater.WithExistingVirtualMachines(new IHasNetworkInterfaces[] { ingressNode.Vm });
            }

            loadBalancer = await ingressBackendUpdater.Parent().ApplyAsync();

            // Rebuild the [master-nodes] backend pool.

            loadBalancerUpdater = loadBalancer.Update();

            var masterBackendUpdater = loadBalancerUpdater.UpdateBackend(loadbalancerMasterBackendName);

            if (loadBalancer.Backends.TryGetValue(loadbalancerMasterBackendName, out var masterBackend))
            {
                // Remove any existing VMs for nodes that no longer exist or are not
                // a master node.

                var vms = new List<IVirtualMachine>();

                await Parallel.ForEachAsync(masterBackend.GetVirtualMachineIds(), parallelOptions,
                    async (vmId, cancellationToken) =>
                    {
                        var vm   = await azure.VirtualMachines.GetByIdAsync(vmId);
                        var node = cluster.FindNode(vm.Tags[neonNodeNameTagKey]);

                        if (node == null || !node.Metadata.IsMaster)
                        {
                            lock (vms)
                            {
                                vms.Add(vm);
                            }
                        }
                    });

                if (vms.Count > 0)
                {
                    masterBackendUpdater.WithoutExistingVirtualMachines(vms.ToArray());
                }
            }

            foreach (var masterNode in nameToVm.Values.Where(node => node.Metadata.IsMaster))
            {
                masterBackendUpdater.WithExistingVirtualMachines(new IHasNetworkInterfaces[] { masterNode.Vm });
            }

            loadBalancer = await masterBackendUpdater.Parent().ApplyAsync();

            //-----------------------------------------------------------------
            // Cluster ingress load balancing rules

            loadBalancerUpdater = loadBalancer.Update();

            // Remove all existing cluster ingress rules.

            foreach (var rule in loadBalancer.LoadBalancingRules.Values
                .Where(rule => rule.Name.StartsWith(ingressRulePrefix, StringComparison.InvariantCultureIgnoreCase)))
            {
                loadBalancerUpdater.WithoutLoadBalancingRule(rule.Name);
            }

            foreach (var rule in subnetNsg.SecurityRules.Values
                .Where(rule => rule.Name.StartsWith(ingressRulePrefix, StringComparison.InvariantCultureIgnoreCase)))
            {
                subnetNsgUpdater.WithoutRule(rule.Name);
            }

            // We also need to remove any existing load balancer ingress related health probes.
            // We'll recreate these below as necessary.

            foreach (var probe in loadBalancer.HttpProbes.Values
                .Where(probe => probe.Name.StartsWith(ingressRulePrefix, StringComparison.InvariantCultureIgnoreCase)))
            {
                loadBalancerUpdater.WithoutProbe(probe.Name);
            }

            foreach (var probe in loadBalancer.HttpsProbes.Values
                .Where(probe => probe.Name.StartsWith(ingressRulePrefix, StringComparison.InvariantCultureIgnoreCase)))
            {
                loadBalancerUpdater.WithoutProbe(probe.Name);
            }

            foreach (var probe in loadBalancer.TcpProbes.Values
                .Where(probe => probe.Name.StartsWith(ingressRulePrefix, StringComparison.InvariantCultureIgnoreCase)))
            {
                loadBalancerUpdater.WithoutProbe(probe.Name);
            }

            // Add the load balancer ingress rules and health probes.

            var defaultHealthCheck = networkOptions.IngressHealthCheck ?? new HealthCheckOptions();

            foreach (var ingressRule in ingressRules)
            {
                var probeName   = $"{ingressRulePrefix}{ingressRule.Name}";
                var healthCheck = ingressRule.IngressHealthCheck ?? defaultHealthCheck;

                loadBalancerUpdater.DefineTcpProbe(probeName)
                    .WithPort(ingressRule.NodePort)
                    .WithIntervalInSeconds(healthCheck.IntervalSeconds)
                    .WithNumberOfProbes(healthCheck.ThresholdCount)
                    .Attach();

                var ruleName    = $"{ingressRulePrefix}{ingressRule.Name}";
                var tcpTimeout  = Math.Min(Math.Max(4, ingressRule.TcpIdleTimeoutMinutes), 30);  // Azure allowed timeout range is [4..30] minutes
                var backendName = (string)null;

                switch (ingressRule.Target)
                {
                    case IngressRuleTarget.User:

                        backendName = loadbalancerIngressBackendName;
                        break;

                    case IngressRuleTarget.Neon:

                        backendName = loadbalancerMasterBackendName;
                        break;

                    default:

                        throw new NotImplementedException();
                }

                loadBalancerUpdater.DefineLoadBalancingRule(ruleName)
                    .WithProtocol(ToTransportProtocol(ingressRule.Protocol))
                    .FromExistingPublicIPAddress(publicAddress)
                    .FromFrontendPort(ingressRule.ExternalPort)
                    .ToBackend(backendName)
                    .ToBackendPort(ingressRule.NodePort)
                    .WithProbe(probeName)
                    .WithIdleTimeoutInMinutes(tcpTimeout)
                    .Attach();
            }

            // Add the NSG rules corresponding to the ingress rules from the cluster definition.
            //
            // To keep things simple, we're going to generate a separate rule for each source address
            // restriction.  In theory, we could have tried collecting allow and deny rules 
            // together to reduce the number of rules but that doesn't seem worth the trouble. 
            // This is possible because NSGs rules allow a comma separated list of IP addresses
            // or subnets to be specified.
            //
            // We may need to revisit this if we approach Azure rule count limits (currently 1000
            // rules per NSG).  That would also be a good time to support port ranges as well.

            var priority = firstIngressNsgRulePriority;

            foreach (var ingressRule in ingressRules)
            {
                var ruleProtocol = ToSecurityRuleProtocol(ingressRule.Protocol);

                if (ingressRule.AddressRules == null || ingressRule.AddressRules.Count == 0)
                {
                    // Default to allowing all addresses when no address rules are specified.

                    var ruleName = $"{ingressRulePrefix}{ingressRule.Name}";

                    subnetNsgUpdater.DefineRule(ruleName)
                        .AllowInbound()
                        .FromAnyAddress()
                        .FromAnyPort()
                        .ToAnyAddress()
                        .ToPort(ingressRule.NodePort)
                        .WithProtocol(ruleProtocol)
                        .WithPriority(priority++)
                        .Attach();
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
                        var ruleName          = multipleAddresses ? $"{ingressRulePrefix}{ingressRule.Name}-{addressRuleIndex++}"
                                                                  : $"{ingressRulePrefix}{ingressRule.Name}";
                        switch (addressRule.Action)
                        {
                            case AddressRuleAction.Allow:

                                if (addressRule.IsAny)
                                {
                                    subnetNsgUpdater.DefineRule(ruleName)
                                        .AllowInbound()
                                        .FromAnyAddress()
                                        .FromAnyPort()
                                        .ToAnyAddress()
                                        .ToPort(ingressRule.NodePort)
                                        .WithProtocol(ruleProtocol)
                                        .WithPriority(priority++)
                                        .Attach();
                                }
                                else
                                {
                                    subnetNsgUpdater.DefineRule(ruleName)
                                        .AllowInbound()
                                        .FromAddress(addressRule.AddressOrSubnet)
                                        .FromAnyPort()
                                        .ToAnyAddress()
                                        .ToPort(ingressRule.NodePort)
                                        .WithProtocol(ruleProtocol)
                                        .WithPriority(priority++)
                                        .Attach();
                                }
                                break;

                            case AddressRuleAction.Deny:

                                if (addressRule.IsAny)
                                {
                                    subnetNsgUpdater.DefineRule(ruleName)
                                        .DenyInbound()
                                        .FromAnyAddress()
                                        .FromAnyPort()
                                        .ToAnyAddress()
                                        .ToPort(ingressRule.NodePort)
                                        .WithProtocol(ruleProtocol)
                                        .WithPriority(priority++)
                                        .Attach();
                                }
                                else
                                {
                                    subnetNsgUpdater.DefineRule(ruleName)
                                        .DenyInbound()
                                        .FromAddress(addressRule.AddressOrSubnet)
                                        .FromAnyPort()
                                        .ToAnyAddress()
                                        .ToPort(ingressRule.NodePort)
                                        .WithProtocol(ruleProtocol)
                                        .WithPriority(priority++)
                                        .Attach();
                                }
                                break;

                            default:

                                throw new NotImplementedException();
                        }
                    }
                }
            }

            // Apply the updates.

            loadBalancer = await loadBalancerUpdater.ApplyAsync();
            subnetNsg    = await subnetNsgUpdater.ApplyAsync();

            // We need to set [EnableTcpReset] for the load balancer rules separately because
            // the Fluent API doesn't support this property yet.
            //
            // It's a bit unfortunate that we can't apply these changes when we updated the
            // load balancer above, but this shouldn't be a big deal.

            loadBalancerUpdater = loadBalancer.Update();

            foreach (var ingressRule in ingressRules)
            {
                var ruleName = $"{ingressRulePrefix}{ingressRule.Name}";
                var lbRule   = loadBalancer.Inner.LoadBalancingRules.Single(rule => rule.Name.Equals(ruleName, StringComparison.InvariantCultureIgnoreCase));

                lbRule.EnableTcpReset = ingressRule.IdleTcpReset;
            }

            await loadBalancerUpdater.ApplyAsync();
        }

        /// <summary>
        /// Adds public SSH NAT and security rules for every node in the cluster.
        /// These are used by neonKUBE tools for provisioning, setting up, and
        /// managing cluster nodes.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task AddSshRulesAsync()
        {
            var loadBalancerUpdater = loadBalancer.Update();
            var subnetNsgUpdater    = subnetNsg.Update();

            // Remove all existing load balancer public SSH related NAT rules.

            foreach (var rule in loadBalancer.LoadBalancingRules.Values
                .Where(rule => rule.Name.StartsWith(publicSshRulePrefix, StringComparison.InvariantCultureIgnoreCase)))
            {
                loadBalancerUpdater.WithoutLoadBalancingRule(rule.Name);
            }

            foreach (var rule in subnetNsg.SecurityRules.Values
                .Where(rule => rule.Name.StartsWith(publicSshRulePrefix, StringComparison.InvariantCultureIgnoreCase)))
            {
                subnetNsgUpdater.WithoutRule(rule.Name);
            }

            loadBalancer        = await loadBalancerUpdater.ApplyAsync();
            loadBalancerUpdater = loadBalancer.Update();

            // Add the SSH NAT rules for each node.  Note that we need to do this in three steps:
            //
            //      1. Add the NAT rule to the load balancer
            //      2. Enable TCP Reset for connections that are idle for too long
            //      3. Tie the node VM's NIC to the rule

            foreach (var azureNode in SortedMasterThenWorkerNodes)
            {
                var ruleName = $"{publicSshRulePrefix}{azureNode.Name}";

                loadBalancerUpdater.DefineInboundNatRule(ruleName)
                    .WithProtocol(TransportProtocol.Tcp)
                    .FromExistingPublicIPAddress(publicAddress)
                    .FromFrontendPort(azureNode.ExternalSshPort)
                    .ToBackendPort(NetworkPorts.SSH)
                    .WithIdleTimeoutInMinutes(30)       // Maximum Azure idle timeout
                    .Attach();
            }

            loadBalancer        = await loadBalancerUpdater.ApplyAsync();
            loadBalancerUpdater = loadBalancer.Update();

            // We need to set [EnableTcpReset] for the load balancer rules separately because
            // the Fluent API doesn't support the property (yet?).

            foreach (var azureNode in SortedMasterThenWorkerNodes)
            {
                var ruleName = $"{publicSshRulePrefix}{azureNode.Name}";
                var natRule  = loadBalancer.Inner.InboundNatRules.Single(rule => rule.Name.Equals(ruleName, StringComparison.InvariantCultureIgnoreCase));

                natRule.EnableTcpReset = true;
            }

            loadBalancer        = await loadBalancerUpdater.ApplyAsync();
            loadBalancerUpdater = loadBalancer.Update();

            foreach (var azureNode in SortedMasterThenWorkerNodes)
            {
                var sshRuleName = $"{publicSshRulePrefix}{azureNode.Name}";
                var nicUpdater  = azureNode.Nic
                    .Update()
                    .WithExistingLoadBalancerInboundNatRule(loadBalancer, sshRuleName);

                if (azureNode.IsMaster)
                {
                    nicUpdater.WithExistingLoadBalancerBackend(loadBalancer, loadbalancerMasterBackendName);
                }

                if (azureNode.Node.Metadata.Ingress)
                {
                    nicUpdater.WithExistingLoadBalancerBackend(loadBalancer, loadbalancerIngressBackendName);
                }

                azureNode.Nic = await nicUpdater.ApplyAsync();
            }

            // Add NSG rules so that the public SSH NAT rules can actually route traffic to the nodes.
            // To keep things simple, we're going to generate a separate rule for each source
            // address restriction.  In theory, we could have tried collecting allow and deny rules 
            // together to reduce the number of rules but that doesn't seem worth the trouble. 
            // This would be possible because NSGs rules allow a comma separated list of IP addresses
            // or subnets to be specified.

            var priority  = firstSshNsgRulePriority;
            var ruleIndex = 0;

            if (networkOptions.ManagementAddressRules == null || networkOptions.ManagementAddressRules.Count == 0)
            {
                // Default to allowing all source addresses when no address rules are specified.

                var ruleName = $"{publicSshRulePrefix}{ruleIndex++}";

                subnetNsgUpdater.DefineRule(ruleName)
                    .AllowInbound()
                    .FromAnyAddress()
                    .FromAnyPort()
                    .ToAnyAddress()
                    .ToPort(NetworkPorts.SSH)
                    .WithProtocol(SecurityRuleProtocol.Tcp)
                    .WithPriority(priority++)
                    .Attach();
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
                    var ruleName          = multipleAddresses ? $"{publicSshRulePrefix}{ruleIndex++}-{addressRuleIndex++}"
                                                              : $"{publicSshRulePrefix}{ruleIndex++}";
                    switch (addressRule.Action)
                    {
                        case AddressRuleAction.Allow:

                            if (addressRule.IsAny)
                            {
                                subnetNsgUpdater.DefineRule(ruleName)
                                    .AllowInbound()
                                    .FromAnyAddress()
                                    .FromAnyPort()
                                    .ToAnyAddress()
                                    .ToPort(NetworkPorts.SSH)
                                    .WithProtocol(SecurityRuleProtocol.Tcp)
                                    .WithPriority(priority++)
                                    .Attach();
                            }
                            else
                            {
                                subnetNsgUpdater.DefineRule(ruleName)
                                    .AllowInbound()
                                    .FromAddress(addressRule.AddressOrSubnet)
                                    .FromAnyPort()
                                    .ToAnyAddress()
                                    .ToPort(NetworkPorts.SSH)
                                    .WithProtocol(SecurityRuleProtocol.Tcp)
                                    .WithPriority(priority++)
                                    .Attach();
                            }
                            break;

                        case AddressRuleAction.Deny:

                            if (addressRule.IsAny)
                            {
                                subnetNsgUpdater.DefineRule(ruleName)
                                    .DenyInbound()
                                    .FromAnyAddress()
                                    .FromAnyPort()
                                    .ToAnyAddress()
                                    .ToPort(NetworkPorts.SSH)
                                    .WithProtocol(SecurityRuleProtocol.Tcp)
                                    .WithPriority(priority++)
                                    .Attach();
                            }
                            else
                            {
                                subnetNsgUpdater.DefineRule(ruleName)
                                    .DenyInbound()
                                    .FromAddress(addressRule.AddressOrSubnet)
                                    .FromAnyPort()
                                    .ToAnyAddress()
                                    .ToPort(NetworkPorts.SSH)
                                    .WithProtocol(SecurityRuleProtocol.Tcp)
                                    .WithPriority(priority++)
                                    .Attach();
                            }
                            break;

                        default:

                            throw new NotImplementedException();
                    }
                }
            }

            // Apply the updates.

            loadBalancer = await loadBalancerUpdater.ApplyAsync();
            subnetNsg    = await subnetNsgUpdater.ApplyAsync();
        }

        /// <summary>
        /// Removes public SSH NAT and security rules for every node in the cluster.
        /// These are used by neonKUBE related tools for provisioning, setting up, and
        /// managing cluster nodes. 
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task RemoveSshRulesAsync()
        {
            var loadBalancerUpdater = loadBalancer.Update();
            var subnetNsgUpdater    = subnetNsg.Update();

            // Remove all existing load balancer public SSH related NAT rules.

            foreach (var lbRule in loadBalancer.LoadBalancingRules.Values
                .Where(rule => rule.Name.StartsWith(publicSshRulePrefix, StringComparison.InvariantCultureIgnoreCase)))
            {
                loadBalancerUpdater.WithoutLoadBalancingRule(lbRule.Name);
            }

            foreach (var rule in subnetNsg.SecurityRules.Values
                .Where(rule => rule.Name.StartsWith(publicSshRulePrefix, StringComparison.InvariantCultureIgnoreCase)))
            {
                subnetNsgUpdater.WithoutRule(rule.Name);
            }

            loadBalancer        = await loadBalancerUpdater.ApplyAsync();
            loadBalancerUpdater = loadBalancer.Update();

            // Remove all of the SSH NAT related NSG rules.

            foreach (var nsgRule in subnetNsg.SecurityRules.Values)
            {
                subnetNsgUpdater.WithoutRule(nsgRule.Name);
            }

            // Apply the changes.

            loadBalancer = await loadBalancerUpdater.ApplyAsync();
            subnetNsg    = await subnetNsgUpdater.ApplyAsync();
        }

        //---------------------------------------------------------------------
        // Cluster life-cycle methods

        /// <inheritdoc/>
        public override HostingCapabilities Capabilities => HostingCapabilities.Stoppable | HostingCapabilities.Removable;


        /// <inheritdoc/>
        public override async Task<HostingResourceAvailability> GetResourceAvailabilityAsync(long reserveMemory = 0, long reserveDisk = 0)
        {
            await SyncContext.Clear;

            var regionName = azureOptions.Region;

            await ConnectAzureAsync();

            // NOTE: We're deferring checking quotas and current utilization for AWS at this time:
            //
            //      https://github.com/nforgeio/neonKUBE/issues/1544

            // Verify that the region exists and is available to the current subscription.

            var subscription = await azure.Subscriptions.GetByIdAsync(azureOptions.SubscriptionId);
            var location     = subscription.ListLocations().SingleOrDefault(location => location.Name.Equals(azureOptions.Region, StringComparison.InvariantCultureIgnoreCase));

            if (location == null)
            {
                var constraint =
                    new HostingResourceConstraint()
                    {
                        ResourceType = HostingConstrainedResourceType.VmHost,
                        Details      = $"Azure region [{regionName}] not found or available.",
                        Nodes        = cluster.Definition.NodeDefinitions.Keys.ToList()
                    };

                return new HostingResourceAvailability()
                {
                    CanBeDeployed = false,
                    Constraints   = 
                        new Dictionary<string, List<HostingResourceConstraint>>()
                        {
                            { $"AWS/{regionName}", new List<HostingResourceConstraint>() { constraint } }
                        }
                };
            }

            // Verify that the instance types required by the cluster are available in the region.

            // $todo(jefflill):
            //
            // We don't currently:
            // 
            //      * ensure that all VM sizes required by the cluster
            //      * VM sizes are AMD64 compatible
            //      * VM is compatible with the storage tier specified for each node
            //
            //      https://github.com/nforgeio/neonKUBE/issues/1545

            var nameToVmSize = new Dictionary<string, IVirtualMachineSize>(StringComparer.InvariantCultureIgnoreCase);
            var nameToVmSku  = new Dictionary<string, IComputeSku>(StringComparer.InvariantCultureIgnoreCase);
            var constraints  = new List<HostingResourceConstraint>();

            foreach (var vmSize in await azure.VirtualMachines.Sizes.ListByRegionAsync(regionName))
            {
                nameToVmSize[vmSize.Name] = vmSize;
            }

            foreach (var vmSku in await azure.ComputeSkus.ListByRegionAsync(regionName))
            {
                nameToVmSku[vmSku.Name.Value] = vmSku;
            }

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
                if (!nameToVmSize.TryGetValue(vmSize, out var vmSizeInfo))
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
            }

            if (constraints.Count == 0)
            {
                return new HostingResourceAvailability()
                {
                    CanBeDeployed = true
                };
            }
            else
            {
                var constraintDictionary = new Dictionary<string, List<HostingResourceConstraint>>();

                constraintDictionary.Add($"AWS/{regionName}", constraints);

                return new HostingResourceAvailability()
                {
                    CanBeDeployed = false,
                    Constraints = constraintDictionary
                };
            }
        }

        /// <inheritdoc/>
        public override async Task<ClusterInfo> GetClusterStatusAsync(TimeSpan timeout = default)
        {
            var clusterStatus = new ClusterInfo(cluster.Definition);

            if (timeout <= TimeSpan.Zero)
            {
                timeout = DefaultStatusTimeout;
            }

            await ConnectAzureAsync();

            // We're going to infer the cluster provisiong status by examining the
            // cluster login and the state of the VMs deployed to AWS.

            var contextName  = $"root@{cluster.Definition.Name}";
            var context      = KubeHelper.Config.GetContext(contextName);
            var clusterLogin = KubeHelper.GetClusterLogin((KubeContextName)contextName);

            // Create a hashset with the names of the nodes that map to deployed AWS
            // machine instances.

            var existingNodes = new HashSet<string>();

            foreach (var item in nameToVm)
            {
                var nodeDefinition = cluster.Definition.NodeDefinitions[item.Key];

                if (nodeDefinition != null)
                {
                    existingNodes.Add(nodeDefinition.Name);
                }
            }

            // Build the cluster status.

            if (context == null && clusterLogin == null)
            {
                // The Kubernetes context for this cluster doesn't exist, so we know that any
                // virtual machines with names matching the virtual machines that would be
                // provisioned for the cluster definition are conflicting.

                clusterStatus.State   = ClusterState.NotFound;
                clusterStatus.Summary = "Cluster does not exist";

                foreach (var node in cluster.Definition.NodeDefinitions.Values)
                {
                    clusterStatus.Nodes.Add(node.Name, existingNodes.Contains(node.Name) ? ClusterNodeState.Conflict : ClusterNodeState.NotProvisioned);
                }

                return clusterStatus;
            }
            else
            {
                // We're going to assume that all virtual machines in the cluster's resource group
                // belong to the cluster and we'll map the actual VM states to public node states.

                foreach (var node in cluster.Definition.NodeDefinitions.Values)
                {
                    var nodePowerState = ClusterNodeState.NotProvisioned;

                    if (existingNodes.Contains(node.Name))
                    {
                        if (nameToVm.TryGetValue(node.Name, out var azureVm))
                        {
                            var powerState = azureVm.Vm.PowerState;

                            if (powerState == PowerState.Starting)
                            {
                                nodePowerState = ClusterNodeState.Starting;
                            }
                            else if (powerState == PowerState.Running)
                            {
                                nodePowerState = ClusterNodeState.Running;
                            }
                            else if (powerState == PowerState.Stopping)
                            {
                                // We don't currently have a status for stopping a node so we'll
                                // consider it to be running because technically, it still is.

                                nodePowerState = ClusterNodeState.Running;
                            }
                            else if (powerState == PowerState.Stopped)
                            {
                                nodePowerState = ClusterNodeState.Off;
                            }
                            else if (powerState == PowerState.Deallocating)
                            {
                                // We don't currently have a status for terminating a node so we'll
                                // consider it to be running because technically, it still is.

                                nodePowerState = ClusterNodeState.Running;
                            }
                            else if (powerState == PowerState.Deallocated)
                            {
                                nodePowerState = ClusterNodeState.NotProvisioned;
                            }
                            else
                            {
                                Covenant.Assert(false, $"Unexpected node instance status: [{powerState}]");
                            }
                        }
                    }

                    clusterStatus.Nodes.Add(node.Name, nodePowerState);
                }

                // We're going to examine the node states from the AWS perspective and
                // short-circuit the Kubernetes level cluster health check when the cluster
                // nodes are not provisioned, are paused or appear to be transitioning
                // between starting, stopping, or paused states.

                var commonNodeState = clusterStatus.Nodes.Values.First();

                foreach (var nodeState in clusterStatus.Nodes.Values)
                {
                    if (nodeState != commonNodeState)
                    {
                        // Nodes have differing states so we're going to consider the cluster
                        // to be transitioning.

                        clusterStatus.State   = ClusterState.Transitioning;
                        clusterStatus.Summary = "Cluster is transitioning";
                        break;
                    }
                }

                if (clusterLogin != null && clusterLogin.SetupDetails.SetupPending)
                {
                    clusterStatus.State   = ClusterState.Configuring;
                    clusterStatus.Summary = "Cluster is partially configured";
                }
                else if (clusterStatus.State != ClusterState.Transitioning)
                {
                    // If we get here then all of the nodes have the same state so
                    // we'll use that common state to set the overall cluster state.

                    switch (commonNodeState)
                    {
                        case ClusterNodeState.Starting:

                            clusterStatus.State   = ClusterState.Unhealthy;
                            clusterStatus.Summary = "Cluster is starting";
                            break;

                        case ClusterNodeState.Running:

                            clusterStatus.State   = ClusterState.Configured;
                            clusterStatus.Summary = "Cluster is configured";
                            break;

                        case ClusterNodeState.Paused:
                        case ClusterNodeState.Off:

                            clusterStatus.State   = ClusterState.Off;
                            clusterStatus.Summary = "Cluster is turned off";
                            break;

                        case ClusterNodeState.NotProvisioned:

                            clusterStatus.State   = ClusterState.NotFound;
                            clusterStatus.Summary = "Cluster is not found.";
                            break;

                        case ClusterNodeState.Unknown:
                        default:

                            clusterStatus.State   = ClusterState.NotFound;
                            clusterStatus.Summary = "Cluster not found";
                            break;
                    }
                }

                if (clusterStatus.State == ClusterState.Off)
                {
                    clusterStatus.Summary = "Cluster is turned off";

                    return clusterStatus;
                }

                return clusterStatus;
            }
        }

        /// <inheritdoc/>
        public override async Task StartClusterAsync()
        {
            // We're going to signal all cluster VMs to start.

            await ConnectAzureAsync();

            await Parallel.ForEachAsync(cluster.Definition.SortedMasterThenWorkerNodes, parallelOptions,
                async (node, cancellationToken) =>
                {
                    var vm = nameToVm[node.Name];

                    await azure.VirtualMachines.StartAsync(resourceGroupName, vm.Name, cancellationToken);
                });
        }

        /// <inheritdoc/>
        public override async Task StopClusterAsync(StopMode stopMode = StopMode.Graceful)
        {
            // We're going to signal all cluster VMs to stop.

            // $todo(jefflill): Note that the fluent SDK doesn't appear to support forced shutdown:
            //
            //      https://github.com/nforgeio/neonKUBE/issues/1546

            await ConnectAzureAsync();

            await Parallel.ForEachAsync(cluster.Definition.SortedMasterThenWorkerNodes, parallelOptions,
                async (node, cancellationToken) =>
                {
                    var vm = nameToVm[node.Name];

                    await azure.VirtualMachines.PowerOffAsync(resourceGroupName, vm.Name, cancellationToken);
                });
        }

        /// <inheritdoc/>
        public override async Task RemoveClusterAsync(bool removeOrphans = false)
        {
            // We just need to delete the cluster resource group and everything within it.

            await ConnectAzureAsync();
            await azure.ResourceGroups.DeleteByNameAsync(resourceGroupName, forceDeletionTypes: "Microsoft.Compute/virtualMachines");
        }
    }
}
