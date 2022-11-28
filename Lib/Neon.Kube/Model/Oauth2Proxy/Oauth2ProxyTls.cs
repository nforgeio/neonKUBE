//-----------------------------------------------------------------------------
// FILE:	    Oauth2ProxyTls.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using System.Security.Policy;
using System.Text;
using DNS.Protocol;
using Newtonsoft.Json;

using YamlDotNet.Serialization;

namespace Neon.Kube
{
    /// <summary>
    /// Oauth2Proxy TLS model.
    /// </summary>
    public class Oauth2ProxyTls
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public Oauth2ProxyTls()
        {
        }

        /// <summary>
        /// TLS key data to use. Typically this will come from a file.
        /// </summary>
        [JsonProperty(PropertyName = "Key", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "Key", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public Oauth2ProxySecretSource Key { get; set; }

        /// <summary>
        /// TLS key data to use. Typically this will come from a file.
        /// </summary>
        [JsonProperty(PropertyName = "Cert", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "Cert", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public Oauth2ProxySecretSource Cert { get; set; }

        /// <summary>
        /// The minimal TLS version that is acceptable.
        /// E.g. Set to "TLS1.3" to select TLS version 1.3
        /// </summary>
        [JsonProperty(PropertyName = "MinVersion", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "MinVersion", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string MinVersion { get; set; }

        /// <summary>
        /// A list of TLS cipher suites that are allowed.
        /// E.g.:
        /// - TLS_RSA_WITH_RC4_128_SHA
        /// - TLS_RSA_WITH_AES_256_GCM_SHA384
        /// If not specified, the default Go safe cipher list is used.
        /// List of valid cipher suites can be found in the crypto/tls documentation.
        /// </summary>
        [JsonProperty(PropertyName = "TLS", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "TLS", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<string> CipherSuites { get; set; }
    }
}