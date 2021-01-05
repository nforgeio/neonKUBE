//-----------------------------------------------------------------------------
// FILE:	    ServiceUpdateStatus.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.Globalization;
using System.Runtime.Serialization;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

namespace Neon.Docker
{
    /// <summary>
    /// Describes the virtual IP address assigned to the service on
    /// a specific attached network.
    /// </summary>
    public class ServiceUpdateStatus : INormalizable
    {
        /// <summary>
        /// Indicates the saervice updating state.
        /// </summary>
        [JsonProperty(PropertyName = "State", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "State", ApplyNamingConventions = false)]
        [DefaultValue(default(ServiceUpdateState))]
        public ServiceUpdateState State { get; set; }

        /// <summary>
        /// Indicates when the service update was started.
        /// </summary>
        [JsonProperty(PropertyName = "StartedAt", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "StartedAt", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string StartedAt { get; set; }

        /// <summary>
        /// Returns the time (UTC) the service was started (as a <see cref="DateTime"/>).
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public DateTime StartedAtUtc
        {
            get { return DateTime.Parse(StartedAt, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal); }
        }

        /// <summary>
        /// Indicates when the service update was completed.
        /// </summary>
        [JsonProperty(PropertyName = "CompletedAt", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "CompletedAt", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string CompletedAt { get; set; }

        /// <summary>
        /// Returns the time (UTC) the service update was completed (as a <see cref="DateTime"/>).
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public DateTime CompletedAtUtc
        {
            get { return DateTime.Parse(CompletedAt, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal); }
        }

        /// <summary>
        /// A textual message describing the update.
        /// </summary>
        [JsonProperty(PropertyName = "Message", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Message", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Message { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
        }
    }
}
