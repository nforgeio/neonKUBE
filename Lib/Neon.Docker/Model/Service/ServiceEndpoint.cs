//-----------------------------------------------------------------------------
// FILE:	    ServiceEndpoint.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

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
        [YamlMember(Alias = "Spec", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public ServiceEndpointSpec Spec { get; set; }

        /// <summary>
        /// Details the network ports actually exposed by the service tasks.
        /// </summary>
        [JsonProperty(PropertyName = "Ports", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Ports", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<ServiceEndpointPortConfig> Ports { get; set; }

        /// <summary>
        /// Lists the virtual IP addresses assigned to this service on the 
        /// attached networks.
        /// </summary>
        [JsonProperty(PropertyName = "VirtualIPs", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "VirtualIPs", ApplyNamingConventions = false)]
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
