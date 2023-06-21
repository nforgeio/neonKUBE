//-----------------------------------------------------------------------------
// FILE:        ClusterNodeState.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Runtime.Serialization;

namespace Neon.Kube
{
    /// <summary>
    /// Enumerates the possible states of a cluster node from the
    /// hosting manager's perspective.
    /// </summary>
    public enum ClusterNodeState
    {
        /// <summary>
        /// The node state is not known.
        /// </summary>
        [EnumMember(Value = "unknown")]
        Unknown = 0,

        /// <summary>
        /// The node is not provisioned.
        /// </summary>
        [EnumMember(Value = "not-provisioned")]
        NotProvisioned,

        /// <summary>
        /// The node conflicts with an existing virtual machine that was
        /// not deployed with the cluster.
        /// </summary>
        [EnumMember(Value = "conflict")]
        Conflict,

        /// <summary>
        /// The node is provisioned but turned off.
        /// </summary>
        [EnumMember(Value = "off")]
        Off,

        /// <summary>
        /// The node is starting.
        /// </summary>
        [EnumMember(Value = "starting")]
        Starting,

        /// <summary>
        /// The node is sleeping.
        /// </summary>
        [EnumMember(Value = "paused")]
        Paused,

        /// <summary>
        /// The node is running.
        /// </summary>
        [EnumMember(Value = "running")]
        Running
    }
}
