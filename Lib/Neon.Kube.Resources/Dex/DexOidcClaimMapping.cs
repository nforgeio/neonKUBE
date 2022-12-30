//-----------------------------------------------------------------------------
// FILE:	    DexOidcClaimMapping.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2021 by NEONFORGE LLC.  All rights reserved.
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
    /// Configuration for backend connectors.
    /// </summary>
    public class DexOidcClaimMapping
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public DexOidcClaimMapping()
        {
        }

        /// <summary>
        /// Configurable key which contains the preferred username claims.
        /// </summary>
        [JsonProperty(PropertyName = "PreferredUsernameKey", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "preferred_username", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string PreferredUsernameKey { get; set; }

        /// <summary>
        /// Configurable key which contains the email claims.
        /// </summary>
        [JsonProperty(PropertyName = "EmailKey", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "email", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string EmailKey { get; set; }

        /// <summary>
        /// Configurable key which contains the groups claims.
        /// </summary>
        [JsonProperty(PropertyName = "GroupsKey", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "groups", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string GroupsKey { get; set; }
    }
}