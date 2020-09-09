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
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;
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
        // this placement groups via [AzureOptions.DisableProximityPlacement].
        //
        // The VNET will be configured using the cluster definition's [NetworkOptions],
        // with [NetworkOptions.NodeSubnet] used to configure the subnet.
        // Node IP addresses will be automatically assigned by default, but this
        // can be customized via the cluster definition when necessary.
        //
        // The load balancer will be created using a public IP address to balance
        // inbound traffic across a backend pool including the VMs designated to
        // accept ingress traffic into the cluster.  These nodes are identified 
        // by the presence of a [neonkube.io/node.ingress=true] label which can be
        // set explicitly.  neonKUBE will default to reasonable ingress nodes when
        // necessary.
        //
        // External load balancer traffic can be enabled for specific ports via 
        // [NetworkOptions.IngressRules] which specify two ports: 
        // 
        //      * The external load balancer port
        //      * The node port where Istio is listening and will forward traffic
        //        into the Kubernetes cluster
        //
        // The [NetworkOptions.IngressRules] can also explicitly allow or deny traffic
        // from specific source IP addresses and/or subnets.
        //
        // NOTE: Only TCP connections are supported at this time because Istio
        //       doesn't support UDP, ICMP, etc. at this time.
        //
        // A network security group will be created and assigned to the subnet.
        // This will include ingress rules constructed from [NetworkOptions.IngressRules]
        // and egress rules constructed from [NetworkOptions.EgressAddressRules].
        //
        // Azure VM NICs will be configured with each node's IP address.  We are not
        // currently assigning network security groups to these NICs.
        //
        // VMs are currently based on the Ubuntu-20.04 Server image provided  
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
        // Node instance and disk types and sizes are specified by the 
        // [NodeDefinition.Azure] property.  Instance types are specified
        // using standard Azure names, disk type is an enum and disk sizes
        // are specified via strings including optional [ByteUnits].  Provisioning
        // will need to verify that the requested instance and drive types are
        // actually available in the target Azure region and will also need
        // to map the disk size specified by the user to the closest matching
        // Azure disk size greater than or equal to the requested size.
        //
        // We'll be managing cluster node setup and maintenance remotely via
        // SSH cconnections and the cluster reserves 1000 external load balancer
        // ports (by default) to accomplish this.  When we need an external SSH
        // connection to a specific cluster node, the hosting manager will allocate
        // a reserved port and then add a NAT rule to the load balancer that
        // routes traffic from the external port to SSH port 22 on the target node
        // in addition to adding a rule to the network security group enabling
        // the traffic.  [NetworkOptions.ManagementAddressRules] can be used to
        // restrict where this management traffic may come from.

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
            public AzureVm(SshProxy<NodeDefinition> node, AzureHostingManager hostingManager)
            {
                Covenant.Requires<ArgumentNullException>(hostingManager != null, nameof(hostingManager));

                this.Proxy          = node;
                this.hostingManager = hostingManager;
            }

            /// <summary>
            /// Returns the associated node proxy.
            /// </summary>
            public SshProxy<NodeDefinition> Proxy { get; private set; }

            /// <summary>
            /// Returns the node metadata (AKA its definition).
            /// </summary>
            public NodeDefinition Metadata => Proxy.Metadata;

            /// <summary>
            /// Returns the name of the node as defined in the cluster definition.
            /// </summary>
            public string Name => Proxy.Metadata.Name;

            /// <summary>
            /// Returns the name of the Azure VM for this node.
            /// </summary>
            public string VmName => hostingManager.GetResourceName("vm", Proxy.Name);

            /// <summary>
            /// Returns the IP address of the node.
            /// </summary>
            public string Address => Proxy.Address.ToString();

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
            public bool IsMaster => Proxy.Metadata.Role == NodeRole.Master;

            /// <summary>
            /// Returns <c>true</c> if the node is a worker.
            /// </summary>
            public bool IsWorker => Proxy.Metadata.Role == NodeRole.Worker;

            /// <summary>
            /// The Azure availability set hosting this node.
            /// </summary>
            public string AvailabilitySetName { get; set; }
        }

        /// <summary>
        /// Flags used to customize how the cluster network is updated.
        /// </summary>
        [Flags]
        private enum NetworkOperations
        {
            /// <summary>
            /// Update the cluster's ingress rules.
            /// </summary>
            UpdateIngressRules = 0x0001,

            /// <summary>
            /// Add public SSH NAT rules for every node in the cluster.
            /// These are used by neonKUBE related tools for provisioning, setting up, and
            /// managing clusters.
            /// </summary>
            AddPublicSshRules = 0x0002,

            /// <summary>
            /// Remove all related SSH NAT rules.
            /// </summary>
            RemovePublicSshRules = 0x0004,
        }

        /// <summary>
        /// Describes an Ubuntu image from the Azure marketplace.
        /// </summary>
        private class AzureUbuntuImage
        {
            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="clusterVersion">Specifies the neonKUBE cluster version.</param>
            /// <param name="ubuntuVersion">Specifies the Ubuntu image version.</param>
            /// <param name="ubuntuBuild">Specifies the Ubuntu build.</param>
            /// <param name="vmGen">Specifies the Azure image generation (1 or 2).</param>
            /// <param name="isPrepared">
            /// Pass <c>true</c> for Ubuntu images that have already seen basic
            /// preparation for inclusion into a neonKUBE cluster, or <c>false</c>
            /// for unmodified base Ubuntu images.
            /// <param name="imageRef">Specifies the Azure VM image reference.</param>
            /// </param>
            public AzureUbuntuImage(string clusterVersion, string ubuntuVersion, string ubuntuBuild, int vmGen, bool isPrepared, ImageReference imageRef)
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(clusterVersion), nameof(clusterVersion));
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(ubuntuVersion), nameof(ubuntuVersion));
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(ubuntuBuild), nameof(ubuntuBuild));
                Covenant.Requires<ArgumentException>(vmGen == 1 || vmGen == 2, nameof(vmGen));
                Covenant.Requires<ArgumentNullException>(imageRef != null, nameof(imageRef));

                this.ClusterVersion = clusterVersion;
                this.UbuntuVersion  = ubuntuVersion;
                this.UbuntuBuild    = ubuntuBuild;
                this.VmGen          = vmGen;
                this.IsPrepared     = isPrepared;
                this.ImageRef       = imageRef;
            }

            /// <summary>
            /// Returns the neonKUBE cluster version.
            /// </summary>
            public string ClusterVersion { get; private set; }

            /// <summary>
            /// Returns the Ubuntu version deployed by the image.
            /// </summary>
            public string UbuntuVersion { get; private set; }

            /// <summary>
            /// Returns the Ubuntu build version.
            /// </summary>
            public string UbuntuBuild { get; private set; }

            /// <summary>
            /// <para>
            /// Returns the Azure VM image type.  Gen1 images use the older BIOS boot
            /// mechanism and use IDE to access the disks.  Gen2 images use UEFI
            /// to boot and use SCSI to access the disks.  Gen2 images allow OS
            /// disks creater than 2TiB but do not support disk encryption.  Gen2
            /// VMs will likely run faster as well because they support accelerated
            /// networking.
            /// </para>
            /// <note>
            /// Most VM sizes can deploy using Gen1 or Gen2 images but this is
            /// not always the case.
            /// </note>
            /// </summary>
            public int VmGen { get; private set; }

            /// <summary>
            /// Returns <c>true</c> for Ubuntu images that have already seen basic
            /// preparation for inclusion into a neonKUBE cluster.  This will be
            /// <c>false</c> for unmodified base Ubuntu images.
            /// </summary>
            public bool IsPrepared { get; private set; }

            /// <summary>
            /// Returns the Azure image reference.
            /// </summary>
            public ImageReference ImageRef { get; private set; }
        }

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// The first NSG rule priority to use for ingress rules.
        /// </summary>
        private const int firstIngressNsgRulePriority = 1000;

        /// <summary>
        /// The first NSG rule priority to use for temporary management SSH rules.
        /// </summary>
        private const int firstNeonNsgRulePriority = 2000;

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
        /// Used to tag VMs with the cluster node name.
        /// </summary>
        private static string NodeNameTag = "neonkube.io.node.name";

        /// <summary>
        /// Returns the list of supported Ubuntu images from the Azure Marketplace.
        /// </summary>
        private static IReadOnlyList<AzureUbuntuImage> ubuntuImages;

        /// <summary>
        /// Returns the list of Azure VM size name <see cref="Regex"/> patterns
        /// that match VMs that are known to be <b>incompatible</b> with Gen1 VM images.
        /// </summary>
        private static IReadOnlyList<Regex> gen1VmSizeNotAllowedRegex;

        /// <summary>
        /// Returns the list of Azure VM size name <see cref="Regex"/> patterns
        /// for VMs that are known to be <b>compatible</b> with Gen2 VM images.
        /// </summary>
        private static IReadOnlyList<Regex> gen2VmSizeAllowedRegex;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static AzureHostingManager()
        {
            // IMPORTANT: 
            //
            // This list will need to be updated as new cluster versions
            // are supported.             

            ubuntuImages = new List<AzureUbuntuImage>()
            {
                new AzureUbuntuImage("0.1.0-alpha", "20.04", "20.04.202007290", vmGen: 1, isPrepared: false,
                    new ImageReference()
                    {
                        Publisher = "Canonical",
                        Offer     = "0001-com-ubuntu-server-focal",
                        Sku       = "20_04-lts",
                        Version   = "20.04.202007290"
                    }),

                new AzureUbuntuImage("0.1.0-alpha", "20.04", "20.04.202007290s", vmGen: 2, isPrepared: false,
                    new ImageReference()
                    {
                        Publisher = "Canonical",
                        Offer     = "0001-com-ubuntu-server-focal",
                        Sku       = "20_04-lts-gen2",
                        Version   = "20.04.202007290"
                    })
            }
            .AsReadOnly();

            // IMPORTANT:
            //
            // These lists should be updated periodically as Azure adds new VM sizes
            // that support or don't support Gen1/Gen2 images.
            //
            //      https://docs.microsoft.com/en-us/azure/virtual-machines/windows/generation-2#generation-2-vm-sizes

            gen1VmSizeNotAllowedRegex = new List<Regex>()
            {
                // Mv2-series VMs do not support Gen1 VMs.

                new Regex(@"^Standard_M.*_v2$", RegexOptions.IgnoreCase)
            }
            .AsReadOnly();

            gen2VmSizeAllowedRegex = new List<Regex>
            {
                new Regex(@"^Standard_B", RegexOptions.IgnoreCase),             // B
                new Regex(@"^Standard_DC.*s_v2$", RegexOptions.IgnoreCase),     // DCsv2
                new Regex(@"^Standard_D.*_v2$", RegexOptions.IgnoreCase),       // Dv2
                new Regex(@"^Standard_Ds.*_v2$", RegexOptions.IgnoreCase),      // Dsv3
                new Regex(@"^Standard_D.*a_v4$", RegexOptions.IgnoreCase),      // Dav4
                new Regex(@"^Standard_D.*as_v4$", RegexOptions.IgnoreCase),     // Dasv4
                new Regex(@"^Standard_E.*_v3$", RegexOptions.IgnoreCase),       // Ev3
                new Regex(@"^Standard_E.*as_v4$", RegexOptions.IgnoreCase),     // Easv4
                new Regex(@"^Standard_F.*s_v2$", RegexOptions.IgnoreCase),      // Fsv2
                new Regex(@"^Standard_GS", RegexOptions.IgnoreCase),            // GS
                new Regex(@"^Standard_HB", RegexOptions.IgnoreCase),            // HB
                new Regex(@"^Standard_HC", RegexOptions.IgnoreCase),            // HC
                new Regex(@"^Standard_L.*s$", RegexOptions.IgnoreCase),         // Ls
                new Regex(@"^Standard_L.*s_v2$", RegexOptions.IgnoreCase),      // Lsv2
                new Regex(@"^Standard_M", RegexOptions.IgnoreCase),             // M
                new Regex(@"^Standard_M.*_v2", RegexOptions.IgnoreCase),        // Mv2
                new Regex(@"^Standard_NC.*s_v2$", RegexOptions.IgnoreCase),     // NCv2
                new Regex(@"^Standard_NC.*s_v3$", RegexOptions.IgnoreCase),     // NCv3
                new Regex(@"^Standard_ND.*s$", RegexOptions.IgnoreCase),        // ND
                new Regex(@"^Standard_NV.*s_v3$", RegexOptions.IgnoreCase),     // NVv3
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
        /// Returns the base Azure Ubuntu image to use for the specified neonKUBE cluster version.
        /// </summary>
        /// <param name="clusterVersion">The neonKUBE cluster version.</param>
        /// <param name="vmGen">The Azure VM generation (1 or 2).</param>
        /// <returns>The Azure base image reference.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if no base image can be located.</exception>
        private static ImageReference GetBaseUbuntuImage(string clusterVersion, int vmGen)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(clusterVersion), nameof(clusterVersion));
            Covenant.Requires<ArgumentException>(vmGen == 1 || vmGen == 2, nameof(vmGen));

            var image = ubuntuImages.SingleOrDefault(img => img.ClusterVersion == clusterVersion &&
                                                           !img.IsPrepared &&
                                                           img.VmGen == vmGen);
            if (image == null)
            {
                throw new KeyNotFoundException($"Cannot locate a base Azure Ubuntu image for cluster version [{clusterVersion}].");
            }

            return image.ImageRef;
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

        //---------------------------------------------------------------------
        // Instance members

        private KubeSetupInfo                           setupInfo;
        private ClusterProxy                            cluster;
        private string                                  clusterName;
        private string                                  nodeUsername;
        private string                                  nodePassword;
        private HostingOptions                          hostingOptions;
        private CloudOptions                            cloudOptions;
        private bool                                    prefixResourceNames;
        private AzureHostingOptions                     azureOptions;
        private AzureCredentials                        azureCredentials;
        private NetworkOptions                          networkOptions;
        private string                                  region;
        private string                                  resourceGroup;
        private Dictionary<string, AzureVm>             nameToVm;
        private IAzure                                  azure;

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

        private string                                  publicAddressName;
        private string                                  vnetName;
        private string                                  subnetName;
        private string                                  proximityPlacementGroupName;
        private string                                  loadbalancerName;
        private string                                  loadbalancerFrontendName;
        private string                                  loadbalancerBackendName;
        private string                                  subnetNsgName;

        // These fields hold various Azure components while provisioning is in progress.

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

            this.setupInfo             = setupInfo;
            this.cluster               = cluster;
            this.clusterName           = cluster.Name;
            this.hostingOptions        = cluster.Definition.Hosting;
            this.cloudOptions          = hostingOptions.Cloud;
            this.azureOptions          = hostingOptions.Azure;
            this.cloudOptions          = hostingOptions.Cloud;
            this.networkOptions        = cluster.Definition.Network;
            this.nameToAvailabilitySet = new Dictionary<string, IAvailabilitySet>(StringComparer.InvariantCultureIgnoreCase);
            this.region                = azureOptions.Region;
            this.resourceGroup         = azureOptions.ResourceGroup ?? $"neon-{clusterName}";

            switch (cloudOptions.PrefixResourceNames)
            {
                case TriState.Default:

                    // Default to FALSE for Azure because all resource names
                    // will be scoped to a resource group.

                    this.prefixResourceNames = true;
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

            this.publicAddressName           = GetResourceName("pip", "cluster", true);
            this.vnetName                    = GetResourceName("vnet", "cluster", true);
            this.subnetName                  = GetResourceName("snet", "cluster", true);
            this.proximityPlacementGroupName = GetResourceName("ppg", "cluster", true);
            this.loadbalancerName            = GetResourceName("lbe", "cluster", true);
            this.subnetNsgName               = GetResourceName("nsg", "subnet");
            this.loadbalancerFrontendName    = "ingress";
            this.loadbalancerBackendName     = "ingress";

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

        /// <inheritdoc/>
        public override void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            if (string.IsNullOrEmpty(clusterDefinition.Hosting.Azure.AppId))
            {
                throw new ClusterDefinitionException($"{nameof(AzureHostingOptions)}.{nameof(AzureHostingOptions.AppId)}] must be specified for Azure clusters.");
            }

            if (string.IsNullOrEmpty(clusterDefinition.Hosting.Azure.AppPassword))
            {
                throw new ClusterDefinitionException($"{nameof(AzureHostingOptions)}.{nameof(AzureHostingOptions.AppPassword)}] must be specified for Azure clusters.");
            }

            AssignNodeAddresses(clusterDefinition);
        }

        /// <inheritdoc/>
        public override async Task<bool> ProvisionAsync(bool force, string secureSshPassword, string orgSshPassword = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(secureSshPassword));

            // Update the node credentials.

            this.nodeUsername = KubeConst.SysAdminUsername;
            this.nodePassword = secureSshPassword;

            // Initialize and run the [SetupController].

            var operation = $"Provisioning [{cluster.Definition.Name}] on Azure [{region}/{resourceGroup}]";
            var controller = new SetupController<NodeDefinition>(operation, cluster.Nodes)
            {
                ShowStatus = this.ShowStatus,
                ShowNodeStatus = true,
                MaxParallel = int.MaxValue       // There's no reason to constrain this
            };

            controller.AddGlobalStep("Azure connect", () => ConnectAzure());
            controller.AddGlobalStep("region check", () => VerifyRegionAndVmSizes());
            controller.AddGlobalStep("resource group", () => CreateResourceGroup());
            controller.AddGlobalStep("availability sets", () => CreateAvailabilitySets());
            controller.AddGlobalStep("network security groups", () => CreateNetworkSecurityGroups());
            controller.AddGlobalStep("virtual network", () => CreateVirtualNetwork());
            controller.AddGlobalStep("public address", () => CreatePublicAddress());
            controller.AddGlobalStep("load balancer", () => CreateLoadBalancer());
            controller.AddGlobalStep("listing virtual machines",
                () =>
                {
                    // Update [azureNodes] with any existing Azure nodes and their NICs.
                    // Note that it's possible for VMs that are unrelated to the cluster
                    // to be in the resource group, so we'll have to ignore those.

                    foreach (var vm in azure.VirtualMachines.ListByResourceGroup(resourceGroup))
                    {
                        if (!vm.Tags.TryGetValue(NodeNameTag, out var nodeName))
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

                        azureNode.Vm = vm;
                        azureNode.Nic = vm.GetPrimaryNetworkInterface();
                    }
                },
                quiet: true);
            controller.AddStep("credentials",
                (node, stepDelay) =>
                {
                    // Update the node SSH proxies to use the secure SSH password.

                    node.UpdateCredentials(SshCredentials.FromUserPassword(KubeConst.SysAdminUsername, secureSshPassword));
                },
                quiet: true);
            controller.AddStep("virtual machines", CreateVm);
            controller.AddGlobalStep("ingress/security rules", () => UpdateNetwork(NetworkOperations.UpdateIngressRules | NetworkOperations.AddPublicSshRules));
            controller.AddStep("configure nodes", Configure);

            if (!controller.Run(leaveNodesConnected: false))
            {
                Console.WriteLine("*** One or more Azure provisioning steps failed.");
                return await Task.FromResult(false);
            }

            return await Task.FromResult(true);
        }

        /// <inheritdoc/>
        public override bool CanManageRouter => true;

        /// <inheritdoc/>
        public override async Task UpdatePublicIngressAsync()
        {
            LoadNetworkResources();

            var operations = NetworkOperations.UpdateIngressRules;

            if (loadBalancer.InboundNatRules.Values.Any(rule => rule.Name.StartsWith(ingressRulePrefix, StringComparison.InvariantCultureIgnoreCase)))
            {
                // It looks like SSH NAT rules are enabled so we'll update
                // those as well.

                operations |= NetworkOperations.AddPublicSshRules;
            }

            UpdateNetwork(operations);
            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override async Task EnablePublicSshAsync()
        {
            LoadNetworkResources();
            UpdateNetwork(NetworkOperations.AddPublicSshRules);
            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override async Task DisablePublicSshAsync()
        {
            LoadNetworkResources();
            UpdateNetwork(NetworkOperations.RemovePublicSshRules);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Establishes an Azure connection when not already connected and then loads the network related
        /// Azure resources such as the cluster's public IP address, load balancer and subnet NSG so they 
        /// can be manipulated by methods that manage public access to the cluster. 
        /// </summary>
        private void LoadNetworkResources()
        {
            ConnectAzure();

            if (publicAddress == null)
            {
                publicAddress = azure.PublicIPAddresses.GetByResourceGroup(resourceGroup, publicAddressName);
            }

            if (loadBalancer == null)
            {
                loadBalancer = azure.LoadBalancers.GetByResourceGroup(resourceGroup, loadbalancerName);
            }

            if (subnetNsg == null)
            {
                subnetNsg = azure.NetworkSecurityGroups.GetByResourceGroup(resourceGroup, subnetNsgName);
            }
        }

        /// <inheritdoc/>
        public override (string Address, int Port) GetSshEndpoint(string nodeName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nodeName), nameof(nodeName));

            // Connect to Azure and fetch the load balancer and public address information,
            // if we don't already have them.

            ConnectAzure();

            if (loadBalancer == null)
            {
                loadBalancer = azure.LoadBalancers.GetByResourceGroup(resourceGroup, loadbalancerName);
            }

            if (publicAddress == null)
            {
                publicAddress = azure.PublicIPAddresses.GetByResourceGroup(resourceGroup, publicAddressName);
            }

            // Lookup the SSH NAT rule for the node so we can identify the port.

            var sshNatRuleName = $"{publicSshRulePrefix}{nodeName}";

            if (loadBalancer.InboundNatRules.TryGetValue(sshNatRuleName, out var sshNatRule))
            {
                return (Address: publicAddress.IPAddress, Port: sshNatRule.FrontendPort);
            }
            else
            {
                return (Address: cluster.GetNode(nodeName).Address.ToString(), Port: NetworkPorts.SSH);
            }
        }

        /// <inheritdoc/>
        public override string GetDataDisk(SshProxy<NodeDefinition> node)
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
        /// Verifies that the requested Azure region exists, supports the requested VM sizes,
        /// and that VMs for nodes that specify UltraSSD actually support UltraSSD.  We'll also
        /// verify that the requested VMs have the minimum required number or cores and RAM.
        /// </para>
        /// <para>
        /// This also updates the node labels to match the capabilities of their VMs.
        /// </para>
        /// </summary>
        private void VerifyRegionAndVmSizes()
        {
            var regionName   = cluster.Definition.Hosting.Azure.Region;
            var vmSizes      = azure.VirtualMachines.Sizes.ListByRegion(regionName);
            var nameToVmSize = new Dictionary<string, IVirtualMachineSize>(StringComparer.InvariantCultureIgnoreCase);
            var nameToVmSku  = new Dictionary<string, IComputeSku>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var vmSize in azure.VirtualMachines.Sizes.ListByRegion(regionName))
            {
                nameToVmSize[vmSize.Name] = vmSize;
            }

            foreach (var vmSku in azure.ComputeSkus.ListByRegion(regionName))
            {
                nameToVmSku[vmSku.Name.Value] = vmSku;
            }

            foreach (var node in cluster.Nodes)
            {
                var vmSizeName = node.Metadata.Azure.VmSize;

                if (!nameToVmSize.TryGetValue(vmSizeName, out var vmSize))
                {
                    throw new KubeException($"Node [{node.Name}] requests [{nameof(node.Metadata.Azure.VmSize)}={vmSizeName}].  This is not available in the [{regionName}] Azure region.");
                }

                if (!nameToVmSku.TryGetValue(vmSizeName, out var vmSku))
                {
                    // This should never happen, right?

                    throw new KubeException($"Node [{node.Name}] requests [{nameof(node.Metadata.Azure.VmSize)}={vmSizeName}].  This is not available in the [{regionName}] Azure region.");
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

                if (node.Metadata.Azure.StorageType == AzureStorageType.UltraSSD)
                {
                    if (!vmSku.Capabilities.Any(Capability => Capability.Name == "UltraSSDAvailable" && Capability.Value == "False"))
                    {
                        throw new KubeException($"Node [{node.Name}] requests an UltraSSD disk.  This is not available in the [{regionName}] Azure region and/or the [{vmSize}] VM Size.");
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
            // Create the availbility sets defined for the cluster nodes.

            var existingAvailabilitySets = azure.AvailabilitySets.ListByResourceGroup(resourceGroup);

            foreach (var existingAvailablitySet in existingAvailabilitySets)
            {
                nameToAvailabilitySet.Add(existingAvailablitySet.Name, existingAvailablitySet);
            }

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
                    newSet = (IAvailabilitySet)azure.AvailabilitySets
                        .Define(azureNode.AvailabilitySetName)
                        .WithRegion(region)
                        .WithExistingResourceGroup(resourceGroup)
                        .WithUpdateDomainCount(azureOptions.UpdateDomains)
                        .WithFaultDomainCount(azureOptions.FaultDomains)
                        .Create();
                }
                else
                {
                    newSet = (IAvailabilitySet)azure.AvailabilitySets
                        .Define(azureNode.AvailabilitySetName)
                        .WithRegion(region)
                        .WithExistingResourceGroup(resourceGroup)
                        .WithNewProximityPlacementGroup(proximityPlacementGroupName, ProximityPlacementGroupType.Standard)
                        .WithUpdateDomainCount(azureOptions.UpdateDomains)
                        .WithFaultDomainCount(azureOptions.FaultDomains)
                        .Create();
                }

                nameToAvailabilitySet.Add(azureNode.AvailabilitySetName, newSet);
            }
        }

        /// <summary>
        /// Creates the network security groups.
        /// </summary>
        private void CreateNetworkSecurityGroups()
        {
            var nsgList = azure.NetworkSecurityGroups.ListByResourceGroup(resourceGroup);

            subnetNsg = nsgList.FirstOrDefault(nsg => nsg.Name.Equals(subnetNsgName, StringComparison.InvariantCultureIgnoreCase));

            if (subnetNsg == null)
            {
                // Note that we're going to add rules later.

                subnetNsg = azure.NetworkSecurityGroups
                    .Define(subnetNsgName)
                    .WithRegion(region)
                    .WithExistingResourceGroup(resourceGroup)
                    .Create();
            }
        }

        /// <summary>
        /// Creates the cluster's virtual network.
        /// </summary>
        private void CreateVirtualNetwork()
        {
            var vnetCreator = azure.Networks
                .Define(vnetName)
                .WithRegion(region)
                .WithExistingResourceGroup(resourceGroup)
                .WithAddressSpace(networkOptions.NodeSubnet)
                .DefineSubnet(subnetName)
                    .WithAddressPrefix(networkOptions.NodeSubnet)
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

            vnet = vnetCreator.Create();
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

            clusterAddress = IPAddress.Parse(publicAddress.IPAddress);
        }

        /// <summary>
        /// Create the cluster's external load balancer.
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
            // and then delete that rule straight away.

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

            loadBalancer = loadBalancer.Update()
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
            var azureNode = nameToVm[node.Name];

            if (azureNode.Vm != null)
            {
                // The VM already exists.

                return;
            }

            node.Status = "create: NIC";

            azureNode.Nic = azure.NetworkInterfaces
                .Define(GetResourceName("nic",azureNode.Proxy.Name))
                .WithRegion(azureOptions.Region)
                .WithExistingResourceGroup(resourceGroup)
                .WithExistingPrimaryNetwork(vnet)
                .WithSubnet(subnetName)
                .WithPrimaryPrivateIPAddressStatic(azureNode.Address)
                .Create();

            node.Status = "create: virtual machine";

            var azureNodeOptions = azureNode.Proxy.Metadata.Azure;
            var azureStorageType = StorageAccountTypes.StandardSSDLRS;

            switch (azureNodeOptions.StorageType)
            {
                case AzureStorageType.PremiumSSD:

                    azureStorageType = StorageAccountTypes.PremiumLRS;
                    break;

                case AzureStorageType.StandardHDD:

                    azureStorageType = StorageAccountTypes.StandardLRS;
                    break;

                case AzureStorageType.StandardSSD:

                    azureStorageType = StorageAccountTypes.StandardSSDLRS;
                    break;

                case AzureStorageType.UltraSSD:

                    azureStorageType = StorageAccountTypes.UltraSSDLRS;
                    break;

                default:

                    throw new NotImplementedException();
            }

            // We're going to favor Gen2 images if the VM size supports that and the
            // user has not overridden the generation for the node.

            var vmGen = azureNodeOptions.VmGen;

            if (!vmGen.HasValue)
            {
                foreach (var regex in gen2VmSizeAllowedRegex)
                {
                    if (regex.Match(node.Metadata.Azure.VmSize).Success)
                    {
                        vmGen = 2;  // Gen2 is supported 
                        break;
                    }
                }

                if (!vmGen.HasValue)
                {
                    vmGen = 1;      // Gen2 is not supported 
                }
            }

            var imageRef = GetBaseUbuntuImage(cluster.Definition.ClusterVersion, vmGen: vmGen.Value);

            azureNode.Vm = azure.VirtualMachines
                .Define(azureNode.VmName)
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
                .WithExistingAvailabilitySet(nameToAvailabilitySet[azureNode.AvailabilitySetName])
                .WithTag(NodeNameTag, azureNode.Metadata.Name)
                .Create();
        }

        /// <summary>
        /// Performs some basic node initialization.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="stepDelay">The step delay.</param>
        private void Configure(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            node.WaitForBoot();

            node.Status = "install: packages";

            node.SudoCommand("apt-get install -yq unzip");
        }

        /// <summary>
        /// Updates the load balancer and related security rules based on the operation flags passed.
        /// </summary>
        /// <param name="operations">Flags that control how the load balancer and related security rules are updated.</param>
        private void UpdateNetwork(NetworkOperations operations)
        {
            if ((operations & NetworkOperations.UpdateIngressRules) != 0)
            {
                UpdateNetworkIngress();
            }

            if ((operations & NetworkOperations.AddPublicSshRules) != 0)
            {
                AddNeonSshRules();
            }

            if ((operations & NetworkOperations.RemovePublicSshRules) != 0)
            {
                RemoveNeonSshRules();
            }
        }

        /// <summary>
        /// Updates the load balancer and network security rules to match the current cluster definition.
        /// This also ensures that some nodes are marked for ingress when the cluster has one or more
        /// ingress rules and that nodes marked for ingress are in the load balancer's backend pool.
        /// </summary>
        private void UpdateNetworkIngress()
        {
            var loadBalancerUpdater = loadBalancer.Update();
            var subnetNsgUpdater    = subnetNsg.Update();

            //-----------------------------------------------------------------
            // Backend pool:

            // Ensure that we actually have some nodes marked for ingress when the cluster
            // defines some ingress rules and then ensure that the load balancer's backend
            // pool includes those node VMs.

            KubeHelper.EnsureIngressNodes(cluster.Definition);

            // Rebuild the backend pool. 
            //
            // Note that we're going to add these VMs to the backend set one at a time
            // because the API only works when the VMs being added in a batch are in 
            // the same availability set.  Masters and workers are located within
            // different sets by default.

            var backendUpdater = loadBalancerUpdater.UpdateBackend(loadbalancerBackendName);

            backendUpdater.WithoutExistingVirtualMachines();

            foreach (var ingressNode in nameToVm.Values.Where(node => node.Metadata.Ingress))
            {
                backendUpdater.WithExistingVirtualMachines(new IHasNetworkInterfaces[] { ingressNode.Vm });
            }

            loadBalancerUpdater = backendUpdater.Parent();
            loadBalancer        = loadBalancerUpdater.Apply();
            loadBalancerUpdater = loadBalancer.Update();

            //-----------------------------------------------------------------
            // Ingress load balancing rules

            // Remove all existing cluster ingress rules.

            foreach (var rule in loadBalancer.LoadBalancingRules.Values
                .Where(r => r.Name.StartsWith(ingressRulePrefix, StringComparison.InvariantCultureIgnoreCase)))
            {
                loadBalancerUpdater.WithoutLoadBalancingRule(rule.Name);
            }

            foreach (var rule in subnetNsg.SecurityRules.Values
                .Where(r => r.Name.StartsWith(ingressRulePrefix, StringComparison.InvariantCultureIgnoreCase)))
            {
                subnetNsgUpdater.WithoutRule(rule.Name);
            }

            // We also need to remove any existing load balancer ingress related health probes.  We'll 
            // recreate these as necessary below.

            foreach (var probe in loadBalancer.HttpProbes.Values
                .Where(p => p.Name.StartsWith(ingressRulePrefix, StringComparison.InvariantCultureIgnoreCase)))
            {
                loadBalancerUpdater.WithoutProbe(probe.Name);
            }

            foreach (var probe in loadBalancer.HttpsProbes.Values
                .Where(p => p.Name.StartsWith(ingressRulePrefix, StringComparison.InvariantCultureIgnoreCase)))
            {
                loadBalancerUpdater.WithoutProbe(probe.Name);
            }

            foreach (var probe in loadBalancer.TcpProbes.Values
                .Where(p => p.Name.StartsWith(ingressRulePrefix, StringComparison.InvariantCultureIgnoreCase)))
            {
                loadBalancerUpdater.WithoutProbe(probe.Name);
            }

            // Add the load balancer ingress rules and probes.

            foreach (var ingressRule in networkOptions.IngressRules)
            {
                var probeName = $"{ingressRulePrefix}{ingressRule.Name}";

                loadBalancerUpdater.DefineTcpProbe(probeName)
                    .WithPort(ingressRule.NodePort)
                    .Attach();

                var ruleName   = $"{ingressRulePrefix}{ingressRule.Name}";
                var tcpTimeout = Math.Min(Math.Max(4, ingressRule.TcpIdleTimeoutMinutes), 30);  // Azure allowed timeout range is [4..30] minutes

                loadBalancerUpdater.DefineLoadBalancingRule(ruleName)
                    .WithProtocol(ToTransportProtocol(ingressRule.Protocol))
                    .FromExistingPublicIPAddress(publicAddress)
                    .FromFrontendPort(ingressRule.ExternalPort)
                    .ToBackend(loadbalancerBackendName)
                    .ToBackendPort(ingressRule.NodePort)
                    .WithProbe(probeName)
                    .WithIdleTimeoutInMinutes(tcpTimeout)
                    .Attach();
            }

            loadBalancer        = loadBalancerUpdater.Apply();
            loadBalancerUpdater = loadBalancer.Update();

            // We need to set [EnableTcpReset] for the load balancer rules separately because
            // the Fluent API doesn't support the property yet.

            foreach (var ingressRule in networkOptions.IngressRules)
            {
                var ruleName = $"{ingressRulePrefix}{ingressRule.Name}";
                var lbRule   = loadBalancer.Inner.LoadBalancingRules.Single(rule => rule.Name.Equals(ruleName, StringComparison.InvariantCultureIgnoreCase));

                lbRule.EnableTcpReset = ingressRule.IdleTcpReset;
            }

            loadBalancer        = loadBalancerUpdater.Apply();
            loadBalancerUpdater = loadBalancer.Update();

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

            foreach (var ingressRule in networkOptions.IngressRules)
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

            loadBalancer = loadBalancerUpdater.Apply();
            subnetNsg    = subnetNsgUpdater.Apply();
        }

        /// <summary>
        /// Adds public SSH NAT rules for every node in the cluster.
        /// These are used by neonKUBE tools for provisioning, setting up, and
        /// managing clusters.  Related NSG rules will also be created. 
        /// </summary>
        private void AddNeonSshRules()
        {
            var loadBalancerUpdater = loadBalancer.Update();
            var subnetNsgUpdater    = subnetNsg.Update();

            // Remove all existing load balancer public SSH related NAT rules.

            foreach (var rule in loadBalancer.LoadBalancingRules.Values
                .Where(r => r.Name.StartsWith(publicSshRulePrefix, StringComparison.InvariantCultureIgnoreCase)))
            {
                loadBalancerUpdater.WithoutLoadBalancingRule(rule.Name);
            }

            foreach (var rule in subnetNsg.SecurityRules.Values
                .Where(r => r.Name.StartsWith(publicSshRulePrefix, StringComparison.InvariantCultureIgnoreCase)))
            {
                subnetNsgUpdater.WithoutRule(rule.Name);
            }

            // Assign unique port to each node that will be used to NAT external
            // SSH traffic to the node.

            var nextPort = networkOptions.FirstSshManagementPort;

            foreach (var azureNode in cluster.Definition.SortedMasterThenWorkerNodes)
            {
                azureNode.PublicSshEndpoint = new IPEndPoint(clusterAddress, nextPort++);
            }

            loadBalancer        = loadBalancerUpdater.Apply();
            loadBalancerUpdater = loadBalancer.Update();

            // Add the SSH NAT rules for each node.  Note that we need to do this in two steps:
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
                    .FromFrontendPort(azureNode.Metadata.PublicSshEndpoint.Port)
                    .ToBackendPort(NetworkPorts.SSH)
                    .WithIdleTimeoutInMinutes(30)       // Maximum Azure idle timeout
                    .Attach();
            }

            loadBalancer        = loadBalancerUpdater.Apply();
            loadBalancerUpdater = loadBalancer.Update();

            // We need to set [EnableTcpReset] for the load balancer rules separately because
            // the Fluent API doesn't support the property yet.

            foreach (var azureNode in SortedMasterThenWorkerNodes)
            {
                var ruleName = $"{publicSshRulePrefix}{azureNode.Name}";
                var natRule  = loadBalancer.Inner.InboundNatRules.Single(rule => rule.Name.Equals(ruleName, StringComparison.InvariantCultureIgnoreCase));

                natRule.EnableTcpReset = true;
            }

            loadBalancer        = loadBalancerUpdater.Apply();
            loadBalancerUpdater = loadBalancer.Update();

            foreach (var azureNode in SortedMasterThenWorkerNodes)
            {
                var ruleName = $"{publicSshRulePrefix}{azureNode.Name}";

                azureNode.Nic = azureNode.Nic.Update()
                    .WithExistingLoadBalancerBackend(loadBalancer, loadbalancerBackendName)
                    .WithExistingLoadBalancerInboundNatRule(loadBalancer, ruleName)
                    .Apply();
            }

            // Add the NSG rules so that the public SSH NAT rules will actually work.
            //
            // To keep things simple, we're going to generate a separate rule for each source address
            // restriction.  In theory, we could have tried collecting allow and deny rules 
            // together to reduce the number of rules but that doesn't seem worth the trouble. 
            // This would be possible because NSGs rules allow a comma separated list of IP addresses
            // or subnets to be specified.
            //
            // We may need to revisit this if we approach Azure rule count limits (currently 1000
            // rules per NSG).  That would also be a good time to support port ranges as well.

            var priority = firstNeonNsgRulePriority;

            foreach (var azureNode in SortedMasterThenWorkerNodes)
            {
                if (networkOptions.SshAddressRules == null || networkOptions.SshAddressRules.Count == 0)
                {
                    // Default to allowing all source addresses when no address rules are specified.

                    var ruleName = $"{publicSshRulePrefix}{azureNode.Name}";

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

                    foreach (var addressRule in networkOptions.SshAddressRules)
                    {
                        var multipleAddresses = networkOptions.SshAddressRules.Count > 1;
                        var ruleName          = multipleAddresses ? $"{publicSshRulePrefix}{azureNode.Name}-{addressRuleIndex++}"
                                                                  : $"{publicSshRulePrefix}{azureNode.Name}";
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
            }

            // Apply the updates.

            loadBalancer = loadBalancerUpdater.Apply();
            subnetNsg    = subnetNsgUpdater.Apply();
        }

        /// <summary>
        /// Removes public SSH NAT rules for every node in the cluster.
        /// These are used by neonKUBE related tools for provisioning, setting up, and
        /// managing clusters.  Related NSG rules will also be removed. 
        /// </summary>
        private void RemoveNeonSshRules()
        {
            var loadBalancerUpdater = loadBalancer.Update();
            var subnetNsgUpdater    = subnetNsg.Update();

            // Remove all existing load balancer public SSH related NAT rules.

            foreach (var lbRule in loadBalancer.LoadBalancingRules.Values
                .Where(r => r.Name.StartsWith(publicSshRulePrefix, StringComparison.InvariantCultureIgnoreCase)))
            {
                loadBalancerUpdater.WithoutLoadBalancingRule(lbRule.Name);
            }

            foreach (var rule in subnetNsg.SecurityRules.Values
                .Where(r => r.Name.StartsWith(publicSshRulePrefix, StringComparison.InvariantCultureIgnoreCase)))
            {
                subnetNsgUpdater.WithoutRule(rule.Name);
            }

            loadBalancer        = loadBalancerUpdater.Apply();
            loadBalancerUpdater = loadBalancer.Update();

            // Remove all of the SSH NAT related NSG rules.

            foreach (var nsgRule in subnetNsg.SecurityRules.Values)
            {
                subnetNsgUpdater.WithoutRule(nsgRule.Name);
            }

            // Apply the changes.

            loadBalancer = loadBalancerUpdater.Apply();
            subnetNsg    = subnetNsgUpdater.Apply();
        }
    }
}
