//-----------------------------------------------------------------------------
// FILE:	    ServiceMount.cs
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
    /// Service mount specification.
    /// </summary>
    public class ServiceMount : INormalizable
    {
        /// <summary>
        /// Specifies where the mount will appear within the service containers. 
        /// </summary>
        [JsonProperty(PropertyName = "Target", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Target", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Target { get; set; }

        /// <summary>
        /// Specifies the external mount source
        /// </summary>
        [JsonProperty(PropertyName = "Source", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Source", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Source { get; set; }

        /// <summary>
        /// The mount type.
        /// </summary>
        [JsonProperty(PropertyName = "Type", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Type", ApplyNamingConventions = false)]
        [DefaultValue(default(ServiceMountType))]
        public ServiceMountType Type { get; set; }

        /// <summary>
        /// Specifies whether the mount is to be read-only within the service containers.
        /// </summary>
        [JsonProperty(PropertyName = "ReadOnly", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "ReadOnly", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool ReadOnly { get; set; }

        /// <summary>
        /// Specifies the mount consistency.
        /// </summary>
        [JsonProperty(PropertyName = "Consistency", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Consistency", ApplyNamingConventions = false)]
        [DefaultValue(default(ServiceMountConsistency))]
        public ServiceMountConsistency Consistency { get; set; }

        /// <summary>
        /// Specifies the bind propagation mode.
        /// </summary>
        [JsonProperty(PropertyName = "BindOptions", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "BindOptions", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public ServiceBindOptions BindOptions { get; set; }

        /// <summary>
        /// Optionally specifies volume mount configuration options.
        /// </summary>
        [JsonProperty(PropertyName = "VolumeOptions", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "VolumeOptions", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public ServiceVolumeOptions VolumeOptions { get; set; }

        /// <summary>
        /// Optionally specifies Tempfs mount configuration options.
        /// </summary>
        [JsonProperty(PropertyName = "TmpfsOptions", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "TmpfsOptions", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public ServiceTmpfsOptions TmpfsOptions { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
            switch (Type)
            {
                case ServiceMountType.Bind:

                    BindOptions = BindOptions ?? new ServiceBindOptions();
                    BindOptions.Normalize();
                    break;

                case ServiceMountType.Tmpfs:

                    TmpfsOptions = TmpfsOptions ?? new ServiceTmpfsOptions();
                    TmpfsOptions.Normalize();
                    break;

                case ServiceMountType.Volume:

                    VolumeOptions = VolumeOptions ?? new ServiceVolumeOptions();
                    VolumeOptions.Normalize();
                    break;
            }
        }
    }
}
