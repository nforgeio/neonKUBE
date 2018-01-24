//-----------------------------------------------------------------------------
// FILE:	    HyperVOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.ComponentModel;
using System.Diagnostics.Contracts;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Neon.Cluster
{
    /// <summary>
    /// Specifies hosting settings for the Microsoft Hyper-V hypervisor.
    /// </summary>
    public class HyperVOptions
    {
        private const string defaultHostVhdxUri      = "https://s3-us-west-2.amazonaws.com/neonforge/neoncluster/ubuntu-16.04.latest-prep.vhdx.zip";
        internal const string defaultVmMinimumMemory = "2GB";

        /// <summary>
        /// Default constructor.
        /// </summary>
        public HyperVOptions()
        {
        }

        /// <summary>
        /// <para>
        /// URI to the zipped VHDX image with the base Docker host operating system.  This defaults to
        /// <b>https://s3-us-west-2.amazonaws.com/neonforge/neoncluster/ubuntu-16.04.latest-prep.vhdx.zip</b>
        /// which is the latest supported Ubuntu 16.04 image.
        /// </para>
        /// <note>
        /// Production cluster definitions should be configured with an VHDX with a specific version
        /// of the host operating system to ensure that cluster nodes are provisioned with the same
        /// operating system version.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "HostVhdxUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultHostVhdxUri)]
        public string HostVhdxUri { get; set; } = defaultHostVhdxUri;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
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
                throw new ClusterDefinitionException($"[{nameof(HyperVOptions)}.{nameof(HostVhdxUri)}] is required when deploying to Hyper-V.");
            }

            clusterDefinition.ValidatePrivateNodeAddresses();                                           // Private node IP addresses must be assigned and valid.
            clusterDefinition.Hosting.ValidateHypervisor(clusterDefinition, remoteHypervisors: false);  // Hypervisor options must be valid.
        }
    }
}
