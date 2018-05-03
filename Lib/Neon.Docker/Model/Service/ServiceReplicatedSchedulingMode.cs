//-----------------------------------------------------------------------------
// FILE:	    ServiceReplicatedSchedulingMode.cs
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
    /// Replicated scheduling mode options.
    /// </summary>
    public class ServiceReplicatedSchedulingMode : INormalizable
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ServiceReplicatedSchedulingMode()
        {
        }

        /// <summary>
        /// The number of service replicas (tasks/containers).
        /// </summary>
        [JsonProperty(PropertyName = "Replicas", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public string Replicas { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
        }
    }
}
