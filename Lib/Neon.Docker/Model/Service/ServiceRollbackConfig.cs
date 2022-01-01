//-----------------------------------------------------------------------------
// FILE:	    ServiceRollbackConfig.cs
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
    /// Specifies the rollback strategy for a service.
    /// </summary>
    public class ServiceRollbackConfig : INormalizable
    {
        /// <summary>
        /// Maximum number of tasks to be rolled back in parallel during an rollback interation.
        /// </summary>
        [JsonProperty(PropertyName = "Parallelism", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Parallelism", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public long? Parallelism { get; set; }

        /// <summary>
        /// Time between rollback iterations (in nanoseconds).
        /// </summary>
        [JsonProperty(PropertyName = "Delay", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Delay", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public long? Delay { get; set; }

        /// <summary>
        /// Action to take if an rolled back task fails to run or stops running during the rollback.
        /// </summary>
        [JsonProperty(PropertyName = "FailureAction", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "FailureAction", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public ServiceRollbackFailureAction? FailureAction { get; set; }

        /// <summary>
        /// Time to monitor rolled back tasks for failure (in nanoseconds).
        /// </summary>
        [JsonProperty(PropertyName = "Monitor", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Monitor", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public long? Monitor { get; set; }

        /// <summary>
        /// The fraction of tasks that may fail during an rollback before the failure ']
        /// action is invoked, specified as a floating point number between 0 and 1.
        /// </summary>
        [JsonProperty(PropertyName = "MaxFailureRatio", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "MaxFailureRatio", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public double? MaxFailureRatio { get; set; }

        /// <summary>
        /// Specifies the order in which the running task is stopped and the rolledback task is started.
        /// </summary>
        [JsonProperty(PropertyName = "Order", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Order", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public ServiceRollbackOrder? Order { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
        }
    }
}
