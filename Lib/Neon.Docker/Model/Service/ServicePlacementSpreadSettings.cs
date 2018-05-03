//-----------------------------------------------------------------------------
// FILE:	    ServicePlacementSpreadSettings.cs
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
    public class ServicePlacementSpreadSettings : INormalizable
    {
        /// <summary>
        /// Label descriptor, such as: engine.labels.az
        /// </summary>
        [JsonProperty(PropertyName = "SpreadDescriptor", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public string SpreadDescriptor { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
        }
    }
}
