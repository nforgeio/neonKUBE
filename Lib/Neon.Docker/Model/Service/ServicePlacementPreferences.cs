//-----------------------------------------------------------------------------
// FILE:	    ServicePlacementPreferences.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

namespace Neon.Docker
{
    /// <summary>
    /// Service container placement preferences.
    /// </summary>
    public class ServicePlacementPreferences : INormalizable
    {
        /// <summary>
        /// Spread swarm orchestrator options.
        /// </summary>
        [JsonProperty(PropertyName = "Spread", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Spread")]
        [DefaultValue(null)]
        public List<ServicePlacementSpreadSettings> Spread { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
            Spread = Spread ?? new List<ServicePlacementSpreadSettings>();

            foreach (var item in Spread)
            {
                item?.Normalize();
            }
        }
    }
}
