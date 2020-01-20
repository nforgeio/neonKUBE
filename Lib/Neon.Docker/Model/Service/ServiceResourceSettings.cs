//-----------------------------------------------------------------------------
// FILE:	    ServiceResourceSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
    /// Describes system resource consumption settings.
    /// </summary>
    public class ServiceResourceSettings : INormalizable
    {
        /// <summary>
        /// CPU utilization expressed as billionths of a CPU.
        /// </summary>
        [JsonProperty(PropertyName = "NanoCPUs", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "NanoCPUs", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public long? NanoCPUs { get; set; }

        /// <summary>
        /// Memory utilization as bytes.
        /// </summary>
        [JsonProperty(PropertyName = "MemoryBytes", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "MemoryBytes", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public long? MemoryBytes { get; set; }

        /// <summary>
        /// User-defined generic resource settings.
        /// </summary>
        [JsonProperty(PropertyName = "GenericResources", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "GenericResources", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<ServiceGenericResources> GenericResources { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
            GenericResources = GenericResources ?? new List<ServiceGenericResources>();

            foreach (var item in GenericResources)
            {
                item?.Normalize();
            }
        }
    }
}
