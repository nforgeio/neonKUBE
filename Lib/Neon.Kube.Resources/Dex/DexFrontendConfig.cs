//-----------------------------------------------------------------------------
// FILE:        DexFrontendConfig.cs
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
using System.Linq;
using System.Text;

using Newtonsoft.Json;

using YamlDotNet.Serialization;

namespace Neon.Kube.Resources.Dex
{
    /// <summary>
    /// Configuration for the Dex Frontend.
    /// </summary>
    public class DexFrontendConfig
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public DexFrontendConfig()
        {
        }

        /// <summary>
        /// Issuer name.
        /// </summary>
        [JsonProperty(PropertyName = "Issuer", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "issuer", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Issuer { get; set; }

        /// <summary>
        /// Logo url ref.
        /// </summary>
        [JsonProperty(PropertyName = "LogoUrl", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "logoUrl", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string LogoUrl { get; set; }

        /// <summary>
        /// Directory.
        /// information.
        /// </summary>
        [JsonProperty(PropertyName = "Dir", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "dir", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Dir { get; set; }

        /// <summary>
        /// Dex theme
        /// information.
        /// </summary>
        [JsonProperty(PropertyName = "Theme", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "theme", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Theme { get; set; }
    }
}
