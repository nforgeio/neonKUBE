//-----------------------------------------------------------------------------
// FILE:        ClusterDeploymentStatus.cs
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
using System.ComponentModel;
using System.Runtime.Serialization;

namespace Neon.Kube
{
    /// <summary>
    /// Enumerates the cluster deployment status.
    /// </summary>
    public enum ClusterDeploymentStatus
    {
        /// <summary>
        /// Indicates that the deployment status is unknown (the default value).
        /// </summary>
        [EnumMember(Value = "unknown")]
        Unknown = 0,

        /// <summary>
        /// Indicates that the cluster has been prepared.
        /// </summary>
        [EnumMember(Value = "prepared")]
        Prepared,

        /// <summary>
        /// Indicates that cluster setup succeeded and the cluster is ready.
        /// </summary>
        [EnumMember(Value = "ready")]
        Ready
    }
}
