//-----------------------------------------------------------------------------
// FILE:	    ClusterReplicationConfig.cs
// CONTRIBUTOR: John Burns
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
using System.Text;

using Newtonsoft.Json;

namespace Neon.Temporal
{
    /// <summary>
    /// Defines configuration for cluster replication.
    /// </summary>
    public class ClusterReplicationConfig
    {
        /// <summary>
        /// The Name of the replication cluster.
        /// </summary>
        [JsonProperty(PropertyName = "cluster_name")]
        public string ClusterName { get; set; }
    }
}
