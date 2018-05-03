//-----------------------------------------------------------------------------
// FILE:	    ServicePlacement.cs
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
    /// Service container placement options.
    /// </summary>
    public class ServicePlacement : INormalizable
    {
        /// <summary>
        /// Service constraints formatted as <b>CONSTRAINT==VALUE</b> or <b>CONSTRAINT!=VALUE</b>.
        /// </summary>
        [JsonProperty(PropertyName = "Constraints", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public List<string> Constraints { get; set; }

        /// <summary>
        /// Service placement preferences.
        /// </summary>
        [JsonProperty(PropertyName = "Preferences", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public ServicePlacementPreferences Preferences { get; set; }

        /// <summary>
        /// Specifies the platforms where the service containers may be deployed or empty
        /// when there is no constraints.
        /// </summary>
        [JsonProperty(PropertyName = "Platforms", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public List<ServicePlatform> Platforms { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
            Constraints = Constraints ?? new List<string>();
            Preferences = Preferences ?? new ServicePlacementPreferences();
            Platforms   = Platforms ?? new List<ServicePlatform>();

            foreach (var item in Platforms)
            {
                item?.Normalize();
            }
        }
    }
}
