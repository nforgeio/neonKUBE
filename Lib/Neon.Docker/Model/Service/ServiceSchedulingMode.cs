//-----------------------------------------------------------------------------
// FILE:	    ServiceSchedulingMode.cs
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
    /// Orchestration scheduling mode for the service.
    /// </summary>
    public class ServiceSchedulingMode : INormalizable
    {
        /// <summary>
        /// Replicated scheduling mode options.
        /// </summary>
        [JsonProperty(PropertyName = "Replicated", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public ServiceReplicatedSchedulingMode Replicated { get; set; }

        /// <summary>
        /// Global scheduling mode options.
        /// </summary>
        [JsonProperty(PropertyName = "Global", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public ServiceGlobalSchedulingMode Global { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
            Replicated?.Normalize();
            Global?.Normalize();
        }
    }
}
