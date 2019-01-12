//-----------------------------------------------------------------------------
// FILE:	    ServiceVolumeDriverConfig.cs
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
    /// Volume driver configuration options.
    /// </summary>
    public class ServiceVolumeDriverConfig : INormalizable
    {
        /// <summary>
        /// Name of the volume driver.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public string Name { get; set; }

        /// <summary>
        /// Volume driver options.
        /// </summary>
        [JsonProperty(PropertyName = "Options", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public Dictionary<string, string> Options { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
            Options = Options ?? new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        }
    }
}
