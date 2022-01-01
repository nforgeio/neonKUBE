//-----------------------------------------------------------------------------
// FILE:	    ServiceEndpointSpec.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
    /// Service endpoint specification.
    /// </summary>
    public class ServiceEndpointSpec : INormalizable
    {
        /// <summary>
        /// Specifies how the Docker swarm will load balance traffic to the service tasks.
        /// </summary>
        [JsonProperty(PropertyName = "Mode", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Mode", ApplyNamingConventions = false)]
        [DefaultValue(default(ServiceEndpointMode))]
        public ServiceEndpointMode Mode { get; set; }

        /// <summary>
        /// Details the network ports exposed by the service tasks.
        /// </summary>
        [JsonProperty(PropertyName = "Ports", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Ports", ApplyNamingConventions = false)]
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
