//-----------------------------------------------------------------------------
// FILE:	    KubeClusterState.cs
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
using System.Runtime.Serialization;

namespace Neon.Kube
{
    /// <summary>
    /// Enumerates the health states for a Kubernetes cluster.
    /// </summary>
    public enum KubeClusterState
    {
        /// <summary>
        /// The health status is not known.
        /// </summary>
        [EnumMember(Value = "unknown")]
        Unknown = 0,

        /// <summary>
        /// The cluster is not healthy.
        /// </summary>
        [EnumMember(Value = "unhealthy")]
        Unhealthy,

        /// <summary>
        /// The cluster is healthy but in the process of adding or removing nodes.
        /// </summary>
        [EnumMember(Value = "transitioning")]
        Transitioning,

        /// <summary>
        /// The cluster is healthy and stable.
        /// </summary>
        [EnumMember(Value = "healthy")]
        Healthy
    }
}
