//-----------------------------------------------------------------------------
// FILE:	    ServicePlacement.cs
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
    /// Service container placement options.
    /// </summary>
    public class ServicePlacement : INormalizable
    {
        /// <summary>
        /// Service constraints formatted as <b>CONSTRAINT==VALUE</b> or <b>CONSTRAINT!=VALUE</b>.
        /// </summary>
        [JsonProperty(PropertyName = "Constraints", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Constraints", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<string> Constraints { get; set; }

        /// <summary>
        /// Service placement preferences.
        /// </summary>
        [JsonProperty(PropertyName = "Preferences", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Preferences", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public ServicePlacementPreferences Preferences { get; set; }

        /// <summary>
        /// Specifies the platforms where the service containers may be deployed or empty
        /// when there is no constraints.
        /// </summary>
        [JsonProperty(PropertyName = "Platforms", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Platforms", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<ServicePlatform> Platforms { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
            Constraints = Constraints ?? new List<string>();
            Preferences = Preferences ?? new ServicePlacementPreferences();
            Platforms   = Platforms ?? new List<ServicePlatform>();

            foreach (var item in Platforms)
            {
                item?.Normalize();
            }
        }
    }
}
