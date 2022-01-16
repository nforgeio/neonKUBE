//-----------------------------------------------------------------------------
// FILE:	    V1NodeExecTask.cs
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
using System.Text;

using k8s;
using k8s.Models;

using DotnetKubernetesClient.Entities;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

using Neon.Kube;

namespace Neon.Kube.Resources
{
    /// <summary>
    /// Describes a task to be executed by <b>neon-node-agent</b> pods running on targeted cluster nodes
    /// </summary>
    /// <remarks>
    /// <para>
    /// neonKUBE clusters deploy the <b>neon-node-agent</b> as a daemonset such that this is running on
    /// every node in the cluster.  This runs as a privileged pod and has full access to the host node's
    /// file system, network, and processes and is used for low-level node maintainance activities.
    /// </para>
    /// <para>
    /// The <see cref="V1NodeExecTask"/> custom resource is used to submit tasks to execute a command on 
    /// on the node by the node agents.  
    /// </para>
    /// </remarks>
    [KubernetesEntity(Group = KubeConst.NeonResourceGroup, ApiVersion = "v1", Kind = "NodeTask", PluralName = "nodetasks")]
    [KubernetesEntityShortNames]
    [EntityScope(EntityScope.Cluster)]
    [Description("Describes a neonKUBE cluster upstream container registry.")]
    public class V1NodeExecTask : CustomKubernetesEntity<V1NodeExecTask.V1NodeExecTaskSpec, V1NodeExecTask.V1NodeExecTaskStatus>
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public V1NodeExecTask()
        {
            ((IKubernetesObject)this).SetMetadata();
        }

        /// <summary>
        /// The node execute task specification.
        /// </summary>
        public class V1NodeExecTaskSpec
        {
            /// <summary>
            /// Identifies the node by host name where the command will be executed.
            /// </summary>
            [Required]
            public string Node { get; set; }

            /// <summary>
            /// Specifies the command and arguments to be executed on the node.
            /// </summary>
            [Required]
            public List<string> Command { get; set; }
        }

        /// <summary>
        /// The node execute task status.
        /// </summary>
        public class V1NodeExecTaskStatus
        {
            /// <summary>
            /// Set to the time 
            /// </summary>
            public DateTime? Started { get; set; }

            /// <summary>
            /// 
            /// </summary>
            public DateTime? Finished { get; set;}

            /// <summary>
            /// The exit code returned by the command.
            /// </summary>
            public int ExitCode { get; set; }

            /// <summary>
            /// The text written to standard output by the command.
            /// </summary>
            public string Output { get; set; }

            /// <summary>
            /// The text written to standard error by the command.
            /// </summary>
            public string Error { get; set; }
        }
    }
}
