//-----------------------------------------------------------------------------
// FILE:	    ServiceRestartPolicy.cs
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
    /// Specifies the restart policy for service containers.
    /// </summary>
    public class ServiceRestartPolicy : INormalizable
    {
        /// <summary>
        /// Specifies the condition under which a service container should be restarted.
        /// </summary>
        [JsonProperty(PropertyName = "Condition", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Condition", ApplyNamingConventions = false)]
        [DefaultValue(default(ServiceRestartCondition))]
        public ServiceRestartCondition Condition { get; set; }

        /// <summary>
        /// Deplay between restart attempts (nanoseconds).
        /// </summary>
        [JsonProperty(PropertyName = "Delay", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Delay", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public long? Delay { get; set; }

        /// <summary>
        /// Specifies the maximum number of container restart attempts before giving up.
        /// </summary>
        [JsonProperty(PropertyName = "MaxAttempts", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "MaxAttempts", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public long? MaxAttempts { get; set; }

        /// <summary>
        /// Specifies the window of time during which the restart policy will be 
        /// enavluated (nanoseconds).
        /// </summary>
        [JsonProperty(PropertyName = "Window", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Window", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public long? Window { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
        }
    }
}
