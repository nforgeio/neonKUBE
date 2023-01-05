//-----------------------------------------------------------------------------
// FILE:	    Oauth2ProxyHeader.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.IO;
using System.Linq;
using System.Text;
using DNS.Protocol;
using Newtonsoft.Json;

using YamlDotNet.Serialization;

namespace Neon.Kube.Oauth2Proxy
{
    /// <summary>
    /// Oauth2Proxy header model.
    /// </summary>
    public class Oauth2ProxyHeader
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public Oauth2ProxyHeader()
        {
        }

        /// <summary>
        /// The header name to be used for this set of values. Names should be unique within a list of Headers.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Always)]
        [YamlMember(Alias = "name", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Name { get; set; }

        /// <summary>
        /// Determines whether any values for this header
        /// should be preserved for the request to the upstream server.
        /// This option only applies to injected request headers.
        /// Defaults to false (headers that match this header will be stripped).
        /// </summary>
        [JsonProperty(PropertyName = "PreserveRequestValue", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "preserveRequestValue", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool PreserveRequestValue { get; set; } = false;

        /// <summary>
        /// Contains the desired values for this header
        /// </summary>
        [JsonProperty(PropertyName = "Values", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "values", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<Oauth2ProxyHeaderValue> Values { get; set; }
    }
}