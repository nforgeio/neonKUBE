// -----------------------------------------------------------------------------
// FILE:        ClusterLockStatus.cs
// CONTRIBUTOR: NEONFORGE Team
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License").
// You may not use this file except in compliance with the License.
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
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using YamlDotNet.Serialization;

namespace Neon.Kube.ClusterMetadata
{
    /// <summary>
    /// Enumerates the cluster lock states.
    /// </summary>
    public enum ClusterLockState
    {
        /// <summary>
        /// Lock state cannot be determined.
        /// </summary>
        [EnumMember(Value = "unknown")]
        Unknown = 0,

        /// <summary>
        /// Cluster is unlocked.
        /// </summary>
        [EnumMember(Value = "unlocked")]
        Unlocked,

        /// <summary>
        /// Cluster is unlocked.
        /// </summary>
        [EnumMember(Value = "locked")]
        Locked
    }

    /// <summary>
    /// Holds the lock status for a cluster.
    /// </summary>
    public class ClusterLockStatus
    {
        /// <summary>
        /// Specifies thge cluster name.
        /// </summary>
        [JsonProperty(PropertyName = "Cluster", Required = Required.Always)]
        [YamlMember(Alias = "Cluster", ApplyNamingConventions = false)]
        public string Cluster { get; set; }

        /// <summary>
        /// Specifies the cluster lock state.
        /// </summary>
        [JsonProperty(PropertyName = "State", Required = Required.Always)]
        [YamlMember(Alias = "State", ApplyNamingConventions = false)]
        public ClusterLockState State { get; set; }
    }
}
