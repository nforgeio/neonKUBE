//-----------------------------------------------------------------------------
// FILE:	    AwsNodeOptions.cs
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Net;

namespace Neon.Kube.ClusterDef
{
    /// <summary>
    /// AWS specific options for a cluster node.  These options can be used to override
    /// defaults specified by <see cref="AwsHostingOptions"/>.  The constructor initializes
    /// reasonable values.
    /// </summary>
    public class AwsNodeOptions
    {
        /// <summary>
        /// <para>
        /// Optionally specifies the type of ECB instance to provision for this node.  The available
        /// instance types are listed <a href="https://aws.amazon.com/ec2/instance-types/">here</a>.
        /// This defaults to <see cref="AwsHostingOptions.DefaultInstanceType"/>.
        /// </para>
        /// <note>
        /// neonKUBE clusters cannot be deployed to ARM-based AWS instance types.  You must
        /// specify an instance type using a Intel or AMD 64-bit processor.
        /// </note>
        /// <note>
        /// neonKUBE requires control-plane and worker instances to have at least 4 CPUs and 8GiB RAM.  Choose
        /// an AWS instance type that satisfies these requirements.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "InstanceType", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "instanceType", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string InstanceType { get; set; } = null;

        /// <summary>
        /// <para>
        /// Specifies whether the cluster instance should be EBS-optimized.  This is a <see cref="TriState"/>
        /// value that defaults to <see cref="TriState.Default"/> which means that the default cluster wide
        /// <see cref="AwsHostingOptions.DefaultEbsOptimized"/> value will be used.  You can override the 
        /// cluster default for this node by setting <see cref="TriState.True"/> or <see cref="TriState.False"/>.
        /// </para>
        /// <para>
        /// <a href="https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/ebs-optimized.html">Amazon EBS–optimized instances</a>
        /// </para>
        /// </summary>
        /// <remarks>
        /// <para>
        /// Non EBS optimized instances perform disk operation I/O to EBS volumes using the same
        /// network used for other network operations.  This means that you may see some disk
        /// performance declines when your instance is busy serving web traffic or running
        /// database queries, etc.
        /// </para>
        /// <para>
        /// EBS optimization can be enabled for some instance types.  This provisions extra dedicated
        /// network bandwidth exclusively for EBS I/O.  Exactly how this works, depends on the specific
        /// VM type.
        /// </para>
        /// <para>
        /// More modern AWS VM types enable EBS optimization by default and you won't incur any
        /// additional charges for these instances and disabling EBS optimization here or via
        /// <see cref="AwsHostingOptions.DefaultEbsOptimized"/> won't have any effect.
        /// </para>
        /// <para>
        /// Some AWS instance types can be optimized but this is disabled by default.  When you
        /// enable this by setting <see cref="AwsHostingOptions.DefaultEbsOptimized"/><c>=true</c> or 
        /// <see cref="AwsNodeOptions.EbsOptimized"/><c>=true</c>, you'll probably an additional
        /// AWS hourly fee for these instances.
        /// </para>
        /// <para>
        /// Some AWS instance types don't support EBS optimization.  You'll need to be sure that
        /// this is disabled for those nodes.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "EbsOptimized", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "ebsOptimized", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public TriState EbsOptimized { get; set; } = TriState.Default;

        /// <summary>
        /// Optionally specifies the AWS placement group partition the node will be provisioned
        /// within.  This is a <b>1-based</b> partition index which <b>defaults to 0</b>, indicating
        /// that node placement will be handled automatically.
        /// </summary>
        /// <remarks>
        /// <para>
        /// You generally don't need to customize this for control-plane nodes since there will generally
        /// be a separate partition available for each control-plane and AWS will spread the instances
        /// across these automatically.  When you specify this for control-plane nodes, the partition index
        /// must be in the range of [1...<see cref="AwsHostingOptions.ControlPlanePlacementPartitions"/>].
        /// </para>
        /// <para>
        /// For some cluster scenarios like a noSQL database cluster, you may wish to explicitly
        /// control the partition where specific worker nodes are provisioned.  For example, if
        /// your database replcates data across multiple worker nodes, you'd like to have the
        /// workers hosting the same data be provisioned to different partitions such that if
        /// the workers in one partition are lost then the workers in the remaining partitions
        /// will still be able to serve the data.  
        /// </para>
        /// <para>
        /// When you specify this for worker nodes, the partition index must be in the range 
        /// of [1...<see cref="AwsHostingOptions.WorkerPlacementPartitions"/>].
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "PlacementPartition", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "placementPartition", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public int PlacementPartition { get; set; } = 0;

        /// <summary>
        /// Optionally specifies the type of AWS volume to be used as the node's primary disk.  This defaults
        /// to <see cref="AwsVolumeType.Default"/> which indicates that <see cref="AwsHostingOptions.DefaultInstanceType"/>
        /// will specify the volume type for the node.
        /// </summary>
        [JsonProperty(PropertyName = "VolumeType", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "volumeType", ApplyNamingConventions = false)]
        [DefaultValue(AwsVolumeType.Default)]
        public AwsVolumeType VolumeType { get; set; } = AwsVolumeType.Default;

        /// <summary>
        /// Optionally specifies the size of the AWS volume to be used as the node's primary disk.
        /// This defaults to <c>null</c> which indicates that <see cref="AwsHostingOptions.DefaultVolumeSize"/>
        /// will be used.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Node disks smaller than 64 GiB are not supported by neonKUBE.  We'll automatically
        /// round up the disk size when necessary.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "VolumeSize", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "volumeSize", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string VolumeSize { get; set; } = null;

        /// <summary>
        /// Optionally specifies the AWS volume type to be used for the the node's OpenEBS cStor disk (if any).  This defaults
        /// to <see cref="AwsVolumeType.Default"/> which indicates that <see cref="AwsHostingOptions.DefaultOpenEbsVolumeType"/>
        /// will specify the volume type for the node.
        /// </summary>
        [JsonProperty(PropertyName = "OpenEbsVolumeType", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "openEbsVolumeType", ApplyNamingConventions = false)]
        [DefaultValue(AwsVolumeType.Default)]
        public AwsVolumeType OpenEbsVolumeType { get; set; } = AwsVolumeType.Default;

        /// <summary>
        /// Optionally specifies the size of the AWS volume to be used for the node's OpenEBS cStor disk (if any).
        /// This defaults to <c>null</c> which indicates that <see cref="AzureHostingOptions.DefaultDiskSize"/>
        /// will be used for the node.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Node disks smaller than 64 GiB are not supported by neonKUBE.  We'll automatically
        /// upgrade the disk size when necessary.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "OpenEbsVolumeSize", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "openEbsVolumeSize", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string OpenEbsVolumeSize { get; set; } = null;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <param name="nodeName">The associated node name.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        public void Validate(ClusterDefinition clusterDefinition, string nodeName)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nodeName));

            var node                 = clusterDefinition.NodeDefinitions[nodeName];
            var awsNodeOptionsPrefix = $"{nameof(ClusterDefinition.NodeDefinitions)}.{nameof(NodeDefinition.Aws)}";

            // Set the cluster default storage types if necessary.

            if (VolumeType == AwsVolumeType.Default)
            {
                VolumeType = clusterDefinition.Hosting.Aws.DefaultVolumeType;

                if (VolumeType == AwsVolumeType.Default)
                {
                    VolumeType = AwsHostingOptions.defaultVolumeType;
                }
            }

            if (OpenEbsVolumeType == AwsVolumeType.Default)
            {
                OpenEbsVolumeType = clusterDefinition.Hosting.Aws.DefaultOpenEbsVolumeType;

                if (OpenEbsVolumeType == AwsVolumeType.Default)
                {
                    VolumeType = AwsHostingOptions.defaultOpenEbsVolumeType;
                }
            }

            // Validate the instance, setting the cluster default if necessary.

            var instanceType = this.InstanceType;

            if (string.IsNullOrEmpty(instanceType))
            {
                instanceType = clusterDefinition.Hosting.Aws.DefaultInstanceType;
            }

            this.InstanceType = instanceType;

            // Validate the placement partition index.

            if (PlacementPartition > 0)
            {
                if (node.IsControlPane)
                {
                    var controlNodeCount = clusterDefinition.ControlNodes.Count();
                    var partitionCount   = 0;

                    if (clusterDefinition.Hosting.Aws.ControlPlanePlacementPartitions == -1)
                    {
                        partitionCount = controlNodeCount;
                    }
                    else
                    {
                        partitionCount = clusterDefinition.Hosting.Aws.ControlPlanePlacementPartitions;
                    }

                    partitionCount = Math.Min(partitionCount, AwsHostingOptions.MaxPlacementPartitions);

                    if (PlacementPartition > partitionCount)
                    {
                        throw new ClusterDefinitionException($"cluster node [{nodeName}] configures [{awsNodeOptionsPrefix}.{nameof(PlacementPartition)}={PlacementPartition}] which is outside the valid range of [1...{partitionCount}].");
                    }
                }
                else if (node.IsWorker)
                {
                    var partitionCount = clusterDefinition.Hosting.Aws.WorkerPlacementPartitions;

                    partitionCount = Math.Min(partitionCount, AwsHostingOptions.MaxPlacementPartitions);

                    if (PlacementPartition > partitionCount)
                    {
                        throw new ClusterDefinitionException($"cluster node [{nodeName}] configures [{awsNodeOptionsPrefix}.{nameof(PlacementPartition)}={PlacementPartition}] which is outside the valid range of [1...{partitionCount}].");
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            // Validate the volume size, setting the cluster default if necessary.

            if (string.IsNullOrEmpty(this.VolumeSize))
            {
                this.VolumeSize = clusterDefinition.Hosting.Aws.DefaultVolumeSize;
            }

            if (!ByteUnits.TryParse(this.VolumeSize, out var volumeSizeBytes) || volumeSizeBytes <= 1)
            {
                throw new ClusterDefinitionException($"cluster node [{nodeName}] configures [{awsNodeOptionsPrefix}.{nameof(VolumeSize)}={VolumeSize}] which is not valid.");
            }

            var driveSizeGiB = AwsHelper.GetVolumeSizeGiB(VolumeType, volumeSizeBytes);

            this.VolumeSize = $"{driveSizeGiB} GiB";

            // Validate the OpenEBS volume size too.

            if (string.IsNullOrEmpty(this.OpenEbsVolumeSize))
            {
                this.OpenEbsVolumeSize = clusterDefinition.Hosting.Aws.DefaultOpenEbsVolumeSize;
            }

            if (!ByteUnits.TryParse(this.VolumeSize, out var openEbsVolumeSizeBytes) || openEbsVolumeSizeBytes <= 1)
            {
                throw new ClusterDefinitionException($"cluster node [{nodeName}] configures [{awsNodeOptionsPrefix}.{nameof(OpenEbsVolumeSize)}={OpenEbsVolumeSize}] which is not valid.");
            }

            var openEbsVolumeSizeGiB = AwsHelper.GetVolumeSizeGiB(OpenEbsVolumeType, openEbsVolumeSizeBytes);

            this.VolumeSize = $"{openEbsVolumeSizeGiB} GiB";
        }
    }
}
