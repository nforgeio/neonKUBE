//-----------------------------------------------------------------------------
// FILE:        AzureNodeOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
    /// Azure specific options for a cluster node.  These options can be used to
    /// override defaults specified by <see cref="AzureHostingOptions"/>.  The
    /// constructor initializes reasonable values.
    /// </summary>
    public class AzureNodeOptions
    {
        /// <summary>
        /// <para>
        /// Optionally specifies the Azure virtual machine size.  The available VM sizes are listed 
        /// <a href="https://docs.microsoft.com/en-us/azure/virtual-machines/sizes-general">here</a>.
        /// This defaults to <see cref="AzureHostingOptions.DefaultVmSize"/>.
        /// </para>
        /// <note>
        /// NeonKUBE clusters cannot be deployed to ARM-based Azure V, sizes at this time.  You must
        /// specify an VM size using a Intel or AMD 64-bit processor.
        /// </note>
        /// <note>
        /// NeonKUBE requires control-plane and worker instances to have at least 4 CPUs and 8GiB RAM.  Choose
        /// an Azure VM size instance type that satisfies these requirements.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "VmSize", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "vmSize", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string VmSize { get; set; } = null;

        /// <summary>
        /// <para>
        /// Optionally specifies the storage type to use the node's primary operating system disk.  This defaults to <see cref="AzureStorageType.Default"/>
        /// indicating that <see cref="AzureHostingOptions.DefaultStorageType"/> will specify the storage type
        /// for this node.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "StorageType", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "storageType", ApplyNamingConventions = false)]
        [DefaultValue(AzureStorageType.Default)]
        public AzureStorageType StorageType { get; set; } = AzureStorageType.Default;

        /// <summary>
        /// Optionally specifies the size of the Azure disk to be used as the node's boot disk.
        /// This defaults to <c>null</c> indicating that <see cref="AzureHostingOptions.DefaultBootDiskSize"/>
        /// will be used.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Node disks smaller than 64 GiB are not supported by NeonKUBE.  We'll automatically
        /// round up the disk size when necessary.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "BootDiskSize", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "bootDiskSize", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string BootDiskSize { get; set; } = null;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values, as required.
        /// </summary>
        /// <param name="clusterDefinition">Specifies the cluster definition.</param>
        /// <param name="nodeName">The associated node name.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        public void Validate(ClusterDefinition clusterDefinition, string nodeName)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            var optionsPrefix = $"{nameof(ClusterDefinition.Hosting)}.{nameof(ClusterDefinition.Hosting.Azure)}";

            // Set the cluster default storage types if necessary.

            if (StorageType == AzureStorageType.Default)
            {
                StorageType = clusterDefinition.Hosting.Azure.DefaultStorageType;

                if (StorageType == AzureStorageType.Default)
                {
                    StorageType = AzureHostingOptions.defaultStorageType;
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

            if (string.IsNullOrEmpty(this.BootDiskSize))
            {
                this.BootDiskSize = clusterDefinition.Hosting.Azure.DefaultBootDiskSize;
            }

            if (!ByteUnits.TryParse(this.BootDiskSize, out var driveSizeBytes) || driveSizeBytes <= 1)
            {
                throw new ClusterDefinitionException($"cluster node [{nodeName}] configures [{optionsPrefix}.{nameof(BootDiskSize)}={BootDiskSize}] which is not valid.");
            }

            var driveSizeGiB = AzureHelper.GetDiskSizeGiB(StorageType, driveSizeBytes);

            this.BootDiskSize = $"{driveSizeGiB} GiB";
        }
    }
}
