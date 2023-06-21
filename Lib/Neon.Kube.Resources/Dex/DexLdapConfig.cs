//-----------------------------------------------------------------------------
// FILE:        DexLdapConfig.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2021 by NEONFORGE LLC.  All rights reserved.
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
    public class DexLdapConfig
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public DexLdapConfig()
        {
        }

        /// <summary>
        /// Ldap Host.
        /// </summary>
        [JsonProperty(PropertyName = "Host", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "host", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Host { get; set; }

        /// <summary>
        /// Whether to use SSL.
        /// </summary>
        [JsonProperty(PropertyName = "InsecureNoSSL", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "insecureNoSSL", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public bool? InsecureNoSSL { get; set; }

        /// <summary>
        /// Whether to use skip verification for insecure requests.
        /// </summary>
        [JsonProperty(PropertyName = "InsecureSkipVerify", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "insecureSkipVerify", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public bool? InsecureSkipVerify { get; set; }

        /// <summary>
        /// Root CA reference.
        /// information.
        /// </summary>
        [JsonProperty(PropertyName = "RootCA", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "rootCA", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string RootCA { get; set; }

        /// <summary>
        /// Bind DN
        /// information.
        /// </summary>
        [JsonProperty(PropertyName = "BindDN", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "bindDN", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string BindDN { get; set; }

        /// <summary>
        /// Bind Password.
        /// information.
        /// </summary>
        [JsonProperty(PropertyName = "BindPW", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "bindPW", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string BindPW { get; set; }

        /// <summary>
        /// Username prompt.
        /// information.
        /// </summary>
        [JsonProperty(PropertyName = "UsernamePrompt", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "usernamePrompt", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string UsernamePrompt { get; set; }

        /// <summary>
        /// User search config.
        /// information.
        /// </summary>
        [JsonProperty(PropertyName = "UserSearch", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "userSearch", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public DexLdapSearch UserSearch { get; set; }

        /// <summary>
        /// Group search config.
        /// information.
        /// </summary>
        [JsonProperty(PropertyName = "GroupSearch", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "groupSearch", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public DexLdapSearch GroupSearch { get; set; }
    }
}
