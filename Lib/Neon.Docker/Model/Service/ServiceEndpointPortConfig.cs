//-----------------------------------------------------------------------------
// FILE:	    ServiceEndpointPortConfig.cs
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
    /// Service port publication specification.
    /// </summary>
    public class ServiceEndpointPortConfig : INormalizable
    {
        /// <summary>
        /// The port name.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Name", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Name { get; set; }

        /// <summary>
        /// Specifies the port protocol.
        /// </summary>
        [JsonProperty(PropertyName = "Protocol", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Protocol", ApplyNamingConventions = false)]
        [DefaultValue(default(ServicePortProtocol))]
        public ServicePortProtocol Protocol { get; set; }

        /// <summary>
        /// Specifies the internal port where external traffic
        /// will be forwarded within the service containers.
        /// </summary>
        [JsonProperty(PropertyName = "TargetPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "TargetPort", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public int TargetPort { get; set; }

        /// <summary>
        /// Specifies the port where the service receives traffic on the
        /// external network.
        /// </summary>
        [JsonProperty(PropertyName = "PublishedPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "PublishedPort", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public int PublishedPort { get; set; }

        /// <summary>
        /// Specifies the port mode.
        /// </summary>
        [JsonProperty(PropertyName = "PublishMode", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "PublishMode", ApplyNamingConventions = false)]
        [DefaultValue(default(ServicePortMode))]
        public ServicePortMode PublishMode { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
        }
    }
}
