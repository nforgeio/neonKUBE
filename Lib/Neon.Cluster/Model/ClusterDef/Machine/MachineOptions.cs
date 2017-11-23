//-----------------------------------------------------------------------------
// FILE:	    LocalOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Net.Sockets;
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
    /// Specifies hosting settings for bare metal or virtual machines.
    /// </summary>
    public class MachineOptions
    {
        private const string defaultHostVhdxUri = "https://s3-us-west-2.amazonaws.com/neonforge/neoncluster/ubuntu-16.04.latest-prep.vhdx.zip";

        /// <summary>
        /// Default constructor.
        /// </summary>
        public MachineOptions()
        {
        }

        /// <summary>
        /// Indicates whether cluster host virtual machines are to be deployed on the current computer
        /// or whether the VMs or servers already exist and are ready to be configured.  This defaults
        /// to <c>true</c>.
        /// </summary>
        [JsonProperty(PropertyName = "DeployVMs", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(true)]
        public bool DeployVMs { get; set; } = true;

        /// <summary>
        /// Path to the folder where vitual machine hard drive folders are to be persisted.
        /// This defaults to the default folder for Windows or Macintosh.
        /// </summary>
        [JsonProperty(PropertyName = "VMDriveFolder", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string VMDriveFolder { get; set; } = null;

        /// <summary>
        /// Specifies the amount of memory to allocate to each cluster virtual machine when <see cref="DeployVMs"/>
        /// is specified.  This is specified as a string that can be an integer byte count or an integer with
        /// units like <b>512MB</b> or <b>2GB</b>.  This defaults to <b>4GB</b>.
        /// </summary>
        [JsonProperty(PropertyName = "VMMemory", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue("4GB")]
        public string VMMemory { get; set; } = "4GB";

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
        /// which is the latest supported Ubuntu 16.04 image.  This must be set if <see cref="DeployVMs"/>
        /// is <c>true</c>.
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

            if (DeployVMs && !clusterDefinition.Network.StaticIP)
            {
                throw new ClusterDefinitionException($"[{nameof(NetworkOptions)}.{nameof(NetworkOptions.StaticIP)}] must be [true] when [{nameof(MachineOptions)}.{nameof(DeployVMs)}=true]");
            }

            if (DeployVMs)
            {
                if (string.IsNullOrEmpty(HostVhdxUri))
                {
                    throw new ClusterDefinitionException($"[{nameof(MachineOptions)}.{nameof(HostVhdxUri)}] is required if [{nameof(MachineOptions)}.{nameof(DeployVMs)}=true].");
                }

                if (!Uri.TryCreate(HostVhdxUri, UriKind.Absolute, out Uri uri))
                {
                    throw new ClusterDefinitionException($"[{nameof(MachineOptions)}.{nameof(HostVhdxUri)}={HostVhdxUri}] is required if [{nameof(MachineOptions)}.{nameof(DeployVMs)}=true].");
                }
            }
        }
    }
}
