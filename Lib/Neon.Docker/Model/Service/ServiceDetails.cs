//-----------------------------------------------------------------------------
// FILE:	    ServiceDetails.cs
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
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Runtime.Serialization;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

namespace Neon.Docker
{
    /// <summary>
    /// <para>
    /// Holds the details describing a running Docker swarm service
    /// from the service list or inspection REST APIs.
    /// </para>
    /// <note>
    /// This type matches the Docker API v1.35.
    /// </note>
    /// </summary>
    public class ServiceDetails : INormalizable
    {
        /// <summary>
        /// The service ID.
        /// </summary>
        [JsonProperty(PropertyName = "ID", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "ID", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string ID { get; set; }

        /// <summary>
        /// Service update version information.
        /// </summary>
        [JsonProperty(PropertyName = "Version", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Version", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public ServiceVersion Version { get; set; }

        /// <summary>
        /// Time when the service was created (as a string).
        /// </summary>
        [JsonProperty(PropertyName = "CreatedAt", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "CreatedAt", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string CreatedAt { get; set; }

        /// <summary>
        /// Returns the time (UTC) the service was created (as a <see cref="DateTime"/>).
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public DateTime CreatedAtUtc
        {
            get { return DateTime.Parse(CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal); }
        }

        /// <summary>
        /// Time when the service was last created or updated (as a string).
        /// </summary>
        [JsonProperty(PropertyName = "UpdatedAt", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "UpdatedAt", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string UpdatedAt { get; set; }

        /// <summary>
        /// Returns the time (UTC) the service was last created or updated (as a <see cref="DateTime"/>).
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public DateTime UpdatedAtUtc
        {
            get { return DateTime.Parse(UpdatedAt, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal); }
        }

        /// <summary>
        /// The service specification.
        /// </summary>
        [JsonProperty(PropertyName = "Spec", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Spec", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public ServiceSpec Spec { get; set; }

        /// <summary>
        /// Optionally describes the service's state before the last update.
        /// This is the state the service will revert to when it's rolled
        /// back.
        /// </summary>
        [JsonProperty(PropertyName = "PreviousSpec", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "PreviousSpec", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public ServiceSpec PreviousSpec { get; set; }

        /// <summary>
        /// Describes the service's current endpoint state.
        /// </summary>
        [JsonProperty(PropertyName = "Endpoint", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Endpoint", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public ServiceEndpoint Endpoint { get; set; }

        /// <summary>
        /// Describes the service update status.
        /// </summary>
        [JsonProperty(PropertyName = "UpdateStatus", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "UpdateStatus", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public ServiceUpdateStatus UpdateStatus { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
            Version  = Version ?? new ServiceVersion();
            Spec     = Spec ?? new ServiceSpec();
            Endpoint = Endpoint ?? new ServiceEndpoint();

            Version?.Normalize();
            Spec?.Normalize();
            PreviousSpec?.Normalize();
            Endpoint?.Normalize();
            UpdateStatus?.Normalize();
        }

        /// <summary>
        /// Returns the value of an environment variable for the current service deployment.
        /// </summary>
        /// <param name="variable">The variable name (case insensitive).</param>
        /// <returns>The value of the variable or <c>null</c> if the variable doesn't exist.</returns>
        public string GetEnv(string variable)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(variable), nameof(variable));

            var pattern = $"{variable}=";

            foreach (var item in Spec.TaskTemplate.ContainerSpec.Env)
            {
                if (item.StartsWith(pattern, StringComparison.InvariantCultureIgnoreCase))
                {
                    return item.Substring(pattern.Length);
                }
            }

            return null;
        }
    }
}
