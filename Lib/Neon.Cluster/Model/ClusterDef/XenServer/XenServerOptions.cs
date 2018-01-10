//-----------------------------------------------------------------------------
// FILE:	    XenServerOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

using System;
using System.ComponentModel;
using System.Diagnostics.Contracts;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Neon.Cluster
{
    /// <summary>
    /// Specifies hosting settings for the Citrix XenServer hypervisor.
    /// </summary>
    public class XenServerOptions
    {
        private const string defaultHostVhdxUri     = "https://s3-us-west-2.amazonaws.com/neonforge/neoncluster/ubuntu-16.04.latest-prep.vhdx.zip";
        private const string defaultVMMemory        = "4GB";
        private const string defaultVMMinimumMemory = "2GB";

        /// <summary>
        /// Default constructor.
        /// </summary>
        public XenServerOptions()
        {
        }

        /// <summary>
        /// Path to the folder where vitual machine hard drive folders are to be persisted.
        /// This defaults to the default folder for Windows or Macintosh.
        /// </summary>
        [JsonProperty(PropertyName = "VMDriveFolder", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string VMDriveFolder { get; set; } = null;

        /// <summary>
        /// Specifies the maximum amount of memory to allocate to each cluster virtual machine.  This is specified as a string
        /// that can be an integer byte count or an integer with units like <b>512MB</b> or <b>2GB</b>.  This defaults to <b>4GB</b>.
        /// </summary>
        [JsonProperty(PropertyName = "VMMemory", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultVMMemory)]
        public string VMMemory { get; set; } = defaultVMMemory;

        /// <summary>
        /// Specifies the minimum amount of memory to allocate to each cluster virtual machine.  This is specified as a string that
        /// can be an integer byte count or an integer with units like <b>512MB</b> or <b>2GB</b> or may be set to <c>null</c> to set
        /// the same value as <see cref="VMMemory"/>.  This defaults to <c>2GB</c>, which is half of the default value of <see cref="VMMemory"/>
        /// which is <b>4GB</b>.
        /// </summary>
        [JsonProperty(PropertyName = "VMMinimumMemory", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultVMMinimumMemory)]
        public string VMMinimumMemory { get; set; } = defaultVMMinimumMemory;

        /// <summary>
        /// The number of virtual processors tom assign to each virtual machine.
        /// </summary>
        [JsonProperty(PropertyName = "VMProcessors", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(4)]
        public int VMProcessors { get; set; } = 4;

        /// <summary>
        /// <para>
        /// URI to the zipped VHDX image with the base Docker host operating system.  This defaults to
        /// <b>https://s3-us-west-2.amazonaws.com/neonforge/neoncluster/ubuntu-16.04.latest-prep.vhdx.zip</b>
        /// which is the latest supported Ubuntu 16.04 image.
        /// </para>
        /// <note>
        /// Production cluster definitions should be configured with VHDX image with a specific version
        /// of the host operating system to ensure that cluster nodes are provisioned with the same
        /// operating system version.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "HostVhdxUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultHostVhdxUri)]
        public string HostVhdxUri { get; set; } = defaultHostVhdxUri;

        /// <summary>
        /// Validates the options definition and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null);

            if (!clusterDefinition.Network.StaticIP)
            {
                throw new ClusterDefinitionException($"[{nameof(NetworkOptions)}.{nameof(NetworkOptions.StaticIP)}] must be [true] when deploying to Hyper-V.");
            }

            if (string.IsNullOrEmpty(HostVhdxUri) || !Uri.TryCreate(HostVhdxUri, UriKind.Absolute, out Uri uri))
            {
                throw new ClusterDefinitionException($"[{nameof(MachineOptions)}.{nameof(HostVhdxUri)}] is required when deploying to Hyper-V.");
            }

            clusterDefinition.ValidatePrivateNodeAddresses();   // Private node IP addresses must be assigned and valid.

            VMMemory        = VMMemory ?? defaultVMMemory;
            VMMinimumMemory = VMMinimumMemory ?? VMMemory;

            ValidateVMMemory(VMMemory, nameof(VMMemory));
            ValidateVMMemory(VMMinimumMemory, nameof(VMMinimumMemory));
        }

        /// <summary>
        /// Ensures that a VM memory size specification is valid.
        /// </summary>
        /// <param name="memorySize">The size string.</param>
        /// <param name="propertyName">The property name.</param>
        private void ValidateVMMemory(string memorySize, string propertyName)
        {
            if (string.IsNullOrEmpty(memorySize))
            {
                throw new ClusterDefinitionException($"[{nameof(MachineOptions)}.{propertyName}] cannot be NULL or empty.");
            }

            if (memorySize.EndsWith("MB", StringComparison.InvariantCultureIgnoreCase) ||
                memorySize.EndsWith("GB", StringComparison.InvariantCultureIgnoreCase))
            {
                var count = memorySize.Substring(0, memorySize.Length - 2);

                if (!int.TryParse(count, out int size) || size <= 0)
                {
                    throw new ClusterDefinitionException($"[{nameof(MachineOptions)}.{propertyName}={memorySize}] is not valid.");
                }
            }
            else if (!int.TryParse(memorySize, out int size) || size <= 0)
            {
                throw new ClusterDefinitionException($"[{nameof(MachineOptions)}.{propertyName}={memorySize}] is not valid.");
            }
        }
    }
}
