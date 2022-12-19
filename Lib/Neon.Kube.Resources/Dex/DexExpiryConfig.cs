//-----------------------------------------------------------------------------
// FILE:	    DexExpiryConfig.cs
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

using Newtonsoft.Json;

using YamlDotNet.Serialization;

namespace Neon.Kube.Resources.Dex
{
    /// <summary>
    /// Configuration for token expiration.
    /// </summary>
    public class DexExpiryConfig
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public DexExpiryConfig()
        {
        }

        /// <summary>
        /// Device request expiration timeout.
        /// </summary>
        [JsonProperty(PropertyName = "DeviceRequests", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "deviceRequests", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string DeviceRequests { get; set; }

        /// <summary>
        /// Signing keys expiration timeout.
        /// </summary>
        [JsonProperty(PropertyName = "SigningKeys", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "signingKeys", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string SigningKeys { get; set; }

        /// <summary>
        /// ID Token expiration timeout.
        /// </summary>
        [JsonProperty(PropertyName = "IdTokens", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "idTokens", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string IdTokens { get; set; }

        /// <summary>
        /// Refresh token config.
        /// </summary>
        [JsonProperty(PropertyName = "RefreshTokens", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "refreshTokens", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public DexRefreshTokenConfig RefreshTokens { get; set; }
    }
}