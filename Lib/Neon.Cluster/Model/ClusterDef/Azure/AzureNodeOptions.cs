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

namespace Neon.Cluster
{
    /// <summary>
    /// Azure specific options for a cluster node.  The default constructor
    /// initializes reasonable defaults.
    /// </summary>
    public class AzureNodeOptions
    {
        private const AzureVmSizes          defaultVmSize      = AzureVmSizes.Standard_DS3_v2;
        private const AzureStorageTypes     defaultStorageType = AzureStorageTypes.PremiumLRS;

        /// <summary>
        /// Specifies the Azure virtual machine size.  This defaults to <see cref="AzureVmSizes.Standard_DS3_v2"/>.
        /// </summary>
        [JsonProperty(PropertyName = "VmSize", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultVmSize)]
        public AzureVmSizes VmSize { get; set; } = defaultVmSize;

        /// <summary>
        /// Specifies the storage type to use for any mounted drives.  This defaults to <see cref="AzureStorageTypes.StandardLRS"/>.
        /// </summary>
        /// <remarks>
        /// <note>
        /// All virtual machine sizes support <see cref="AzureStorageTypes.StandardLRS"/> which is why that
        /// is the default value.  Consult the consult the Azure documentation to virtual machine size specified 
        /// by <see cref="VmSize"/> can support <see cref="AzureStorageTypes.PremiumLRS"/>.
        /// </note>
        /// <para>
        /// <see cref="AzureStorageTypes.StandardLRS"/> specifies relatively slow rotating hard drives and
        /// <see cref="AzureStorageTypes.PremiumLRS"/> specifies fast SSD based drives.  Azure recommends that
        /// most production applications deploy with SSDs.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "StorageType", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultStorageType)]
        public AzureStorageTypes StorageType { get; set; } = defaultStorageType;

        /// <summary>
        /// Specifies the number of managed Azure data drives to attach to the node's virtual machine.
        /// This defaults to <b>1</b>.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Before setting this, consult the Azure documentation to see how many drives the
        /// virtual machine size specified by <see cref="VmSize"/> can support.
        /// </note>
        /// <para>
        /// This may be set to <b>0</b> which specifies that the node will store its data on 
        /// the local ephemeral (temporary) drive belonging to the Azure virtual machine.
        /// This is not recommended for neonCLUSTER nodes.
        /// </para>
        /// <para>
        /// For most clusters, you'll wish to provision one or more drives per node.  Multiple
        /// drives will be auytomatically combined into a consolidated RAID0 drive on the node.
        /// The size of each drive is specified by <see cref="HardDriveSizeGB"/>.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "HardDriveCount", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(1)]
        public int HardDriveCount { get; set; } = 1;

        /// <summary>
        /// Specifies the size of each of the mounted managed drives in gigabytes.  Multiple
        /// managed drives will be combined into a single large RAID0 drive.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Azure <see cref="AzureStorageTypes.StandardLRS"/> based drives may be provisioned
        /// with one of these sizes: <b>32 GB</b>, <b>64 GB</b>, <b>128 GB</b>, <b>256 GB</b>,
        /// <b>512 GB</b> or <b>1024 GB</b>.
        /// </para>
        /// <para>
        /// Azure <see cref="AzureStorageTypes.PremiumLRS"/> based drives may be provisioned
        /// with fewer sizes: <b>128 GB</b>, <b>256 GB</b>, <b>512 GB</b> or <b>1024 GB</b>.
        /// </para>
        /// <note>
        /// This size will be rounded up to the next valid drive size for the given storage type
        /// and rounded down to the maximum allowed size, if necessary.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "HardDriveSizeGB", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(128)]
        public int HardDriveSizeGB { get; set; } = 128;

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
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null);

            var caps = AzureVmCapabilities.Get(VmSize);

            if (!caps.LoadBalancing)
            {
                throw new ClusterDefinitionException($"Cluster node [{nodeName}] has size [{VmSize}] which does not support load balancing and cannot be used for a neonCLUSTER.");
            }

            if (!caps.SupportsDataStorageType(StorageType))
            {
                throw new ClusterDefinitionException($"Cluster node [{nodeName}] has size [{VmSize}] which does not support [{StorageType}] managed data drives.");
            }

            if (caps.DataDriveCount < HardDriveCount)
            {
                throw new ClusterDefinitionException($"Cluster node [{nodeName}] has size [{VmSize}] which does not support [{HardDriveCount}] managed data drives. Up to [{caps.DataDriveCount}] drives are allowed.");
            }

            AzureHelper.GetDiskSizeGB(StorageType, HardDriveSizeGB);
        }
    }
}
