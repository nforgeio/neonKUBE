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
        [JsonProperty(PropertyName = "Name", Required = Required.Always)]
        public string Name { get; set; }

        /// <summary>
        /// Specifies the port where the service receives traffic on the
        /// external network.
        /// </summary>
        [JsonProperty(PropertyName = "Published", Required = Required.Always)]
        public int Published { get; set; }

        /// <summary>
        /// Specifies the internal port where external traffic
        /// will be forwarded within the service containers.
        /// </summary>
        [JsonProperty(PropertyName = "Target", Required = Required.Always)]
        public int Target { get; set; }

        /// <summary>
        /// Specifies the port mode.
        /// </summary>
        [JsonProperty(PropertyName = "Mode", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(ServicePortMode.Ingress)]
        public ServicePortMode Mode { get; set; } = ServicePortMode.Ingress;

        /// <summary>
        /// Specifies the port protocol.
        /// </summary>
        [JsonProperty(PropertyName = "Protocol", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(ServicePortProtocol.Tcp)]
        public ServicePortProtocol Protocol { get; set; } = ServicePortProtocol.Tcp;

        /// <inheritdoc/>
        public void Normalize()
        {
        }
    }
}
