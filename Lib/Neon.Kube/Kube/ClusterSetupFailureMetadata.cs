//-----------------------------------------------------------------------------
// FILE:	    ClusterSetupFailureMetadata.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using YamlDotNet.Serialization;

namespace Neon.Kube
{
    /// <summary>
    /// Holds metadata about a cluster whose prepare or setup operations failed.
    /// This is included in the ZIP file uploaded to the headend as a file named
    /// <b>metadata.yaml</b>.
    /// </summary>
    public class ClusterSetupFailureMetadata
    {
        /// <summary>
        /// The timestamp (UTC) when the failure occured.
        /// </summary>
        [JsonProperty(PropertyName = "TimestampUtc", Required = Required.Always)]
        [YamlMember(Alias = "timestampUtc", ApplyNamingConventions = false)]
        public DateTime TimestampUtc { get; set; }

        /// <summary>
        /// The current NEONKUBE version.
        /// </summary>
        [JsonProperty(PropertyName = "NeonKubeVersion", Required = Required.Always)]
        [YamlMember(Alias = "neonKubeVersion", ApplyNamingConventions = false)]
        public string NeonKubeVersion { get; set; }

        /// <summary>
        /// Set to the UUID for the client installation.
        /// </summary>
        [JsonProperty(PropertyName = "CliendId", Required = Required.Always)]
        [YamlMember(Alias = "cliendId", ApplyNamingConventions = false)]
        public Guid CliendId { get; set; }

        /// <summary>
        /// Set to the UUID for the user.  Note that this will be set to <see cref="Guid.Empty"/>
        /// until we have the chance to implement NEONCLOUD users.
        /// </summary>
        [JsonProperty(PropertyName = "UserId", Required = Required.Always)]
        [YamlMember(Alias = "userId", ApplyNamingConventions = false)]
        public Guid UserId { get; set; }

        /// <summary>
        /// Information about the exception that caused the failure.
        /// </summary>
        [JsonProperty(PropertyName = "Exception", Required = Required.Always)]
        [YamlMember(Alias = "exception", ApplyNamingConventions = false)]
        public string Exception { get; set; }
    }
}
