//-----------------------------------------------------------------------------
// FILE:        Oauth2ProxyLoginUrlParameters.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text;

using Newtonsoft.Json;

using YamlDotNet.Serialization;

namespace Neon.Kube.Oauth2Proxy
{
    /// <summary>
    /// Oauth2Proxy login url parameters model.
    /// </summary>
    public class Oauth2ProxyLoginUrlParameters
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public Oauth2ProxyLoginUrlParameters()
        {
        }

        /// <summary>
        /// Specifies the name of the query parameter.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Always)]
        [YamlMember(Alias = "name", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Name { get; set; }

        /// <summary>
        /// Specifies a default value or values that will be passed to the IdP if not overridden.
        /// </summary>
        [JsonProperty(PropertyName = "Default", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "default", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<string> Default { get; set; }

        /// <summary>
        /// Specifies rules about how the default (if any) may be overridden via the query string to /oauth2/start. Only
        /// values that match one or more of the allow rules will be forwarded to the IdP.
        /// </summary>
        [JsonProperty(PropertyName = "Allow", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "allow", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<Oauth2ProxyLoginUrlParameterRule> Allow { get; set; }
    }
}
