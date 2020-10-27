//-----------------------------------------------------------------------------
// FILE:	    ServiceUpdateConfig.cs
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
    /// Specifies the update strategy for a service.
    /// </summary>
    public class ServiceUpdateConfig : INormalizable
    {
        /// <summary>
        /// Maximum number of tasks to be updated in parallel during an update interation.
        /// </summary>
        [JsonProperty(PropertyName = "Parallelism", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Parallelism", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public long? Parallelism { get; set; }

        /// <summary>
        /// Time between update interations (in nanoseconds).
        /// </summary>
        [JsonProperty(PropertyName = "Delay", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Delay", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public long? Delay { get; set; }

        /// <summary>
        /// Action to take if an updated task fails to run or stops running during the update.
        /// </summary>
        [JsonProperty(PropertyName = "FailureAction", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "FailureAction", ApplyNamingConventions = false)]
        [DefaultValue(default(ServiceUpdateFailureAction))]
        public ServiceUpdateFailureAction FailureAction { get; set; }

        /// <summary>
        /// Time to monitor updated tasks for failure (in nanoseconds).
        /// </summary>
        [JsonProperty(PropertyName = "Monitor", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Monitor", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public long? Monitor { get; set; }

        /// <summary>
        /// The fraction of tasks that may fail during an update before the failure ']
        /// action is invoked, specified as a floating point number between 0 and 1.
        /// </summary>
        [JsonProperty(PropertyName = "MaxFailureRatio", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "MaxFailureRatio", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public double? MaxFailureRatio { get; set; }

        /// <summary>
        /// Specifies the order in which the running task is stopped and the new task is started.
        /// </summary>
        [JsonProperty(PropertyName = "Order", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Order", ApplyNamingConventions = false)]
        [DefaultValue(default(ServiceUpdateOrder))]
        public ServiceUpdateOrder Order { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
        }
    }
}
