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
    /// Service endpoint and load balancer settings.
    /// </summary>
    public class ServiceEndpointSpec : INormalizable
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ServiceEndpointSpec()
        {
        }

        /// <summary>
        /// Specifies how the Docker swarm will load balance traffic to the service tasks.
        /// </summary>
        [JsonProperty(PropertyName = "Mode", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(ServiceEndpointMode.Vip)]
        public ServiceEndpointMode Mode { get; set; } = ServiceEndpointMode.Vip;

        /// <summary>
        /// Details the network ports exposed by the service tasks.
        /// </summary>
        [JsonProperty(PropertyName = "Ports", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public List<ServicePublishPort> Ports { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
            Ports = Ports ?? new List<ServicePublishPort>();

            foreach (var item in Ports)
            {
                item?.Normalize();
            }
        }
    }
}
