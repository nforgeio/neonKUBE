//-----------------------------------------------------------------------------
// FILE:	    DexRefreshTokenConfig.cs
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

namespace Neon.Kube
{
    /// <summary>
    /// Configuration for the HTTP endpoints.
    /// </summary>
    public class DexRefreshTokenConfig
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public DexRefreshTokenConfig()
        {
        }

        /// <summary>
        /// Token reuse interval.
        /// </summary>
        [JsonProperty(PropertyName = "ReuseInterval", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "reuseInterval", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string ReuseInterval { get; set; }

        /// <summary>
        /// Duration token valid if not used.
        /// </summary>
        [JsonProperty(PropertyName = "ValidIfNotUsedFor", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "validIfNotUsedFor", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string ValidIfNotUsedFor { get; set; }

        /// <summary>
        /// Absolute refresh token lifetime.
        /// </summary>
        [JsonProperty(PropertyName = "AbsoluteLifetime", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "absoluteLifetime", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string AbsoluteLifetime { get; set; }
    }
}