//-----------------------------------------------------------------------------
// FILE:	    DexOauth2Config.cs
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
using System.Linq;
using System.Text;

using Neon.Kube.Resources;

using Newtonsoft.Json;
using YamlDotNet.Serialization;

namespace Neon.Kube.Resources.Dex
{
    /// <summary>
    /// Dex Oauth2 configuration model.
    /// </summary>
    public class DexOauth2Config
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public DexOauth2Config()
        {
        }

        /// <summary>
        /// use ["code", "token", "id_token"] to enable implicit flow for web-only clients.
        /// </summary>
        [JsonProperty(PropertyName = "ResponseTypes", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "responseTypes", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        [System.Text.Json.Serialization.JsonConverter(typeof(JsonCollectionItemConverter<Oauth2ResponseType, System.Text.Json.Serialization.JsonStringEnumMemberConverter>))]

        public IEnumerable<Oauth2ResponseType> ResponseTypes { get; set; }

        /// <summary>
        /// By default, Dex will ask for approval to share data with application
        /// (approval for sharing data from connected IdP to Dex is separate process on IdP)
        /// </summary>
        [JsonProperty(PropertyName = "SkipApprovalScreen", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "skipApprovalScreen", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public bool? SkipApprovalScreen { get; set; }

        /// <summary>
        /// If only one authentication method is enabled, the default behavior is to
        /// go directly to it. For connected IdPs, this redirects the browser away
        /// from application to upstream provider such as the Google login page
        /// </summary>
        [JsonProperty(PropertyName = "AlwaysShowLoginScreen", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "alwaysShowLoginScreen", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public bool? AlwaysShowLoginScreen { get; set; }

        /// <summary>
        /// Optionally use a specific connector for password grants.
        /// </summary>
        [JsonProperty(PropertyName = "PasswordConnector", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "passwordConnector", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string PasswordConnector { get; set; }
    }
}
