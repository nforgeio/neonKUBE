//-----------------------------------------------------------------------------
// FILE:        Oauth2ProxyLoginUrlParameterRule.cs
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
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Security.Policy;
using System.Text;

using Newtonsoft.Json;

using YamlDotNet.Serialization;

namespace Neon.Kube.Oauth2Proxy
{
    /// <summary>
    /// Oauth2Proxy login url parameters model.
    /// </summary>
    public class Oauth2ProxyLoginUrlParameterRule
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public Oauth2ProxyLoginUrlParameterRule()
        {
        }

        /// <summary>
        /// A Value rule matches just this specific value.
        /// </summary>
        [JsonProperty(PropertyName = "Value", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "value", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Value { get; set; }

        /// <summary>
        /// A Pattern rule gives a regular expression that must be matched by some substring of the value.The expression is not automatically
        /// anchored to the start and end of the value, if you want to restrict the whole parameter value you must anchor it yourself with ^ and $.
        /// </summary>
        [JsonProperty(PropertyName = "Pattern", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "pattern", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Pattern { get; set; }
    }
}
