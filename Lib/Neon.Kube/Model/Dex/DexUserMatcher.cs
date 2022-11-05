//-----------------------------------------------------------------------------
// FILE:	    DexUserMatcher.cs
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

namespace Neon.Kube
{
    /// <summary>
    /// Dex User Matcher.
    /// </summary>
    public class DexUserMatcher
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public DexUserMatcher()
        {
        }

        /// <summary>
        /// Group Attribute
        /// </summary>
        [JsonProperty(PropertyName = "GroupAttr", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "groupAttr", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string GroupAttr { get; set; }

        /// <summary>
        /// User Attribute
        /// </summary>
        [JsonProperty(PropertyName = "UserAttr", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "userAttr", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string UserAttr { get; set; }
    }
}