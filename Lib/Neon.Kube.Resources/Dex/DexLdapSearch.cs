//-----------------------------------------------------------------------------
// FILE:        DexLdapSearch.cs
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
    public class DexLdapSearch
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public DexLdapSearch()
        {
        }

        /// <summary>
        /// Base search DN
        /// </summary>
        [JsonProperty(PropertyName = "BaseDN", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "baseDN", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public string BaseDN { get; set; }

        /// <summary>
        /// User search filter
        /// </summary>
        [JsonProperty(PropertyName = "Filter", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "filter", SerializeAs = typeof(int), ScalarStyle = YamlDotNet.Core.ScalarStyle.DoubleQuoted, ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public string Filter { get; set; }

        /// <summary>
        /// The username attribute name on the LDAP server.
        /// </summary>
        [JsonProperty(PropertyName = "Username", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "username", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public string Username { get; set; }

        /// <summary>
        /// The ID attribute name on the LDAP server.
        /// </summary>
        [JsonProperty(PropertyName = "IdAttr", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "idAttr", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public string IdAttr { get; set; }

        /// <summary>
        /// The Email attribute name on the LDAP server.
        /// </summary>
        [JsonProperty(PropertyName = "EmailAttr", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "emailAttr", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public string EmailAttr { get; set; }

        /// <summary>
        /// The name attribute name on the LDAP server.
        /// </summary>
        [JsonProperty(PropertyName = "NameAttr", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "nameAttr", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public string NameAttr { get; set; }

        /// <summary>
        /// User matching settings.
        /// </summary>
        [JsonProperty(PropertyName = "UserMatchers", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "userMatchers", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public List<DexUserMatcher> UserMatchers { get; set; }
    }
}
