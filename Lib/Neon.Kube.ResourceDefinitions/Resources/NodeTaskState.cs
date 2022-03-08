//-----------------------------------------------------------------------------
// FILE:	    NodeTaskState.cs
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
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

using k8s;
using k8s.Models;

#if KUBEOPS
using DotnetKubernetesClient.Entities;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;
#endif

namespace Neon.Kube.Resources
{
    /// <summary>
    /// Enumerates the possible status of a <see cref="V1NodeTask"/>.
    /// </summary>
    public enum NodeTaskState
    {
        /// <summary>
        /// The task is waiting to be executed by the <b>neon-node-agent</b>.
        /// </summary>
        [EnumMember(Value = "pending")]
        Pending,

        /// <summary>
        /// The task is currently running.
        /// </summary>
        [EnumMember(Value = "running")]
        Running,

        /// <summary>
        /// The task timed out while awaiting execution.
        /// </summary>
        [EnumMember(Value = "execute-timeout")]
        PendingTimeout,

        /// <summary>
        /// The task timed out while executing.
        /// </summary>
        [EnumMember(Value = "execute-timeout")]
        ExecuteTimeout,

        /// <summary>
        /// The task started executing on one <b>neon-node-agent</b> pod which
        /// crashed or was otherwise terminated and a newly scheduled pod detected
        /// this sutuation.
        /// </summary>
        [EnumMember(Value = "orphaned")]
        Orphaned,

        /// <summary>
        /// The task finished executing.
        /// </summary>
        [EnumMember(Value = "finished")]
        Finished
    }
}
