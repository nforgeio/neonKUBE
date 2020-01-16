//-----------------------------------------------------------------------------
// FILE:	    ServiceVirtualIP.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
    /// Describes the virtual IP address assigned to the service on
    /// a specific attached network.
    /// </summary>
    public class ServiceVirtualIP : INormalizable
    {
        /// <summary>
        /// Specifies the attached network ID.
        /// </summary>
        [JsonProperty(PropertyName = "NetworkID", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "NetworkID", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string NetworkID { get; set; }

        /// <summary>
        /// Specifies assigned IP address.
        /// </summary>
        [JsonProperty(PropertyName = "Addr", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Addr", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Addr { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
        }
    }
}
