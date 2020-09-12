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
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.ElasticLoadBalancingV2;
using Amazon.ElasticLoadBalancingV2.Model;
using Amazon.ResourceGroups;
using Amazon.ResourceGroups.Model;
using Amazon.Runtime;
using System.Runtime.InteropServices;

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
        //      * Network Load balancer with public Elastic IP
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
        // The load balancer will be created using a public Elastic IP address to balance
        // inbound traffic across a backend target including the instances designated 
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
        // Thye default VPC network ACL will be used to manage network security
        // and will include ingress rules constructed from [NetworkOptions.IngressRules],
        // any temporary SSH related rules as well as egress rules constructed from
        // [NetworkOptions.EgressAddressRules].
        //
        // AWS instances will be configured with each node's private IP address
        // within the subnet.  The provisioner assigns these addresses automatically.
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
        // SSH connections and the cluster reserves 1000 external load balancer
        // ports (by default) to accomplish this.  When we need an external SSH
        // connection to any cluster node, the hosting manager will add one or
        // more rules to allow traffic to the range of external SSH ports assigned to
        // the cluster nodes.  Load balancer rules will also be created to 
        // effectively port forward traffic from the external SSH port to 
        // port 22 on the nodes.
        //
        // Note that we also support source address white/black listing for both
        // ingress and SSH rules and as well as destination address white/black
        // listing for general outbound cluster traffic.
        //
        // Managing the network load balancer and ACL rules:
        // -------------------------------------------------
        // AWS VPCs come with a default network ACL that allows all ingress/egress
        // traffic.  We're going to remove the allow rules, leaving just the deny-all
        // rules for each direction.  We're not going to rely on the default VPC rule
        // in favor of rules we'll associate with the subnet.
        //
        // We're going to create two independent network ACLs and use these to control
        // traffic entering and leaving the subnet (and by extension, the cluster).
        // The idea is that we'll alternate associating one of these rules with the
        // subnet.  This way we can perform potentially multiple operations to update
        // the network ACL not currently in use and then atomically replace the existing
        // ACL in one go.  This is much better than modifying the live ACL because that
        // could temporarily disrupt network traffic.

        /// <summary>
        /// Relates cluster node information to an AWS VM instance.
        /// </summary>
        private class AwsInstance
        {
            private AwsHostingManager   hostingManager;
            private string              instanceName;

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
            public string InstanceName
            {
                get
                {
                    // Cache the result so we won't regenerate the name on every call.

                    if (instanceName != null)
                    {
                        return instanceName;
                    }

                    return instanceName = hostingManager.GetResourceName($"{Proxy.Name}");
                }
            }

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
        /// Flags used to control how the cluster network is updated.
        /// </summary>
        [Flags]
        private enum NetworkOperations
        {
            /// <summary>
            /// Update the cluster's ingress/egress rules.
            /// </summary>
            UpdateIngressEgressRules = 0x0001,

            /// <summary>
            /// Add public SSH NAT rules for every node in the cluster.
            /// These are used by neonKUBE related tools for provisioning, 
            /// setting up, and managing clusters.
            /// </summary>
            AddSshRules = 0x0002,

            /// <summary>
            /// Remove all SSH NAT rules.
            /// </summary>
            RemoveshRules = 0x0004,
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

        /// <summary>
        /// <para>
        /// Abstracts the multiple AWS tag implementations into a single common
        /// implementation.
        /// </para>
        /// <para>
        /// Unforuntately, AWS defines multiple <c>Tag</c> classes within their
        /// various "Model" assemblies.  This makes it hard to implement common
        /// resource tagging code.
        /// </para>
        /// <para>
        /// We're going to handle this by implementing our own command tag class
        /// that is parameterized by the desired AWS tag type.  This code assumes
        /// that all of the AWS tag implementations have a public default constructor
        /// as well as read/write Key/Value string properties.
        /// </para>
        /// </summary>
        /// <typeparam name="T">The desired AWS tag type.</typeparam>
        private class Tag<T>
        {
            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="key">Specifies the tag key.</param>
            /// <param name="value">Optionally specifies the tag value.</param>
            public Tag(string key, string value = null)
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(key), nameof(key));

                this.Key   = key;
                this.Value = value ?? string.Empty;
            }

            /// <summary>
            /// Returns the tag's key.
            /// </summary>
            public string Key { get; private set; }

            /// <summary>
            /// Returns the tag's value.
            /// </summary>
            public string Value { get; private set; }

            /// <summary>
            /// Converts the tag into the AWS tag type <typeparamref name="T"/>.
            /// </summary>
            /// <returns>The AWS tag.</returns>
            public T ToAws()
            {
                // Some low-level reflection magic.

                var tagType       = typeof(T);
                var rawTag        = Activator.CreateInstance(tagType);
                var keyProperty   = tagType.GetProperty("Key");
                var valueProperty = tagType.GetProperty("Value");

                keyProperty.SetValue(rawTag, Key);
                valueProperty.SetValue(rawTag, Value);

                return (T)rawTag;
            }
        }

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Specifies the ID to use when querying for Canonical images 
        /// </summary>
        private const string canonicalOwnerId = "099720109477";

        /// <summary>
        /// AWS generic name tag.
        /// </summary>
        private const string nameTag = "Name";

        /// <summary>
        /// The (namespace) prefix used for neonKUBE related Azure resource tags.
        /// </summary>
        private const string neonTagPrefix = "neon:";

        /// <summary>
        /// Used to tag resources with the cluster name.
        /// </summary>
        private const string neonClusterTag = neonTagPrefix + "cluster";

        /// <summary>
        /// Used to tag resources with the cluster environment.
        /// </summary>
        private const string neonEnvironmentTag = neonTagPrefix + "environment";

        /// <summary>
        /// Used to tag instances resources with the cluster node name.
        /// </summary>
        private const string neonNodeNameTag = neonTagPrefix + "node.name";

        /// <summary>
        /// The default deny everything network ACL rule number.
        /// </summary>
        private const int aclDenyAllRuleNumber = 32767;

        /// <summary>
        /// The first NSG rule priority to use for ingress rules.
        /// </summary>
        private const int firstIngressAclRuleNumber = 1000;

        /// <summary>
        /// The first NSG rule priority to use for egress rules.
        /// </summary>
        private const int firstEgressAclRuleNumber = 1000;

        /// <summary>
        /// The first NSG rule priority to use for temporary SSH rules.
        /// </summary>
        private const int firstSshAclRuleNumber = 2000;

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

        /// <summary>
        /// <para>
        /// Converts an <see cref="IngressProtocol"/> into the corresponding string AWS
        /// uses to identify the protocol for a network ACL entry.
        /// </para>
        /// <note>
        /// The values returned are internet protocol numbers as strings.  AWS also supports
        ///<b> "-1"</b> which means <b>any</b> protocol, but we don't use that.
        /// </note>
        /// </summary>
        /// <param name="protocol">The input protocol.</param>
        /// <returns>The corresponding AWS protocol string.</returns>
        private static string ToNetworkAclEntryProtocol(IngressProtocol protocol)
        {
            switch (protocol)
            {
                case IngressProtocol.Http:
                case IngressProtocol.Https:
                case IngressProtocol.Tcp:

                    return "6";     // TCP

                default:

                    throw new NotImplementedException();
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private KubeSetupInfo                       setupInfo;
        private ClusterProxy                        cluster;
        private string                              clusterName;
        private string                              clusterEnvironment;
        private HostingOptions                      hostingOptions;
        private CloudOptions                        cloudOptions;
        private bool                                prefixResourceNames;
        private AwsHostingOptions                   awsOptions;
        private NetworkOptions                      networkOptions;
        private BasicAWSCredentials                 awsCredentials;
        private string                              region;
        private string                              resourceGroupName;
        private Region                              awsRegion;
        private RegionEndpoint                      regionEndpoint;
        private AmazonEC2Client                     ec2Client;
        private AmazonElasticLoadBalancingV2Client  elbClient;
        private AmazonResourceGroupsClient          rgClient;
        private string                              ami;
        private Group                               resourceGroup;
        private Address                             elasticIp;
        private Vpc                                 vpc;
        private NetworkAcl                          defaultNetworkAcl;
        private NetworkAcl                          networkAcl1;
        private NetworkAcl                          networkAcl2;
        private DhcpOptions                         dhcpOptions;
        private Subnet                              subnet;
        private InternetGateway                     gateway;
        private LoadBalancer                        loadBalancer;

        // These are the names we'll use for cluster AWS resources.

        private string                              elasticIpName;
        private string                              vpcName;
        private string                              dhcpOptionName;
        private string                              subnetName;
        private String                              networkAclName1;
        private String                              networkAclName2;
        private string                              gatewayName;
        private string                              loadBalancerName;

        /// <summary>
        /// Table mapping a cluster node name to its AWS VM instance information.
        /// Note that <see cref="nodeNameToInstance"/> and <see cref="instanceNameToInstance"/>
        /// both refer to the same <see cref="AwsInstance"/> so a change to one value
        /// will be reflected in the other table.
        /// </summary>
        private Dictionary<string, AwsInstance> nodeNameToInstance;

        /// <summary>
        /// Table mapping a cluster AWS instance name to its AWS VM instance information.
        /// Note that <see cref="nodeNameToInstance"/> and <see cref="instanceNameToInstance"/>
        /// both refer to the same <see cref="AwsInstance"/> so a change to one value
        /// will be reflected in the other table.
        /// </summary>
        private Dictionary<string, AwsInstance> instanceNameToInstance;

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

            cluster.HostingManager  = this;

            this.setupInfo          = setupInfo;
            this.cluster            = cluster;
            this.clusterName        = cluster.Name;
            this.clusterEnvironment = NeonHelper.EnumToString(cluster.Definition.Environment);
            this.hostingOptions     = cluster.Definition.Hosting;
            this.cloudOptions       = hostingOptions.Cloud;
            this.awsOptions         = hostingOptions.Aws;
            this.networkOptions     = cluster.Definition.Network;
            this.region             = awsOptions.Region;
            this.resourceGroupName  = awsOptions.ResourceGroup;

            // We're always going to prefix AWS resource names with the cluster name because
            // AWS resource names have scope and because load balancer names need to be unique
            // within an AWS account and region.

            this.prefixResourceNames = true;

            // Initialize the cluster resource names.

            elasticIpName    = GetResourceName("elastic-ip");
            vpcName          = GetResourceName("vpc");
            dhcpOptionName   = GetResourceName("dhcp-opt");
            subnetName       = GetResourceName("subnet");
            networkAclName1  = GetResourceName("networkAcl1");
            networkAclName2  = GetResourceName("networkAcl2");
            gatewayName      = GetResourceName("internet-gateway");
            loadBalancerName = GetResourceName("load-balancer");

            // Initialize the instance/node mapping dictionaries and also ensure
            // that each node has reasonable AWS node options.

            this.nodeNameToInstance = new Dictionary<string, AwsInstance>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var node in cluster.Nodes)
            {
                nodeNameToInstance.Add(node.Name, new AwsInstance(node, this));

                if (node.Metadata.Aws == null)
                {
                    node.Metadata.Aws = new AwsNodeOptions();
                }
            }

            this.instanceNameToInstance = new Dictionary<string, AwsInstance>();

            foreach (var instanceInfo in nodeNameToInstance.Values)
            {
                instanceNameToInstance.Add(instanceInfo.InstanceName, instanceInfo);
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

            ec2Client?.Dispose();
            ec2Client = null;

            elbClient?.Dispose();
            elbClient = null;

            rgClient?.Dispose();
            rgClient = null;
        }

        /// <summary>
        /// Indicates when an AWS connection is established.
        /// </summary>
        private bool isConnected => ec2Client != null;

        /// <summary>
        /// Enumerates the cluster nodes in no particular order.
        /// </summary>
        private IEnumerable<AwsInstance> Nodes => nodeNameToInstance.Values;

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
        /// suffix, the cluster name and the base resource name.  This is based on AWS tagging
        /// best practices:
        /// </para>
        /// <para>
        /// <a href="https://docs.aws.amazon.com/general/latest/gr/aws_tagging.html">AWS Tagging Best Practices</a>
        /// </para>
        /// </summary>
        /// <param name="resourceName">The base resource name.</param>
        /// <returns>The fully quallified resource name.</returns>
        private string GetResourceName(string resourceName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(resourceName), nameof(resourceName));

            if (prefixResourceNames)
            {
                return $"{clusterName}-{resourceName}";
            }
            else
            {
                return $"{resourceName}";
            }
        }

        /// <summary>
        /// Creates the tags for a resource including the resource name, cluster details,
        /// as well as optional tags.
        /// </summary>
        /// <typeparam name="T">Specifies the desired AWS tag type.</typeparam>
        /// <param name="name">The resource name.</param>
        /// <param name="tags">The optional tags.</param>
        /// <returns>The <see cref="TagSpecification"/> list with a single element.</returns>
        private List<T> GetTags<T>(string name, params KeyValuePair<string, string>[] tags)
        {
            var tagList = new List<T>();

            tagList.Add(new Tag<T>(nameTag, name).ToAws());
            tagList.Add(new Tag<T>(neonClusterTag, clusterName).ToAws());
            tagList.Add(new Tag<T>(neonEnvironmentTag, clusterEnvironment).ToAws());

            foreach (var tag in tags)
            {
                tagList.Add(new Tag<T>(tag.Key, tag.Value).ToAws());
            }

            return tagList;
        }

        /// <summary>
        /// Creates a tag specification for an EC2 resource including the resource name, 
        /// additional cluster details as well as optional tags.
        /// </summary>
        /// <param name="name">The resource name.</param>
        /// <param name="resourceType">The fully qualified resource type.</param>
        /// <param name="tags">The optional tags.</param>
        /// <returns>The <see cref="TagSpecification"/> list with a single element.</returns>
        private List<TagSpecification> GetTagSpecifications(string name, ResourceType resourceType, params KeyValuePair<string, string>[] tags)
        {
            return new List<TagSpecification>()
            {
                new TagSpecification()
                {
                    ResourceType = resourceType,
                    Tags         = GetTags<Amazon.EC2.Model.Tag>(name, tags)
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

            var operation  = $"Provisioning [{cluster.Definition.Name}] on AWS [{region}/{resourceGroupName}]";
            var controller = new SetupController<NodeDefinition>(operation, cluster.Nodes)
            {
                ShowStatus     = this.ShowStatus,
                ShowNodeStatus = true,
                MaxParallel    = int.MaxValue       // There's no reason to constrain this
            };

            controller.AddGlobalStep("AWS connect", ConnectAwsAsync);
            controller.AddGlobalStep("region check", () => VerifyRegionAndInstanceTypesAsync());
            controller.AddGlobalStep("locate ami", LocateAmiAsync);
            controller.AddGlobalStep("resource group", CreateResourceGroupAsync);
            controller.AddGlobalStep("elastic ip", CreateElasticIpAsync);
            controller.AddGlobalStep("network", ConfigureNetworkAsync);
            controller.AddStep("node instances", CreateInstanceAsync);

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
        public override async Task UpdateInternetRoutingAsync()
        {
            var operations = NetworkOperations.UpdateIngressEgressRules;
            
            // $todo(jefflill): IMPLEMENT THIS!

            //if (loadBalancer.InboundNatRules.Values.Any(rule => rule.Name.StartsWith(ingressRulePrefix, StringComparison.InvariantCultureIgnoreCase)))
            //{
            //    // It looks like SSH NAT rules are enabled so we'll update
            //    // those as well.

            //    operations |= NetworkOperations.AddPublicSshRules;
            //}

            await UpdateNetworkAsync(operations);
        }

        /// <inheritdoc/>
        public override async Task EnableInternetSshAsync()
        {
            await UpdateNetworkAsync(NetworkOperations.AddSshRules);
        }

        /// <inheritdoc/>
        public override async Task DisableInternetSshAsync()
        {
            await UpdateNetworkAsync(NetworkOperations.RemoveshRules);
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
            ec2Client      = new AmazonEC2Client(awsCredentials, regionEndpoint);
            elbClient      = new AmazonElasticLoadBalancingV2Client(awsCredentials, regionEndpoint);
            rgClient       = new AmazonResourceGroupsClient(awsCredentials, regionEndpoint);

            // Load information about any existing cluster resources.

            await GetResourcesAsync();
        }

        /// <summary>
        /// Loads information about cluster related resources already provisioned to AWS.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task GetResourcesAsync()
        {
            // Resource group

            try
            {
                var groupResponse = await rgClient.GetGroupAsync(
                    new GetGroupRequest()
                    {
                        Group = resourceGroupName
                    });

                resourceGroup = groupResponse.Group;
            }
            catch (NotFoundException)
            {
                resourceGroup = null;
            }

            if (resourceGroup != null)
            {
                // Ensure that the group was created for the cluster.
                //
                // For AWS, we're going to require that the resource group be restricted
                // to just cluster resources.  This is different from what we do for Azure
                // to reduce complexity and because there's less of a need for including
                // non-cluster resources in the group because AWS supports nested resource
                // groups.

                var groupQueryResponse = await rgClient.GetGroupQueryAsync(
                    new GetGroupQueryRequest()
                    {
                        Group = resourceGroupName
                    });

                var groupQuery = groupQueryResponse.GroupQuery;

                if (groupQuery.ResourceQuery.Type != QueryType.TAG_FILTERS_1_0 ||
                    groupQuery.ResourceQuery.Query != $"{{\"ResourceTypeFilters\":[\"AWS::AllSupported\"],\"TagFilters\":[{{\"Key\":\"NEON:Cluster\",\"Values\":[\"{clusterName}\"]}}]}}")
                {
                    throw new KubeException($"]{resourceGroup}] resource was not created exclusively for the [{clusterName}] neonKUBE cluster.");
                }
            }

            // Elastic IP

            var addressResponse = await ec2Client.DescribeAddressesAsync();

            foreach (var addressItem in addressResponse.Addresses)
            {
                if (addressItem.Tags.Any(tag => tag.Key == nameTag && tag.Value == elasticIpName) &&
                    addressItem.Tags.Any(tag => tag.Key == neonClusterTag && tag.Value == clusterName))
                {
                    elasticIp = addressItem;
                    break;
                }
            }

            // VPC and it's default network ACL.

            var vpcPaginator = ec2Client.Paginators.DescribeVpcs(new DescribeVpcsRequest());

            await foreach (var vpcItem in vpcPaginator.Vpcs)
            {
                if (vpcItem.Tags.Any(tag => tag.Key == nameTag && tag.Value == vpcName) &&
                    vpcItem.Tags.Any(tag => tag.Key == neonClusterTag && tag.Value == clusterName))
                {
                    vpc = vpcItem;
                    break;
                }
            }

            if (vpc != null)
            {
                defaultNetworkAcl = await GetDefaultNetworkAclAsync(vpc);
            }

            // DHCP options

            var dhcpPaginator = ec2Client.Paginators.DescribeDhcpOptions(new DescribeDhcpOptionsRequest());

            await foreach (var dhcpItem in dhcpPaginator.DhcpOptions)
            {
                if (dhcpItem.Tags.Any(tag => tag.Key == nameTag && tag.Value == dhcpOptionName) &&
                    dhcpItem.Tags.Any(tag => tag.Key == neonClusterTag && tag.Value == clusterName))
                {
                    dhcpOptions = dhcpItem;
                    break;
                }
            }

            // Subnet

            var subnetPaginator = ec2Client.Paginators.DescribeSubnets(new DescribeSubnetsRequest());

            await foreach (var subnetItem in subnetPaginator.Subnets)
            {
                if (subnetItem.Tags.Any(tag => tag.Key == nameTag && tag.Value == subnetName) &&
                    subnetItem.Tags.Any(tag => tag.Key == neonClusterTag && tag.Value == clusterName))
                {
                    subnet = subnetItem;
                    break;
                }
            }

            // Network ACLs

            var networkAclPagenator = ec2Client.Paginators.DescribeNetworkAcls(new DescribeNetworkAclsRequest());

            await foreach (var networkAclItem in networkAclPagenator.NetworkAcls)
            {
                if (!networkAclItem.Tags.Any(tag => tag.Key == neonClusterTag && tag.Value == clusterName))
                {
                    continue;   // ACL doesn't belong to the cluster.
                }

                if (networkAclItem.Tags.Any(tag => tag.Key == nameTag && tag.Value == networkAclName1))
                {
                    networkAcl1 = networkAclItem;
                }
                else if (networkAclItem.Tags.Any(tag => tag.Key == nameTag && tag.Value == networkAclName2))
                {
                    networkAcl2 = networkAclItem;
                }
            }

            // Gateway

            var gatewayPaginator = ec2Client.Paginators.DescribeInternetGateways(new DescribeInternetGatewaysRequest());

            await foreach (var gatewayItem in gatewayPaginator.InternetGateways)
            {
                if (gatewayItem.Tags.Any(tag => tag.Key == nameTag && tag.Value == gatewayName) &&
                    gatewayItem.Tags.Any(tag => tag.Key == neonClusterTag && tag.Value == clusterName))
                {
                    gateway = gatewayItem;
                    break;
                }
            }

            // Load Balancer

            var loadbalancerPaginator = elbClient.Paginators.DescribeLoadBalancers(new DescribeLoadBalancersRequest());
            var loadbalancerName      = this.loadBalancerName.Replace('.', '-');    // AWS doesn't allow periods in LB names

            await foreach (var loadbalancerItem in loadbalancerPaginator.LoadBalancers)
            {
                if (loadbalancerItem.LoadBalancerName == loadbalancerName)
                {
                    loadBalancer = loadbalancerItem;
                    break;
                }
            }

            // Instances

            var instancePaginator = ec2Client.Paginators.DescribeInstances(new DescribeInstancesRequest());

            await foreach (var reservation in instancePaginator.Reservations)
            {
                foreach (var instance in reservation.Instances)
                {
                    var name    = instance.Tags.SingleOrDefault(tag => tag.Key == nameTag)?.Value;
                    var cluster = instance.Tags.SingleOrDefault(tag => tag.Key == neonClusterTag)?.Value;

                    if (name != null && cluster == clusterName && instanceNameToInstance.TryGetValue(name, out var instanceInfo))
                    {
                        instanceInfo.Instance = instance;
                    }
                }
            }
        }

        /// <summary>
        /// Returns the VPC's network ACL.
        /// </summary>
        /// <param name="vpc">The VPC.</param>
        /// <returns>The network ACL.</returns>
        private async Task<NetworkAcl> GetDefaultNetworkAclAsync(Vpc vpc)
        {
            Covenant.Requires<ArgumentNullException>(vpc != null, nameof(vpc));

            var associationPaginator = ec2Client.Paginators.DescribeNetworkAcls(new DescribeNetworkAclsRequest());

            await foreach (var association in associationPaginator.NetworkAcls)
            {
                if (association.VpcId == vpc.VpcId && association.IsDefault)
                {
                    var networkAclPaginator = ec2Client.Paginators.DescribeNetworkAcls(
                        new DescribeNetworkAclsRequest()
                        {
                            NetworkAclIds = new List<string>() { association.NetworkAclId }
                        });

                    await foreach (var networkAclItem in networkAclPaginator.NetworkAcls)
                    {
                        return networkAclItem;
                    }
                }
            }

            Covenant.Assert(false, "There should always be an network ACL assigned to the VPC.");
            return null;
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
            var instanceTypePaginator  = ec2Client.Paginators.DescribeInstanceTypes(new DescribeInstanceTypesRequest());

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
            //      * Filter for x86_64 architecture
            //      * Filter for machine images
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

            var response        = await ec2Client.DescribeImagesAsync(request);
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
        /// <para>
        /// Creates the resource group for the cluster if it doesn't already exist. The resource
        /// group query will look for resources tagged with:
        /// </para>
        /// <code>
        /// NEON:Cluster == CLUSTER_NAME
        /// </code>
        /// <para>
        /// This method will fail if the resource group already exists and was not created for the
        /// cluster being deployed.
        /// </para>
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task CreateResourceGroupAsync()
        {
            Group group;

            try
            {
                var groupResponse = await rgClient.GetGroupAsync(
                    new GetGroupRequest()
                    {
                        Group = resourceGroupName
                    });

                group = groupResponse.Group;
            }
            catch (NotFoundException)
            {
                group = null;
            }

            if (group == null)
            {
                // The resource group doesn't exist so create it.

                await rgClient.CreateGroupAsync(
                    new CreateGroupRequest()
                    {
                        Name        = resourceGroupName,
                        Description = $"Identifies the resources for the {clusterName} neonKUBE cluster",
                        Tags        = new Dictionary<string, string>()
                        {
                            {  neonClusterTag, clusterName }
                        },
                        ResourceQuery = new ResourceQuery()
                        {
                            Query = $"{{\"ResourceTypeFilters\":[\"AWS::AllSupported\"],\"TagFilters\":[{{\"Key\":\"{neonClusterTag}\",\"Values\":[\"{clusterName}\"]}}]}}",
                            Type  = QueryType.TAG_FILTERS_1_0
                        }
                    });
            }
            else
            {
                // The resource group already exists.  We'll check to see if the query
                // corresponds to the cluster being deployed and if it doesn't, we're
                // going to fail the operation.
                //
                // For AWS, we're going to require that the resource group is restricted
                // to just cluster resources.  This is different from what we do for Azure
                // to reduce complexity and because there's less of a need for including
                // non-cluster resources in the group because AWS supports nested resource
                // groups.

                var groupQueryResponse = await rgClient.GetGroupQueryAsync(
                    new GetGroupQueryRequest()
                    {
                        Group = resourceGroupName
                    });

                var groupQuery = groupQueryResponse.GroupQuery;

                if (groupQuery.ResourceQuery.Type != QueryType.TAG_FILTERS_1_0 ||
                    groupQuery.ResourceQuery.Query != $"{{\"ResourceTypeFilters\":[\"AWS::AllSupported\"],\"TagFilters\":[{{\"Key\":\"NEON:Cluster\",\"Values\":[\"{clusterName}\"]}}]}}")
                {
                    throw new KubeException($"{resourceGroupName} - neonKUBE cluster related resources");
                }
            }
        }

        /// <summary>
        /// Creates the elastic IP address for the cluster if it doesn't already exist.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task CreateElasticIpAsync()
        {
            if (elasticIp == null)
            {
                var allocateResponse = await ec2Client.AllocateAddressAsync();
                
                var addressId = allocateResponse.AllocationId;

                await ec2Client.CreateTagsAsync(
                    new CreateTagsRequest()
                    {
                        Resources = new List<string>() { addressId },
                        Tags      = GetTags<Amazon.EC2.Model.Tag>(elasticIpName)
                    });

                // Retrieve the elastic IP resource.

                var addressResponse = await ec2Client.DescribeAddressesAsync();

                foreach (var addr in addressResponse.Addresses)
                {
                    if (addr.Tags.Any(tag => tag.Key == nameTag && tag.Value == elasticIpName) &&
                        addr.Tags.Any(tag => tag.Key == neonClusterTag && tag.Value == clusterName))
                    {
                        elasticIp = addr;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Creates the cluster networking components including the VPC, subnet, internet gateway
        /// and network ACLs.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task ConfigureNetworkAsync()
        {
            // Create the VPC.

            if (vpc == null)
            {
                var vpcResponse = await ec2Client.CreateVpcAsync(
                    new CreateVpcRequest()
                    {
                        CidrBlock         = networkOptions.NodeSubnet,
                        TagSpecifications = GetTagSpecifications(vpcName, ResourceType.Vpc)
                    });

                vpc = vpcResponse.Vpc;
            }

            // Clear the VPC's default network ACL by deleting all inbound and outbound 
            // rules except for the default DENY rule for both directions.

            defaultNetworkAcl = await GetDefaultNetworkAclAsync(vpc);

            foreach (var entry in defaultNetworkAcl.Entries.Where(entry => entry.RuleNumber != aclDenyAllRuleNumber))
            {
                await ec2Client.DeleteNetworkAclEntryAsync(
                    new DeleteNetworkAclEntryRequest()
                    {
                        NetworkAclId = defaultNetworkAcl.NetworkAclId,
                        RuleNumber   = entry.RuleNumber,
                        Egress       = entry.Egress
                    });
            }

            // Override the default AWS DNS servers if the user has specified 
            // custom nameservers in the cluster definition.  We'll accomplish
            // this by creating DHCP options and associating them with the VPC.

            if (networkOptions.Nameservers != null && networkOptions.Nameservers.Count > 0)
            {
                if (dhcpOptions == null)
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

                    var dhcpOptionsResponse = await ec2Client.CreateDhcpOptionsAsync(
                        new CreateDhcpOptionsRequest(dhcpConfigurations)
                        {
                            TagSpecifications = GetTagSpecifications(dhcpOptionName, ResourceType.DhcpOptions)
                        });

                    dhcpOptions = dhcpOptionsResponse.DhcpOptions;
                }

                // Associate the DHCP options with the VPC.

                await ec2Client.AssociateDhcpOptionsAsync(
                    new AssociateDhcpOptionsRequest()
                    {
                        VpcId         = vpc.VpcId,
                        DhcpOptionsId = dhcpOptions.DhcpOptionsId
                    });
            }

            // Create the subnet and associate it with the VPC if the subnet doesn't already exist.

            if (subnet == null)
            {
                var subnetResponse = await ec2Client.CreateSubnetAsync(
                    new CreateSubnetRequest(vpc.VpcId, networkOptions.NodeSubnet)
                    {
                        VpcId             = vpc.VpcId,
                        TagSpecifications = GetTagSpecifications(subnetName, ResourceType.Subnet)
                    });

                subnet = subnetResponse.Subnet;
            }

            // Create the two network ACLs we'll use for securing the subnet.
            // We won't attach either of these to the subnet here; we'll defer
            // that until we update the network.

            var networkAclResponse = await ec2Client.CreateNetworkAclAsync(
                new CreateNetworkAclRequest()
                {
                    TagSpecifications = GetTagSpecifications(networkAclName1, ResourceType.NetworkAcl)
                });

            networkAcl1 = networkAclResponse.NetworkAcl;

            networkAclResponse = await ec2Client.CreateNetworkAclAsync(
                new CreateNetworkAclRequest()
                {
                    TagSpecifications = GetTagSpecifications(networkAclName2, ResourceType.NetworkAcl)
                });

            networkAcl2 = networkAclResponse.NetworkAcl;

            // Create the Internet gateway and attach it to the VPC if it's
            // not already attached.

            if (gateway == null)
            {
                var gatewayResponse = await ec2Client.CreateInternetGatewayAsync(
                    new CreateInternetGatewayRequest()
                    {
                        TagSpecifications = GetTagSpecifications(gatewayName, ResourceType.InternetGateway)
                    });

                gateway = gatewayResponse.InternetGateway;
            }

            if (!gateway.Attachments.Any(association => association.VpcId == vpc.VpcId))
            {
                await ec2Client.AttachInternetGatewayAsync(
                    new AttachInternetGatewayRequest()
                    {
                        VpcId             = vpc.VpcId,
                        InternetGatewayId = gateway.InternetGatewayId
                    });
            }

            // Create the load balancer if it doesn't already exist.

            if (loadBalancer == null)
            {
                var loadbalancerResponse = await elbClient.CreateLoadBalancerAsync(
                    new CreateLoadBalancerRequest()
                    {
                        Name          = loadBalancerName.Replace('.', '-'),     // AWS doesn't allow periods in LB names
                        IpAddressType = IpAddressType.Ipv4,
                        Scheme        = LoadBalancerSchemeEnum.InternetFacing,
                        Subnets       = new List<string>() { subnet.SubnetId },
                        Type          = LoadBalancerTypeEnum.Network,
                        Tags          = GetTags<Amazon.ElasticLoadBalancingV2.Model.Tag>(loadBalancerName)
                    });

                loadBalancer = loadbalancerResponse.LoadBalancers.Single();
            }

            // Configure the ingress/egress rules as well as enable the SSH port forwarding.

            await UpdateNetworkAsync(NetworkOperations.UpdateIngressEgressRules | NetworkOperations.AddSshRules);
        }

        /// <summary>
        /// Creates the AWS instance for a node.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="stepDelay">The step delay.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task CreateInstanceAsync(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            // Create the instance if it doesn't already exist.

            var instance     = nodeNameToInstance[node.Name];
            var instanceName = instance.InstanceName;

            if (instance.Instance == null)
            {
                node.Status = "create instance";

                var runResponse = await ec2Client.RunInstancesAsync(
                    new RunInstancesRequest()
                    {
                        ImageId           = ami,
                        InstanceType      = InstanceType.FindValue(node.Metadata.Aws.InstanceType),
                        MinCount          = 1,
                        MaxCount          = 1,
                        SubnetId          = subnet.SubnetId,
                        PrivateIpAddress  = node.Address.ToString(),
                        TagSpecifications = GetTagSpecifications(instanceName, ResourceType.Instance, new KeyValuePair<string, string>(neonNodeNameTag, node.Name))
                    });

                instance.Instance = runResponse.Reservation.Instances.Single();
            }
        }

        /// <summary>
        /// Updates the load balancer and related security rules based on the operation flags passed.
        /// </summary>
        /// <param name="operations">Flags that control how the load balancer and related security rules are updated.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task UpdateNetworkAsync(NetworkOperations operations)
        {
            if ((operations & NetworkOperations.UpdateIngressEgressRules) != 0)
            {
                await UpdateIngressEgressRulesAsync();
            }

            if ((operations & NetworkOperations.AddSshRules) != 0)
            {
                await AddSshRulesAsync();
            }

            if ((operations & NetworkOperations.RemoveshRules) != 0)
            {
                await RemoveSshRulesAsync();
            }
        }

        /// <summary>
        /// Updates the load balancer and network ACLs to match the current cluster definition.
        /// This also ensures that some nodes are marked for ingress when the cluster has one or more
        /// ingress rules and that nodes marked for ingress are in the load balancer's backend pool.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task UpdateIngressEgressRulesAsync()
        {
            KubeHelper.EnsureIngressNodes(cluster.Definition);

            // Determine whether either of the two network ACLs are associated with
            // the subnet.  If one is assigned, then we'll modify the other one and
            // then associate that one with the subnet to complete the operation.
            // If neither ACL is currently assigned, we'll use [networkAcl1].
            //
            // The idea here is that we're going to alternate between assigning
            // the two ACLs so we can make a bunch of effective rule changes in
            // one atomic operation.

            NetworkAcl              updateAcl;
            NetworkAclAssociation   association;

            association = networkAcl1.Associations.SingleOrDefault(association => association.SubnetId == subnet.SubnetId);

            if (association != null)
            {
                updateAcl = networkAcl1;
            }
            else
            {
                association = networkAcl2.Associations.SingleOrDefault(association => association.SubnetId == subnet.SubnetId);

                if (association != null)
                {
                    updateAcl = networkAcl2;
                }
                else
                {
                    // Use the first network ACL when the subnet has no ACL yet.

                    updateAcl   = networkAcl1;
                    association = null;
                }
            }

            // Remove any existing entries from the ACL we'll be assigning next.

            var entries = updateAcl.Entries.ToList();

            foreach (var entry in entries)
            {
                await ec2Client.DeleteNetworkAclEntryAsync(
                    new DeleteNetworkAclEntryRequest()
                    {
                        NetworkAclId = updateAcl.NetworkAclId,
                        RuleNumber   = entry.RuleNumber,
                        Egress       = entry.Egress
                    });
            }

            // Add any ingress rules from the cluster definition.

            var ingressRuleNumber = firstIngressAclRuleNumber;

            foreach (var ingressRule in networkOptions.IngressRules)
            {
                if (ingressRule.AddressRules.Count() == 0)
                {
                    // The ingress rule has no source address constraints.

                    await ec2Client.CreateNetworkAclEntryAsync(
                        new CreateNetworkAclEntryRequest()
                        {
                            RuleNumber = ingressRuleNumber++,
                            Protocol   = ToNetworkAclEntryProtocol(ingressRule.Protocol),
                            Egress     = false,
                            PortRange  = new PortRange() { From = ingressRule.ExternalPort, To = ingressRule.ExternalPort },
                            RuleAction = RuleAction.Allow
                        });
                }
                else
                {
                    // The ingress rule has source address constraints, so we'll 
                    // create a new network ACL entry for each address constraint.

                    foreach (var addressRule in ingressRule.AddressRules)
                    {
                        var sourceCidr = (string)null;

                        if (addressRule.IsAny)
                        {
                            sourceCidr = "0.0.0.0/0";
                        }
                        else
                        {
                            sourceCidr = addressRule.AddressOrSubnet;

                            if (!sourceCidr.Contains('/'))
                            {
                                // Convert a single IP address into a one address CIDR.

                                sourceCidr += "/32";
                            }
                        }

                        await ec2Client.CreateNetworkAclEntryAsync(
                            new CreateNetworkAclEntryRequest()
                            {
                                RuleNumber = ingressRuleNumber++,
                                Protocol   = ToNetworkAclEntryProtocol(ingressRule.Protocol),
                                Egress     = false,
                                PortRange  = new PortRange() { From = ingressRule.ExternalPort, To = ingressRule.ExternalPort },
                                CidrBlock  = sourceCidr,
                                RuleAction = addressRule.Action == AddressRuleAction.Allow ? RuleAction.Allow : RuleAction.Deny
                            });
                    }
                }
            }

            // Add any egress related destination address rules.

            var egressRuleNumber = firstIngressAclRuleNumber;

            foreach (var egressAddressRule in networkOptions.EgressAddressRules)
            {
                var destinationCidr = (string)null;

                if (egressAddressRule.IsAny)
                {
                    destinationCidr = "0.0.0.0/0";
                }
                else
                {
                    destinationCidr = egressAddressRule.AddressOrSubnet;

                    if (!destinationCidr.Contains('/'))
                    {
                        // Convert a single IP address into a one address CIDR.

                        destinationCidr += "/32";
                    }
                }

                await ec2Client.CreateNetworkAclEntryAsync(
                    new CreateNetworkAclEntryRequest()
                    {
                        RuleNumber = egressRuleNumber++,
                        Protocol   = "-1",      // "-1" means any protocol
                        Egress     = true,
                        PortRange  = new PortRange() { From = 1, To = ushort.MaxValue },
                        CidrBlock  = destinationCidr,
                        RuleAction = egressAddressRule.Action == AddressRuleAction.Allow ? RuleAction.Allow : RuleAction.Deny
                    });
            }

            // The new network ACL is ready so associate it with the subnet, replacing
            // the previous ACL (if any).

            await ec2Client.ReplaceNetworkAclAssociationAsync(
                new ReplaceNetworkAclAssociationRequest()
                {
                    AssociationId = association?.NetworkAclAssociationId,
                    NetworkAclId  = updateAcl.NetworkAclId
                });
        }

        /// <summary>
        /// Adds public SSH NAT and security rules for every node in the cluster.
        /// These are used by neonKUBE tools for provisioning, setting up, and
        /// managing cluster nodes.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task AddSshRulesAsync()
        {
            // $todo(jefflill): IMPLEMENT THIS!
        }

        /// <summary>
        /// Removes public SSH NAT and security rules for every node in the cluster.
        /// These are used by neonKUBE related tools for provisioning, setting up, and
        /// managing cluster nodes.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task RemoveSshRulesAsync()
        {
            // $todo(jefflill): IMPLEMENT THIS!
        }
    }
}
