//-----------------------------------------------------------------------------
// FILE:	    Oauth2ProxyUpstreamConfig.cs
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

using Newtonsoft.Json;
using YamlDotNet.Serialization;

namespace Neon.Kube.Oauth2Proxy
{
    /// <summary>
    /// Oauth2Proxy header model.
    /// </summary>
    public class Oauth2ProxyUpstreamConfig
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public Oauth2ProxyUpstreamConfig()
        {
        }

        /// <summary>
        /// Will pass the raw url path to upstream allowing for url's
        /// like: "/%2F/" which would otherwise be redirected to "/"
        /// </summary>
        [JsonProperty(PropertyName = "ProxyRawPath", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "proxyRawPath", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public bool? ProxyRawPath { get; set; } = null;

        /// <summary>
        /// Represents the configuration for the upstream servers. Requests will be proxied to this upstream if the path matches the request path.
        /// </summary>
        [JsonProperty(PropertyName = "Upstreams", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "upstreams", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<Oauth2ProxyUpstream> Upstreams { get; set; }
    }
}