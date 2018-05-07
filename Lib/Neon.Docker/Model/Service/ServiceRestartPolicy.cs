//-----------------------------------------------------------------------------
// FILE:	    ServiceRestartPolicy.cs
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
    /// Specifies the restart policy for service containers.
    /// </summary>
    public class ServiceRestartPolicy : INormalizable
    {
        /// <summary>
        /// Specifies the condition under which a service container should be restarted.
        /// </summary>
        [JsonProperty(PropertyName = "Condition", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(default(ServiceRestartCondition))]
        public ServiceRestartCondition Condition { get; set; }

        /// <summary>
        /// Deplay between restart attempts (nanoseconds).
        /// </summary>
        [JsonProperty(PropertyName = "Delay", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public long? Delay { get; set; }

        /// <summary>
        /// Specifies the maximum number of container restart attempts before giving up.
        /// </summary>
        [JsonProperty(PropertyName = "MaxAttempts", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public long? MaxAttempts { get; set; }

        /// <summary>
        /// Specifies the window of time during which the restart policy will be 
        /// enavluated (nanoseconds).
        /// </summary>
        [JsonProperty(PropertyName = "Window", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public long? Window { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
        }
    }
}
