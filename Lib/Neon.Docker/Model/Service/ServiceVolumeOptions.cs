//-----------------------------------------------------------------------------
// FILE:	    ServiceVolumeOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
    /// Volume options for volume service mounts.
    /// </summary>
    public class ServiceVolumeOptions : INormalizable
    {
        /// <summary>
        /// Enables populating the volume with data from the container target.
        /// </summary>
        [JsonProperty(PropertyName = "NoCopy", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "NoCopy", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool NoCopy { get; set; } = false;

        /// <summary>
        /// Volume driver labels.
        /// </summary>
        [JsonProperty(PropertyName = "Labels", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Labels", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public Dictionary<string, string> Labels { get; set; }

        /// <summary>
        /// Optionally specifies volume driver and options.
        /// </summary>
        [JsonProperty(PropertyName = "DriverConfig", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "DriverConfig", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public ServiceVolumeDriverConfig DriverConfig { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
            Labels       = Labels ?? new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            DriverConfig = DriverConfig ?? new ServiceVolumeDriverConfig();

            DriverConfig.Normalize();
        }
    }
}
