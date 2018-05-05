//-----------------------------------------------------------------------------
// FILE:	    ServiceNetwork.cs
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
    /// Service container spread placement settings.
    /// </summary>
    public class ServiceNetwork : INormalizable
    {
        /// <summary>
        /// Target network ID.
        /// </summary>
        [JsonProperty(PropertyName = "Target", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public string Target { get; set; }

        /// <summary>
        /// Network aliases (network IDs).
        /// </summary>
        [JsonProperty(PropertyName = "Aliases", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public List<string> Aliases { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
            Aliases = Aliases ?? new List<string>();
        }
    }
}
