//-----------------------------------------------------------------------------
// FILE:        AwsHostingOptions.cs
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
using System.Runtime;

namespace Neon.Kube.ClusterDef
{
    /// <summary>
    /// Specifies the Amazon Web Services hosting settings.
    /// </summary>
    public class AwsHostingOptions
    {
        /// <summary>
        /// The maximum number of placement group partitions supported by AWS.
        /// </summary>
        internal const int MaxPlacementPartitions = 7;

        private const string            defaultInstanceType      = "c5.2xlarge";
        internal const AwsVolumeType    defaultVolumeType        = AwsVolumeType.Gp2;
        private const string            defaultVolumeSize        = "128 GiB";
        internal const AwsVolumeType    defaultOpenEbsVolumeType = defaultVolumeType;
        private const string            defaultOpenEbsVolumeSize = "128 GiB";

        /// <summary>
        /// Constructor.
        /// </summary>
        public AwsHostingOptions()
        {
        }

        /// <summary>
        /// The AWS access key ID that identifies the IAM key created for the IAM
        /// user assigned to NEONKUBE for management activities, including creating
        /// the cluster.  This combined with <see cref="SecretAccessKey"/> will be
        /// used to confirm the identity.
        /// </summary>
        [JsonProperty(PropertyName = "AccessKeyId", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "accessKeyId", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string AccessKeyId { get; set; }

        /// <summary>
        /// The AWS secret used to confirm the <see cref="AccessKeyId"/> identity.
        /// </summary>
        [JsonProperty(PropertyName = "SecretAccessKey", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "secretAccessKey", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string SecretAccessKey { get; set; }

        /// <summary>
        /// Specifies the AWS zone where the cluster will be provisioned.  This is required.
        /// </summary>
        [JsonProperty(PropertyName = "AvailabilityZone", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "availabilityZone", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string AvailabilityZone { get; set; }

        /// <summary>
        /// Specifies the AWS related cluster network options.
        /// </summary>
        [JsonProperty(PropertyName = "Network", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "network", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public AwsNetworkOptions Network { get; set; }

        /// <summary>
        /// Returns the AWS region where the cluster will be provisioned.  This is
        /// derived from <see cref="AvailabilityZone"/> by removing the last character, which
        /// is the zone suffix.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public string Region => AvailabilityZone?.Substring(0, AvailabilityZone.Length - 1);

        /// <summary>
        /// Specifies the number of control-plane placement group partitions the cluster control-plane node
        /// instances will be deployed to.  This defaults to <b>-1</b> which means that the number 
        /// of partitions will equal the number of control-plane nodes.  AWS supports a maximum of 7
        /// placement partitions.
        /// </summary>
        /// <remarks>
        /// <para>
        /// AWS provides three different types of <b>placement groups</b> to user help manage
        /// where virtual machine instances are provisioned within an AWS availability zone
        /// to customize fault tolerance due to AWS hardware failures.
        /// </para>
        /// <para>
        /// <a href="https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/placement-groups.html">AWS Placement groups</a>
        /// </para>
        /// <para>
        /// NEONKUBE provisions instances using two <b>partition placement groups</b>, one for
        /// the cluster control-plane nodes and the other for the workers.  The idea is that control-plane
        /// nodes should be deployed on separate hardware for fault tolerance because if the
        /// majority of control-plane nodes go offline, the entire cluster will be dramatically impacted.
        /// In general, the number of <see cref="ControlPlanePlacementPartitions"/> partitions should
        /// equal the number of control-plane nodes.
        /// </para>
        /// <para>
        /// Worker nodes are distributed across <see cref="WorkerPlacementPartitions"/> partitions
        /// in a separate placement group.  The number of worker partitions defaults to 1, potentially
        /// limiting the resilience to AWS hardware failures while making it more likely that AWS
        /// will be able to actually satisfy the conditions to provision the cluster node instances.
        /// </para>
        /// <para>
        /// Unfortunately, AWS may not have enough distinct hardware available to satisfy 
        /// your requirements.  In this case, we recommend that you try another availability
        /// zone first and if that doesn't work try reducing the number of partitions which
        /// can be as low as 1 partition.  
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "ControlPlanePlacementPartitions", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "controlPlanePlacementPartitions", ApplyNamingConventions = false)]
        [DefaultValue(-1)]
        public int ControlPlanePlacementPartitions { get; set; } = -1;

        /// <summary>
        /// Specifies the number of worker placement group partitions the cluster worker node
        /// instances will be deployed to.  This defaults to <b>1</b> trading off resilience
        /// to hardware failures against increasing the chance that AWS will actually be able
        /// to provision the cluster nodes.  AWS supports a maximum of 7 placement partitions.
        /// </summary>
        /// <remarks>
        /// <para>
        /// AWS provides three different types of <b>placement groups</b> to user help manage
        /// where virtual machine instances are provisioned within an AWS availability zone
        /// to customize fault tolerance due to AWS hardware failures.
        /// </para>
        /// <para>
        /// <a href="https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/placement-groups.html">AWS Placement groups</a>
        /// </para>
        /// <para>
        /// NEONKUBE provisions instances using two <b>partition placement groups</b>, one for
        /// the cluster control-plane nodes and the other for the workers.  The idea is that control-plane
        /// nodes should be deployed on separate hardware for fault tolerance because if the
        /// majority of control-plane nodes go offline, the entire cluster will be dramatically impacted.
        /// In general, the number of <see cref="ControlPlanePlacementPartitions"/> partitions should
        /// equal the number of control-plane nodes.
        /// </para>
        /// <para>
        /// Worker nodes are distributed across <see cref="WorkerPlacementPartitions"/> partitions
        /// in a separate placement group.  The number of worker partitions defaults to 1, potentially
        /// limiting the resilience to AWS hardware failures while making it more likely that AWS
        /// will be able to actually satisfy the conditions to provision the cluster node instances.
        /// </para>
        /// <para>
        /// Unfortunately, AWS may not have enough distinct hardware available to satisfy 
        /// your requirements.  In this case, we recommend that you try another availability
        /// zone first and if that doesn't work try reducing the number of partitions which
        /// can be as low as 1 partition.  
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "WorkerPlacementPartitions", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "workerPlacementPartitions", ApplyNamingConventions = false)]
        [DefaultValue(1)]
        public int WorkerPlacementPartitions { get; set; } = 1;

        /// <summary>
        /// AWS resource group where all cluster components are to be provisioned.  This defaults
        /// to "neon-" plus the cluster name but can be customized as required.
        /// </summary>
        [JsonProperty(PropertyName = "ResourceGroup", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "resourceGroup", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string ResourceGroup { get; set; }

        /// <summary>
        /// <para>
        /// Identifies the default AWS instance type to be provisioned for cluster nodes that don't
        /// specify an instance type.  This defaults to <b>c5.2xlarge</b> which includes 8 virtual
        /// cores and 16 GiB RAM but can be overridden for specific cluster nodes via
        /// <see cref="AwsNodeOptions.InstanceType"/>.
        /// </para>
        /// <note>
        /// NEONKUBE clusters cannot be deployed to ARM-based AWS instance types.  You must
        /// specify an instance type using a Intel or AMD 64-bit processor.
        /// </note>
        /// <note>
        /// NEONKUBE requires control-plane and worker instances to have at least 4 CPUs and 8GiB RAM.  Choose
        /// an AWS instance type that satisfies these requirements.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "DefaultInstanceType", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "defaultInstanceType", ApplyNamingConventions = false)]
        [DefaultValue(defaultInstanceType)]
        public string DefaultInstanceType { get; set; } = defaultInstanceType;

        /// <summary>
        /// <para>
        /// Specifies whether the cluster instances should be EBS-optimized by default.  
        /// This defaults to <c>false</c> and can be overidden for specific cluster nodes
        /// via <see cref="AwsNodeOptions.EbsOptimized"/>.
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
        /// <see cref="AwsNodeOptions.EbsOptimized"/> won't have any effect.
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
        [JsonProperty(PropertyName = "DefaultEbsOptimized", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "defaultEbsOptimized", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool DefaultEbsOptimized { get; set; } = false;

        /// <summary>
        /// Specifies the default AWS volume type for cluster node primary disks.  This defaults
        /// to <see cref="AwsVolumeType.Gp2"/> which is SSD based and offers a reasonable
        /// compromise between performance and cost.  This can be overriden for specific cluster
        /// nodes via <see cref="AwsNodeOptions.VolumeType"/>.
        /// </summary>
        [JsonProperty(PropertyName = "DefaultVolumeType", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "defaultVolumeType", ApplyNamingConventions = false)]
        [DefaultValue(defaultVolumeType)]
        public AwsVolumeType DefaultVolumeType { get; set; } = defaultVolumeType;

        /// <summary>
        /// Specifies the default AWS volume size for the cluster node primary disks.
        /// This defaults to <b>128 GiB</b> but can be overridden for specific cluster
        /// nodes via <see cref="AwsNodeOptions.VolumeSize"/>.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Node disks smaller than 32 GiB are not supported by NEONKUBE.  We'll automatically
        /// round up the disk size when necessary.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "DefaultVolumeSize", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "defaultVolumeSize", ApplyNamingConventions = false)]
        [DefaultValue(defaultVolumeSize)]
        public string DefaultVolumeSize { get; set; } = defaultVolumeSize;

        /// <summary>
        /// Specifies the default AWS volume type to use for OpenEBS cStor disks.  This defaults
        /// to <see cref="AwsVolumeType.Gp2"/> which is SSD based and offers a reasonable
        /// compromise between performance and cost.  This can be overridden for specific
        /// cluster nodes via <see cref="AwsNodeOptions.OpenEbsVolumeType"/>.
        /// </summary>
        [JsonProperty(PropertyName = "DefaultOpenEbsVolumeType", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "defaultOpenEbsVolumeType", ApplyNamingConventions = false)]
        [DefaultValue(defaultOpenEbsVolumeType)]
        public AwsVolumeType DefaultOpenEbsVolumeType { get; set; } = defaultOpenEbsVolumeType;

        /// <summary>
        /// Specifies the default AWS volume size to be used when creating 
        /// OpenEBS cStor disks.  This defaults to <b>128 GiB</b> but can
        /// be overridden for specific cluster nodes via <see cref="AwsNodeOptions.OpenEbsVolumeSize"/>.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Node disks smaller than 32 GiB are not supported by NEONKUBE.  We'll automatically
        /// round up the disk size when necessary.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "DefaultOpenEbsVolumeSize", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "defaultOpenEbsVolumeSize", ApplyNamingConventions = false)]
        [DefaultValue(defaultOpenEbsVolumeSize)]
        public string DefaultOpenEbsVolumeSize { get; set; } = defaultVolumeSize;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        public void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            Network ??= new AwsNetworkOptions();

            Network.Validate(clusterDefinition);

            var awsHostionOptionsPrefix = $"{nameof(ClusterDefinition.Hosting)}.{nameof(ClusterDefinition.Hosting.Aws)}";

            foreach (var ch in clusterDefinition.Name)
            {
                if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
                {
                    continue;
                }

                throw new ClusterDefinitionException($"cluster name [{clusterDefinition.Name}] is not valid for AWS deployment.  Only letters, digits, dashes, or underscores are allowed.");
            }

            if (string.IsNullOrEmpty(AccessKeyId))
            {
                throw new ClusterDefinitionException($"[{awsHostionOptionsPrefix}.{nameof(AccessKeyId)}] is required.");
            }

            if (string.IsNullOrEmpty(SecretAccessKey))
            {
                throw new ClusterDefinitionException($"[{awsHostionOptionsPrefix}.{nameof(SecretAccessKey)}] is required.");
            }

            if (string.IsNullOrEmpty(AvailabilityZone))
            {
                throw new ClusterDefinitionException($"[{awsHostionOptionsPrefix}.{nameof(AvailabilityZone)}] is required.");
            }

            // Verify [ResourceGroup].

            if (string.IsNullOrEmpty(ResourceGroup))
            {
                ResourceGroup = clusterDefinition.Name;
            }

            if (ResourceGroup.Length > 64)
            {
                throw new ClusterDefinitionException($"[{awsHostionOptionsPrefix}.{nameof(ResourceGroup)}={ResourceGroup}] is longer than 64 characters.");
            }

            if (!char.IsLetter(ResourceGroup.First()))
            {
                throw new ClusterDefinitionException($"[{awsHostionOptionsPrefix}.{nameof(ResourceGroup)}={ResourceGroup}] does not begin with a letter.");
            }

            if (ResourceGroup.Last() == '_' || ResourceGroup.Last() == '-')
            {
                throw new ClusterDefinitionException($"[{awsHostionOptionsPrefix}.{nameof(ResourceGroup)}={ResourceGroup}] ends with a dash or underscore.");
            }

            foreach (var ch in ResourceGroup)
            {
                if (!(char.IsLetterOrDigit(ch) || ch == '_' || ch == '-'))
                {
                    throw new ClusterDefinitionException($"[{awsHostionOptionsPrefix}.{nameof(ResourceGroup)}={ResourceGroup}] includes characters other than letters, digits, dashes and underscores.");
                }
            }

            // Verify [ControlPlanePlacementPartitions]

            if (ControlPlanePlacementPartitions < 0)
            {
                ControlPlanePlacementPartitions = Math.Min(MaxPlacementPartitions, clusterDefinition.ControlNodes.Count());
            }
            else
            {
                if (ControlPlanePlacementPartitions < 1 || MaxPlacementPartitions < ControlPlanePlacementPartitions)
                {
                    throw new ClusterDefinitionException($"[{awsHostionOptionsPrefix}.{nameof(ControlPlanePlacementPartitions)}={ControlPlanePlacementPartitions}] cannot be in the range [1...{MaxPlacementPartitions}]");
                }
            }

            // Verify [ControlPlanePlacementPartitions]

            if (WorkerPlacementPartitions < 1 || MaxPlacementPartitions < WorkerPlacementPartitions)
            {
                throw new ClusterDefinitionException($"[{awsHostionOptionsPrefix}.{nameof(WorkerPlacementPartitions)}={WorkerPlacementPartitions}] cannot be in the range [1...{MaxPlacementPartitions}]");
            }

            // Verify [DefaultInstanceType]

            if (string.IsNullOrEmpty(DefaultInstanceType))
            {
                DefaultInstanceType = defaultInstanceType;
            }

            // Verify [DefaultVolumeSize].

            if (string.IsNullOrEmpty(DefaultVolumeSize))
            {
                DefaultVolumeSize = defaultVolumeSize;
            }

            if (!ByteUnits.TryParse(DefaultVolumeSize, out var volumeSize) || volumeSize <= 0)
            {
                throw new ClusterDefinitionException($"[{awsHostionOptionsPrefix}.{nameof(DefaultVolumeSize)}={DefaultVolumeSize}] is not valid.");
            }

            // Verify [DefaultOpenEbsVolumeSize].

            if (string.IsNullOrEmpty(DefaultOpenEbsVolumeSize))
            {
                DefaultOpenEbsVolumeSize = defaultOpenEbsVolumeSize;
            }

            if (!ByteUnits.TryParse(DefaultOpenEbsVolumeSize, out var openEbsVolumeSize) || openEbsVolumeSize <= 0)
            {
                throw new ClusterDefinitionException($"[{awsHostionOptionsPrefix}.{nameof(DefaultOpenEbsVolumeSize)}={DefaultOpenEbsVolumeSize}] is not valid.");
            }

            // Check AWS cluster limits.

            if (clusterDefinition.ControlNodes.Count() > KubeConst.MaxControlPlaneNodes)
            {
                throw new ClusterDefinitionException($"cluster control-plane count [{awsHostionOptionsPrefix}.{clusterDefinition.ControlNodes.Count()}] exceeds the [{KubeConst.MaxControlPlaneNodes}] limit for clusters.");
            }

            if (clusterDefinition.Nodes.Count() > AwsHelper.MaxClusterNodes)
            {
                throw new ClusterDefinitionException($"cluster node count [{awsHostionOptionsPrefix}.{clusterDefinition.Nodes.Count()}] exceeds the [{AwsHelper.MaxClusterNodes}] limit for clusters deployed to AWS.");
            }
        }

        /// <summary>
        /// Clears all hosting related secrets.
        /// </summary>
        public void ClearSecrets()
        {
            AccessKeyId     = null;
            SecretAccessKey = null;
        }
    }
}
