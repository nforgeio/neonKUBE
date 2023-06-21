//-----------------------------------------------------------------------------
// FILE:        DexStaticUser.cs
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
    /// Configuration for backend connectors.
    /// </summary>
    public class DexStaticUser
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public DexStaticUser()
        {
        }

        /// <summary>
        /// Client ID
        /// </summary>
        [JsonProperty(PropertyName = "Email", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "email", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Email { get; set; }

        /// <summary>
        /// bcrypt hash of the string "password": $(echo password | htpasswd -BinC 10 admin | cut -d: -f2)
        /// </summary>
        [JsonProperty(PropertyName = "Hash", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "hash", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Hash { get; set; }

        /// <summary>
        /// Client name.
        /// information.
        /// </summary>
        [JsonProperty(PropertyName = "Username", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "username", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Username { get; set; }

        /// <summary>
        /// Client secret.
        /// information.
        /// </summary>
        [JsonProperty(PropertyName = "UserId", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "userId", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string UserId { get; set; }
    }
}
