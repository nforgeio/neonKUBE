//-----------------------------------------------------------------------------
// FILE:	    ServiceResources.cs
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
    /// Specifies the service resource requirements and limits.
    /// </summary>
    public class ServiceResources : INormalizable
    {
        /// <summary>
        /// Specifies resource limits for service containers.
        /// </summary>
        [JsonProperty(PropertyName = "Limits", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Limits", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public ServiceResourceSettings Limits { get; set; }

        /// <summary>
        /// Specifies resource reservations for service containers.
        /// </summary>
        [JsonProperty(PropertyName = "Reservations", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Reservations", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public ServiceResourceSettings Reservations { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
            Limits       = Limits ?? new ServiceResourceSettings();
            Reservations = Reservations ?? new ServiceResourceSettings();

            Limits?.Normalize();
            Reservations?.Normalize();
        }
    }
}
