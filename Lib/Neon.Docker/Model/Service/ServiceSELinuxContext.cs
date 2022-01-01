//-----------------------------------------------------------------------------
// FILE:	    ServiceSELinuxContext.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Runtime.Serialization;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

namespace Neon.Docker
{
    /// <summary>
    /// SELinux labels for the container.
    /// </summary>
    public class ServiceSELinuxContext : INormalizable
    {
        /// <summary>
        /// Disable SELinux.
        /// </summary>
        [JsonProperty(PropertyName = "Disable", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Disable", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool Disable { get; set; }

        /// <summary>
        /// SELinux user label.
        /// </summary>
        [JsonProperty(PropertyName = "User", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "User", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string User { get; set; }

        /// <summary>
        /// SELinux role label.
        /// </summary>
        [JsonProperty(PropertyName = "Role", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Role", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Role { get; set; }

        /// <summary>
        /// SELinux type label.
        /// </summary>
        [JsonProperty(PropertyName = "Type", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Type", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Type { get; set; }

        /// <summary>
        /// SELinux level label.
        /// </summary>
        [JsonProperty(PropertyName = "Level", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Level", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Level { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
        }
    }
}
