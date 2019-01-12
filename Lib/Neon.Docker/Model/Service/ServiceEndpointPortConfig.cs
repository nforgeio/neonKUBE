//-----------------------------------------------------------------------------
// FILE:	    ServiceEndpointPortConfig.cs
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
    /// Service port publication specification.
    /// </summary>
    public class ServiceEndpointPortConfig : INormalizable
    {
        /// <summary>
        /// The port name.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public string Name { get; set; }

        /// <summary>
        /// Specifies the port protocol.
        /// </summary>
        [JsonProperty(PropertyName = "Protocol", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(default(ServicePortProtocol))]
        public ServicePortProtocol Protocol { get; set; }

        /// <summary>
        /// Specifies the internal port where external traffic
        /// will be forwarded within the service containers.
        /// </summary>
        [JsonProperty(PropertyName = "TargetPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public int TargetPort { get; set; }

        /// <summary>
        /// Specifies the port where the service receives traffic on the
        /// external network.
        /// </summary>
        [JsonProperty(PropertyName = "PublishedPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public int PublishedPort { get; set; }

        /// <summary>
        /// Specifies the port mode.
        /// </summary>
        [JsonProperty(PropertyName = "PublishMode", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(default(ServicePortMode))]
        public ServicePortMode PublishMode { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
        }
    }
}
