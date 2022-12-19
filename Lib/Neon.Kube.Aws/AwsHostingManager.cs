//-----------------------------------------------------------------------------
// FILE:	    AwsHostingManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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

using Neon.Collections;
using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.SSH;
using Neon.Tasks;
using Neon.Time;

using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.ElasticLoadBalancingV2;
using Amazon.ElasticLoadBalancingV2.Model;
using Amazon.ResourceGroups;
using Amazon.ResourceGroups.Model;
using Amazon.Runtime;

using Ec2Instance    = Amazon.EC2.Model.Instance;
using Ec2Tag         = Amazon.EC2.Model.Tag;
using Ec2VolumeType  = Amazon.EC2.VolumeType;
using ElbAction      = Amazon.ElasticLoadBalancingV2.Model.Action;
using ElbTag         = Amazon.ElasticLoadBalancingV2.Model.Tag;
using ElbTargetGroup = Amazon.ElasticLoadBalancingV2.Model.TargetGroup;

namespace Neon.Kube.Hosting.Aws
{
    /// <summary>
    /// Manages cluster provisioning on Amazon Web Services.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Optional capability support:
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><see cref="HostingCapabilities.Pausable"/></term>
    ///     <description><b>YES</b></description>
    /// </item>
    /// <item>
    ///     <term><see cref="HostingCapabilities.Stoppable"/></term>
    ///     <description><b>YES</b></description>
    /// </item>
    /// </list>
    /// </remarks>
    [HostingProvider(HostingEnvironment.Aws)]
    public class AwsHostingManager : HostingManager
    {
        //---------------------------------------------------------------------
        // IMPLENTATION NOTE:
        //
        // A neonKUBE AWS cluster will require provisioning these things:
        //
        //      * VPC (virtual private cloud, equivilent to an Azure VNET)
        //
        //      * Public subnet where the network load balancer, internet
        //        gateway and NAT gateway will be deployed to manage internet 
        //        traffic.
        //
        //      * Node private subnet where the nodes will be deployed.
        //
        //      * Instances & EC2 volumes
        //
        //      * Elastic IP for the load balancer.
        //
        // In the future, we may relax the public load balancer requirement so
        // that virtual air-gapped clusters can be supported.
        //
        // The network will be configured using cluster definition's [AwsHostingOptions].
        // [VpcSubnet] will be used to configure the VPC and [PublicSubnet] and
        // [NodeSubnet] will be used for the public subnet and the private node subnet.
        // Node IP addresses will be automatically assigned from the node subnet by default, 
        // but this can be customized via the cluster definition when necessary.
        //
        // The load balancer will be created using a public Elastic IP address to balance
        // inbound traffic across a backend target including the instances designated 
        // to accept ingress traffic into the cluster.  These nodes are identified 
        // by the presence of a [neonkube.io/node.ingress=true] label which can be
        // set explicitly.  neonKUBE will default to reasonable ingress nodes when
        // necessary.  We'll be automatically managing the AWS target groups and
        // network ACLs to make this all work.
        //
        // External load balancer traffic can be enabled for specific ports via 
        // [NetworkOptions.IngressRules] which specify three ports: 
        // 
        //      * The external load balancer port.
        //
        //      * The node port where where traffic will be routed 
        //        into the cluster.
        //
        //      * The optional target port for traffic processed by the
        //        Istio ingress gateway.  This is used bu Istio when processing
        //        its routing rules.
        //
        // The [NetworkOptions.IngressRules] can also explicitly allow or deny traffic
        // from specific source IP addresses and/or subnets.
        //
        // We're going to use two network ACLs to try make ingress rule changessbname
        // as non-disruptive as possible.  The idea is to update the network ACL
        // not currently in use with any rule changes and then swap the ACLs
        // so all of the rules will be applied in a single opertation (rather
        // than performing multiple operations on the current ACL).
        //
        // VMs are currently based on the Ubuntu-22.04 Server AMIs published to the
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
        // image can be used (which is a good thing).
        //
        // This hosting manager requires that the node image AMI be present in
        // target region.  Project maintainers can deploy alpha node image releases
        // from the US-WEST-2 (Oregon) region using the NEONFORGE AWS account.
        // Preview and Release node images can be used from the AWS Marketplace
        // by normal users.
        //
        // Node instance, disk types and sizes are specified by the 
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
        // more rules to allow traffic to the range of external SSH ports assigned
        // to the cluster nodes.  Load balancer rules will also be created to 
        // effectively port forward traffic from the external SSH port to 
        // port 22 on the nodes.
        //
        // Note that we also support source address white/black listing for both
        // ingress and SSH rules and as well as destination address white/black
        // listing for general outbound cluster traffic.
        //
        // DHCP:
        // -----
        // We are not using AWS [cloud-init] to configure instance network settings
        // (this means that we're not provisioning [DhcpOptions].  Instead, we're
        // creating instances with a user-data boot script that configures NetPlan.
        //
        // Node instances will be provisioned with the standard AWS nameserver at
        // [169.254.169.253] when no nameservers are specified in the cluster definition,
        // otherwise we'll configure the defined nameservers.
        //
        // Managing the network load balancer and ACL rules:
        // -------------------------------------------------
        // AWS VPCs come with a default network ACL that allows all ingress/egress
        // traffic.  We're going to remove the allow rules, leaving just the deny-all
        // rules for each direction.  We're not going to rely on the default VPC rule
        // and instead control this via subnet rules.
        //
        // We're going to create two independent network ACLs and use these to control
        // traffic entering and leaving the subnet (and by extension, the cluster).
        // The idea is that we'll alternate associating one of these rules with the
        // subnet.  This way we can perform potentially multiple operations to update
        // the network ACL not currently in use and then atomically replace the existing
        // ACL in one go.  This is much better than modifying the live ACL because that
        // could temporarily disrupt network traffic.
        //
        // Regions, Availability Zones and Placement Groups
        // ------------------------------------------------
        // neonKUBE clusters are currently deployed to a single AWS region and availability
        // zone within the region to ensure that internode communication will have low
        // latency.  Both of these are specified in the AWS cloud options.
        //
        // In theory we could allow users to distribute instances across availability
        // zones for better fault tolerance, but we're not going to support this now
        // and probably never.  The logic is that if you need more resilence, just deploy
        // another cluster in another availability zone or region and load balance
        // traffic between them.  I believe that will address most reasonable scenarios
        // and this will be easy to implement and test.  Of course, we'll revisit this
        // upon user demand.
        //
        // AWS supports three types of placement groups: cluster, partition, and spread:
        //
        //      https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/placement-groups.html
        //
        // neonKUBE will be deployed using two partition placement groups, one for control-plane
        // nodes and the other for workers.  The number of control-plane placement partitions is
        // controlled by [AwsHostingOptions.ControlPlanePlacementPartitions] which defaults to
        // the number of control-plane nodes in the cluster.  Doing this helps to avoid losing
        // a majority of the control-plane nodes with the loss of a single partition, which would
        // dramatically impact cluster functionality.
        //
        // Worker placement partitions are controlled by [AwsOptions.WorkerPlacementGroups].
        // This defaults to one partition, which means that potentially all of the workers
        // could be impacted due to a single hardware failure but it will be much more
        // likely that AWS will be able to satisfy the conditions and acutally provision
        // all of the nodes.  Users can increase the number of partitions and also optionally
        // assign worker nodes to specific partitions using [AwsNodeOptions.PlacementPartition].
        //
        // Idempotent Implementation
        // -------------------------
        // The AWS hosting manager is designed to be able to be interrupted and restarted
        // for cluster creation as well as management of the cluster afterwards.  This works
        // by reading the current state of the cluster resources.

        //---------------------------------------------------------------------
        // Local types

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
            public AwsInstance(NodeSshProxy<NodeDefinition> node, AwsHostingManager hostingManager)
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
            /// Returns the AWS instance ID.
            /// </summary>
            public string InstanceId => Instance?.InstanceId;

            /// <summary>
            /// Returns AWS instance information for the node.
            /// </summary>
            public Ec2Instance Instance { get; set; }

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

                    return instanceName = hostingManager.GetResourceName($"{Node.Name}");
                }
            }

            /// <summary>
            /// Returns the private IP address of the node.
            /// </summary>
            public string Address => Node.Address.ToString();

            /// <summary>
            /// Returns <c>true</c> if the node is a control-plane.
            /// </summary>
            public bool IsControlPlane => Node.Metadata.Role == NodeRole.ControlPlane;

            /// <summary>
            /// Returns <c>true</c> if the node is a worker.
            /// </summary>
            public bool IsWorker => Node.Metadata.Role == NodeRole.Worker;

            /// <summary>
            /// The external SSH port assigned to the instance.  This port is
            /// allocated from the range of external SSH ports configured for
            /// the cluster and is persisted as tag for each AWS instance.
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

        /// <summary>
        /// Enumerates the possible AWS instance status codes.  AWS doesn't seem
        /// to define an enumeration for this so we'll roll our own.  Note that
        /// the high byte of the 16-bit status code returned by the AWS API is
        /// for internal AWS use only and must be cleared before comparing the
        /// status to one of these values.
        /// </summary>
        private static class InstanceStateCode
        {
            /// <summary>
            /// The instance is provisioning or starting.
            /// </summary>
            public const int Pending = 0;

            /// <summary>
            /// The instance is running.
            /// </summary>
            public const int Running = 16;

            /// <summary>
            /// The instance is shutting down.
            /// </summary>
            public const int ShuttingDown = 32;

            /// <summary>
            /// The instance has been terminated.
            /// </summary>
            public const int Terminated = 48;

            /// <summary>
            /// The instance is stopping.
            /// </summary>
            public const int Stopping = 64;

            /// <summary>
            /// The instance has been stopped.
            /// </summary>
            public const int Stopped = 80;

            /// <summary>
            /// Clears the high byte of the raw code passed and returns one of the
            /// constants above.
            /// </summary>
            /// <param name="rawCode">The raw instance state code.</param>
            /// <returns>The cleaned code.</returns>
            public static int GetCode(int rawCode)
            {
                return rawCode & 0x00FF;
            }

            /// <summary>
            /// Determines whether the instance status passed indicates that the
            /// instance is pending.
            /// </summary>
            /// <param name="rawCode">The raw instance state code.</param>
            /// <returns><c>true</c> for pending.</returns>
            public static bool IsPending(int rawCode)
            {
                return (rawCode & 0x00FF) == Pending;
            }

            /// <summary>
            /// Determines whether the instance state passed indicates that the
            /// instance is running.
            /// </summary>
            /// <param name="rawCode">The raw instance state code.</param>
            /// <returns><c>true</c> for running.</returns>
            public static bool IsRunning(int rawCode)
            {
                return (rawCode & 0x00FF) == Running;
            }

            /// <summary>
            /// Determines whether the instance state passed indicates that the
            /// instance is stopping.
            /// </summary>
            /// <param name="rawCode">The raw instance state code.</param>
            /// <returns><c>true</c> for stopping.</returns>
            public static bool IsStopping(int rawCode)
            {
                return (rawCode & 0x00FF) == Stopping;
            }

            /// <summary>
            /// Determines whether the instance status passed indicates that the
            /// instance is stopped.
            /// </summary>
            /// <param name="rawCode">The raw instance state code.</param>
            /// <returns><c>true</c> for stopped.</returns>
            public static bool IsStopped(int rawCode)
            {
                return (rawCode & 0x00FF) == Stopping;
            }

            /// <summary>
            /// Determines whether the instance status passed indicates that the
            /// instance is terminated.
            /// </summary>
            /// <param name="rawCode">The raw instance state code.</param>
            /// <returns><c>true</c> for terminated.</returns>
            public static bool IsTerminated(int rawCode)
            {
                return (rawCode & 0x00FF) == Terminated;
            }
        }

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Used to limit how many threads will be created by parallel operations.
        /// </summary>
        private static readonly ParallelOptions parallelOptions = new ParallelOptions() { MaxDegreeOfParallelism = MaxAsyncParallelHostingOperations };

        /// <summary>
        /// Specifies the owner ID to use when querying for Canonical AMIs 
        /// </summary>
        private const string canonicalOwnerId = "099720109477";

        /// <summary>
        /// AWS generic name tag.
        /// </summary>
        private const string nameTagKey = "Name";

        /// <summary>
        /// The (namespace) prefix used for neonKUBE related AWS resource tags.
        /// </summary>
        private const string neonTagKeyPrefix = "neon:";

        /// <summary>
        /// Used to tag <b>neon-image tool</b> related resources so they can be managed
        /// by this class.  The tag value will be set to "true".
        /// </summary>
        private const string neonImageTagKey = neonTagKeyPrefix + "image";

        /// <summary>
        /// Used to tag instance volumes and published AMIs with the image type so 
        /// that the underlying snapshots created when the AMI is published will also 
        /// include this tag.  This is used to identify the snapshots that need to be
        /// deleted when the AMI is deleted.
        /// </summary>
        private const string neonImageTypeTagKey = neonTagKeyPrefix + "image-type";

        /// <summary>
        /// Used to tag instance volume, snapshots and published AMIs with the image 
        /// operating system.
        /// </summary>
        private const string neonImageOsTagKey = neonTagKeyPrefix + "image-os";

        /// <summary>
        /// Used to tag instance volume, snapshots and published AMIs with the image 
        /// architecture: <b>amd64</b> or <b>arm64</b>.
        /// </summary>
        private const string neonArchTagKey = neonTagKeyPrefix + "image-arch";

        /// <summary>
        /// Used to tag resources with the cluster name.
        /// </summary>
        private const string neonClusterTagKey = neonTagKeyPrefix + "cluster";

        /// <summary>
        /// Used to tag resources with the cluster environment.
        /// </summary>
        private const string neonEnvironmentTagKey = neonTagKeyPrefix + "environment";

        /// <summary>
        /// Used to tag instances resources with the cluster node name.
        /// </summary>
        private const string neonNodeNameTagKey = neonTagKeyPrefix + "node.name";

        /// <summary>
        /// Used to tag VM instances resources with the external SSH port to be used to 
        /// establish a SSH connection to the instance.
        /// </summary>
        private const string neonNodeSshTagKey = neonTagKeyPrefix + "node.ssh-port";

        /// <summary>
        /// Used to tag VM instances with an indication that the user-data passed
        /// on create has already been cleared.  This data includes the secure SSH
        /// password and we don't want to leave it around because it can be retrieved
        /// via the AWS portal.
        /// </summary>
        private const string neonNodeUserDataTagKey = neonTagKeyPrefix + "node.user-data";

        /// <summary>
        /// Used to tag VPC instances with a boolean indicating whether external
        /// SSH access to the cluster is currently enabled or disabled.
        /// </summary>
        private const string neonVpcSshEnabledTagKey = neonTagKeyPrefix + "vpc.ssh-enabled";

        /// <summary>
        /// The default deny everything network ACL rule number.
        /// </summary>
        private const int denyAllAclRuleNumber = 32767;

        /// <summary>
        /// The first network ACL rule number for internal rules.
        /// </summary>
        private const int firstInternalAclRuleNumber = 1;

        /// <summary>
        /// The first network ACL rule numberm for temporary SSH rules.
        /// </summary>
        private const int firstSshAclRuleNumber = 1000;

        /// <summary>
        /// The network ACL rule number for ingress rules.
        /// </summary>
        private const int firstIngressAclRuleNumber = 2000;

        /// <summary>
        /// The network ACL rule number for egress rules.
        /// </summary>
        private const int firstEgressAclRuleNumber = 2000;

        /// <summary>
        /// Identifies the instance VM block device for the OS disk. 
        /// </summary>
        private const string osDeviceName = "/dev/sda1";

        /// <summary>
        /// Identifies the instance VM block device for the data disk. 
        /// </summary>
        private const string dataDeviceName = "/dev/sdb";

        /// <summary>
        /// Identifies the target VM block device for the OpenEBS cStor disk. 
        /// </summary>
        private const string openEbsDeviceName = "/dev/sdf";

        /// <summary>
        /// Some AWS operations (like creating a NAT gateway or waiting for a load balancer
        /// target group to initialize and transition to healthy) can take a very long time 
        /// to complete.
        /// </summary>
        private static readonly TimeSpan operationTimeout = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Polling interval for slow operations.
        /// </summary>
        private static readonly TimeSpan operationPollInternal = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Ensures that the assembly hosting this hosting manager is loaded.
        /// </summary>
        public static void Load()
        {
            // We don't have to do anything here because the assembly is loaded
            // as a byproduct of calling this method.
        }

        /// <summary>
        /// Converts an <see cref="IngressProtocol"/> value into a <see cref="ProtocolEnum"/>.
        /// </summary>
        /// <param name="protocol">The ingress protocol.</param>
        /// <returns>The corresponding <see cref="ProtocolEnum"/>.</returns>
        private static ProtocolEnum ToElbProtocol(IngressProtocol protocol)
        {
            switch (protocol)
            {
                case IngressProtocol.Http:
                case IngressProtocol.Https:
                case IngressProtocol.Tcp:

                    // We're deploying a network load balancer, so all of these
                    // protocols map to TCP.

                    return ProtocolEnum.TCP;

                default:

                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Converts an <see cref="AwsVolumeType"/> into an <see cref="Ec2VolumeType"/>.
        /// </summary>
        /// <param name="volumeType">The input type.</param>
        /// <returns>The corresponding <see cref="Ec2VolumeType"/>.</returns>
        private static Ec2VolumeType ToEc2VolumeType(AwsVolumeType volumeType)
        {
            switch (volumeType)
            {
                case AwsVolumeType.Gp2: return Ec2VolumeType.Gp2;
                case AwsVolumeType.Io1: return Ec2VolumeType.Io1;
                case AwsVolumeType.Io2: return Ec2VolumeType.Io2;
                case AwsVolumeType.Sc1: return Ec2VolumeType.Sc1;
                case AwsVolumeType.St1: return Ec2VolumeType.St1;

                default:

                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Constructs a name suitable for a load balancer or load balancer related resource
        /// by combining the cluster name with the resource name.
        /// </summary>
        /// <param name="clusterName">The cluster name.</param>
        /// <param name="resourceName">The resource name.</param>
        /// <returns>The composed resource name.</returns>
        /// <remarks>
        /// <para>
        /// AWS Elastic Load Balancers and Target Groups need to have unique names for the
        /// account and region where the cluster is deployed.  These names are in addition
        /// to and independent of any name specified by the the resource tags.
        /// </para>
        /// <para>
        /// AWS places additional constraints on these unique names: only alphanumeric and
        /// dash characters are allowed and names may be up to 32 characters long.
        /// </para>
        /// <para>
        /// The problem is that neonKUBE cluster names may also include periods and underscores.
        /// This method converts any periods and underscores in the cluster name into dashes,
        /// appends the <paramref name="resourceName"/> with a leading dash and ensures that
        /// the combined name includes 32 characters or fewer.
        /// </para>
        /// <para>
        /// AWS also doesn't allow load balancer names to start with "internal-".  We're going
        /// to change this prefix to "x-internal-" in this case.
        /// </para>
        /// <note>
        /// <para>
        /// It's possible but very unlikely for a user to try to deploy two clusters to the
        /// same region who's cluster names differ only by a period and underscore.  For example
        /// <b>my.cluster</b> and <b>my_cluster</b>.  The period and underscore will bot be 
        /// converted to a dash which means that both clusters will try to provision ELB
        /// resources with the same <b>my-cluster</b> name prefix.  The second cluster deployment
        /// will fail with resource name conflicts.
        /// </para>
        /// <para>
        /// We're not going to worry about this.
        /// </para>
        /// </note>
        /// </remarks>
        private static string GetLoadBalancerName(string clusterName, string resourceName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(clusterName), nameof(clusterName));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(resourceName), nameof(resourceName));

            // $hack(jefflill):
            //
            // AWS doesn't tolerate target group names with periods and dashes so
            // convert both of these to dashes.  This is a bit fragile because it
            // assumes that users will never name two different clusters such that
            // the only difference is due to a period or underscore in place of a
            // dash.

            clusterName = clusterName.Replace('.', '-');
            clusterName = clusterName.Replace('_', '-');

            var name = $"{clusterName}-{resourceName}";

            if (name.StartsWith("internal-"))
            {
                name = "x-internal" + name.Substring("internal-".Length);
            }

            if (name.Length > 32)
            {
                throw new NeonKubeException($"Generated ELB related resource name [{name}] exceeds the 32 character AWS limit.");
            }

            return name;
        }

        //---------------------------------------------------------------------
        // Instance members

        private readonly TimeSpan timeout      = TimeSpan.FromMinutes(30);
        private readonly TimeSpan pollInterval = TimeSpan.FromSeconds(5);

        private bool                                cloudMarketplace;
        private ClusterProxy                        cluster;
        private string                              clusterName;
        private SetupController<NodeDefinition>     controller;
        private string                              clusterEnvironment;
        private HostingOptions                      hostingOptions;
        private CloudOptions                        cloudOptions;
        private AwsHostingOptions                   awsOptions;
        private NetworkOptions                      networkOptions;
        private BasicAWSCredentials                 awsCredentials;
        private string                              region;
        private string                              availabilityZone;
        private string                              resourceGroupName;
        private Region                              awsRegion;
        private RegionEndpoint                      regionEndpoint;
        private List<Filter>                        clusterFilter;      // Used to filter resources that belong to our cluster
        private AmazonEC2Client                     ec2Client;
        private AmazonElasticLoadBalancingV2Client  elbClient;
        private AmazonResourceGroupsClient          rgClient;

        // These are the names we'll use for cluster AWS resources.

        private string                              ingressAddressName;
        private string                              egressAddressName;
        private string                              vpcName;
        private string                              securityGroupName;
        private string                              publicSubnetName;
        private string                              nodeSubnetName;
        private string                              publicRouteTableName;
        private string                              nodeRouteTableName;
        private string                              internetGatewayName;
        private string                              natGatewayName;
        private string                              loadBalancerName;
        private string                              elbName;
        private string                              controlPlacementGroupName;
        private string                              workerPlacementGroupName;

        // These reference the AWS resources.

        private Image                               nodeImage;
        private Group                               resourceGroup;
        private Address                             ingressAddress;
        private Address                             egressAddress;
        private Vpc                                 vpc;
        private SecurityGroup                       securityGroup;
        private Subnet                              publicSubnet;
        private Subnet                              nodeSubnet;
        private RouteTable                          publicRouteTable;
        private RouteTable                          nodeRouteTable;
        private InternetGateway                     internetGateway;
        private NatGateway                          natGateway;
        private LoadBalancer                        loadBalancer;
        private PlacementGroup                      controlPlanePlacementGroup;
        private PlacementGroup                      workerPlacementGroup;

        /// <summary>
        /// Table mapping a ELB target group name to the group.
        /// </summary>
        private Dictionary<string, ElbTargetGroup> nameToTargetGroup;

        /// <summary>
        /// Table mapping a cluster node name to its AWS VM instance information.
        /// Note that <see cref="nodeNameToAwsInstance"/> and <see cref="instanceNameToAwsInstance"/>
        /// both refer to the same <see cref="AwsInstance"/> so a change to one value
        /// will be reflected in the other table.
        /// </summary>
        private Dictionary<string, AwsInstance> nodeNameToAwsInstance;

        /// <summary>
        /// Table mapping a cluster AWS instance name to its AWS VM instance information.
        /// Note that <see cref="nodeNameToAwsInstance"/> and <see cref="instanceNameToAwsInstance"/>
        /// both refer to the same <see cref="AwsInstance"/> so a change to one value
        /// will be reflected in the other table.
        /// </summary>
        private Dictionary<string, AwsInstance> instanceNameToAwsInstance;

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
        public AwsHostingManager(ClusterProxy cluster, bool cloudMarketplace, string nodeImageUri = null, string nodeImagePath = null, string logFolder = null)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null, nameof(cluster));

            cluster.HostingManager  = this;

            this.cloudMarketplace   = cloudMarketplace;
            this.cluster            = cluster;
            this.clusterName        = cluster.Name;
            this.clusterEnvironment = NeonHelper.EnumToString(cluster.Definition.Purpose);
            this.hostingOptions     = cluster.Definition.Hosting;
            this.cloudOptions       = hostingOptions.Cloud;
            this.awsOptions         = hostingOptions.Aws;
            this.networkOptions     = cluster.Definition.Network;
            this.region             = awsOptions.Region;
            this.availabilityZone   = awsOptions.AvailabilityZone;
            this.clusterFilter      = new List<Filter>()
            {
                new Filter()
                {
                    Name   = $"tag:{neonClusterTagKey}",
                    Values = new List<string>() { clusterName }
                }
            };

            // Apparently, resource group names cannot start with "aws".  We'll workaround this
            // by adding an (ugly) dash prefix.
            //
            //      https://github.com/nforgeio/neonKUBE/issues/1627

            this.resourceGroupName = cluster.Definition.Deployment.GetPrefixedName(awsOptions.ResourceGroup);

            if (this.resourceGroupName.StartsWith("aws", StringComparison.InvariantCultureIgnoreCase))
            {
                this.resourceGroupName = "-" + this.resourceGroupName;
            }

            // Initialize the cluster resource names.

            ingressAddressName        = GetResourceName("ingress-address");
            egressAddressName         = GetResourceName("egress-address");
            vpcName                   = GetResourceName("vpc");
            securityGroupName         = GetResourceName("security-group");
            publicSubnetName          = GetResourceName("public-subnet");
            nodeSubnetName            = GetResourceName("node-subnet");
            publicRouteTableName      = GetResourceName("public-route-table");
            nodeRouteTableName        = GetResourceName("node-route-table");
            internetGatewayName       = GetResourceName("internet-gateway");
            natGatewayName            = GetResourceName("nat-gateway");
            loadBalancerName          = GetResourceName("load-balancer");
            elbName                   = GetLoadBalancerName(clusterName, "elb");
            controlPlacementGroupName = GetResourceName("control-placement");
            workerPlacementGroupName  = GetResourceName("worker-placement");

            // Initialize the dictionary we'll use for mapping ELB target group
            // names to the specific target group.

            this.nameToTargetGroup = new Dictionary<string, ElbTargetGroup>();

            // This identifies the cluster manager instance with the cluster proxy
            // so that the proxy can have the hosting manager perform some operations
            // like managing the SSH port mappings on the load balancer.

            cluster.HostingManager = this;

            // Initialize the mappings between node and AWS instanc information.

            InitializeNodeDictionaries();
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
        private IEnumerable<AwsInstance> Nodes => nodeNameToAwsInstance.Values;

        /// <summary>
        /// Enumerates the cluster nodes in ascending order by name.
        /// </summary>
        private IEnumerable<AwsInstance> SortedNodes => Nodes.OrderBy(node => node.Name, StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Enumerates the cluster control-plane nodes in no particular order.
        /// </summary>
        private IEnumerable<AwsInstance> ControlNodes => Nodes.Where(node => node.IsControlPlane);

        /// <summary>
        /// Enumerates the cluster control-plane nodes in ascending order by name.
        /// </summary>
        private IEnumerable<AwsInstance> SortedControlNodes => Nodes.Where(node => node.IsControlPlane).OrderBy(node => node.Name, StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Enumerates the cluster worker nodes in no particular order.
        /// </summary>
        private IEnumerable<AwsInstance> WorkerNodes => Nodes.Where(node => node.IsControlPlane);

        /// <summary>
        /// Enumerates the cluster worker nodes in ascending order by name.
        /// </summary>
        private IEnumerable<AwsInstance> SorteWorkerNodes => Nodes.Where(node => node.IsWorker).OrderBy(node => node.Name, StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Enumerates the cluster worker nodes in ascending order by name followed by the sorted worker nodes.
        /// </summary>
        private IEnumerable<AwsInstance> SortedControlThenWorkerNodes => SortedControlNodes.Union(SorteWorkerNodes);

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

            return $"{clusterName}.{resourceName}";
        }

        /// <summary>
        /// Creates the tags for a resource including the resource name, 
        /// additional cluster details, any custom user resource tags, as well as any 
        /// optional tags passed.
        /// </summary>
        /// <typeparam name="T">Specifies the desired AWS tag type.</typeparam>
        /// <param name="name">The resource name.</param>
        /// <param name="tags">Any optional tags.</param>
        /// <returns>The tag list.</returns>
        private List<T> GetTags<T>(string name, params ResourceTag[] tags)
        {
            var tagList = new List<T>();

            tagList.Add(new Tag<T>(nameTagKey, name).ToAws());
            tagList.Add(new Tag<T>(neonClusterTagKey, clusterName).ToAws());
            tagList.Add(new Tag<T>(neonEnvironmentTagKey, clusterEnvironment).ToAws());

            if (cluster.Definition.ResourceTags != null)
            {
                foreach (var tag in cluster.Definition.ResourceTags)
                {
                    tagList.Add(new Tag<T>(tag.Key, tag.Value).ToAws());
                }
            }

            foreach (var tag in tags)
            {
                tagList.Add(new Tag<T>(tag.Key, tag.Value).ToAws());
            }

            return tagList;
        }

        /// <summary>
        /// Creates a tag specification for an EC2 resource including the resource name, 
        /// additional cluster details, any custom user resource tags, as well as any 
        /// optional tags passed.
        /// </summary>
        /// <param name="name">The resource name.</param>
        /// <param name="resourceType">The fully qualified resource type.</param>
        /// <param name="tags">Any optional tags.</param>
        /// <returns>The <see cref="TagSpecification"/> list with a single element.</returns>
        private List<TagSpecification> GetTagSpecifications(string name, ResourceType resourceType, params ResourceTag[] tags)
        {
            return new List<TagSpecification>()
            {
                new TagSpecification()
                {
                    ResourceType = resourceType,
                    Tags         = GetTags<Ec2Tag>(name, tags)
                }
            };
        }

        /// <inheritdoc/>
        public override HostingEnvironment HostingEnvironment => HostingEnvironment.Aws;

        /// <inheritdoc/>
        public override bool RequiresNodeAddressCheck => false;

        /// <inheritdoc/>
        public override void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            if (clusterDefinition.Hosting.Environment != HostingEnvironment.Aws)
            {
                throw new ClusterDefinitionException($"{nameof(HostingOptions)}.{nameof(HostingOptions.Environment)}] must be set to [{HostingEnvironment.Aws}].");
            }

            if (string.IsNullOrEmpty(clusterDefinition.Hosting.Aws.AccessKeyId))
            {
                throw new ClusterDefinitionException($"{nameof(AwsHostingOptions)}.{nameof(AwsHostingOptions.AccessKeyId)}] must be specified for AWS clusters.");
            }

            if (string.IsNullOrEmpty(clusterDefinition.Hosting.Aws.SecretAccessKey))
            {
                throw new ClusterDefinitionException($"{nameof(AwsHostingOptions)}.{nameof(AwsHostingOptions.SecretAccessKey)}] must be specified for AWS clusters.");
            }

            AssignNodeAddresses(clusterDefinition);

            // Set the cluster definition datacenter to the target region when the
            // user hasn't explictly specified a datacenter.

            if (string.IsNullOrEmpty(clusterDefinition.Datacenter))
            {
                clusterDefinition.Datacenter = clusterDefinition.Hosting.Aws.Region.ToUpperInvariant();
            }
        }

        /// <summary>
        /// Initializes the node mdictionaries: <see cref="nodeNameToAwsInstance"/> and <see cref="instanceNameToAwsInstance"/>.
        /// </summary>
        private void InitializeNodeDictionaries()
        {
            // Initialize the instance/node mapping dictionaries and also ensure
            // that each node has reasonable AWS node options.

            this.nodeNameToAwsInstance = new Dictionary<string, AwsInstance>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var node in cluster.Nodes)
            {
                nodeNameToAwsInstance.Add(node.Name, new AwsInstance(node, this));

                if (node.Metadata.Aws == null)
                {
                    node.Metadata.Aws = new AwsNodeOptions();
                }
            }

            this.instanceNameToAwsInstance = new Dictionary<string, AwsInstance>();

            foreach (var instanceInfo in nodeNameToAwsInstance.Values)
            {
                instanceNameToAwsInstance.Add(instanceInfo.InstanceName, instanceInfo);
            }
        }

        /// <inheritdoc/>
        public override void AddProvisioningSteps(SetupController<NodeDefinition> controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Assert(cluster != null, $"[{nameof(AwsHostingManager)}] was created with the wrong constructor.");

            var clusterLogin = controller?.Get<ClusterLogin>(KubeSetupProperty.ClusterLogin);

            this.controller = controller;

            // We need to ensure that the cluster has at least one ingress node.

            KubeHelper.EnsureIngressNodes(cluster.Definition);

            var operation = $"Provisioning [{cluster.Definition.Name}] on AWS [{availabilityZone}/{resourceGroupName}]";

            controller.AddGlobalStep("AWS connect", ConnectAwsAsync);
            controller.AddGlobalStep("region check", VerifyRegionAndInstanceTypesAsync);
            controller.AddGlobalStep("locate node image", LocateNodeImageAsync);
            controller.AddGlobalStep("resource group", CreateResourceGroupAsync);
            controller.AddGlobalStep("elastic ip", InitializeAddressessAsync);
            controller.AddGlobalStep("placement groups", ConfigurePlacementGroupAsync);
            controller.AddGlobalStep("network", ConfigureNetworkAsync);
            controller.AddNodeStep("node instances", CreateNodeInstanceAsync);
            controller.AddGlobalStep("ssh config", ConfigureNodeSsh);
            controller.AddNodeStep("credentials",
                (controller, node) =>
                {
                    // Update the node SSH proxies to use the secure SSH password.

                    node.UpdateCredentials(SshCredentials.FromUserPassword(KubeConst.SysAdminUser, clusterLogin.SshPassword));
                },
                quiet: true);
            controller.AddGlobalStep("load balancer", ConfigureLoadBalancerAsync);
            controller.AddNodeStep("load balancer targets", WaitForSshTargetAsync);
            controller.AddGlobalStep("internet access", async controller => await UpdateNetworkAsync(NetworkOperations.InternetRouting | NetworkOperations.EnableSsh));
        }

        /// <inheritdoc/>
        public override void AddPostProvisioningSteps(SetupController<NodeDefinition> controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Assert(object.ReferenceEquals(controller, this.controller));

            var cluster = this.controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);

            if (cluster.Definition.Storage.OpenEbs.Engine == OpenEbsEngine.cStor)
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
                        node.Status = "openebs: checking";

                        var volumeName        = GetResourceName($"{node.Name}-openebs");
                        var awsInstance       = nodeNameToAwsInstance[node.Name];
                        var openEbsVolumeType = ToEc2VolumeType(awsInstance.Metadata.Aws.OpenEbsVolumeType);
                        var volumePagenator   = ec2Client.Paginators.DescribeVolumes(new DescribeVolumesRequest() { Filters = clusterFilter });
                        var volume            = (Volume)null;

                        // Check if we've already created the volume.

                        await foreach (var volumeItem in volumePagenator.Volumes)
                        {
                            if (volumeItem.State != VolumeState.Deleting && 
                                volumeItem.State != VolumeState.Deleted &&
                                volumeItem.Tags.Any(tag => tag.Key == nameTagKey && tag.Value == volumeName) &&
                                volumeItem.Tags.Any(tag => tag.Key == neonClusterTagKey && tag.Value == clusterName))
                            {
                                volume = volumeItem;
                                break;
                            }
                        }

                        // Create the volume if it doesn't exist.

                        if (volume == null)
                        {
                            node.Status = "openebs: create cStor volume";

                            var volumeResponse = await ec2Client.CreateVolumeAsync(
                                new CreateVolumeRequest()
                                {
                                    AvailabilityZone   = availabilityZone,
                                    VolumeType         = openEbsVolumeType,
                                    Size               = (int)(ByteUnits.Parse(node.Metadata.Aws.OpenEbsVolumeSize) / ByteUnits.GibiBytes),
                                    MultiAttachEnabled = false,
                                    TagSpecifications  = GetTagSpecifications(volumeName, ResourceType.Volume, new ResourceTag(neonNodeNameTagKey, node.Name))
                                });

                            volume = volumeResponse.Volume;
                        }

                        // Wait for the volume to become available.

                        await NeonHelper.WaitForAsync(
                            async () =>
                            {
                                node.Status = "openebs: waiting for cStor volume...";

                                var volumePagenator = ec2Client.Paginators.DescribeVolumes(new DescribeVolumesRequest() { Filters = clusterFilter });

                                await foreach (var volumeItem in volumePagenator.Volumes)
                                {
                                    if (volumeItem.Tags.Any(tag => tag.Key == nameTagKey && tag.Value == volumeName) &&
                                        volumeItem.Tags.Any(tag => tag.Key == neonClusterTagKey && tag.Value == clusterName))
                                    {
                                        volume = volumeItem;
                                        break;
                                    }
                                }

                                return volume.State == VolumeState.Available || volume.State == VolumeState.InUse;
                            },
                            timeout:      operationTimeout,
                            pollInterval: operationPollInternal);

                        // Attach the volume to the VM if it's not already attached.

                        if (!volume.Attachments.Any(attachment => attachment.InstanceId == awsInstance.InstanceId))
                        {
                            await ec2Client.AttachVolumeAsync(
                                new AttachVolumeRequest()
                                {
                                    VolumeId   = volume.VolumeId,
                                    InstanceId = awsInstance.InstanceId,
                                    Device     = openEbsDeviceName,
                                });
                        }

                        // AWS defaults to deleting volumes on termination only for the
                        // volumes created along with the new instance.  We want the 
                        // OpenEBS cStor volume to be deleted as well.

                        await ec2Client.ModifyInstanceAttributeAsync(
                            new ModifyInstanceAttributeRequest()
                            {
                                InstanceId          = awsInstance.InstanceId,
                                BlockDeviceMappings = new List<InstanceBlockDeviceMappingSpecification>()
                                {
                                    new InstanceBlockDeviceMappingSpecification()
                                    {
                                        DeviceName = openEbsDeviceName,
                                        Ebs        = new EbsInstanceBlockDeviceSpecification()
                                        {
                                            DeleteOnTermination = true,
                                        }
                                    }
                                }
                            });
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

            controller.AddGlobalStep("connect aws",
                async controller =>
                {
                    await ConnectAwsAsync(controller);
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

            var operations = NetworkOperations.InternetRouting;

            // Update the SSH listeners if there are any SSH listeners already.  This will
            // be important after we support dynamically adding and removing cluster nodes.

            var listenerPagenator = elbClient.Paginators.DescribeListeners(
                new DescribeListenersRequest()
                {
                    LoadBalancerArn = loadBalancer.LoadBalancerArn
                });

            var listeners = new List<Listener>();

            await foreach (var listenerItem in listenerPagenator.Listeners)
            {
                listeners.Add(listenerItem);
            }

            if (listeners.Any(listener => networkOptions.FirstExternalSshPort <= listener.Port && listener.Port < networkOptions.LastExternalSshPort))
            {
                operations |= NetworkOperations.EnableSsh;
            }

            // Perform the update.

            await UpdateNetworkAsync(operations);
        }

        /// <inheritdoc/>
        public override async Task EnableInternetSshAsync()
        {
            await SyncContext.Clear;
            await UpdateNetworkAsync(NetworkOperations.EnableSsh);
        }

        /// <inheritdoc/>
        public override async Task DisableInternetSshAsync()
        {
            await SyncContext.Clear;
            await UpdateNetworkAsync(NetworkOperations.DisableSsh);
        }

        /// <inheritdoc/>
        public override (string Address, int Port) GetSshEndpoint(string nodeName)
        {
            ConnectAwsAsync(controller).WaitWithoutAggregate();

            if (!nodeNameToAwsInstance.TryGetValue(nodeName, out var awsInstance))
            {
                throw new NeonKubeException($"Node [{nodeName}] does not exist.");
            }

            Covenant.Assert(awsInstance.ExternalSshPort != 0, $"Node [{nodeName}] does not have an external SSH port assignment.");

            return (Address: ingressAddress.PublicIp, Port: awsInstance.ExternalSshPort);
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
            return new List<string>() { ingressAddress.PublicIp } ;
        }

        /// <summary>
        /// <para>
        /// Establishes the necessary client connection to AWS and validates the credentials,
        /// when a connection has not been established yet.
        /// </para>
        /// <note>
        /// The current state of the deployed resources will always be loaded by this method,
        /// even if an connection has already been established.
        /// </note>
        /// </summary>
        /// <param name="controller">Optionally specifies the setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task ConnectAwsAsync(ISetupController controller = null)
        {
            await SyncContext.Clear;

            controller?.SetGlobalStepStatus("connect: AWS");

            if (isConnected)
            {
                await LoadResourcesAsync();
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
                    throw new NeonKubeException($"AWS region [{region}] does not exist or is not available to your AWS account.");
                }
            }

            regionEndpoint = RegionEndpoint.GetBySystemName(region);
            ec2Client      = new AmazonEC2Client(awsCredentials, regionEndpoint);
            elbClient      = new AmazonElasticLoadBalancingV2Client(awsCredentials, regionEndpoint);
            rgClient       = new AmazonResourceGroupsClient(awsCredentials, regionEndpoint);

            // Load information about any existing cluster resources.

            await LoadResourcesAsync();
        }

        /// <summary>
        /// Loads references to any existing cluster resources.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task LoadResourcesAsync()
        {
            await SyncContext.Clear;

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
                // Ensure that the group was created exclusively for the cluster.

                var groupQueryResponse = await rgClient.GetGroupQueryAsync(
                    new GetGroupQueryRequest()
                    {
                        Group = resourceGroupName
                    });

                ValidateResourceGroupQuery(groupQueryResponse.GroupQuery);
            }

            // Elastic IPs

            if (awsOptions.Network.HasCustomElasticIPs)
            {
                var describeResponse = await ec2Client.DescribeAddressesAsync(
                    new DescribeAddressesRequest()
                    {
                        AllocationIds = new List<string>()
                         {
                             awsOptions.Network.ElasticIpIngressId,
                             awsOptions.Network.ElasticIpEgressId
                         }
                    });

                ingressAddress = describeResponse.Addresses.SingleOrDefault(address => address.AllocationId == awsOptions.Network.ElasticIpIngressId);
                egressAddress  = describeResponse.Addresses.SingleOrDefault(address => address.AllocationId == awsOptions.Network.ElasticIpEgressId);

                if (ingressAddress == null)
                {
                    throw new NeonKubeException($"Ingress Elastic IP [{awsOptions.Network.ElasticIpIngressId}] does not exist.");
                }

                if (egressAddress == null)
                {
                    throw new NeonKubeException($"Egress Elastic IP [{awsOptions.Network.ElasticIpEgressId}] does not exist.");
                }

                ingressAddressName = ingressAddress.Tags
                    .Where(tag => tag.Key == nameTagKey)
                    .Select(tag => tag.Value)
                    .Single();

                egressAddressName = egressAddress.Tags
                    .Where(tag => tag.Key == nameTagKey)
                    .Select(tag => tag.Value)
                    .Single();
            }
            else
            {
                ingressAddress = await GetElasticIpAsync(ingressAddressName);
                egressAddress  = await GetElasticIpAsync(egressAddressName);
            }

            // VPC and it's default network ACL.

            var vpcPaginator = ec2Client.Paginators.DescribeVpcs(new DescribeVpcsRequest() { Filters = clusterFilter });

            await foreach (var vpcItem in vpcPaginator.Vpcs)
            {
                if (vpcItem.Tags.Any(tag => tag.Key == nameTagKey && tag.Value == vpcName) &&
                    vpcItem.Tags.Any(tag => tag.Key == neonClusterTagKey && tag.Value == clusterName))
                {
                    vpc = vpcItem;
                    break;
                }
            }

            // Security Groups

            var securityGroupPagenator = ec2Client.Paginators.DescribeSecurityGroups(new DescribeSecurityGroupsRequest() { Filters = clusterFilter });

            await foreach (var securityGroupItem in securityGroupPagenator.SecurityGroups)
            {
                if (securityGroupItem.Tags.Any(tag => tag.Key == nameTagKey && tag.Value == securityGroupName) &&
                    securityGroupItem.Tags.Any(tag => tag.Key == neonClusterTagKey && tag.Value == clusterName))
                {
                    securityGroup = securityGroupItem;
                    break;
                }
            }

            // Public and node subnets

            var subnetPaginator = ec2Client.Paginators.DescribeSubnets(new DescribeSubnetsRequest() { Filters = clusterFilter });

            await foreach (var subnetItem in subnetPaginator.Subnets)
            {
                if (subnetItem.Tags.Any(tag => tag.Key == nameTagKey && tag.Value == publicSubnetName) &&
                    subnetItem.Tags.Any(tag => tag.Key == neonClusterTagKey && tag.Value == clusterName))
                {
                    publicSubnet = subnetItem;
                }
                else if (subnetItem.Tags.Any(tag => tag.Key == nameTagKey && tag.Value == nodeSubnetName) &&
                         subnetItem.Tags.Any(tag => tag.Key == neonClusterTagKey && tag.Value == clusterName))
                {
                    nodeSubnet = subnetItem;
                }

                if (publicSubnet != null && nodeSubnet != null)
                {
                    break;
                }
            }

            // Subnet route tables

            var routeTablePagenator = ec2Client.Paginators.DescribeRouteTables(new DescribeRouteTablesRequest() { Filters = clusterFilter });

            await foreach (var routeTableItem in routeTablePagenator.RouteTables)
            {
                if (routeTableItem.Tags.Any(tag => tag.Key == nameTagKey && tag.Value == publicRouteTableName) &&
                    routeTableItem.Tags.Any(tag => tag.Key == neonClusterTagKey && tag.Value == clusterName))
                {
                    publicRouteTable = routeTableItem;
                }
                else if (routeTableItem.Tags.Any(tag => tag.Key == nameTagKey && tag.Value == nodeRouteTableName) &&
                         routeTableItem.Tags.Any(tag => tag.Key == neonClusterTagKey && tag.Value == clusterName))
                {
                    nodeRouteTable = routeTableItem;
                }

                if (publicRouteTable != null && nodeRouteTable != null)
                {
                    break;
                }
            }

            // Internet and NAT Gateways

            internetGateway = await GetInternetGatewayAsync();
            natGateway      = await GetNatGatewayAsync();

            // Load Balancer

            loadBalancer = await GetLoadBalancerAsync();

            // Load balancer target groups.

            await LoadElbTargetGroupsAsync();

            // Placement groups

            var placementGroupPaginator = await ec2Client.DescribePlacementGroupsAsync(new DescribePlacementGroupsRequest() { Filters = clusterFilter });

            foreach (var placementGroupItem in placementGroupPaginator.PlacementGroups)
            {
                if (placementGroupItem.GroupName == controlPlacementGroupName)
                {
                    controlPlanePlacementGroup = placementGroupItem;
                }
                else if (placementGroupItem.GroupName == workerPlacementGroupName)
                {
                    workerPlacementGroup = placementGroupItem;
                }

                if (controlPlanePlacementGroup != null && workerPlacementGroup != null)
                {
                    break;  // We have both groups.
                }
            }

            // Instances

            var instancePaginator = ec2Client.Paginators.DescribeInstances(new DescribeInstancesRequest() { Filters = clusterFilter });

            await foreach (var reservation in instancePaginator.Reservations)
            {
                // Note that terminated instances will show up for a while, so we
                // need to ignore them.

                foreach (var instance in reservation.Instances
                    .Where(instance => instance.State.Name.Value != InstanceStateName.Terminated))
                {
                    var name        = instance.Tags.SingleOrDefault(tag => tag.Key == nameTagKey)?.Value;
                    var cluster     = instance.Tags.SingleOrDefault(tag => tag.Key == neonClusterTagKey)?.Value;
                    var awsInstance = (AwsInstance)null;

                    if (name != null && cluster == clusterName && instanceNameToAwsInstance.TryGetValue(name, out awsInstance))
                    {
                        awsInstance.Instance = instance;
                    }

                    // Retrieve the external SSH port for the instance from the instance tag.

                    var sshPortString = instance.Tags.SingleOrDefault(tag => tag.Key == neonNodeSshTagKey)?.Value;

                    if (!string.IsNullOrEmpty(sshPortString) && int.TryParse(sshPortString, out var sshPort) && NetHelper.IsValidPort(sshPort))
                    {
                        awsInstance.ExternalSshPort = sshPort;
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to locate the cluster load balancer.
        /// </summary>
        /// <returns>The load balancer or <c>null</c>.</returns>
        private async Task<LoadBalancer> GetLoadBalancerAsync()
        {
            await SyncContext.Clear;

            var loadBalancerPaginator = elbClient.Paginators.DescribeLoadBalancers(new DescribeLoadBalancersRequest());

            await foreach (var loadBalancerItem in loadBalancerPaginator.LoadBalancers)
            {
                if (loadBalancerItem.LoadBalancerName == elbName)
                {
                    return loadBalancerItem;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the internet gateway.
        /// </summary>
        /// <returns>The internet gateway or <c>null</c>.</returns>
        private async Task<InternetGateway> GetInternetGatewayAsync()
        {
            await SyncContext.Clear;

            var gatewayPaginator = ec2Client.Paginators.DescribeInternetGateways(new DescribeInternetGatewaysRequest() { Filters = clusterFilter });

            await foreach (var gatewayItem in gatewayPaginator.InternetGateways)
            {
                if (gatewayItem.Tags.Any(tag => tag.Key == nameTagKey && tag.Value == internetGatewayName) &&
                    gatewayItem.Tags.Any(tag => tag.Key == neonClusterTagKey && tag.Value == clusterName))
                {
                    return gatewayItem;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the NAT gateway.
        /// </summary>
        /// <returns>The internet gateway or <c>null</c>.</returns>
        private async Task<NatGateway> GetNatGatewayAsync()
        {
            await SyncContext.Clear;

            var gatewayPaginator = ec2Client.Paginators.DescribeNatGateways(new DescribeNatGatewaysRequest());

            await foreach (var gatewayItem in gatewayPaginator.NatGateways)
            {
                if (gatewayItem.State != NatGatewayState.Deleted &&
                    gatewayItem.Tags.Any(tag => tag.Key == nameTagKey && tag.Value == natGatewayName) &&
                    gatewayItem.Tags.Any(tag => tag.Key == neonClusterTagKey && tag.Value == clusterName))
                {
                    return gatewayItem;
                }
            }

            return null;
        }

        /// <summary>
        /// Loads information about any load balancer target groups into
        /// <see cref="nameToTargetGroup"/>.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task LoadElbTargetGroupsAsync()
        {
            await SyncContext.Clear;

            nameToTargetGroup.Clear();

            if (vpc == null)
            {
                return; // There can't be any target groups without a VPC.
            }

            var targetGroupPagenator = elbClient.Paginators.DescribeTargetGroups(new DescribeTargetGroupsRequest());

            await foreach (var targetGroup in targetGroupPagenator.TargetGroups)
            {
                if (targetGroup.VpcId == vpc.VpcId)
                {
                    nameToTargetGroup.Add(targetGroup.TargetGroupName, targetGroup);
                }
            }
        }

        /// <summary>
        /// Returns the <see cref="NetworkAcl"/> with the specified ID.
        /// </summary>
        /// <param name="networkAclId">The target network ACL ID.</param>
        /// <returns>The <see cref="NetworkAcl"/>.</returns>
        private async Task<NetworkAcl> GetNetworkAclAsync(string networkAclId)
        {
            await SyncContext.Clear;

            // $todo(jefflill): This would be more efficient with a filter.

            var networkAclPagenator = ec2Client.Paginators.DescribeNetworkAcls(new DescribeNetworkAclsRequest() { Filters = clusterFilter });

            await foreach (var networkAclItem in networkAclPagenator.NetworkAcls)
            {
                if (networkAclItem.NetworkAclId == networkAclId)
                {
                    return networkAclItem;
                }
            }

            Covenant.Assert(false, $"Network ACL [id={networkAclId}] not found.");
            return null;
        }

        /// <summary>
        /// Returns the VPC's network ACL.
        /// </summary>
        /// <param name="vpc">The VPC.</param>
        /// <returns>The network ACL.</returns>
        private async Task<NetworkAcl> GetVpcNetworkAclAsync(Vpc vpc)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(vpc != null, nameof(vpc));

            var associationPaginator = ec2Client.Paginators.DescribeNetworkAcls(new DescribeNetworkAclsRequest() { Filters = clusterFilter });

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
        /// Verifies that the requested AWS region and availability zone exists and supports 
        /// the requested VM sizes.  We'll also verify that the requested VMs have the minimum 
        /// required number or cores and RAM.
        /// </para>
        /// <para>
        /// This also updates the node labels to match the capabilities of their VMs.
        /// </para>
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task VerifyRegionAndInstanceTypesAsync(ISetupController controller)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            controller.SetGlobalStepStatus("verify: AWS region, availibility zone, and machine types exist");

            var regionName = awsOptions.Region;
            var zoneName   = awsOptions.AvailabilityZone;

            // Verify that the zone and (implicitly) the region exist.

            var regionsResponse = await ec2Client.DescribeRegionsAsync(new DescribeRegionsRequest());

            if (!regionsResponse.Regions.Any(region => region.RegionName.Equals(regionName, StringComparison.InvariantCultureIgnoreCase)))
            {
                throw new NeonKubeException($"The AWS [{nameof(AwsHostingOptions)}.{nameof(AwsHostingOptions.AvailabilityZone)}={zoneName}] does not exist.");
            }

            // Verify that the instance types required by the cluster are available in the region
            // and also that all instance types support the [x86_64].

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
                    throw new NeonKubeException($"Node [{node.Name}] requests [{nameof(node.Metadata.Aws.InstanceType)}={instanceType}].  This is not available in the [{regionName}] AWS region.");
                }

                if (!instanceTypeInfo.ProcessorInfo.SupportedArchitectures.Any(architecture => architecture == "x86_64"))
                {
                    throw new NeonKubeException($"Node [{node.Name}] requests [{nameof(node.Metadata.Aws.InstanceType)}={instanceType}] which does not support the [x86_64] architecture.");
                }

                switch (node.Metadata.Role)
                {
                    case NodeRole.ControlPlane:

                        if (instanceTypeInfo.VCpuInfo.DefaultVCpus < KubeConst.MinControlNodeCores)
                        {
                            throw new NeonKubeException($"Control-plane node [{node.Name}] requests [{nameof(node.Metadata.Aws.InstanceType)}={instanceType}] with [Cores={instanceTypeInfo.VCpuInfo.DefaultVCpus}] which is lower than the required [{KubeConst.MinControlNodeCores}] cores.]");
                        }

                        if (instanceTypeInfo.MemoryInfo.SizeInMiB < KubeConst.MinControlNodeRamMiB)
                        {
                            throw new NeonKubeException($"Control-plane node [{node.Name}] requests [{nameof(node.Metadata.Aws.InstanceType)}={instanceType}] with [RAM={instanceTypeInfo.MemoryInfo.SizeInMiB} MiB] which is lower than the required [{KubeConst.MinControlNodeRamMiB} MiB].]");
                        }
                        break;

                    case NodeRole.Worker:

                        if (instanceTypeInfo.VCpuInfo.DefaultVCpus < KubeConst.MinWorkerCores)
                        {
                            throw new NeonKubeException($"Worker node [{node.Name}] requests [{nameof(node.Metadata.Aws.InstanceType)}={instanceType}] with [Cores={instanceTypeInfo.VCpuInfo.DefaultVCpus}] which is lower than the required [{KubeConst.MinWorkerCores}] cores.]");
                        }

                        if (instanceTypeInfo.MemoryInfo.SizeInMiB < KubeConst.MinWorkerRamMiB)
                        {
                            throw new NeonKubeException($"Worker node [{node.Name}] requests [{nameof(node.Metadata.Aws.InstanceType)}={instanceType}] with [RAM={instanceTypeInfo.MemoryInfo.SizeInMiB} MiB] which is lower than the required [{KubeConst.MinWorkerRamMiB} MiB].]");
                        }
                        break;

                    default:

                        throw new NotImplementedException();
                }

                // Update the node labels to match the actual VM capabilities.

                node.Metadata.Labels.ComputeCores     = instanceTypeInfo.VCpuInfo.DefaultVCpus;
                node.Metadata.Labels.ComputeRam       = (int)instanceTypeInfo.MemoryInfo.SizeInMiB;
                node.Metadata.Labels.StorageSize      = $"{AwsHelper.GetVolumeSizeGiB(node.Metadata.Aws.VolumeType, ByteUnits.Parse(node.Metadata.Aws.VolumeSize))} GiB";
                node.Metadata.Labels.StorageHDD       = node.Metadata.Aws.VolumeType == AwsVolumeType.St1 || node.Metadata.Aws.VolumeType == AwsVolumeType.Sc1;
                node.Metadata.Labels.StorageEphemeral = false;
                node.Metadata.Labels.StorageLocal     = false;
                node.Metadata.Labels.StorageRedundant = true;
            }
        }

        /// <summary>
        /// Locates the AMI to use for provisioning the nodes in the target region.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task LocateNodeImageAsync(ISetupController controller)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            // Locate the node image AMI.
            // 
            // ALPHA RELEASES
            // --------------
            // We'll use the local AMI from the current NEONFORGE AWS account, 
            // matching the tags applied by the [neon-image} tool.
            //
            // PRODUCTION RELEASES
            // -------------------
            // We'll use the matching AMI from our AWS Marketplace offering.

            controller.SetGlobalStepStatus("locate: node image");

            var neonKubeVersion = SemanticVersion.Parse(KubeVersions.NeonKube);
            var nodeImageName   = $"neonkube-{KubeVersions.NeonKube}";
            var operatingSystem = "ubuntu-22.04";
            var architecture    = "amd64";

            if (neonKubeVersion.Prerelease != null && neonKubeVersion.Prerelease.StartsWith("alpha", StringComparison.InvariantCultureIgnoreCase))
            {
                var neonImageFilter = new List<Filter>()
                {
                    new Filter() { Name = $"tag:{neonImageTagKey}", Values = new List<string>() { "true" } }
                };

                var response = await ec2Client.DescribeImagesAsync(
                    new DescribeImagesRequest()
                    {
                        Filters = neonImageFilter,
                    });

                nodeImage = response.Images
                    .Where(image => image.Name == nodeImageName &&
                                    image.Tags.Any(tag => tag.Key == neonImageTypeTagKey && tag.Value == "node") &&
                                    image.Tags.Any(tag => tag.Key == neonImageOsTagKey && tag.Value == operatingSystem) &&
                                    image.Tags.Any(tag => tag.Key == neonArchTagKey && tag.Value == architecture))
                    .SingleOrDefault();

                if (nodeImage == null)
                {
                    throw new NeonKubeException($"Cannot locate the node image AMI for [{nodeImageName}: {operatingSystem}/{architecture}] in region: [{region}]");
                }
            }
            else
            {
                throw new NotImplementedException("$todo(jefflill): Implement AWS Marketplace support.");
            }
        }

        /// <summary>
        /// Returns the cluster's resource group query JSON.
        /// </summary>
        private string ResourceGroupQuery => $"{{\"ResourceTypeFilters\":[\"AWS::AllSupported\"],\"TagFilters\":[{{\"Key\":\"{neonClusterTagKey}\",\"Values\":[\"{clusterName}\"]}}]}}";

        /// <summary>
        /// Ensures that the resource group query was created exclusively for cluster.
        /// </summary>
        /// <param name="groupQuery">The resource group Query.</param>
        /// <exception cref="NeonKubeException">Thrown if the resource group is not valid.</exception>
        private void ValidateResourceGroupQuery(GroupQuery groupQuery)
        {
            if (groupQuery.ResourceQuery.Type != QueryType.TAG_FILTERS_1_0 ||
                groupQuery.ResourceQuery.Query != ResourceGroupQuery)
            {
                throw new NeonKubeException($"Invalid resource group [{resourceGroupName}]: This resource group already exists for some other purpose or was edited after being created for a neonKUBE cluster.");
            }
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
        /// <param name="controller">The setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task CreateResourceGroupAsync(ISetupController controller)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            controller.SetGlobalStepStatus($"create: [{resourceGroupName}] resource group");

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
                            {  neonClusterTagKey, clusterName }
                        },
                        ResourceQuery = new ResourceQuery()
                        {
                            Type  = QueryType.TAG_FILTERS_1_0,
                            Query = ResourceGroupQuery
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

                ValidateResourceGroupQuery(groupQueryResponse.GroupQuery);
            }
        }

        /// <summary>
        /// Creates the the control-plane and worker placement groups used to provision the cluster node instances.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task ConfigurePlacementGroupAsync(ISetupController controller)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            if (controlPlanePlacementGroup == null)
            {
                controller.SetGlobalStepStatus($"configure: [{controlPlacementGroupName}] placement group");

                var partitionGroupResponse = await ec2Client.CreatePlacementGroupAsync(
                    new CreatePlacementGroupRequest(controlPlacementGroupName, PlacementStrategy.Partition)
                    {
                        PartitionCount    = awsOptions.ControlPlanePlacementPartitions,
                        TagSpecifications = GetTagSpecifications(controlPlacementGroupName, ResourceType.PlacementGroup)
                    });

                controlPlanePlacementGroup = partitionGroupResponse.PlacementGroup;
            }

            if (workerPlacementGroup == null)
            {
                controller.SetGlobalStepStatus($"configure: [{workerPlacementGroupName}] placement group");

                var partitionGroupResponse = await ec2Client.CreatePlacementGroupAsync(
                    new CreatePlacementGroupRequest(workerPlacementGroupName, PlacementStrategy.Partition)
                    {
                        PartitionCount    = awsOptions.WorkerPlacementPartitions,
                        TagSpecifications = GetTagSpecifications(workerPlacementGroupName, ResourceType.PlacementGroup)
                    });

                workerPlacementGroup = partitionGroupResponse.PlacementGroup;
            }
        }

        /// <summary>
        /// Returns the cluster's elastic IP address.
        /// </summary>
        /// <param name="addressName">The address name.</param>
        /// <returns>The elastic IP address or <c>null</c> if it doesn't exist.</returns>
        private async Task<Address> GetElasticIpAsync(string addressName)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(addressName), nameof(addressName));

            var addressResponse = await ec2Client.DescribeAddressesAsync();

            foreach (var addressItem in addressResponse.Addresses)
            {
                if (addressItem.Tags.Any(tag => tag.Key == nameTagKey && tag.Value == addressName) &&
                    addressItem.Tags.Any(tag => tag.Key == neonClusterTagKey && tag.Value == clusterName))
                {
                    return addressItem;
                }
            }

            return null;
        }

        /// <summary>
        /// Creates the ingress and egress elastic IP addresses for the cluster if they don't already exist
        /// or ensures that any existing Elastic IPs specified in the cluster definition actually exist.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task InitializeAddressessAsync(ISetupController controller)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            if (awsOptions.Network.HasCustomElasticIPs)
            {
                controller.SetGlobalStepStatus("check: elastic IP addresses");

                var describeResponse = await ec2Client.DescribeAddressesAsync(
                    new DescribeAddressesRequest()
                    {
                         AllocationIds = new List<string>()
                         {
                             awsOptions.Network.ElasticIpIngressId,
                             awsOptions.Network.ElasticIpEgressId
                         }
                    });

                ingressAddress = describeResponse.Addresses.SingleOrDefault(address => address.AllocationId == awsOptions.Network.ElasticIpIngressId);
                egressAddress  = describeResponse.Addresses.SingleOrDefault(address => address.AllocationId == awsOptions.Network.ElasticIpEgressId);

                if (ingressAddress == null)
                {
                    throw new NeonKubeException($"Ingress Elastic IP [{awsOptions.Network.ElasticIpIngressId}] does not exist.");
                }

                if (egressAddress == null)
                {
                    throw new NeonKubeException($"Egress Elastic IP [{awsOptions.Network.ElasticIpEgressId}] does not exist.");
                }

                ingressAddressName = ingressAddress.Tags
                    .Where(tag => tag.Key == nameTagKey)
                    .Select(tag => tag.Value)
                    .Single();

                egressAddressName = egressAddress.Tags
                    .Where(tag => tag.Key == nameTagKey)
                    .Select(tag => tag.Value)
                    .Single();
            }
            else
            {
                controller.SetGlobalStepStatus("create: elastic IP addresses");

                if (ingressAddress == null)
                {
                    var allocateResponse = await ec2Client.AllocateAddressAsync(
                        new AllocateAddressRequest()
                        {
                            Domain = DomainType.Vpc
                        });

                    var addressId = allocateResponse.AllocationId;

                    await ec2Client.CreateTagsAsync(
                        new CreateTagsRequest()
                        {
                            Resources = new List<string>() { addressId },
                            Tags      = GetTags<Ec2Tag>(ingressAddressName)
                        });

                    // Retrieve the ingress address resource.

                    var describeResponse = await ec2Client.DescribeAddressesAsync();

                    foreach (var addr in describeResponse.Addresses)
                    {
                        if (addr.Tags.Any(tag => tag.Key == nameTagKey && tag.Value == ingressAddressName) &&
                            addr.Tags.Any(tag => tag.Key == neonClusterTagKey && tag.Value == clusterName))
                        {
                            ingressAddress = addr;
                            break;
                        }
                    }
                }

                if (egressAddress == null)
                {
                    var allocateResponse = await ec2Client.AllocateAddressAsync(
                        new AllocateAddressRequest()
                        {
                            Domain = DomainType.Vpc
                        });

                    var addressId = allocateResponse.AllocationId;

                    await ec2Client.CreateTagsAsync(
                        new CreateTagsRequest()
                        {
                            Resources = new List<string>() { addressId },
                            Tags      = GetTags<Ec2Tag>(egressAddressName)
                        });

                    // Retrieve the egress address resource.

                    var addressResponse = await ec2Client.DescribeAddressesAsync();

                    foreach (var addr in addressResponse.Addresses)
                    {
                        if (addr.Tags.Any(tag => tag.Key == nameTagKey && tag.Value == egressAddressName) &&
                            addr.Tags.Any(tag => tag.Key == neonClusterTagKey && tag.Value == clusterName))
                        {
                            egressAddress = addr;
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Creates the egress elastic IP address for the cluster if it doesn't already exist.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task CreateEgressAddressAsync()
        {
            await SyncContext.Clear;

            if (egressAddress == null)
            {
                var allocateResponse = await ec2Client.AllocateAddressAsync(
                    new AllocateAddressRequest()
                    {
                        Domain = DomainType.Vpc
                    });

                var addressId = allocateResponse.AllocationId;

                await ec2Client.CreateTagsAsync(
                    new CreateTagsRequest()
                    {
                        Resources = new List<string>() { addressId },
                        Tags = GetTags<Ec2Tag>(egressAddressName)
                    });

                // Retrieve the elastic IP resource.

                var addressResponse = await ec2Client.DescribeAddressesAsync();

                foreach (var addr in addressResponse.Addresses)
                {
                    if (addr.Tags.Any(tag => tag.Key == nameTagKey && tag.Value == egressAddressName) &&
                        addr.Tags.Any(tag => tag.Key == neonClusterTagKey && tag.Value == clusterName))
                    {
                        egressAddress = addr;
                        break;
                    }
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

            foreach (var instance in instanceNameToAwsInstance.Values.Where(awsInstance => awsInstance.ExternalSshPort != 0))
            {
                allocatedPorts.Add(instance.ExternalSshPort);
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

            foreach (var awsInstance in SortedControlThenWorkerNodes.Where(awsInstance => awsInstance.ExternalSshPort == 0))
            {
                awsInstance.ExternalSshPort = unallocatedPorts[nextUnallocatedPortIndex++];
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

            Covenant.Assert(ingressAddress != null);

            foreach (var node in cluster.Nodes)
            {
                var awsInstance = nodeNameToAwsInstance[node.Name];

                node.Address = IPAddress.Parse(ingressAddress.PublicIp);
                node.SshPort = awsInstance.ExternalSshPort;
            }
        }

        /// <summary>
        /// Configures the cluster networking components including the VPC, subnet, internet gateway
        /// security group and network ACLs.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task ConfigureNetworkAsync(ISetupController controller)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            controller.SetGlobalStepStatus("configure: cluster network");

            // Create the VPC.

            if (vpc == null)
            {
                var vpcResponse = await ec2Client.CreateVpcAsync(
                    new CreateVpcRequest()
                    {
                        CidrBlock         = awsOptions.Network.VpcSubnet,
                        TagSpecifications = GetTagSpecifications(vpcName, ResourceType.Vpc)
                    });;

                vpc = vpcResponse.Vpc;
            }

            // Create the ALLOW-ALL security group if it doesn't exist.

            if (securityGroup == null)
            {
                var securityGroupResponse = await ec2Client.CreateSecurityGroupAsync(
                    new CreateSecurityGroupRequest()
                    {
                        GroupName         = securityGroupName,
                        Description       = "Allow all traffic",
                        VpcId             = vpc.VpcId,
                        TagSpecifications = GetTagSpecifications(securityGroupName, ResourceType.SecurityGroup)
                    });

                var securityGroupId = securityGroupResponse.GroupId;

                await NeonHelper.WaitForAsync(
                    async () =>
                    {
                        var securityGroupPagenator = ec2Client.Paginators.DescribeSecurityGroups(new DescribeSecurityGroupsRequest() { Filters = clusterFilter });

                        await foreach (var securityGroupItem in securityGroupPagenator.SecurityGroups)
                        {
                            if (securityGroupItem.GroupId == securityGroupId)
                            {
                                securityGroup = securityGroupItem;
                                break;
                            }
                        }

                        return securityGroup != null;
                    },
                    timeout:      timeout,
                    pollInterval: pollInterval);

                Covenant.Assert(securityGroup != null);

                // Security groups are created with an ALLOW-ALL egress rule.  We need to add
                // the same for ingress.   Note that this is not a security vulnerablity because
                // the load balancer only forwards traffic for explicit listeners and we also
                // use network ACLs to secure the network.

                if (securityGroup.IpPermissions.Count == 0)
                {
                    await ec2Client.AuthorizeSecurityGroupIngressAsync(
                        new AuthorizeSecurityGroupIngressRequest()
                        {
                            GroupId       = securityGroup.GroupId,
                            IpPermissions = new List<IpPermission>
                            {
                                new IpPermission()
                                {
                                    IpProtocol = "-1",      // All protocols
                                    FromPort   = 0,
                                    ToPort     = ushort.MaxValue,
                                    Ipv4Ranges = new List<IpRange>()
                                    {
                                        new IpRange()
                                        {
                                            CidrIp = "0.0.0.0/0"
                                        }
                                    }
                                }
                            }
                        });
                }
            }

            // Create the public and node subnets alonhg with their route tables
            // and associate them with the VPC.

            if (publicSubnet == null)
            {
                var subnetResponse = await ec2Client.CreateSubnetAsync(
                    new CreateSubnetRequest(vpc.VpcId, awsOptions.Network.PublicSubnet)
                    {
                        VpcId             = vpc.VpcId,
                        AvailabilityZone  = awsOptions.AvailabilityZone,
                        TagSpecifications = GetTagSpecifications(publicSubnetName, ResourceType.Subnet)
                    });

                publicSubnet = subnetResponse.Subnet;
            }

            if (nodeSubnet == null)
            {
                var subnetResponse = await ec2Client.CreateSubnetAsync(
                    new CreateSubnetRequest(vpc.VpcId, awsOptions.Network.NodeSubnet)
                    {
                        VpcId             = vpc.VpcId,
                        AvailabilityZone  = awsOptions.AvailabilityZone,
                        TagSpecifications = GetTagSpecifications(nodeSubnetName, ResourceType.Subnet)
                    });

                nodeSubnet = subnetResponse.Subnet;
            }

            if (publicRouteTable == null)
            {
                var routeTableResponse = await ec2Client.CreateRouteTableAsync(
                    new CreateRouteTableRequest()
                    {
                        VpcId             = vpc.VpcId,
                        TagSpecifications = GetTagSpecifications(publicRouteTableName, ResourceType.RouteTable)
                    });

                publicRouteTable = routeTableResponse.RouteTable;
            }

            if (!publicRouteTable.Associations.Any(association => association.SubnetId == publicSubnet.SubnetId))
            {
                await ec2Client.AssociateRouteTableAsync(
                    new AssociateRouteTableRequest()
                    {
                        RouteTableId = publicRouteTable.RouteTableId,
                        SubnetId     = publicSubnet.SubnetId
                    });
            }

            if (nodeRouteTable == null)
            {
                var routeTableResponse = await ec2Client.CreateRouteTableAsync(
                    new CreateRouteTableRequest()
                    {
                        VpcId             = vpc.VpcId,
                        TagSpecifications = GetTagSpecifications(nodeRouteTableName, ResourceType.RouteTable)
                    });

                nodeRouteTable = routeTableResponse.RouteTable;
            }

            if (!nodeRouteTable.Associations.Any(association => association.SubnetId == nodeSubnet.SubnetId))
            {
                await ec2Client.AssociateRouteTableAsync(
                    new AssociateRouteTableRequest()
                    {
                        RouteTableId = nodeRouteTable.RouteTableId,
                        SubnetId     = nodeSubnet.SubnetId
                    });
            }

            // Create the internet gateway and attach it to the VPC.

            if (internetGateway == null)
            {
                var gatewayResponse = await ec2Client.CreateInternetGatewayAsync(
                    new CreateInternetGatewayRequest()
                    {
                        TagSpecifications = GetTagSpecifications(internetGatewayName, ResourceType.InternetGateway)
                    });

                internetGateway = gatewayResponse.InternetGateway;
            }

            if (!internetGateway.Attachments.Any(association => association.VpcId == vpc.VpcId))
            {
                await ec2Client.AttachInternetGatewayAsync(
                    new AttachInternetGatewayRequest()
                    {
                        VpcId             = vpc.VpcId,
                        InternetGatewayId = internetGateway.InternetGatewayId
                    });
            }

            // Add a default route to the public subnet that sends traffic 
            // to the Internet Gateway.

            if (!publicRouteTable.Routes.Any(route => route.GatewayId == internetGateway.InternetGatewayId))
            {
                await ec2Client.CreateRouteAsync(
                    new CreateRouteRequest()
                    {
                        RouteTableId         = publicRouteTable.RouteTableId,
                        GatewayId            = internetGateway.InternetGatewayId,
                        DestinationCidrBlock = "0.0.0.0/0"
                    });
            }

            // Create the NAT gateway and attach it to the public subnet.  Note that it
            // can take some time for the NAT Gateway be be available, so we'll have
            // to wait.

            if (natGateway == null)
            {
                var natGatewayResponse = await ec2Client.CreateNatGatewayAsync(
                    new CreateNatGatewayRequest()
                    {
                        SubnetId          = publicSubnet.SubnetId,
                        AllocationId      = egressAddress.AllocationId,
                        TagSpecifications = GetTagSpecifications(natGatewayName, ResourceType.Natgateway)
                    });

                natGateway = natGatewayResponse.NatGateway;
            }

            await NeonHelper.WaitForAsync(
                async () =>
                {
                    natGateway = await GetNatGatewayAsync();

                    if (natGateway.State == NatGatewayState.Pending)
                    {
                        return false;
                    }
                    else if (natGateway.State == NatGatewayState.Available)
                    {
                        return true;
                    }
                    else
                    {
                        throw new NeonKubeException($"Unexpected NAT Gateway state: [{natGateway.State}].");
                    }
                },
                timeout:      operationTimeout,
                pollInterval: operationPollInternal);

            // Add a default route to the node subnet that sends traffic to
            // the NAT gateway.

            if (!nodeRouteTable.Routes.Any(route => route.NatGatewayId == natGateway.NatGatewayId))
            {
                await ec2Client.CreateRouteAsync(
                    new CreateRouteRequest()
                    {
                        RouteTableId         = nodeRouteTable.RouteTableId,
                        GatewayId            = natGateway.NatGatewayId,
                        DestinationCidrBlock = "0.0.0.0/0"
                    });
            }
        }

        /// <summary>
        /// Configures the load balancer.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task ConfigureLoadBalancerAsync(ISetupController controller)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            // Create the load balancer in the subnet.

            if (loadBalancer == null)
            {
                controller.SetGlobalStepStatus("create: cluster load balancer");

                await elbClient.CreateLoadBalancerAsync(
                    new CreateLoadBalancerRequest()
                    {
                        Name           = elbName,
                        Type           = LoadBalancerTypeEnum.Network,
                        Scheme         = LoadBalancerSchemeEnum.InternetFacing,
                        SubnetMappings = new List<SubnetMapping>()
                        {
                            new SubnetMapping() { SubnetId = publicSubnet.SubnetId, AllocationId = ingressAddress.AllocationId }
                        },
                        IpAddressType  = IpAddressType.Ipv4,
                        Tags           = GetTags<ElbTag>("load-balancer"),
                    });

                loadBalancer = await GetLoadBalancerAsync();
            }

            // Configure the ingress/egress listeners and target groups.

            controller.SetGlobalStepStatus("configure: cluster routing");

            await UpdateNetworkAsync(NetworkOperations.InternetRouting | NetworkOperations.EnableSsh);
        }

        /// <summary>
        /// Waits for the load balancer SSH target group for the node to become healthy.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="node">The target node.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task WaitForSshTargetAsync(ISetupController controller, NodeSshProxy<NodeDefinition> node)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            node.Status = "waiting...";

            // Locate the SSH load balancer target for this node.
            
            // $hack(jefflill):
            //
            // This is a bit of a hack; I'm going to rely on the fact that the SSH target group
            // names end with the external port number for the target node.

            var awsInstance       = nodeNameToAwsInstance[node.Name];
            var targetGroupSuffix = $"{awsInstance.ExternalSshPort}";
            var targetGroup       = nameToTargetGroup.Values.Single(targetGroup => targetGroup.Protocol == ProtocolEnum.TCP && 
                                                                                   targetGroup.Port == NetworkPorts.SSH && 
                                                                                   targetGroup.TargetGroupName.EndsWith(targetGroupSuffix));
            await NeonHelper.WaitForAsync(
                async () =>
                {
                    var targetHealthResponse = await elbClient.DescribeTargetHealthAsync(
                        new DescribeTargetHealthRequest()
                        {
                            TargetGroupArn = targetGroup.TargetGroupArn
                        });

                    var targetHealthState = targetHealthResponse.TargetHealthDescriptions.Single().TargetHealth.State;

                    if (targetHealthState == TargetHealthStateEnum.Initial)
                    {
                        node.Status = $"target: registering (slow)...";
                        return false;
                    }
                    else if (targetHealthState == TargetHealthStateEnum.Unhealthy)
                    {
                        node.Status = $"target: checking...";
                        return false;
                    }
                    else if (targetHealthState == TargetHealthStateEnum.Healthy)
                    {
                        node.Status = $"target: {targetHealthState}";
                        return true;
                    }

                    // Report unexpected target health states.

                    throw new NeonKubeException($"Unexpected target group health state: [{targetHealthState}]");
                },
                timeout:      operationTimeout,
                pollInterval: operationPollInternal);
        }

        /// <summary>
        /// Creates the AWS instance for a node.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="node">The target node.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task CreateNodeInstanceAsync(ISetupController controller, NodeSshProxy<NodeDefinition> node)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            var clusterLogin = controller.Get<ClusterLogin>(KubeSetupProperty.ClusterLogin);

            //-----------------------------------------------------------------
            // Create the instance if it doesn't already exist.

            var awsInstance     = nodeNameToAwsInstance[node.Name];
            var awsInstanceName = awsInstance.InstanceName;
            var awsNodeOptions  = node.Metadata.Aws;

            if (awsInstance.Instance == null)
            {
                node.Status = "create instance";

                // Determine the placement group (1-based) partition number for the node.

                var placementGroupName = (string)null;
                var partitionNumber = -1;

                if (node.Metadata.IsControlPane)
                {
                    placementGroupName = controlPlacementGroupName;

                    if (awsOptions.ControlPlanePlacementPartitions <= 1)
                    {
                        // No effective partitioning.

                        partitionNumber = 1;
                    }
                    else
                    {
                        // Spread the control-plane instances across the partitions while honoring
                        // node specific partition settings.

                        var partitionAssignmentCounts = new List<int>();

                        for (int i = 1; i < awsOptions.ControlPlanePlacementPartitions; i++)
                        {
                            partitionAssignmentCounts.Add(0);
                        }

                        foreach (var controlNode in cluster.Definition.SortedControlNodes.ToList())
                        {
                            if (controlNode.Aws.PlacementPartition > 0)
                            {
                                // User explicitly specified the partition.

                                partitionNumber = controlNode.Aws.PlacementPartition;
                            }
                            else
                            {
                                // Assign the node to a partition with the fewest nodes.

                                var minPartitionNodeCount = partitionAssignmentCounts.Min();

                                partitionNumber = partitionAssignmentCounts.IndexOf(minPartitionNodeCount) + 1;
                            }

                            partitionAssignmentCounts[partitionNumber - 1]++;
                        }
                    }
                }
                else if (node.Metadata.IsWorker)
                {
                    placementGroupName = workerPlacementGroupName;

                    if (awsOptions.WorkerPlacementPartitions <= 1)
                    {
                        // No effective partitioning.

                        partitionNumber = 1;
                    }
                    else
                    {
                        // Spread the worker instances across the partitions while honoring
                        // node specific partition settings.

                        var partitionAssignmentCounts = new List<int>();

                        for (int i = 1; i < awsOptions.WorkerPlacementPartitions; i++)
                        {
                            partitionAssignmentCounts.Add(0);
                        }

                        foreach (var worker in cluster.Definition.SortedWorkerNodes.ToList())
                        {
                            if (worker.Aws.PlacementPartition > 0)
                            {
                                // User explicitly specified the partition.

                                partitionNumber = worker.Aws.PlacementPartition;
                            }
                            else
                            {
                                // Assign the node to a partition with the fewest nodes.

                                var minPartitionNodeCount = partitionAssignmentCounts.Min();

                                partitionNumber = partitionAssignmentCounts.IndexOf(minPartitionNodeCount) + 1;
                            }

                            partitionAssignmentCounts[partitionNumber - 1]++;
                        }
                    }
                }
                else
                {
                    Covenant.Assert(false);
                }

                Covenant.Assert(!string.IsNullOrEmpty(placementGroupName));
                Covenant.Assert(partitionNumber > 0);

                //-------------------------------------------------------------
                // Create the instance in the node subnet.
                //
                // Note that AWS does not support starting new instances with a specific
                // SSH password by default; they use a SSH key instead.  We also want
                // to rename the default [ubuntu] user to our standard [sysadmin].
                //
                // I'm going to address this by passing a boot script as user-data when
                // creating the instance, which will:
                //
                //      1. The script will run everytime the instance boots (because we
                //         added the [#cloud-boothook] line but the script should only 
                //         do anything once.  We'll touch a file the first time the script
                //         runs and then exit the script immediately when this file exists
                //         for subsequent restarts.
                //
                //         https://cloudinit.readthedocs.io/en/latest/topics/format.html#cloud-boothook
                //
                //      2. Set the secure password for [sysadmin].
                //
                //      3. Configure the node's static IP address, gateway and name servers.
                //
                //      4. Disable [cloud-init] network config to prevent the network
                //         configuration from being overwritten again.
                //
                // Note that we needed to disable [cloud-init] networking when we created 
                // the node image.  This means that we need to generate the NetPlan config
                // ourselves for node instances.
                //
                // We're going to rely CIDR base + 1 being the default gateway and 
                // [169.254.169.253] always being the AWS nameserver (we'll use the
                // [169.254.169.253] nameserver when none are specified in the cluster
                // definition.
                //
                //      x.x.x.1             - default gateway
                //      169.254.169.253     - AWS DNS nameserver
                //
                // Note that we'll override the AWS nameserver when the cluster definition
                // explicitly specifies nameservers.

                var sbNameServers = new StringBuilder();

                if (cluster.Definition.Network.Nameservers.Count == 0)
                {
                    sbNameServers.Append("169.254.169.253");
                }
                else
                {
                    foreach (var nameserver in cluster.Definition.Network.Nameservers)
                    {
                        sbNameServers.AppendWithSeparator(nameserver, ", ");
                    }
                }

                var nodeMtu          = NodeMtu == 0 ? NetConst.DefaultMTU : NodeMtu;
                var netInterfacePath = LinuxPath.Combine(KubeNodeFolder.Bin, "net-interface");
                var privateSubnet    = NetworkCidr.Parse(cluster.Definition.Hosting.Aws.Network.NodeSubnet);
                var bootScript       =
$@"#cloud-boothook
#!/bin/bash

# To enable logging for this AWS user-data script, add ""-ex"" to the SHABANG above.
# the SHEBANG above uncomment the EXEC command below.  Then each command and its
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

#------------------------------------------------------------------------------
# Configure the node's static IP address:

interface=$({netInterfacePath})

rm -rf /etc/netplan/*

cat <<EOF > /etc/netplan/50-static.yaml
network:
  version: 2
  ethernets:
    $interface:
      mtu: {nodeMtu}
      dhcp4: false
      dhcp6: false
      addresses: [{node.Metadata.Address}/{privateSubnet.PrefixLength}]
      routes:
      - to: default
        via: {privateSubnet.FirstUsableAddress}
      nameservers:
        addresses: [{sbNameServers}]
EOF

chmod 644 /etc/netplan/50-static.yaml
netplan apply

# Disable [cloud-init] network configuration.

mkdir -p /etc/cloud/cloud.cfg.d
echo 'network: {{config: disabled}}' > /etc/cloud/cloud.cfg.d/99-disable-network-config.cfg
";
                var ebsOptimized = awsOptions.DefaultEbsOptimized;

                switch (awsNodeOptions.EbsOptimized)
                {
                    case TriState.True:

                        ebsOptimized = true;
                        break;

                    case TriState.False:

                        ebsOptimized = false;
                        break;
                }

                var runResponse = await ec2Client.RunInstancesAsync(
                    new RunInstancesRequest()
                    {
                        ImageId          = nodeImage.ImageId,
                        InstanceType     = InstanceType.FindValue(awsNodeOptions.InstanceType),
                        MinCount         = 1,
                        MaxCount         = 1,
                        SubnetId         = nodeSubnet.SubnetId,
                        EbsOptimized     = ebsOptimized,
                        PrivateIpAddress = node.Metadata.Address,
                        SecurityGroupIds = new List<string>() { securityGroup.GroupId },
                        UserData         = Convert.ToBase64String(Encoding.UTF8.GetBytes(NeonHelper.ToLinuxLineEndings(bootScript))),
                        Placement        = new Placement()
                        {
                            AvailabilityZone = awsOptions.AvailabilityZone,
                            GroupName        = placementGroupName,
                            PartitionNumber  = partitionNumber
                        },
                        BlockDeviceMappings = new List<BlockDeviceMapping>()
                        {
                            new BlockDeviceMapping()
                            {
                                DeviceName = osDeviceName,
                                Ebs        = new EbsBlockDevice()
                                {
                                    VolumeType          = ToEc2VolumeType(awsNodeOptions.VolumeType),
                                    VolumeSize          = (int)(ByteUnits.Parse(awsNodeOptions.VolumeSize) / ByteUnits.GibiBytes),
                                    DeleteOnTermination = true
                                }
                            },
                            new BlockDeviceMapping()
                            {
                                DeviceName = dataDeviceName,
                                Ebs        = new EbsBlockDevice()
                                {
                                    VolumeType          = ToEc2VolumeType(awsNodeOptions.OpenEbsVolumeType),
                                    VolumeSize          = (int)(ByteUnits.Parse(awsNodeOptions.OpenEbsVolumeSize) / ByteUnits.GibiBytes),
                                    DeleteOnTermination = true
                                }
                            }
                        },
                        TagSpecifications = GetTagSpecifications(awsInstanceName, ResourceType.Instance, new ResourceTag(neonNodeNameTagKey, node.Name))
                    });

                awsInstance.Instance = runResponse.Reservation.Instances.Single();
            }

            //-----------------------------------------------------------------
            // Wait for the instance to indicate that it's running.
            //
            // NOTE:
            // -----
            // It's possible that the instance is still stopped when a previous user-data 
            // clearing operation was interrupted (below).  We'll just restart it here
            // to handle that.

            node.Status = "pending...";

            var invalidStates = 
                new HashSet<int>
                {
                    InstanceStateCode.ShuttingDown,
                    InstanceStateCode.Stopping,
                    InstanceStateCode.Terminated
                };

            await NeonHelper.WaitForAsync(
                async () =>
                {
                    // It's possible that the instance created above hasn't gotten far enough
                    // along in the provisioning process for the DescribeInstanceStatusAsync()
                    // call below to see it.  We'll need to use a retry policy to deal with this.

                    var retry = new LinearRetryPolicy(typeof(AmazonEC2Exception), retryInterval: pollInterval, timeout: timeout);

                    var statusResponse = await retry.InvokeAsync(
                        async () =>
                        {
                            return await ec2Client.DescribeInstanceStatusAsync(
                                new DescribeInstanceStatusRequest()
                                {
                                    InstanceIds = new List<string>() { awsInstance.InstanceId },
                                    IncludeAllInstances = true
                                });
                        });

                    var status = statusResponse.InstanceStatuses.SingleOrDefault();

                    if (status == null)
                    {
                        return false;       // The instance provisioning operation must still be pending?
                    }

                    var state = status.InstanceState.Code & 0x00FF;        // Clear the internal AWS status code bits

                    if (invalidStates.Contains(state))
                    {
                        throw new NeonKubeException($"Cluster instance [id={awsInstance.InstanceId}] is in an unexpected state [{status.InstanceState.Name}].");
                    }

                    if (InstanceStateCode.IsRunning(status.InstanceState.Code))
                    {
                        node.Status = "starting (slow)...";
                    }
                    else if (InstanceStateCode.IsStopped(status.InstanceState.Code))
                    {
                        node.Status = "restarting (slow)...";

                        await ec2Client.StartInstancesAsync(
                            new StartInstancesRequest()
                            {
                                InstanceIds = new List<string>() { awsInstance.InstanceId }
                            });
                    }
                    else
                    {
                        return false;
                    }

                    // This verifies that the instance has finished running thr boot
                    // script and is ready to go.

                    if (!status.SystemStatus.Status.Value.Equals("ok", StringComparison.InvariantCultureIgnoreCase))
                    {
                        return false;
                    }

                    node.Status = "ready";

                    return true;
                },
                timeout:      operationTimeout,
                pollInterval: operationPollInternal);

            //-----------------------------------------------------------------
            // Tag the EC2 volumes created for the instance.
            
            node.Status = "tagging volumes";

            // We need to reload the instance to obtain information on its
            // attached volumes.

            var instancePagenator = ec2Client.Paginators.DescribeInstances(new DescribeInstancesRequest() { Filters = clusterFilter });

            await foreach (var reservationItem in instancePagenator.Reservations)
            {
                if (reservationItem.Instances.Count != 1)
                {
                    // We're creating instances one at a time so we can ignore
                    // reservations with more than one instance.

                    continue;
                }

                var instanceItem = reservationItem.Instances.Single();

                if (instanceItem.InstanceId == awsInstance.InstanceId)
                {
                    awsInstance.Instance = instanceItem;
                    break;
                }
            }

            // So our AWS node instances will have either one or two attached 
            // volumes.  It will always have the data volume we specified when
            // we created the instance above.  This will have a well-known device
            // name.  The node may (always?) also have the boot volume.  I believe
            // that this might not be present in some situations but I'm not sure.
            // 
            // We'll assume that any second volume will be the OS disk.

            var dataVolumeMapping = awsInstance.Instance.BlockDeviceMappings.Single(mapping => mapping.DeviceName == dataDeviceName);

            await ec2Client.CreateTagsAsync(
                new CreateTagsRequest()
                {
                     Resources = new List<string> { dataVolumeMapping.Ebs.VolumeId },
                     Tags      = GetTags<Ec2Tag>(GetResourceName($"{node.Name}.data"), new ResourceTag(neonNodeNameTagKey, node.Name))
                });

            var osVolumeMapping = awsInstance.Instance.BlockDeviceMappings.SingleOrDefault(mapping => mapping.DeviceName == osDeviceName);

            if (osVolumeMapping != null)
            {
                await ec2Client.CreateTagsAsync(
                    new CreateTagsRequest()
                    {
                        Resources = new List<string> { osVolumeMapping.Ebs.VolumeId },
                        Tags      = GetTags<Ec2Tag>(GetResourceName($"{node.Name}.os"), new ResourceTag(neonNodeNameTagKey, node.Name))
                    });
            }
        }

        /// <summary>
        /// AWS limits target group names to 32 characters so we're going to
        /// abbreviate <see cref="IngressRuleTarget.ControlPlane"/> targets
        /// with this string in the generated name.
        /// </summary>
        private const char IngressControlPlaneTargetAbbreviation = 'c';

        /// <summary>
        /// AWS limits target group names to 32 characters so we're going to
        /// abbreviate <see cref="IngressRuleTarget.Ingress"/> targets
        /// with this string in the generated name.
        /// </summary>
        private const char IngressIngressTargetAbbreviation = 'i';

        /// <summary>
        /// AWS limits target group names to 32 characters so we're going to
        /// abbreviate <see cref="IngressRuleTarget.Ssh"/> targets
        /// with this string in the generated name.
        /// </summary>
        private const char IngressSshTargetAbbreviation = 's';

        /// <summary>
        /// Constructs a target group name by appending the protocol, port and target group type 
        /// to the base cluster name passed.
        /// </summary>
        /// <param name="clusterName">The cluster name.</param>
        /// <param name="ingressTarget">The neonKUBE target group type.</param>
        /// <param name="protocol">The ingress protocol.</param>
        /// <param name="port">The ingress port.</param>
        /// <returns>The fully qualified target group name.</returns>
        /// <remarks>
        /// <note>
        /// AWS limits target group names to 32 characters, so we're going to try to reduce
        /// the size of these names by using abbrievations so help avoid exceeding this limit.
        /// </note>
        /// </remarks>
        private string GetTargetGroupName(string clusterName, IngressRuleTarget ingressTarget, IngressProtocol protocol, int port)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(clusterName), nameof(clusterName));

            // $hack(jefflill):
            //
            // AWS doesn't tolerate target group names with periods and dashes so
            // convert both of these to underscores.  This is a bit fragile because it
            // assumes that users will never name two different clusters such that
            // the only difference is due to a period or underscore in place of a
            // dash.

            clusterName = clusterName.Replace('.', '_');
            clusterName = clusterName.Replace('-', '_');

            if (protocol == IngressProtocol.Http || protocol == IngressProtocol.Https)
            {
                // We don't actually support HTTP/HTTPS at the load balancer.

                protocol = IngressProtocol.Tcp;
            }

            string  protocolString;
            char    targetCh;

            switch (ingressTarget)
            {
                case IngressRuleTarget.ControlPlane:

                    targetCh = IngressControlPlaneTargetAbbreviation;
                    break;

                case IngressRuleTarget.Ingress:

                    targetCh = IngressIngressTargetAbbreviation;
                    break;

                case IngressRuleTarget.Ssh:

                    targetCh = IngressSshTargetAbbreviation;
                    break;

                default:

                    throw new NotImplementedException();
            }

            switch (protocol)
            {
                case IngressProtocol.Http:

                    protocolString = "h";
                    break;

                case IngressProtocol.Https:

                    protocolString = "s";
                    break;

                case IngressProtocol.Tcp:

                    protocolString = "t";
                    break;

                case IngressProtocol.Udp:

                    protocolString = "u";
                    break;

                default:

                    throw new NotImplementedException();
            }

            // NOTE: AWS does like underscores or periods in this name so we'll
            //       convert any of those to dashes.

            return $"{clusterName}-{targetCh}{protocolString}{port}"
                .Replace('_', '-')
                .Replace('.', '-');
        }

        /// <summary>
        /// Parses the target group name to determine the ingress target group type.
        /// </summary>
        /// <param name="targetGroup">The target group.</param>
        /// <returns>The target group's <see cref="IngressRuleTarget"/>.</returns>
        private IngressRuleTarget GetTargetGroupType(ElbTargetGroup targetGroup)
        {
            Covenant.Requires<ArgumentNullException>(targetGroup != null, nameof(targetGroup));

            // The first character after the last dash in the name is the
            // abbreviation identifying the ingress group type.

            var lastDashPos = targetGroup.TargetGroupName.LastIndexOf('-');

            Covenant.Assert(lastDashPos > 0);

            switch (targetGroup.TargetGroupName[lastDashPos + 1])
            {
                case IngressControlPlaneTargetAbbreviation:

                    return IngressRuleTarget.ControlPlane;

                case IngressIngressTargetAbbreviation:

                    return IngressRuleTarget.Ingress;

                case IngressSshTargetAbbreviation:

                    return IngressRuleTarget.Ssh;

                default:

                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Updates the load balancer and related security rules based on the operation flags passed.
        /// </summary>
        /// <param name="operations">Flags that control how the load balancer and related security rules are updated.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task UpdateNetworkAsync(NetworkOperations operations)
        {
            await SyncContext.Clear;

            // We're going to use the boolean VPC tag named [neonVpcSshEnabledTagKey]
            // to persist the current external SSH access status for the cluster.
            //
            // Update the tag for state changes.

            controller.SetGlobalStepStatus("configure: network security");

            var externalSshEnabledTag = vpc.Tags.SingleOrDefault(tag => tag.Key == neonVpcSshEnabledTagKey);

            if (externalSshEnabledTag == null)
            {
                externalSshEnabledTag = new Ec2Tag()
                {
                    Key   = neonVpcSshEnabledTagKey,
                    Value = "false"
                };

                vpc.Tags.Add(externalSshEnabledTag);
            }

            var externalSshEnabled = externalSshEnabledTag.Value == "true";
            var updateVpcTags      = false;

            if ((operations & NetworkOperations.EnableSsh) != 0 && !externalSshEnabled)
            {
                externalSshEnabledTag.Value = "true";
                updateVpcTags               = true;
            }
            else if ((operations & NetworkOperations.DisableSsh) != 0 && externalSshEnabled)
            {
                externalSshEnabledTag.Value = "false";
                updateVpcTags               = true;
            }

            if (updateVpcTags)
            {
                await ec2Client.CreateTagsAsync(
                    new CreateTagsRequest()
                    {
                        Resources = new List<string>() { vpc.VpcId },
                        Tags      = vpc.Tags
                    });
            }

            // Perform the network operations.

            if ((operations & NetworkOperations.InternetRouting) != 0)
            {
                await UpdateLoadBalancerAsync();
            }

            if ((operations & NetworkOperations.EnableSsh) != 0)
            {
                await AddSshListenersAsync();
            }

            if ((operations & NetworkOperations.DisableSsh) != 0)
            {
                await RemoveSshListenersAsync();
            }
        }

        /// <summary>
        /// <para>
        /// Updates the load balancer and network ACLs to match the current cluster definition.
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

            KubeHelper.EnsureIngressNodes(cluster.Definition);

            // We need to add a special ingress rule for the Kubernetes API on port 6442 and
            // load balance this traffic to the control-plane nodes.

            var clusterRules = new IngressRule[]
                {
                    new IngressRule()
                    {
                        Name                  = "kubeapi",
                        Protocol              = IngressProtocol.Tcp,
                        ExternalPort          = NetworkPorts.KubernetesApiServer,
                        NodePort              = NetworkPorts.KubernetesApiServer,
                        Target                = IngressRuleTarget.ControlPlane,
                        AddressRules          = networkOptions.ManagementAddressRules,
                        IdleTcpReset          = true,
                        TcpIdleTimeoutMinutes = 5
                    }
                };

            var ingressRules = networkOptions.IngressRules.Union(clusterRules).ToArray();

            //-----------------------------------------------------------------
            // Load target groups.

            var defaultHealthCheck = networkOptions.IngressHealthCheck ?? new HealthCheckOptions();

            var targetControlNodes = SortedControlNodes
                .Select(awsInstance => new TargetDescription() { Id = awsInstance.InstanceId })
                .ToList();

            var targetIngressNodes = SortedControlThenWorkerNodes
                .Where(awsInstance => awsInstance.Node.Metadata.Ingress)
                .Select(awsInstance => new TargetDescription() { Id = awsInstance.InstanceId })
                .ToList();

            // Ensure that all of the required ingress rule related target groups exist 
            // and that they forward traffic to the correct nodes.

            foreach (var ingressRule in ingressRules)
            {
                var targetGroupName = GetTargetGroupName(clusterName, ingressRule.Target, ingressRule.Protocol, ingressRule.ExternalPort);
                var healthCheck     = ingressRule.IngressHealthCheck ?? defaultHealthCheck;

                if (!nameToTargetGroup.TryGetValue(targetGroupName, out var targetGroup))
                {
                    var targetGroupResponse = await elbClient.CreateTargetGroupAsync(
                        new CreateTargetGroupRequest()
                        {
                            VpcId                      = vpc.VpcId,
                            Name                       = targetGroupName,
                            Protocol                   = ToElbProtocol(ingressRule.Protocol),
                            TargetType                 = TargetTypeEnum.Instance,
                            Port                       = ingressRule.NodePort,
                            HealthCheckEnabled         = true,
                            HealthCheckProtocol        = ProtocolEnum.TCP,
                            HealthCheckIntervalSeconds = healthCheck.IntervalSeconds,
                            HealthyThresholdCount      = healthCheck.ThresholdCount,
                            UnhealthyThresholdCount    = healthCheck.ThresholdCount,
                        });

                    targetGroup = targetGroupResponse.TargetGroups.Single();
                    nameToTargetGroup.Add(targetGroupName, targetGroup);

                    // Add target group tags.

                    await elbClient.AddTagsAsync(
                        new AddTagsRequest()
                        {
                             ResourceArns = new List<string>() { targetGroup.TargetGroupArn },
                             Tags         = GetTags<ElbTag>(GetResourceName(targetGroupName))
                        });
                }

                // We're going to re-register the targets every time in case nodes
                // have been added or removed from the cluster or the set of nodes
                // marked for ingress has changed.

                var targetNodes = (List<TargetDescription>)null;

                switch (ingressRule.Target)
                {
                    case IngressRuleTarget.ControlPlane:

                        targetNodes = targetControlNodes;
                        break;

                    case IngressRuleTarget.Ingress:

                        targetNodes = targetIngressNodes;
                        break;

                    case IngressRuleTarget.Ssh:

                        throw new InvalidOperationException($"[Target={ingressRule.Target}] cannot be used for a cluster ingress rule.");

                    default:

                        throw new NotImplementedException($"[Target={ingressRule.Target}] is not implemented.");
                }

                await elbClient.RegisterTargetsAsync(
                    new RegisterTargetsRequest()
                    {
                        TargetGroupArn = targetGroup.TargetGroupArn,
                        Targets        = targetNodes
                    });
            }

            // Ensure that we have an external SSH target group that forwards traffic to each node.
            // We'll enable/disable external SSH access by creating or removing the listeners
            // further below.

            foreach (var awsInstance in SortedControlThenWorkerNodes)
            {
                Covenant.Assert(awsInstance.ExternalSshPort != 0, $"Node [{awsInstance.Name}] does not have an external SSH port assignment.");

                var targetGroupName = GetTargetGroupName(clusterName, IngressRuleTarget.Ssh, IngressProtocol.Tcp, awsInstance.ExternalSshPort);

                if (!nameToTargetGroup.TryGetValue(targetGroupName, out var targetGroup))
                {
                    var targetGroupResponse = await elbClient.CreateTargetGroupAsync(
                        new CreateTargetGroupRequest()
                        {
                            VpcId                      = vpc.VpcId,
                            Name                       = targetGroupName,
                            Protocol                   = ToElbProtocol(IngressProtocol.Tcp),
                            TargetType                 = TargetTypeEnum.Instance,
                            Port                       = NetworkPorts.SSH,
                            HealthCheckEnabled         = true,
                            HealthCheckProtocol        = ProtocolEnum.TCP,
                            HealthCheckIntervalSeconds = defaultHealthCheck.IntervalSeconds,
                            HealthyThresholdCount      = defaultHealthCheck.ThresholdCount,
                            UnhealthyThresholdCount    = defaultHealthCheck.ThresholdCount
                        });

                    targetGroup = targetGroupResponse.TargetGroups.Single();
                    nameToTargetGroup.Add(targetGroupName, targetGroup);
                }

                // Add target group tags.

                await elbClient.AddTagsAsync(
                    new AddTagsRequest()
                    {
                        ResourceArns = new List<string>() { targetGroup.TargetGroupArn },
                        Tags         = GetTags<ElbTag>(GetResourceName(targetGroupName))
                    });

                // Register the target node.

                await elbClient.RegisterTargetsAsync(
                    new RegisterTargetsRequest()
                    { 
                        TargetGroupArn = targetGroup.TargetGroupArn,
                        Targets        = new List<TargetDescription>()
                        {
                            new TargetDescription() { Id = awsInstance.InstanceId }
                        }
                    });
            }

            // Add load balancer listeners for cluster ingress rules as required.

            var listenerPagenator = elbClient.Paginators.DescribeListeners(
                new DescribeListenersRequest()
                {
                    LoadBalancerArn = loadBalancer.LoadBalancerArn
                });

            var portToListener = new Dictionary<int, Listener>();

            await foreach (var listener in listenerPagenator.Listeners)
            {
                portToListener.Add(listener.Port, listener);
            }

            foreach (var ingressRule in ingressRules)
            {
                ElbTargetGroup targetGroup;

                switch (ingressRule.Target)
                {
                    case IngressRuleTarget.ControlPlane:
                    case IngressRuleTarget.Ingress:

                        targetGroup = nameToTargetGroup[GetTargetGroupName(clusterName, ingressRule.Target, ingressRule.Protocol, ingressRule.ExternalPort)];
                        break;

                    case IngressRuleTarget.Ssh:

                        throw new InvalidOperationException($"[Target={ingressRule.Target}] cannot be used for a cluster ingress rule.");

                    default:

                        throw new NotImplementedException($"[Target={ingressRule.Target}] is not implemented.");
                }

                if (!portToListener.TryGetValue(ingressRule.ExternalPort, out var listener))
                {
                    // A listener doesn't exist for this rule's port so create one.

                    await elbClient.CreateListenerAsync(
                        new CreateListenerRequest()
                        {
                             LoadBalancerArn = loadBalancer.LoadBalancerArn,
                             Port            = ingressRule.ExternalPort,
                             Protocol        = ToElbProtocol(ingressRule.Protocol),
                             DefaultActions  = new List<ElbAction>()
                             {
                                 new ElbAction()
                                 {
                                      Type           = ActionTypeEnum.Forward,
                                      TargetGroupArn = targetGroup.TargetGroupArn
                                 }
                             }
                        });
                }
            }

            // Remove any load balancer listeners for external ports that are not in the reserved 
            // SSH port range and do not correspond to the external port of a cluster ingress rule.

            var ingressRulePorts = new HashSet<int>();

            foreach (var ingressRule in ingressRules)
            {
                ingressRulePorts.Add(ingressRule.ExternalPort);
            }

            foreach (var listener in portToListener.Values.Where(listener => !networkOptions.IsExternalSshPort(listener.Port)))
            {
                if (!ingressRulePorts.Contains(listener.Port))
                {
                    await elbClient.DeleteListenerAsync(
                        new DeleteListenerRequest()
                        {
                            ListenerArn = listener.ListenerArn
                        });
                }
            }
        }

        /// <summary>
        /// Enables external SSH node access by adding the listeners to the load balancer.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task AddSshListenersAsync()
        {
            await SyncContext.Clear;

            await ConnectAwsAsync(controller);
            ConfigureNodeSsh(controller);

            foreach (var awsInstance in nodeNameToAwsInstance.Values)
            {
                var targetGroupName = GetTargetGroupName(clusterName, IngressRuleTarget.Ssh, IngressProtocol.Tcp, awsInstance.ExternalSshPort);
                var targetGroup     = nameToTargetGroup[targetGroupName];

                await elbClient.CreateListenerAsync(
                    new CreateListenerRequest()
                    {
                         LoadBalancerArn = loadBalancer.LoadBalancerArn,
                         Port            = awsInstance.ExternalSshPort,
                         Protocol        = ProtocolEnum.TCP,
                         DefaultActions  = new List<ElbAction>()
                         {
                             new ElbAction()
                             {
                                Type           = ActionTypeEnum.Forward,
                                TargetGroupArn = targetGroup.TargetGroupArn
                             }
                         }
                    });
            }
        }

        /// <summary>
        /// Disables external SSH node access by removing the SSH listeners from the load balancer.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task RemoveSshListenersAsync()
        {
            await SyncContext.Clear;

            await ConnectAwsAsync(controller);
            ConfigureNodeSsh(controller);

            var listenerPagenator = elbClient.Paginators.DescribeListeners(
                new DescribeListenersRequest()
                {
                    LoadBalancerArn = loadBalancer.LoadBalancerArn
                });

            var listeners = new List<Listener>();

            await foreach (var listener in listenerPagenator.Listeners)
            {
                listeners.Add(listener);
            }

            foreach (var listener in listeners.Where(listener => networkOptions.IsExternalSshPort(listener.Port)))
            {
                await elbClient.DeleteListenerAsync(
                    new DeleteListenerRequest()
                    {
                        ListenerArn = listener.ListenerArn
                    });
            }
        }

        /// <summary>
        /// Returns the name to use for the virtual machine that will host the node.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <returns>The virtual machine name.</returns>
        private string GetVmName(NodeDefinition node)
        {
            Covenant.Requires<ArgumentNullException>(node != null, nameof(node));

            return $"{cluster.Definition.Hosting.Vm.GetVmNamePrefix(cluster.Definition)}{node.Name}";
        }

        /// <summary>
        /// Converts a virtual machine name to the matching node definition.
        /// </summary>
        /// <param name="vmName">The virtual machine name.</param>
        /// <returns>The matching node definition or <c>null</c>.</returns>
        private NodeDefinition VmNameToNodeDefinition(string vmName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(vmName), nameof(vmName));

            var prefix = $"{clusterName}.";

            if (!vmName.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            var nodeName = vmName.Substring(prefix.Length);

            if (cluster.Definition.NodeDefinitions.TryGetValue(nodeName, out var nodeDefinition))
            {
                return nodeDefinition;
            }

            return null;
        }

        //---------------------------------------------------------------------
        // Cluster life-cycle methods

        // $note(jefflill):
        //
        // AWS supports pausing instances but only for instances running on a limited number
        // of instance types and only when running Amazon Linux, so we're not going to support
        // pausing on AWS.

        /// <inheritdoc/>
        public override HostingCapabilities Capabilities => HostingCapabilities.Stoppable | HostingCapabilities.Removable;

        /// <inheritdoc/>
        public override async Task<HostingResourceAvailability> GetResourceAvailabilityAsync(long reserveMemory = 0, long reserveDisk = 0)
        {
            await SyncContext.Clear;

            // NOTE: We're deferring checking quotas and current utilization for AWS at this time:
            //
            //      https://github.com/nforgeio/neonKUBE/issues/1544

            var regionName = awsOptions.Region;
            var zoneName   = awsOptions.AvailabilityZone;

            await ConnectAwsAsync();

            // Verify that the zone and (implicitly) the region exist.

            var regionsResponse = await ec2Client.DescribeRegionsAsync(new DescribeRegionsRequest());

            if (!regionsResponse.Regions.Any(region => region.RegionName.Equals(regionName, StringComparison.InvariantCultureIgnoreCase)))
            {
                var constraint =
                    new HostingResourceConstraint()
                    {
                        ResourceType = HostingConstrainedResourceType.VmHost,
                        Details      = $"AWS region [{regionName}] not found or available.",
                        Nodes        = cluster.Definition.NodeDefinitions.Keys.ToList()
                    };

                return new HostingResourceAvailability()
                {
                    Constraints   = 
                        new Dictionary<string, List<HostingResourceConstraint>>()
                        {
                            { $"AWS/{regionName}", new List<HostingResourceConstraint>() { constraint } }
                        }
                };
            }

            // Verify that the instance types required by the cluster are available in the region
            // and also that all instance types support the [x86_64] architecture.

            var nameToInstanceTypeInfo = new Dictionary<string, InstanceTypeInfo>(StringComparer.InvariantCultureIgnoreCase);
            var instanceTypePaginator  = ec2Client.Paginators.DescribeInstanceTypes(new DescribeInstanceTypesRequest());
            var constraints            = new List<HostingResourceConstraint>();

            await foreach (var instanceTypeInfo in instanceTypePaginator.InstanceTypes)
            {
                nameToInstanceTypeInfo[instanceTypeInfo.InstanceType] = instanceTypeInfo;
            }

            var instanceTypes = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var node in cluster.Nodes)
            {
                var nodeInstanceType = node.Metadata.Aws.InstanceType;

                if (!instanceTypes.Contains(nodeInstanceType))
                {
                    instanceTypes.Add(nodeInstanceType);
                }
            }

            foreach (var instanceType in instanceTypes)
            {
                if (!nameToInstanceTypeInfo.TryGetValue(instanceType, out var instanceTypeInfo))
                {
                    constraints.Add(
                        new HostingResourceConstraint()
                        {
                            ResourceType = HostingConstrainedResourceType.VmHost,
                            Details      = $"Instance type [{instanceType}] is not available in AWS region [{regionName}].",
                            Nodes        = cluster.Nodes
                                               .Where(node => node.Metadata.Aws.InstanceType == instanceType)
                                               .Select(node => node.Name)
                                               .ToList()
                        });

                    continue;
                }

                if (!instanceTypeInfo.ProcessorInfo.SupportedArchitectures.Any(architecture => architecture == "x86_64"))
                {
                    constraints.Add(
                        new HostingResourceConstraint()
                        {
                            ResourceType = HostingConstrainedResourceType.VmHost,
                            Details      = $"Instance type [{instanceType}] does not support the [x86_64] architecture.",
                            Nodes        = cluster.Nodes
                                               .Where(node => node.Metadata.Aws.InstanceType == instanceType)
                                               .Select(node => node.Name)
                                               .ToList()
                        });

                    continue;
                }
            }

            if (constraints.Count == 0)
            {
                return new HostingResourceAvailability();
            }
            else
            {
                var constraintDictionary = new Dictionary<string, List<HostingResourceConstraint>>();

                constraintDictionary.Add($"AWS/{regionName}", constraints);

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
            Covenant.Requires<NotSupportedException>(cluster != null, $"[{nameof(AwsHostingManager)}] was created with the wrong constructor.");

            var clusterHealth = new ClusterHealth();

            if (timeout <= TimeSpan.Zero)
            {
                timeout = DefaultStatusTimeout;
            }

            await ConnectAwsAsync();

            // We're going to infer the cluster provisiong status by examining the
            // cluster login and the state of the VMs deployed to AWS.

            var contextName  = $"root@{cluster.Definition.Name}";
            var context      = KubeHelper.Config.GetContext(contextName);
            var clusterLogin = KubeHelper.GetClusterLogin((KubeContextName)contextName);

            // Create a hashset with the names of the nodes that map to deployed AWS
            // machine instances.

            var existingNodes = new HashSet<string>();

            foreach (var item in instanceNameToAwsInstance)
            {
                var nodeDefinition = VmNameToNodeDefinition(item.Key);

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

                clusterHealth.State   = ClusterState.NotFound;
                clusterHealth.Summary = "Cluster does not exist";

                foreach (var node in cluster.Definition.NodeDefinitions.Values)
                {
                    clusterHealth.Nodes.Add(node.Name, existingNodes.Contains(node.Name) ? ClusterNodeState.Conflict : ClusterNodeState.NotProvisioned);
                }

                return clusterHealth;
            }
            else
            {
                // We're going to assume that all virtual machines that match cluster node names
                // (after stripping off any cluster prefix) belong to the cluster and we'll
                // map the actual VM states to public node states.

                foreach (var node in cluster.Definition.NodeDefinitions.Values)
                {
                    var nodeState = ClusterNodeState.NotProvisioned;

                    if (existingNodes.Contains(node.Name))
                    {
                        if (nodeNameToAwsInstance.TryGetValue(node.Name, out var awsInstance))
                        {
                            var stateCode = InstanceStateCode.GetCode(awsInstance.Instance.State.Code);

                            switch (stateCode)
                            {
                                case InstanceStateCode.Pending:

                                    nodeState = ClusterNodeState.Starting;
                                    break;
                                    
                                case InstanceStateCode.Running:

                                    nodeState = ClusterNodeState.Running;
                                    break;

                                case InstanceStateCode.Stopping:
                                case InstanceStateCode.ShuttingDown:

                                    // We don't currently have a status for stopping a node so we'll
                                    // consider it to be running because technically, it still is.

                                    nodeState = ClusterNodeState.Running;
                                    break;

                                case InstanceStateCode.Stopped:

                                    nodeState = ClusterNodeState.Off;
                                    break;

                                case InstanceStateCode.Terminated:

                                    nodeState = ClusterNodeState.NotProvisioned;
                                    break;

                                default:

                                    Covenant.Assert(false, $"Unexpected node instance status: [{stateCode}]");
                                    break;
                            }
                        }
                    }

                    clusterHealth.Nodes.Add(node.Name, nodeState);
                }

                // We're going to examine the node states from the AWS perspective and
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

                if (clusterLogin != null && clusterLogin.SetupDetails.SetupPending)
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
                            clusterHealth.Summary = "Cluster is configured";
                            break;

                        case ClusterNodeState.Paused:
                        case ClusterNodeState.Off:

                            clusterHealth.State   = ClusterState.Off;
                            clusterHealth.Summary = "Cluster is turned off";
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
                    clusterHealth.Summary = "Cluster is turned off";

                    return clusterHealth;
                }

                return clusterHealth;
            }
        }

        /// <inheritdoc/>
        public override async Task StartClusterAsync()
        {
            await SyncContext.Clear;
            Covenant.Requires<NotSupportedException>(cluster != null, $"[{nameof(AwsHostingManager)}] was created with the wrong constructor.");

            // Connect to AWS and read any cluster resources.

            await ConnectAwsAsync();

            // We just need to start all instances.

            var instanceIds = nodeNameToAwsInstance.Values
                .Select(awsInstance => awsInstance.InstanceId)
                .ToList();

            await ec2Client.StartInstancesAsync(new StartInstancesRequest(instanceIds));

            // ...and then wait for the cluster to report being healthy.

            await NeonHelper.WaitForAsync(
                async () =>
                {
                    var status = await GetClusterHealthAsync();

                    return status.State == ClusterState.Healthy;
                },
                timeout:      timeout,
                pollInterval: pollInterval);
        }

        /// <inheritdoc/>
        public override async Task StopClusterAsync(StopMode stopMode = StopMode.Graceful)
        {
            await SyncContext.Clear;
            Covenant.Requires<NotSupportedException>(cluster != null, $"[{nameof(AwsHostingManager)}] was created with the wrong constructor.");

            // Connect to AWS and read any cluster resources.

            await ConnectAwsAsync();

            // We just need to stop all cluster instances.

            var instanceIds = nodeNameToAwsInstance.Values
                .Select(awsInstance => awsInstance.InstanceId)
                .ToList();

            await ec2Client.StopInstancesAsync(new StopInstancesRequest(instanceIds) { Force = stopMode == StopMode.TurnOff });

            // ...and then wait for cluster to report being stopped.

            await NeonHelper.WaitForAsync(
                async () =>
                {
                    var status = await GetClusterHealthAsync();

                    return status.State == ClusterState.Off;
                },
                timeout:      timeout,
                pollInterval: pollInterval);
        }

        /// <inheritdoc/>
        public override async Task DeleteClusterAsync(bool removeOrphans = false)
        {
            await SyncContext.Clear;
            Covenant.Requires<NotSupportedException>(cluster != null, $"[{nameof(AwsHostingManager)}] was created with the wrong constructor.");

            // Connect to AWS and read any cluster resources.

            await ConnectAwsAsync();

            // Here's how we're going to do this:
            //
            //      1. Terminate all cluster instances
            //      2. Remove the placement groups
            //      3. Remove the load balancer
            //      4. Remove all target groups
            //      5. Remove the NAT gateway and the route tables
            //      6. Remove resources referenced by the VPC
            //      7. Remove the VPC
            //      8. Release Elastic IPs created with the cluster
            //      9. Remove the resource group
            //
            // Note that these resources need to be deleted in this order to unwind
            // any dependencies and also that we're going to retry [DependencyViolation]
            // failures because some resources take some time to be actually removed
            // and we're also going to retry for network related problems.

            var transientDetector = new Func<Exception, bool>(
                e =>
                {
                    var ec2Exception = e as AmazonEC2Exception;

                    if (ec2Exception != null)
                    {
                        switch (ec2Exception.ErrorCode)
                        {
                            case "DependencyViolation":
                            case "InvalidPlacementGroup.InUse":

                                return true;
                        }

                        if (e.InnerException != null)
                        {
                            return Retry.TransientDetector.NetworkOrHttp(e.InnerException);
                        }
                        else
                        {
                            return false;
                        }
                    }

                    return Retry.TransientDetector.NetworkOrHttp(e);
                });

            var retry = new LinearRetryPolicy(transientDetector, retryInterval: TimeSpan.FromSeconds(5), timeout: TimeSpan.FromSeconds(900));

            var instanceIds = instanceNameToAwsInstance.Values
                .Where(value => value.InstanceId != null)
                .Select(value => value.Instance.InstanceId)
                .ToList();

            //-----------------------------------------------------------------
            // Step 1: Terminate all cluster instances

            if (instanceIds.Count > 0)
            {
                await retry.InvokeAsync(async () => await ec2Client.TerminateInstancesAsync(new TerminateInstancesRequest(instanceIds)));
            }

            //-----------------------------------------------------------------
            // Step 2: Remove the placement groups

            if (controlPlanePlacementGroup != null)
            {
                await retry.InvokeAsync(async () => await ec2Client.DeletePlacementGroupAsync(new DeletePlacementGroupRequest(controlPlanePlacementGroup.GroupName)));
            }

            if (workerPlacementGroup != null)
            {
                await retry.InvokeAsync(async () => await ec2Client.DeletePlacementGroupAsync(new DeletePlacementGroupRequest(workerPlacementGroup.GroupName)));
            }

            //-----------------------------------------------------------------
            // Step 3: Remove the load balancer

            if (loadBalancer != null)
            {
                await retry.InvokeAsync(async () => await elbClient.DeleteLoadBalancerAsync(new DeleteLoadBalancerRequest() { LoadBalancerArn = loadBalancer.LoadBalancerArn }));
            }

            //-----------------------------------------------------------------
            // Step #4: Remove all of the target groups

            await Parallel.ForEachAsync(nameToTargetGroup.Values, parallelOptions,
                async (targetGroup, cancellationToken) =>
                {
                    await retry.InvokeAsync(async () => await elbClient.DeleteTargetGroupAsync(new DeleteTargetGroupRequest() { TargetGroupArn = targetGroup.TargetGroupArn }));
                });

            //-----------------------------------------------------------------
            // Step 5: Remove the NAT gateway

            if (ingressAddress != null && ingressAddress.AssociationId != null)
            {
                await retry.InvokeAsync(
                    async () =>
                    {
                        try
                        {
                            await ec2Client.DisassociateAddressAsync(
                                new DisassociateAddressRequest()
                                {
                                    AssociationId = ingressAddress.AssociationId
                                });
                        }
                        catch (AmazonEC2Exception e)
                        {
                            // $hack(jefflill):
                            //
                            // We're seeing these exceptions when it looks like the 
                            // address was actually disassociated, so we're going to
                            // ignore this.

                            if (e.ErrorCode != "AuthFailure")
                            {
                                throw;
                            }
                        }
                    });
            }

            if (egressAddress != null && egressAddress.AssociationId != null)
            {
                await retry.InvokeAsync(
                    async () =>
                    {
                        await retry.InvokeAsync(
                            async () =>
                            {
                                try
                                {
                                    await ec2Client.DisassociateAddressAsync(
                                        new DisassociateAddressRequest()
                                        {
                                            AssociationId = egressAddress.AssociationId
                                        });
                                }
                                catch (AmazonEC2Exception e)
                                {
                                    // $hack(jefflill):
                                    //
                                    // We're seeing these exceptions when it looks like the 
                                    // address was actually disassociated, so we're going to
                                    // ignore this.

                                    if (e.ErrorCode != "AuthFailure")
                                    {
                                        throw;
                                    }
                                }
                            });
                    });
            }

            if (natGateway != null)
            {
                await retry.InvokeAsync(async () => await ec2Client.DeleteNatGatewayAsync(new DeleteNatGatewayRequest() { NatGatewayId = natGateway.NatGatewayId }));
            }

            //-----------------------------------------------------------------
            // Step 6: Remove resources referenced by the VPC:
            //
            //      Internet Gateway
            //      Node Subnet
            //      Public Subnet
            //      Security Group
            //      Node Route Table
            //      Public Route Table

            if (internetGateway != null)
            {
                await retry.InvokeAsync(
                    async () =>
                    {
                        await ec2Client.DetachInternetGatewayAsync(
                            new DetachInternetGatewayRequest()
                            {
                                VpcId             = vpc.VpcId,
                                InternetGatewayId = internetGateway.InternetGatewayId
                            });
                    });

                await retry.InvokeAsync(
                    async () =>
                    {
                        await ec2Client.DeleteInternetGatewayAsync(
                            new DeleteInternetGatewayRequest()
                            { 
                                InternetGatewayId = internetGateway.InternetGatewayId 
                            });
                    });
            }

            if (nodeSubnet != null)
            {
                await retry.InvokeAsync(
                    async () =>
                    {
                        await ec2Client.DeleteSubnetAsync(
                            new DeleteSubnetRequest
                            {
                                SubnetId = nodeSubnet.SubnetId
                            });
                    });
            }

            if (publicSubnet != null)
            {
                await retry.InvokeAsync(
                    async () =>
                    {
                        await ec2Client.DeleteSubnetAsync(
                            new DeleteSubnetRequest
                            {
                                SubnetId = publicSubnet.SubnetId
                            });
                    });
            }

            if (securityGroup != null)
            {
                await retry.InvokeAsync(
                    async () =>
                    {
                        await ec2Client.DeleteSecurityGroupAsync(
                            new DeleteSecurityGroupRequest
                            {
                                GroupId = securityGroup.GroupId
                            });
                    });
            }

            if (nodeRouteTable != null)
            {
                await retry.InvokeAsync(
                    async () =>
                    {
                        await ec2Client.DeleteRouteTableAsync(
                            new DeleteRouteTableRequest()
                            {
                                RouteTableId = nodeRouteTable.RouteTableId
                            });
                    });
            }

            if (publicRouteTable != null)
            {
                await retry.InvokeAsync(
                    async () =>
                    {
                        await ec2Client.DeleteRouteTableAsync(
                            new DeleteRouteTableRequest()
                            {
                                RouteTableId = publicRouteTable.RouteTableId
                            });
                    });
            }

            //-----------------------------------------------------------------
            // Step 7: Remove the VPC

            if (vpc != null)
            {
                await retry.InvokeAsync(async () => await ec2Client.DeleteVpcAsync(new DeleteVpcRequest(vpc.VpcId)));
            }

            //-----------------------------------------------------------------
            // Step 8: Release Elastic IPs created for the cluster

            if (!cluster.Definition.Hosting.Aws.Network.HasCustomElasticIPs)
            {
                if (ingressAddress != null)
                {
                    await retry.InvokeAsync(async () => await ec2Client.ReleaseAddressAsync(new ReleaseAddressRequest() { AllocationId = ingressAddress.AllocationId }));
                }

                if (egressAddress != null)
                {
                    await retry.InvokeAsync(async () => await ec2Client.ReleaseAddressAsync(new ReleaseAddressRequest() { AllocationId = egressAddress.AllocationId }));
                }
            }

            //-----------------------------------------------------------------
            // Step 9: Remove the resource group

            if (resourceGroup != null)
            {
                await retry.InvokeAsync(async () => await rgClient.DeleteGroupAsync(new DeleteGroupRequest() { Group = resourceGroup.GroupArn }));
            }
        }
    }
}
