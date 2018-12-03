//-----------------------------------------------------------------------------
// FILE:	    AzureNodeOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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

using Neon.Common;
using Neon.Net;

namespace Neon.Hive
{
    /// <summary>
    /// Azure specific options for a hive node.  The default constructor
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
        [DefaultValue(defaultVmSize)]
        public AzureVmSizes VmSize { get; set; } = defaultVmSize;

        /// <summary>
        /// <para>
        /// Specifies the storage type to use for any mounted drives.  This defaults to <see cref="AzureStorageTypes.StandardHDD_LRS"/>
        /// as the lowest cost option.
        /// </para>
        /// <note>
        /// You should really consider upgrading production hives to one of the SSD based storage types.
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
        /// This is not recommended for neonHIVE nodes.
        /// </para>
        /// <para>
        /// For most hives, you'll wish to provision one or more drives per node.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "HardDriveCount", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(1)]
        public int HardDriveCount { get; set; } = 1;

        /// <summary>
        /// Specifies the size of each of the mounted managed drives in gigabytes.  This
        /// defaults to <b>64GB</b>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Azure <see cref="AzureStorageTypes.StandardHDD_LRS"/> based drives may be provisioned
        /// with one of these sizes: <b>32 GB</b>, <b>64 GB</b>, <b>128 GB</b>, <b>256 GB</b>,
        /// <b>512 GB</b>, <b>1TB</b>, <b>2TB</b>, <b>4TB</b> or <b>8TB</b>.
        /// </para>
        /// <para>
        /// Azure <see cref="AzureStorageTypes.StandardSSD_LRS"/> based drives may be provisioned
        /// with one of these sizes: <b>32 GB</b>, <b>64 GB</b>, <b>128 GB</b>, <b>256 GB</b>,
        /// <b>512 GB</b>, <b>1TB</b>, <b>2TB</b>, <b>4TB</b>, <b>8TB</b>, <b>16TB</b> or <b>32TB</b>.
        /// </para>
        /// <para>
        /// Azure <see cref="AzureStorageTypes.PremiumSSD_LRS"/> based drives may be provisioned
        /// with sizes: <b>32GB</b>, <b>64GB</b>, <b>128GB</b>, <b>256GB</b>, <b>512GB</b>,
        /// <b>1TB</b>, <b>2TB</b>, <b>4TB</b> or <b>8TB</b>.
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
        [JsonProperty(PropertyName = "HardDriveSizeGB", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(128)]
        public int HardDriveSizeGB { get; set; } = 64;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="hiveDefinition">The hive definition.</param>
        /// <param name="nodeName">The associated node name.</param>
        /// <exception cref="HiveDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(HiveDefinition hiveDefinition, string nodeName)
        {
            Covenant.Requires<ArgumentNullException>(hiveDefinition != null);

            var caps = AzureVmCapabilities.Get(VmSize);

            if (!caps.LoadBalancing)
            {
                throw new HiveDefinitionException($"Hive node [{nodeName}] configures [{nameof(VmSize)}={VmSize}] which does not support load balancing and cannot be used for a neonHIVE.");
            }

            if (!caps.SupportsDataStorageType(StorageType))
            {
                throw new HiveDefinitionException($"Hive node [{nodeName}] configures [{nameof(VmSize)}={VmSize}] which does not support [{StorageType}] managed data drives.");
            }

            if (HardDriveCount > 1)
            {
                throw new HiveDefinitionException($"Hive node [{nodeName}] configures [{nameof(HardDriveCount)}={HardDriveCount}] managed data drives.  Only zero or one managed drive is currently supported.");
            }

            if (caps.MaxDataDrives < HardDriveCount)
            {
                throw new HiveDefinitionException($"Hive node [{nodeName}]configures [{nameof(HardDriveCount)}={HardDriveCount}] managed data drives.  Only up to [{caps.MaxDataDrives}] drives are allowed.");
            }

            AzureHelper.GetDiskSizeGB(StorageType, HardDriveSizeGB);
        }
    }
}
