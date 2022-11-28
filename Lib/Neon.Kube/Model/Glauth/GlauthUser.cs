//-----------------------------------------------------------------------------
// FILE:	    GlauthUser.cs
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
using Neon.Cryptography;
using Newtonsoft.Json;

using YamlDotNet.Serialization;

namespace Neon.Kube
{
    /// <summary>
    /// Defines a Glauth User.
    /// </summary>
    public class GlauthUser
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public GlauthUser()
        {
        }

        /// <summary>
        /// User Name
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "name", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Name { get; set; }

        /// <summary>
        /// User UID Number
        /// </summary>
        [JsonProperty(PropertyName = "UidNumber", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "uidNumber", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public int? UidNumber { get; set; }

        /// <summary>
        /// User Primary Group
        /// </summary>
        [JsonProperty(PropertyName = "PrimaryGroup", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "primaryGroup", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public int? PrimaryGroup { get; set; }

        /// <summary>
        /// Other Groups the user belongs to.
        /// </summary>
        [JsonProperty(PropertyName = "OtherGroups", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "otherGroups", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<int> OtherGroups { get; set; }

        /// <summary>
        /// User Given Name
        /// </summary>
        [JsonProperty(PropertyName = "GivenName", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "givenName", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string GivenName { get; set; }

        /// <summary>
        /// SN
        /// </summary>
        [JsonProperty(PropertyName = "Sn", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "sn", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Sn { get; set; }

        /// <summary>
        /// User Email address.
        /// </summary>
        [JsonProperty(PropertyName = "Mail", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "mail", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Mail { get; set; }

        /// <summary>
        /// Whether the user is disabled.
        /// </summary>
        [JsonProperty(PropertyName = "Disabled", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "disabled", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public bool? Disabled { get; set; }

        /// <summary>
        /// Password represented as SHA256
        /// </summary>
        [JsonProperty(PropertyName = "PassSha256", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "passSha256", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string PassSha256 { get; set; }

        /// <summary>
        /// String password
        /// </summary>
        [JsonProperty(PropertyName = "Password", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "password", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Password { get; set; }

        /// <summary>
        /// String password
        /// </summary>
        [JsonProperty(PropertyName = "Capabilities", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "capabilities", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<GlauthUserCapability> Capabilities { get; set; }
    }
}