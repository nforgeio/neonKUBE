//-----------------------------------------------------------------------------
// FILE:	    AwsHostingManager.cs
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

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;
using Neon.Time;

using Amazon;
using Amazon.Runtime;
using Amazon.EC2;
using Amazon.EC2.Model;

namespace Neon.Kube
{
    /// <summary>
    /// Manages cluster provisioning on Amazon Web Services.
    /// </summary>
    [HostingProvider(HostingEnvironment.Aws)]
    public class AwsHostingManager : HostingManager
    {
        //---------------------------------------------------------------------
        // IMPLENTATION NOTE:
        //
        // A neonKUBE AWS cluster will require provisioning these things:
        //
        //      * VPC (virtual private cloud, equivilant to an Azure VNET)
        //      * Instances & EC2 volumes
        //      * Load balancer with public IP
        //
        // In the future, we may relax the public load balancer requirement so
        // that virtual air-gapped clusters can be supported (more on that below).
        //
        // Nodes will be deployed in two AWS availability sets, one set for the         <<< EDIT THIS PARAGRAPH!
        // masters and the other one for the workers.  We're doing this to ensure
        // that there will always be a quorum of masters available during planned
        // Azure maintenance.
        //
        // By default, we're also going to create an Azure proximity placement group    <<< EDIT THIS PARAGRAPH!
        // for the cluster and then add both the master and worker availability sets
        // to the proximity group.  This ensures the shortest possible network latency
        // between all of the cluster nodes but with the increased chance that Azure
        // won't be able to satisfy the deployment constraints.  The user can disable
        // this placement groups via [AzureOptions.DisableProximityPlacement].
        //
        // The VPC will be configured using the cluster definition's [NetworkOptions],
        // with [NetworkOptions.NodeSubnet] used to configure the subnet.
        // Node IP addresses will be automatically assigned by default, but this
        // can be customized via the cluster definition when necessary.
        //
        // The load balancer will be created using a public IP address to balance
        // inbound traffic across a backend pool including the instances designated 
        // to accept ingress traffic into the cluster.  These nodes are identified 
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
        // A network ACL will be created and assigned to the subnet.  This will 
        // include ingress rules constructed from [NetworkOptions.IngressRules]
        // and egress rules constructed from [NetworkOptions.EgressAddressRules].
        //
        // AWS instance NICs will be configured with each node's IP address.
        //
        // VMs are currently based on the Ubuntu-20.04 Server AMIs published to the
        // AWS regions by Canonical.  Note that AWS VM images work differently from
        // Azure.  Azure images automatically exist in all of MSFT's regions and
        // each image has a unique ID that is the same across these regions.
        //
        // AWS AMIs need to be explicitly published to all regions and the same
        // image will have different IDs in different regions.  This appears to
        // be the case for AWS Marketplace images as well.  This means that we'll
        // need to query for images within the region where we're deploying the
        // cluster (which is a bit of a pain).  AWS also appears to require that
        // the user "subscribe" to marketplace images via the portal before the
        // image can be used.
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
        // NOTE: We're not going to use the base Canonical image from the AWS 
        //       Marketplace because marketplace images cannot be copied and
        //       we're going to need to do that when we make our own marketplace
        //       image.
        //
        // Node instance and disk types and sizes are specified by the 
        // [NodeDefinition.Aws] property.  Instance types are specified
        // using standard AWS names, disk type is an enum and disk sizes
        // are specified via strings including optional [ByteUnits].  Provisioning
        // will need to verify that the requested instance and drive types are
        // actually available in the target AWS region.
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

        /// <summary>
        /// Relates cluster node information to an AWS VM instance.
        /// </summary>
        private class AwsInstance
        {
            private AwsHostingManager hostingManager;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="node">The associated node proxy.</param>
            /// <param name="hostingManager">The parent hosting manager.</param>
            public AwsInstance(SshProxy<NodeDefinition> node, AwsHostingManager hostingManager)
            {
                Covenant.Requires<ArgumentNullException>(hostingManager != null, nameof(hostingManager));

                this.Proxy = node;
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
            /// Returns AWS instance information for the node.
            /// </summary>
            public Instance Instance { get; set; }

            /// <summary>
            /// Returns the name of the AWS instance VM for this node.
            /// </summary>
            public string InstanceName => hostingManager.GetResourceName("vm", Proxy.Name);

            /// <summary>
            /// Returns the IP address of the node.
            /// </summary>
            public string Address => Proxy.Address.ToString();

            /// <summary>
            /// Returns <c>true</c> if the node is a master.
            /// </summary>
            public bool IsMaster => Proxy.Metadata.Role == NodeRole.Master;

            /// <summary>
            /// Returns <c>true</c> if the node is a worker.
            /// </summary>
            public bool IsWorker => Proxy.Metadata.Role == NodeRole.Worker;
        }

        /// <summary>
        /// Describes an Ubuntu ami from the AWS Marketplace.
        /// </summary>
        private class AwsUbuntuImage
        {
            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="clusterVersion">Specifies the neonKUBE cluster version.</param>
            /// <param name="ubuntuVersion">Specifies the Ubuntu image version.</param>
            /// <param name="ubuntuBuild">Specifies the Ubuntu build.</param>
            /// <param name="isPrepared">
            /// Pass <c>true</c> for Ubuntu images that have already seen basic
            /// preparation for inclusion into a neonKUBE cluster, or <c>false</c>
            /// for unmodified base Ubuntu images.
            /// </param>
            public AwsUbuntuImage(string clusterVersion, string ubuntuVersion, string ubuntuBuild, bool isPrepared)
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(clusterVersion), nameof(clusterVersion));
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(ubuntuVersion), nameof(ubuntuVersion));
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(ubuntuBuild), nameof(ubuntuBuild));

                this.ClusterVersion = clusterVersion;
                this.UbuntuVersion  = ubuntuVersion;
                this.UbuntuBuild    = ubuntuBuild;
                this.IsPrepared     = isPrepared;
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
            /// Returns <c>true</c> for Ubuntu images that have already seen basic
            /// preparation for inclusion into a neonKUBE cluster.  This will be
            /// <c>false</c> for unmodified base Ubuntu images.
            /// </summary>
            public bool IsPrepared { get; private set; }
        }

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Specifies the ID to use when querying for Canonical images 
        /// </summary>
        private const string canonicalOwnerId = "099720109477";

        /// <summary>
        /// String used to prefix AWS resource tags.
        /// </summary>
        private const string neonTagPrefix = "NEON";

        /// <summary>
        /// Returns the list of supported Ubuntu images from the AWS Marketplace.
        /// </summary>
        private static IReadOnlyList<AwsUbuntuImage> ubuntuImages;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static AwsHostingManager()
        {
            ubuntuImages = new List<AwsUbuntuImage>()
            {
                new AwsUbuntuImage("0.1.0-alpha", "20.04", "20.04.20200729", isPrepared: false)
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

        //---------------------------------------------------------------------
        // Instance members

        private KubeSetupInfo           setupInfo;
        private ClusterProxy            cluster;
        private string                  clusterName;
        private HostingOptions          hostingOptions;
        private CloudOptions            cloudOptions;
        private AwsHostingOptions       awsOptions;
        private NetworkOptions          networkOptions;
        private BasicAWSCredentials     awsCredentials;
        private string                  region;
        private string                  resourceGroup;
        private Region                  awsRegion;
        private RegionEndpoint          regionEndpoint;
        private AmazonEC2Client         ec2;
        private string                  ami;
        private Vpc                     vpc;
        private Subnet                  subnet;

        /// <summary>
        /// Table mapping a cluster node name to its AWS VM instance information.
        /// </summary>
        private Dictionary<string, AwsInstance> nameToInstance;

        /// <summary>
        /// Creates an instance that is only capable of validating the hosting
        /// related options in the cluster definition.
        /// </summary>
        public AwsHostingManager()
        {
        }

        /// <summary>
        /// Creates an instance that is capable of provisioning a cluster on AWS.
        /// </summary>
        /// <param name="cluster">The cluster being managed.</param>
        /// <param name="setupInfo">Specifies the cluster setup information.</param>
        /// <param name="logFolder">
        /// The folder where log files are to be written, otherwise or <c>null</c> or 
        /// empty if logging is disabled.
        /// </param>
        public AwsHostingManager(ClusterProxy cluster, KubeSetupInfo setupInfo, string logFolder = null)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null, nameof(cluster));
            Covenant.Requires<ArgumentNullException>(setupInfo != null, nameof(setupInfo));

            cluster.HostingManager = this;

            this.setupInfo      = setupInfo;
            this.cluster        = cluster;
            this.clusterName    = cluster.Name;
            this.hostingOptions = cluster.Definition.Hosting;
            this.cloudOptions   = hostingOptions.Cloud;
            this.awsOptions     = hostingOptions.Aws;
            this.networkOptions = cluster.Definition.Network;
            this.region         = awsOptions.Region;
            this.resourceGroup  = awsOptions.ResourceGroup;

            // Initialize the instance/node mapping dictionary and also ensure
            // that each node has reasonable AWS node options.

            this.nameToInstance = new Dictionary<string, AwsInstance>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var node in cluster.Nodes)
            {
                nameToInstance.Add(node.Name, new AwsInstance(node, this));

                if (node.Metadata.Aws == null)
                {
                    node.Metadata.Aws = new AwsNodeOptions();
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

            ec2?.Dispose();
            ec2 = null;
        }

        /// <summary>
        /// Indicates when an AWS connection is established.
        /// </summary>
        private bool isConnected => ec2 != null;

        /// <summary>
        /// Enumerates the cluster nodes in no particular order.
        /// </summary>
        private IEnumerable<AwsInstance> Nodes => nameToInstance.Values;

        /// <summary>
        /// Enumerates the cluster nodes in ascending order by name.
        /// </summary>
        private IEnumerable<AwsInstance> SortedNodes => Nodes.OrderBy(node => node.Name, StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Enumerates the cluster master nodes in no particular order.
        /// </summary>
        private IEnumerable<AwsInstance> MasterNodes => Nodes.Where(node => node.IsMaster);

        /// <summary>
        /// Enumerates the cluster master nodes in ascending order by name.
        /// </summary>
        private IEnumerable<AwsInstance> SortedMasterNodes => Nodes.Where(node => node.IsMaster).OrderBy(node => node.Name, StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Enumerates the cluster worker nodes in no particular order.
        /// </summary>
        private IEnumerable<AwsInstance> WorkerNodes => Nodes.Where(node => node.IsMaster);

        /// <summary>
        /// Enumerates the cluster worker nodes in ascending order by name.
        /// </summary>
        private IEnumerable<AwsInstance> SorteWorkerNodes => Nodes.Where(node => node.IsWorker).OrderBy(node => node.Name, StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Enumerates the cluster worker nodes in ascending order by name followed by the sorted worker nodes.
        /// </summary>
        private IEnumerable<AwsInstance> SortedMasterThenWorkerNodes => SortedMasterNodes.Union(SorteWorkerNodes);

        /// <summary>
        /// <para>
        /// Returns the name to use for a cluster related resource based on the standard AWS resource type
        /// suffix, the cluster name (if enabled) and the base resource name.  This is based on AWS tagging
        /// best practices:
        /// </para>
        /// <para>
        /// <a href="https://docs.aws.amazon.com/general/latest/gr/aws_tagging.html">AWS Tagging Best Practices</a>
        /// </para>
        /// </summary>
        /// <param name="resourceTypeSuffix">The AWS resource type prefix (like "vm" for instance or "vpc" for virtual private cloud).</param>
        /// <param name="resourceName">The base resource name.</param>
        /// <param name="omitResourceNameWhenPrefixed">Optionall omit <paramref name="resourceName"/> when resource names include the cluster name.</param>
        /// <returns>The full resource name.</returns>
        private string GetResourceName(string resourceTypeSuffix, string resourceName, bool omitResourceNameWhenPrefixed = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(resourceTypeSuffix), nameof(resourceTypeSuffix));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(resourceName), nameof(resourceName));

            if (cloudOptions.PrefixResourceNames)
            {
                if (omitResourceNameWhenPrefixed)
                {
                    return $"{clusterName}.{resourceTypeSuffix}";
                }
                else
                {
                    return $"{clusterName}-{resourceName}.{resourceTypeSuffix}";
                }
            }
            else
            {
                return $"{resourceName}.{resourceTypeSuffix}";
            }
        }

        /// <summary>
        /// Creates a tag specification for a resource including the resource name, cluster details,
        /// as well as optional tags.
        /// </summary>
        /// <param name="name">The resource name.</param>
        /// <param name="resourceType">The fully qualified resource type.</param>
        /// <param name="tags">The optional tags.</param>
        /// <returns>The <see cref="TagSpecification"/> list with a single element.</returns>
        private List<TagSpecification> GetTagSpecifications(string name, ResourceType resourceType, params Tag[] tags)
        {
            var tagList = new List<Tag>();

            tagList.Add(new Tag("Name", name));
            tagList.Add(new Tag($"{neonTagPrefix}:ClusterName", clusterName));
            tagList.Add(new Tag($"{neonTagPrefix}:Environment", NeonHelper.EnumToString(cluster.Definition.Environment)));

            foreach (var tag in tags)
            {
                tagList.Add(tag);
            }

            return new List<TagSpecification>()
            {
                new TagSpecification()
                {
                    ResourceType = resourceType,
                    Tags         = tagList
                }
            };
        }

        /// <inheritdoc/>
        public override void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            if (string.IsNullOrEmpty(clusterDefinition.Hosting.Aws.AccessKeyId))
            {
                throw new ClusterDefinitionException($"{nameof(AwsHostingOptions)}.{nameof(AwsHostingOptions.AccessKeyId)}] must be specified for AWS clusters.");
            }

            if (string.IsNullOrEmpty(clusterDefinition.Hosting.Aws.SecretAccessKey))
            {
                throw new ClusterDefinitionException($"{nameof(AwsHostingOptions)}.{nameof(AwsHostingOptions.SecretAccessKey)}] must be specified for AWS clusters.");
            }

            AssignNodeAddresses(clusterDefinition);
        }

        /// <inheritdoc/>
        public override async Task<bool> ProvisionAsync(bool force, string secureSshPassword, string orgSshPassword = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(secureSshPassword));

            var operation  = $"Provisioning [{cluster.Definition.Name}] on AWS [{region}/{resourceGroup}]";
            var controller = new SetupController<NodeDefinition>(operation, cluster.Nodes)
            {
                ShowStatus     = this.ShowStatus,
                ShowNodeStatus = true,
                MaxParallel    = int.MaxValue       // There's no reason to constrain this
            };

            controller.AddGlobalStep("AWS connect", ConnectAwsAsync);
            controller.AddGlobalStep("region check", () => VerifyRegionAndInstanceTypesAsync());
            controller.AddGlobalStep("locate ami", LocateAmiAsync);
            controller.AddGlobalStep("create vpc/subnet", CreateVpcSubnetAsync);
            controller.AddStep("create node instances", CreateInstanceAsync);

            if (!controller.Run(leaveNodesConnected: false))
            {
                Console.WriteLine("*** One or more AWS provisioning steps failed.");
                return await Task.FromResult(false);
            }

            return await Task.FromResult(true);
        }

        /// <inheritdoc/>
        public override bool CanManageRouter => true;

        /// <inheritdoc/>
        public override async Task UpdatePublicIngressAsync()
        {
            // $todo(jefflil): Implement this

            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override async Task EnablePublicSshAsync()
        {
            // $todo(jefflil): Implement this

            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override async Task DisablePublicSshAsync()
        {
            // $todo(jefflil): Implement this

            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override (string Address, int Port) GetSshEndpoint(string nodeName)
        {
            return (Address: cluster.GetNode(nodeName).Address.ToString(), Port: NetworkPorts.SSH);
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
        /// Establishes the necessary client connections to AWS and validates the credentials,
        /// when a connection has not been established yet.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task ConnectAwsAsync()
        {
            if (isConnected)
            {
                return;
            }

            // Initialize the credentials and clients and retrieve the list of
            // AWS regions to verify that we can connect and perform operations
            // as well as to verify the target region.  We'll then create new
            // client(s) for the target region.

            awsCredentials = new BasicAWSCredentials(awsOptions.AccessKeyId, awsOptions.SecretAccessKey);

            using (var ec2 = new AmazonEC2Client(awsCredentials, RegionEndpoint.USEast1))
            {
                var awsRegions = (await ec2.DescribeRegionsAsync()).Regions;

                awsRegion = awsRegions.SingleOrDefault(r => r.RegionName.Equals(region, StringComparison.InvariantCultureIgnoreCase));

                if (awsRegion == null)
                {
                    throw new KubeException($"AWS region [{region}] does not exist or is not available to your AWS account.");
                }
            }

            regionEndpoint = RegionEndpoint.GetBySystemName(region);
            ec2            = new AmazonEC2Client(awsCredentials, regionEndpoint);
        }

        /// <summary>
        /// <para>
        /// Verifies that the requested AWS region exists, supports the requested VM sizes,
        /// and that VMs for nodes that specify UltraSSD actually support UltraSSD.  We'll also
        /// verify that the requested VMs have the minimum required number or cores and RAM.
        /// </para>
        /// <para>
        /// This also updates the node labels to match the capabilities of their VMs.
        /// </para>
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task VerifyRegionAndInstanceTypesAsync()
        {
            var regionName             = awsOptions.Region;
            var nameToInstanceTypeInfo = new Dictionary<string, InstanceTypeInfo>(StringComparer.InvariantCultureIgnoreCase);
            var instanceTypePaginator  = ec2.Paginators.DescribeInstanceTypes(new DescribeInstanceTypesRequest());

            await foreach (var instanceTypeInfo in instanceTypePaginator.InstanceTypes)
            {
                nameToInstanceTypeInfo[instanceTypeInfo.InstanceType] = instanceTypeInfo;
            }

            foreach (var node in cluster.Nodes)
            {
                var instanceType = node.Metadata.Aws.InstanceType;

                if (!nameToInstanceTypeInfo.TryGetValue(instanceType, out var instanceTypeInfo))
                {
                    throw new KubeException($"Node [{node.Name}] requests [{nameof(node.Metadata.Aws.InstanceType)}={instanceType}].  This is not available in the [{regionName}] AWS region.");
                }

                if (!instanceTypeInfo.ProcessorInfo.SupportedArchitectures.Any(architecture => architecture == "x86_64"))
                {
                    throw new KubeException($"Node [{node.Name}] requests [{nameof(node.Metadata.Aws.InstanceType)}={instanceType}] which is not supported due to not being x86_64.");
                }

                switch (node.Metadata.Role)
                {
                    case NodeRole.Master:

                        if (instanceTypeInfo.VCpuInfo.DefaultVCpus < KubeConst.MinMasterCores)
                        {
                            throw new KubeException($"Master node [{node.Name}] requests [{nameof(node.Metadata.Aws.InstanceType)}={instanceType}] with [Cores={instanceTypeInfo.VCpuInfo.DefaultVCpus} MiB] which is lower than the required [{KubeConst.MinMasterCores}] cores.]");
                        }

                        if (instanceTypeInfo.MemoryInfo.SizeInMiB < KubeConst.MinMasterRamMiB)
                        {
                            throw new KubeException($"Master node [{node.Name}] requests [{nameof(node.Metadata.Aws.InstanceType)}={instanceType}] with [RAM={instanceTypeInfo.MemoryInfo.SizeInMiB} MiB] which is lower than the required [{KubeConst.MinMasterRamMiB} MiB].]");
                        }
                        break;

                    case NodeRole.Worker:

                        if (instanceTypeInfo.VCpuInfo.DefaultVCpus < KubeConst.MinWorkerCores)
                        {
                            throw new KubeException($"Worker node [{node.Name}] requests [{nameof(node.Metadata.Aws.InstanceType)}={instanceType}] with [Cores={instanceTypeInfo.VCpuInfo.DefaultVCpus} MiB] which is lower than the required [{KubeConst.MinWorkerCores}] cores.]");
                        }

                        if (instanceTypeInfo.MemoryInfo.SizeInMiB < KubeConst.MinWorkerRamMiB)
                        {
                            throw new KubeException($"Worker node [{node.Name}] requests [{nameof(node.Metadata.Aws.InstanceType)}={instanceType}] with [RAM={instanceTypeInfo.MemoryInfo.SizeInMiB} MiB] which is lower than the required [{KubeConst.MinWorkerRamMiB} MiB].]");
                        }
                        break;

                    default:

                        throw new NotImplementedException();
                }

                // Update the node labels to match the actual VM capabilities.

                node.Metadata.Labels.ComputeCores     = instanceTypeInfo.VCpuInfo.DefaultVCpus;
                node.Metadata.Labels.ComputeRam       = (int)instanceTypeInfo.MemoryInfo.SizeInMiB;

                node.Metadata.Labels.StorageSize      = $"{AwsHelper.GetDiskSizeGiB(node.Metadata.Aws.VolumeType, ByteUnits.Parse(node.Metadata.Aws.VolumeSize))} GiB";
                node.Metadata.Labels.StorageHDD       = node.Metadata.Aws.VolumeType == AwsVolumeType.Sc1 || node.Metadata.Aws.VolumeType == AwsVolumeType.Sc2;
                node.Metadata.Labels.StorageEphemeral = false;
                node.Metadata.Labels.StorageLocal     = false;
                node.Metadata.Labels.StorageRedundant = true;
            }
        }

        /// <summary>
        /// Locates tha AMI to use for provisioning the nodes in the target region.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task LocateAmiAsync()
        {
            // $hack(jefflill):
            //
            // We're going to do this by querying the region for AMIs published by Canonical
            // that satisfy somewhat fragile conditions.  This won't be a big risk after we
            // publish our own marketplace image because we'll control things and hupefully,
            // we won't need to build new markeplace images from base Canonical images more
            // than a few times a year and we'll be able to debug problems if anything bad
            // happens.  Here's how we're going to accomplish this:
            //
            //      * Filter for Cannonical owned images
            //
            //      * Filter for x86_64 architecture
            //
            //      * Filter for machine images
            //
            //      * Filter the description for Ubuntu 20.04 images
            //
            //      * Filter out images with "UNSUPPORTED" in the description (daily builds)
            //        AWS doesn't support NOT filters, so we'll need to do this on the client.
            //
            //      * The image location specifies the date of the build at the and of the
            //        string, like:
            //
            //            099720109477/ubuntu/images/hvm-ssd/ubuntu-focal-20.04-amd64-server-20200729
            //
            //        We'll use this to find the image for a specific Ubuntu release. 

            var request = new DescribeImagesRequest()
            {
                Owners  = new List<string>() { canonicalOwnerId },
                Filters = new List<Filter>
                {
                    new Filter("architecture", new List<string>() { "x86_64" }),
                    new Filter("image-type", new List<string>() { "machine" }),
                    new Filter("description", new List<string>() { "Canonical, Ubuntu, 20.04 LTS*" }),
                }
            };

            var response        = await ec2.DescribeImagesAsync(request);
            var supportedImages = response.Images.Where(image => !image.Description.Contains("UNSUPPORTED")).ToList();

            // Locate the AMI for the version of Ubuntu for the current cluster version.

            var ubuntuImage = ubuntuImages.SingleOrDefault(img => img.ClusterVersion == cluster.Definition.ClusterVersion && !img.IsPrepared);

            // If this fails, we probably forgot to add the entry for a new cluster version to [ubuntuImages] above:

            Covenant.Assert(ubuntuImage != null, $"Cannot locate AWS image information for cluster version [{cluster.Definition.ClusterVersion}].");

            // Strip the date from the image Ubuntu build and use that to locate the AMI
            // based on its location.

            var pos       = ubuntuImage.UbuntuBuild.LastIndexOf('.');
            var buildDate = ubuntuImage.UbuntuBuild.Substring(pos + 1);
            var image     = supportedImages.SingleOrDefault(image => image.ImageLocation.EndsWith(buildDate));

            if (image == null)
            {
                throw new KubeException($"Cannot locate the base Ubuntu [{ubuntuImage.UbuntuBuild}] AMI for the [{region}] region.");
            }

            ami = image.ImageId;
        }

        /// <summary>
        /// Creates the cluster VPC (aka VNET) and its subnet.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task CreateVpcSubnetAsync()
        {
            // Create the VPC

            var createVpcResponse = await ec2.CreateVpcAsync(
                new CreateVpcRequest()
                {
                    CidrBlock         = networkOptions.NodeSubnet,
                    TagSpecifications = GetTagSpecifications(GetResourceName("vpc", clusterName), ResourceType.Vpc)
                });
            
            vpc = createVpcResponse.Vpc;

            // Override the default AWS DNS servers if the user has specified 
            // custom nameservers in the cluster definition.  We'll accomplish
            // this by creating DHCP options and associating them with the VPC.

            if (networkOptions.Nameservers != null && networkOptions.Nameservers.Count > 0)
            {
                var sbNameservers = new StringBuilder();

                foreach (var nameserver in networkOptions.Nameservers)
                {
                    sbNameservers.AppendWithSeparator(nameserver, ",");
                }

                var dhcpConfigurations = new List<DhcpConfiguration>()
                {
                    new DhcpConfiguration() { Key = "domain-name-servers", Values = new List<string> { sbNameservers.ToString() } }
                };

                var createDhcpOptionsResponse = await ec2.CreateDhcpOptionsAsync(
                    new CreateDhcpOptionsRequest(dhcpConfigurations)
                    {
                        TagSpecifications = GetTagSpecifications(GetResourceName("dopt", clusterName), ResourceType.DhcpOptions)
                    });

                await ec2.AssociateDhcpOptionsAsync(
                    new AssociateDhcpOptionsRequest()
                    {
                        VpcId         = vpc.VpcId,
                        DhcpOptionsId = createDhcpOptionsResponse.DhcpOptions.DhcpOptionsId
                    });
            }

            var createSubnetResponse = await ec2.CreateSubnetAsync(
                new CreateSubnetRequest(vpc.VpcId, networkOptions.NodeSubnet)
                {
                    TagSpecifications = GetTagSpecifications(GetResourceName("subnet", clusterName), ResourceType.Subnet)
                });

            subnet = createSubnetResponse.Subnet;
        }

        /// <summary>
        /// Creates the AWS instance for a node.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="stepDelay">The step delay.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task CreateInstanceAsync(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            node.Status = "create instance";

            var launchResponse = await ec2.RunInstancesAsync(
                new RunInstancesRequest()
                {
                    ImageId           = ami,
                    InstanceType      = InstanceType.FindValue(node.Metadata.Aws.InstanceType),
                    MinCount          = 1,
                    MaxCount          = 1,
                    SubnetId          = subnet.SubnetId,
                    PrivateIpAddress  = node.Address.ToString(),
                    TagSpecifications = GetTagSpecifications(GetResourceName("i", node.Name), ResourceType.Instance)
                });

            nameToInstance[node.Name].Instance = launchResponse.Reservation.Instances.Single();
        }
    }
}
