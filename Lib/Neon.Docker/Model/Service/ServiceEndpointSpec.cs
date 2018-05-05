//-----------------------------------------------------------------------------
// FILE:	    ServiceEndpointSpec.cs
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
    /// Service endpoint specification.
    /// </summary>
    public class ServiceEndpointSpec : INormalizable
    {
        /// <summary>
        /// Specifies how the Docker swarm will load balance traffic to the service tasks.
        /// </summary>
        [JsonProperty(PropertyName = "Mode", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(default(ServiceEndpointMode))]
        public ServiceEndpointMode Mode { get; set; }

        /// <summary>
        /// Details the network ports exposed by the service tasks.
        /// </summary>
        [JsonProperty(PropertyName = "Ports", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public List<ServiceEndpointPortConfig> Ports { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
            Ports = Ports ?? new List<ServiceEndpointPortConfig>();

            foreach (var item in Ports)
            {
                item?.Normalize();
            }
        }
    }
}
