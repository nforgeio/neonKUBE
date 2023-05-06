//-----------------------------------------------------------------------------
// FILE:	    ClusterState.cs
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
using System.Runtime.Serialization;

namespace Neon.Kube
{
    /// <summary>
    /// Enumerates the possible overall states for a cluster.
    /// </summary>
    public enum ClusterState
    {
        /// <summary>
        /// Status could not be determined. 
        /// </summary>
        [EnumMember(Value = "unknown")]
        Unknown = 0,

        /// <summary>
        /// Cluster not found.
        /// </summary>
        [EnumMember(Value = "not-found")]
        NotFound,

        /// <summary>
        /// One or more virtual machines exist with names conflicting
        /// with the nodes defined for the cluster being checked.
        /// </summary>
        [EnumMember(Value = "conflict")]
        Conflict,

        /// <summary>
        /// Cluster provisioning is incomplete.
        /// </summary>
        [EnumMember(Value = "provisioning")]
        Provisoning,

        /// <summary>
        /// Cluster has been provisioned but not configured.
        /// </summary>
        [EnumMember(Value = "provisioned")]
        Provisioned,

        /// <summary>
        /// Cluster configuration is incomplete.
        /// </summary>
        [EnumMember(Value = "configuring")]
        Configuring,

        /// <summary>
        /// Cluster is configured but is turned off.
        /// </summary>
        [EnumMember(Value = "off")]
        Off,

        /// <summary>
        /// Cluster is configured but transitoning between sleeping, starting,
        /// or being turned off.
        /// </summary>
        [EnumMember(Value = "transitoning")]
        Transitioning,

        /// <summary>
        /// Cluster is configured but not healthy.
        /// </summary>
        [EnumMember(Value = "unhealthy")]
        Unhealthy,

        /// <summary>
        /// Cluster is configured and healthy.
        /// </summary>
        [EnumMember(Value = "healthy")]
        Healthy,

        /// <summary>
        /// Cluster is configured but is paused.
        /// </summary>
        [EnumMember(Value = "paused")]
        Paused,

        /// <summary>
        /// Cluster didn't respond to a health request.
        /// </summary>
        [EnumMember(Value = "no-response")]
        NoResponse
    }
}
