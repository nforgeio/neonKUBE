//-----------------------------------------------------------------------------
// FILE:	    ServiceHealthCheck.cs
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
    /// Specifies a custom service logging driver.
    /// </summary>
    public class ServiceHealthCheck : INormalizable
    {
        /// <summary>
        /// Specifies the health test to be performed.
        /// </summary>
        [JsonProperty(PropertyName = "Test", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Test", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<string> Test { get; set; }

        /// <summary>
        /// Time to wait between health checks (in nanoseconds).
        /// </summary>
        [JsonProperty(PropertyName = "Interval", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Interval", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public long? Interval { get; set; }

        /// <summary>
        /// Time to wait before considering a health check to have hung (in nanoseconds).
        /// </summary>
        [JsonProperty(PropertyName = "Timeout", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Timeout", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public long? Timeout { get; set; }

        /// <summary>
        /// Number of consecutive health check failures required to consider the container
        /// to be unhealhy.
        /// </summary>
        [JsonProperty(PropertyName = "Retries", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Retries", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public long? Retries { get; set; }

        /// <summary>
        /// Time to wait for the container to start ands initialize before enforcing
        /// health check failures.
        /// </summary>
        [JsonProperty(PropertyName = "StartPeriod", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "StartPeriod", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public long? StartPeriod { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
            Test = Test ?? new List<string>();
        }
    }
}
