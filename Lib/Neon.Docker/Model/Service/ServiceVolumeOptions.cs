//-----------------------------------------------------------------------------
// FILE:	    ServiceVolumeOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Neon.Docker
{
    /// <summary>
    /// Volume options for volume service mounts.
    /// </summary>
    public class ServiceVolumeOptions : INormalizable
    {
        /// <summary>
        /// Enables populating the volume with data from the container target.
        /// </summary>
        [JsonProperty(PropertyName = "NoCopy", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(false)]
        public bool NoCopy { get; set; } = false;

        /// <summary>
        /// Volume driver labels.
        /// </summary>
        [JsonProperty(PropertyName = "Labels", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public Dictionary<string, string> Labels { get; set; }

        /// <summary>
        /// Optionally specifies volume driver and options.
        /// </summary>
        [JsonProperty(PropertyName = "DriverConfig", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public ServiceVolumeDriverConfig DriverConfig { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
            Labels       = Labels ?? new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            DriverConfig = DriverConfig ?? new ServiceVolumeDriverConfig();

            DriverConfig.Normalize();
        }
    }
}
