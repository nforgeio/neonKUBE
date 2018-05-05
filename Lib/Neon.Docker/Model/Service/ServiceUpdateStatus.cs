//-----------------------------------------------------------------------------
// FILE:	    ServiceUpdateStatus.cs
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
    /// Describes the virtual IP address assigned to the service on
    /// a specific attached network.
    /// </summary>
    public class ServiceUpdateStatus : INormalizable
    {
        /// <summary>
        /// Indicates the saervice updating state.
        /// </summary>
        [JsonProperty(PropertyName = "State", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(default(ServiceUpdateState))]
        public ServiceUpdateState State { get; set; }

        /// <summary>
        /// Indicates when the service update was started.
        /// </summary>
        [JsonProperty(PropertyName = "StartedAt", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public string StartedAt { get; set; }

        /// <summary>
        /// Indicates when the service update was completed.
        /// </summary>
        [JsonProperty(PropertyName = "CompletedAt", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public string CompletedAt { get; set; }

        /// <summary>
        /// A textual message describing the update.
        /// </summary>
        [JsonProperty(PropertyName = "Message", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public string Message { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
        }
    }
}
