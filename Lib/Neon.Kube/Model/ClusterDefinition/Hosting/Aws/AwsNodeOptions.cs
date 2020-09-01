//-----------------------------------------------------------------------------
// FILE:	    AwsNodeOptions.cs
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

namespace Neon.Kube
{
    /// <summary>
    /// AWS specific options for a cluster node.  These options can be used to override
    /// defaults specified by <see cref="AwsHostingOptions"/>.  The constructor initializes
    /// reasonable values.
    /// </summary>
    public class AwsNodeOptions
    {
        /// <summary>
        /// Optionally specifies the type of ECB instance to provision for this node.  The available
        /// instance types are listed <a href="https://aws.amazon.com/ec2/instance-types/">here</a>.
        /// This defaults to <see cref="AwsHostingOptions.DefaultInstanceType"/>.
        /// </summary>
        [JsonProperty(PropertyName = "InstanceType", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "instanceType", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string InstanceType { get; set; } = null;

        /// <summary>
        /// Optionally specifies the type of volume to attach to the cluster node.  This defaults
        /// to <see cref="AwsVolumeType.Default"/> which indicates that <see cref="AwsHostingOptions.DefaultInstanceType"/>
        /// will specify the volume type for the node.
        /// </summary>
        [JsonProperty(PropertyName = "InstanceType", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "instanceType", ApplyNamingConventions = false)]
        [DefaultValue(AwsVolumeType.Default)]
        public AwsVolumeType VolumeType { get; set; } = AwsVolumeType.Default;

        /// <summary>
        /// Optionally specifies the size of the EBS volume to be created and attached to the cluster node.
        /// This defaults to <c>null</c> which indicates that <see cref="AwsHostingOptions.DefaultVolumeSize"/>
        /// will be used, and that defaults to <b>128 GiB</b>.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Node disks smaller than 32 GiB are not supported by neonKUBE.  We'll automatically
        /// upgrade the disk size when necessary.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "VolumeSize", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "volumeSize", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string VolumeSize { get; set; } = null;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <param name="nodeName">The associated node name.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(ClusterDefinition clusterDefinition, string nodeName)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            if (VolumeType == AwsVolumeType.Default)
            {
                VolumeType = clusterDefinition.Hosting.Aws.DefaultVolumeType;

                if (VolumeType == AwsVolumeType.Default)
                {
                    VolumeType = AwsVolumeType.Gp2;
                }
            }

            // Validate the instance, setting the cluster default if necessary.

            var instanceType = this.InstanceType;

            if (string.IsNullOrEmpty(instanceType))
            {
                instanceType = clusterDefinition.Hosting.Aws.DefaultInstanceType;
            }

            this.InstanceType = instanceType;

            // Validate the volume size, setting the cluster default if necessary.

            if (string.IsNullOrEmpty(this.VolumeSize))
            {
                this.VolumeSize = clusterDefinition.Hosting.Aws.DefaultVolumeSize;
            }

            if (!ByteUnits.TryParse(this.VolumeSize, out var volumeSizeBytes) || volumeSizeBytes <= 1)
            {
                throw new ClusterDefinitionException($"cluster node [{nodeName}] configures [{nameof(AwsNodeOptions)}.{nameof(VolumeSize)}={VolumeSize}] which is not valid.");
            }

            var driveSizeGiB = AwsHelper.GetDiskSizeGiB(VolumeType, volumeSizeBytes);

            this.VolumeSize = $"{driveSizeGiB} GiB";
        }
    }
}
