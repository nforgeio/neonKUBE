//-----------------------------------------------------------------------------
// FILE:	    Oauth2ProxySecretSource.cs
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
using System.Text;
using DNS.Protocol;
using Newtonsoft.Json;

using YamlDotNet.Serialization;

namespace Neon.Kube
{
    /// <summary>
    /// Oauth2Proxy header model.
    /// </summary>
    public class Oauth2ProxySecretSource
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public Oauth2ProxySecretSource()
        {
        }

        /// <summary>
        /// A base64 encoded string value.
        /// </summary>
        [JsonProperty(PropertyName = "Value", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "value", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Value { get; set; }

        /// <summary>
        /// Expects the name of an environment variable.
        /// </summary>
        [JsonProperty(PropertyName = "FromEnv", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "fromEnv", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public string FromEnv { get; set; }

        /// <summary>
        /// Expects a path to a file containing the secret value.
        /// </summary>
        [JsonProperty(PropertyName = "FromFile", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "fromFile", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string FromFile { get; set; }
    }
}