//-----------------------------------------------------------------------------
// FILE:	    AzureNodeOptions.cs
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
using Castle.DynamicProxy.Generators.Emitters.SimpleAST;

namespace Neon.Kube
{
    /// <summary>
    /// Azure specific options for cluster cluster node.  The default constructor
    /// initializes reasonable defaults.
    /// </summary>
    public class AzureNodeOptions
    {
        /// <summary>
        /// <para>
        /// Specifies the Azure virtual machine size.  You the available VM sizes are listed 
        /// <a href="https://docs.microsoft.com/en-us/azure/virtual-machines/sizes-general">here</a>.
        /// </para>
        /// <note>
        /// This defaults to <b>Standard_B2S</b> which should be suitable for testing purposes
        /// as well as relatively idle clusters.  Each <b>Standard_B2S</b> VM includes 2 virtual
        /// cores and 4 GiB RAM.  At the time this was written, the pay-as-you-go cost for this
        /// VM is listed at $0.0416/hour or about $30/month in a USA datacenter.  <b>Bs-series</b>
        /// VMs are available in almost all Azure datacenters.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "VmSize", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "vmSize", ApplyNamingConventions = false)]
        [DefaultValue("Standard_B2S")]
        public string VmSize { get; set; } = "Standard_B2S";

        /// <summary>
        /// <para>
        /// Optionally specifies the storage type to use for any mounted drives.  This defaults to <see cref="AzureStorageTypes.Default"/>
        /// which indicates that <see cref="AzureOptions.DefaultStorageType"/> will specify the storage type
        /// for this node.  By default, <see cref="AzureStorageTypes.StandardSSD"/> drives will be provisioned
        /// when storage type is not specified.
        /// </para>
        /// <note>
        /// You should really consider upgrading production clusters to one of the SSD based storage types.
        /// </note>
        /// </summary>
        /// <remarks>
        /// <note>
        /// All virtual machine sizes support <see cref="AzureStorageTypes.StandardHDD"/> which is why that
        /// is the default value.  Consult the consult the Azure documentation to virtual machine size specified 
        /// by <see cref="VmSize"/> can support <see cref="AzureStorageTypes.PremiumSSD"/>.
        /// </note>
        /// <para>
        /// <see cref="AzureStorageTypes.StandardHDD"/> specifies relatively slow rotating hard drives,
        /// <see cref="AzureStorageTypes.StandardSSD"/> specifies standard SSD based drives,
        /// <see cref="AzureStorageTypes.PremiumSSD"/> specifies fast SSD based drives, and finally
        /// <see cref="AzureStorageTypes.UltraSSD"/> specifies super fast SSD based drives.  Azure recommends that
        /// most production VMs deploy with SSDs.
        /// </para>
        /// <note>
        /// <see cref="AzureStorageTypes.UltraSSD"/> storage is still relatively new and your region may not be able to
        /// attach ultra drives to all VM instance types.  See this <a href="https://docs.microsoft.com/en-us/azure/virtual-machines/windows/disks-enable-ultra-ssd">note</a>
        /// for more information.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "StorageType", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "storageType", ApplyNamingConventions = false)]
        [DefaultValue(AzureStorageTypes.Default)]
        public AzureStorageTypes StorageType { get; set; } = AzureStorageTypes.Default;

        /// <summary>
        /// Optionally specifies the size of the mounted managed Azure disk as <see cref="ByteUnits"/>.  This
        /// defaults to <c>null</c> which indicates that <see cref="AzureOptions.DefaultDiskSize"/>
        /// will be used instead, and that defaults to <b>128 GiB</b>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="AzureStorageTypes.StandardHDD"/>, <see cref="AzureStorageTypes.StandardSSD"/>, and
        /// <see cref="AzureStorageTypes.PremiumSSD"/> drives may be provisioned in these
        /// sizes: <b>4GiB</b>, <b>8GiB</b>, <b>16GiB</b>, <b>32GiB</b>, <b>64GiB</b>, <b>128GiB</b>, <b>256GiB</b>, <b>512GiB</b>,
        /// <b>1TiB</b>, <b>2TiB</b>, <b>4TiB</b>, <b>8TiB</b>, <b>16TiB</b>, or <b>32TiB</b>.
        /// </para>
        /// <para>
        /// <see cref="AzureStorageTypes.UltraSSD"/> based drives can be provisioned in these sizes:
        /// <b>4 GiB</b>,<b>8 GiB</b>,<b> GiB</b>,<b>16 GiB</b>,<b>32 GiB</b>,<b>64 GiB</b>,<b>128 GiB</b>,<b>256 GiB</b>,<b>512 GiB</b>,
        /// or from <b>1 TiB</b> to <b>64TiB</b> in increments of <b>1 TiB</b>.
        /// </para>
        /// <note>
        /// This size will be rounded up to the next valid drive size for the given storage type
        /// and set to the maximum allowed size, when necessary.
        /// </note>
        /// <note>
        /// The Azure disk sizes listed above may become out-of-date as Azure enhances their
        /// services.  Review the Azure documentation for more information about what is
        /// currently supported.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "DiskSize", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "diskSize", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string DiskSize { get; set; } = null;

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

            if (StorageType == AzureStorageTypes.Default)
            {
                StorageType = clusterDefinition.Hosting.Azure.DefaultStorageType;

                if (StorageType == AzureStorageTypes.Default)
                {
                    StorageType = AzureStorageTypes.StandardSSD;
                }
            }

            // Validate the VM size, setting the cluster default if necessary.

            var vmSize = this.VmSize;

            if (string.IsNullOrEmpty(vmSize))
            {
                vmSize = clusterDefinition.Hosting.Azure.DefaultVmSize;
            }

            this.VmSize = vmSize;

            // Validate the drive size, setting the cluster default if necessary.

            if (string.IsNullOrEmpty(this.DiskSize))
            {
                this.DiskSize = clusterDefinition.Hosting.Azure.DefaultDiskSize;
            }

            if (!ByteUnits.TryParse(this.DiskSize, out var driveSizeBytes) || driveSizeBytes <= 1)
            {
                throw new ClusterDefinitionException($"cluster node [{nodeName}] configures [{nameof(DiskSize)}={DiskSize}] which is not valid.");
            }

            var driveSizeGiB = AzureHelper.GetDiskSizeGiB(StorageType, driveSizeBytes);

            this.DiskSize = $"{driveSizeGiB} GiB";
        }
    }
}
