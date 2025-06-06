//-----------------------------------------------------------------------------
// FILE:        DexWebConfig.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
using System.Linq;
using System.Text;

using Newtonsoft.Json;
using YamlDotNet.Serialization;

namespace Neon.Kube.Resources.Dex
{
    /// <summary>
    /// Configuration for the http server.
    /// </summary>
    public class DexWebConfig
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public DexWebConfig()
        {
        }

        /// <summary>
        /// Http Endpoint.
        /// </summary>
        [JsonProperty(PropertyName = "Http", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "http", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Http { get; set; }

        /// <summary>
        /// Https Endpoint.
        /// </summary>
        [JsonProperty(PropertyName = "Https", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "https", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Https { get; set; }

        /// <summary>
        /// Reference to TLS certificate file.
        /// information.
        /// </summary>
        [JsonProperty(PropertyName = "TlsCert", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "tlsCert", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string TlsCert { get; set; }

        /// <summary>
        /// Reference to TLS certificate key file.
        /// information.
        /// </summary>
        [JsonProperty(PropertyName = "TlsKey", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "tlsKey", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string TlsKey { get; set; }

        /// <summary>
        /// Configuration for dex appearance.
        /// information.
        /// </summary>
        [JsonProperty(PropertyName = "Frontend", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "frontend", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public DexFrontendConfig Frontend { get; set; }
    }
}
