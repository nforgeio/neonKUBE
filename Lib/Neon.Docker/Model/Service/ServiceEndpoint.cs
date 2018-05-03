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
    /// Describes a service's current endpoints.
    /// </summary>
    public class ServiceEndpoint : INormalizable
    {
        /// <summary>
        /// Service endpoint specification.
        /// </summary>
        [JsonProperty(PropertyName = "Spec", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public ServiceEndpointSpec Spec { get; set; }

        /// <summary>
        /// Service port specifications.
        /// </summary>
        [JsonProperty(PropertyName = "Ports", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public List<ServiceEndpointPortConfig> Ports { get; set; }

        /// <summary>
        /// Service virtual IP address assigments.
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
