//-----------------------------------------------------------------------------
// FILE:	    ServiceEndpoint.cs
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
    /// Service endpoint and network settings.
    /// </summary>
    public class ServiceEndpoint : INormalizable
    {
        /// <summary>
        /// Specifies the service endpoint mode and ports to be exposed.
        /// </summary>
        [JsonProperty(PropertyName = "Spec", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public ServiceEndpointSpec Spec { get; set; }

        /// <summary>
        /// Details the network ports actually exposed by the service tasks.
        /// </summary>
        [JsonProperty(PropertyName = "Ports", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public List<ServiceEndpointPortConfig> Ports { get; set; }

        /// <summary>
        /// Lists the virtual IP addresses assigned to this service on the 
        /// attached networks.
        /// </summary>
        [JsonProperty(PropertyName = "VirtualIPs", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public List<ServiceVirtualIP> VirtualIPs { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
            Spec       = Spec ?? new ServiceEndpointSpec();
            Ports      = Ports ?? new List<ServiceEndpointPortConfig>();
            VirtualIPs = VirtualIPs ?? new List<ServiceVirtualIP>();

            Spec?.Normalize();

            foreach (var item in Ports)
            {
                item?.Normalize();
            }

            foreach (var item in VirtualIPs)
            {
                item?.Normalize();
            }
        }
    }
}
