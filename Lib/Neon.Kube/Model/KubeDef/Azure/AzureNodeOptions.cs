//-----------------------------------------------------------------------------
// FILE:	    AzureNodeOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
    /// Azure specific options for cluster cluster node.  The default constructor
    /// initializes reasonable defaults.
    /// </summary>
    public class AzureNodeOptions
    {
        private const AzureVmSizes          defaultVmSize      = AzureVmSizes.Standard_DS3_v2;
        private const AzureStorageTypes     defaultStorageType = AzureStorageTypes.StandardHDD_LRS;

        /// <summary>
        /// Specifies the Azure virtual machine size.  This defaults to <see cref="AzureVmSizes.Standard_DS3_v2"/>.
        /// </summary>
        [JsonProperty(PropertyName = "VmSize", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "vmSize", ApplyNamingConventions = false)]
        [DefaultValue(defaultVmSize)]
        public AzureVmSizes VmSize { get; set; } = defaultVmSize;

        /// <summary>
        /// <para>
        /// Specifies the storage type to use for any mounted drives.  This defaults to <see cref="AzureStorageTypes.StandardHDD_LRS"/>
        /// as the lowest cost option.
        /// </para>
        /// <note>
        /// You should really consider upgrading production clusters to one of the SSD based storage types.
        /// </note>
        /// </summary>
        /// <remarks>
        /// <note>
        /// All virtual machine sizes support <see cref="AzureStorageTypes.StandardHDD_LRS"/> which is why that
        /// is the default value.  Consult the consult the Azure documentation to virtual machine size specified 
        /// by <see cref="VmSize"/> can support <see cref="AzureStorageTypes.PremiumSSD_LRS"/>.
        /// </note>
        /// <para>
        /// <see cref="AzureStorageTypes.StandardHDD_LRS"/> specifies relatively slow rotating hard drives,
        /// <see cref="AzureStorageTypes.StandardSSD_LRS"/> specifies standard SSD based drives,
        /// <see cref="AzureStorageTypes.PremiumSSD_LRS"/> specifies fast SSD based drives.  Azure recommends that
        /// most production applications deploy with SSDs.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "StorageType", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "storageType", ApplyNamingConventions = false)]
        [DefaultValue(defaultStorageType)]
        public AzureStorageTypes StorageType { get; set; } = defaultStorageType;

        /// <summary>
        /// <para>
        /// Specifies the number of managed Azure data drives to attach to the node's virtual machine.
        /// This defaults to <b>1</b>.
        /// </para>
        /// <note>
        /// Currently only values of <b>0..1</b> are supported.  In the future we may allow multiple
        /// data disks to be mounted and combined into a RAID0 array.
        /// </note>
        /// </summary>
        /// <remarks>
        /// <note>
        /// Before setting this, consult the Azure documentation to see how many drives the
        /// virtual machine size specified by <see cref="VmSize"/> can support.
        /// </note>
        /// <para>
        /// This may be set to <b>0</b> which specifies that the node will store its data on 
        /// the local ephemeral (temporary) drive belonging to the Azure virtual machine.
        /// This is not recommended for cluster nodes.
        /// </para>
        /// <para>
        /// For most clusters, you'll wish to provision one or more drives per node.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "HardDriveCount", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "hardDriveCount", ApplyNamingConventions = false)]
        [DefaultValue(1)]
        public int HardDriveCount { get; set; } = 1;

        /// <summary>
        /// Specifies the size of each of the mounted managed drives in gigabytes.  This
        /// defaults to <b>64GiB</b>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Azure <see cref="AzureStorageTypes.StandardHDD_LRS"/> based drives may be provisioned
        /// with one of these sizes: <b>32 GiB</b>, <b>64 GiB</b>, <b>128 GiB</b>, <b>256 GiB</b>,
        /// <b>512 GiB</b>, <b>1TiB</b>, <b>2TiB</b>, <b>4TiB</b> or <b>8TiB</b>.
        /// </para>
        /// <para>
        /// Azure <see cref="AzureStorageTypes.StandardSSD_LRS"/> based drives may be provisioned
        /// with one of these sizes: <b>32 GiB</b>, <b>64 GiB</b>, <b>128 GiB</b>, <b>256 GiB</b>,
        /// <b>512 GiB</b>, <b>1TiB</b>, <b>2TiB</b>, <b>4TiB</b>, <b>8TiB</b>, <b>16TiB</b> or <b>32TiB</b>.
        /// </para>
        /// <para>
        /// Azure <see cref="AzureStorageTypes.PremiumSSD_LRS"/> based drives may be provisioned
        /// with sizes: <b>32GiB</b>, <b>64GiB</b>, <b>128GiB</b>, <b>256GiB</b>, <b>512GiB</b>,
        /// <b>1TiB</b>, <b>2TiB</b>, <b>4TiB</b> or <b>8TiB</b>.
        /// </para>
        /// <note>
        /// This size will be rounded up to the next valid drive size for the given storage type
        /// and rounded down to the maximum allowed size, if necessary.
        /// </note>
        /// <note>
        /// The Azure drive sizes listed above may become out-of-date as Azure enhances their
        /// services.  Review the Azure documentation for more information about what they
        /// currently support.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "HardDriveSizeGiB", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "hardDriveSizeGiB", ApplyNamingConventions = false)]
        [DefaultValue(128)]
        public int HardDriveSizeGiB { get; set; } = 64;

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

            var caps = AzureVmCapabilities.Get(VmSize);

            if (!caps.LoadBalancing)
            {
                throw new ClusterDefinitionException($"cluster node [{nodeName}] configures [{nameof(VmSize)}={VmSize}] which does not support load balancing and cannot be used for a cluster.");
            }

            if (!caps.SupportsDataStorageType(StorageType))
            {
                throw new ClusterDefinitionException($"cluster node [{nodeName}] configures [{nameof(VmSize)}={VmSize}] which does not support [{StorageType}] managed data drives.");
            }

            if (HardDriveCount > 1)
            {
                throw new ClusterDefinitionException($"cluster node [{nodeName}] configures [{nameof(HardDriveCount)}={HardDriveCount}] managed data drives.  Only zero or one managed drive is currently supported.");
            }

            if (caps.MaxDataDrives < HardDriveCount)
            {
                throw new ClusterDefinitionException($"cluster node [{nodeName}]configures [{nameof(HardDriveCount)}={HardDriveCount}] managed data drives.  Only up to [{caps.MaxDataDrives}] drives are allowed.");
            }

            AzureHelper.GetDiskSizeGiB(StorageType, HardDriveSizeGiB);
        }
    }
}
