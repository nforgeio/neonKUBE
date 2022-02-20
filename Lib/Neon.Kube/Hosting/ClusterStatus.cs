//-----------------------------------------------------------------------------
// FILE:	    ClusterStatus.cs
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

namespace Neon.Kube
{
    /// <summary>
    /// Enumerates the possible states for a cluster.
    /// </summary>
    public enum ClusterStatus
    {
        /// <summary>
        /// Status could not be determined. 
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Cluster not found.
        /// </summary>
        NotFound,

        /// <summary>
        /// Cluster provisioning is incomplete.
        /// </summary>
        Provisoning,

        /// <summary>
        /// Cluster has been provisioned but not configured.
        /// </summary>
        Provisioned,

        /// <summary>
        /// Cluster configuration is incomplete.
        /// </summary>
        Configuring,

        /// <summary>
        /// Cluster is configured.
        /// </summary>
        Configured,

        /// <summary>
        /// Cluster is configured but not healthy.
        /// </summary>
        Unhealthy,

        /// <summary>
        /// Cluster is configured and healthy.
        /// </summary>
        Healthy
    }
}
