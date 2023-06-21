//-----------------------------------------------------------------------------
// FILE:        GlauthUserCapability.cs
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

namespace Neon.Kube.Glauth
{
    /// <summary>
    /// Defines a Glauth user group.
    /// </summary>
    public class GlauthUserCapability
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public GlauthUserCapability()
        {
        }

        /// <summary>
        /// Group Name
        /// </summary>
        [JsonProperty(PropertyName = "Action", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "action", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Action { get; set; }

        /// <summary>
        /// Group ID Number
        /// </summary>
        [JsonProperty(PropertyName = "Object", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "object", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Object { get; set; }
    }
}
