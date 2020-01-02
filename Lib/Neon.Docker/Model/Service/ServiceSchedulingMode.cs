//-----------------------------------------------------------------------------
// FILE:	    ServiceSchedulingMode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
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
    /// Orchestration scheduling mode for the service.
    /// </summary>
    public class ServiceSchedulingMode : INormalizable
    {
        /// <summary>
        /// Replicated scheduling mode options.
        /// </summary>
        [JsonProperty(PropertyName = "Replicated", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public ServiceReplicatedSchedulingMode Replicated { get; set; }

        /// <summary>
        /// Global scheduling mode options.
        /// </summary>
        [JsonProperty(PropertyName = "Global", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public ServiceGlobalSchedulingMode Global { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
            Replicated?.Normalize();
            Global?.Normalize();
        }
    }
}
