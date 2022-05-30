//-----------------------------------------------------------------------------
// FILE:	    V1NodeTask.cs
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

#if KUBEOPS
using DotnetKubernetesClient.Entities;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;
#endif

#if KUBEOPS
namespace Neon.Kube.ResourceDefinitions
#else
namespace Neon.Kube.Resources
#endif
{
    /// <summary>
    /// Describes a task to be executed as a command on a node by the <b>neon-node-agent</b> pods running on 
    /// the target cluster node.
    /// </summary>
    /// <remarks>
    /// <para>
    /// neonKUBE clusters deploy the <b>neon-node-agent</b> as a daemonset such that this is running on
    /// every node in the cluster.  This runs as a privileged pod and has full access to the host node's
    /// file system, network, and processes and is typically used for low-level node maintainance activities.
    /// </para>
    /// <para><b>LIFECYCLE</b></para>
    /// <para>
    /// Here is the description of a NodeTask lifecycle:
    /// </para>
    /// <list type="number">
    /// <item>
    /// <b>neon-cluster-operator</b> determines that a command needs to be run on a specific node
    /// and creates a <see cref="V1NodeTask"/> specifiying the name of the target node as well as 
    /// the command and any arguments.
    /// </item>
    /// <item>
    /// <b>neon-node-agent</b> is running as a daemonset on all cluster nodes and each instance is
    /// watching for node tasks assigned to its node.
    /// </item>
    /// <item>
    /// When a <b>neon-node-agent</b> sees a pending <see cref="V1NodeTask"/> assigned to the
    /// node it's managing, the agent compares the <see cref="V1NodeTaskSpec.StartLimitUtc"/>
    /// property to the current time.  If the current time is before the limit, the agent will 
    /// assign its unique ID to the task status, set the <see cref="V1NodeTaskStatus.StartedUtc"/>
    /// to the current time and change the state to <see cref="NodeTaskState.Running"/>, saving
    /// these status changes to the API server.
    /// </item>
    /// <item>
    /// The agent will then execute the command on the node, persisting the process ID to the node task 
    /// status and then capturing its exit code and standard output and error streams when the command completes.
    /// The command execution time will be limited by <see cref="V1NodeTaskSpec.TimeoutSeconds"/>.
    /// </item>
    /// <note>
    /// <para>
    /// <b>WARNING!</b> You need to recognize that secrets included in a node task command line will
    /// can be observed by examining the <b>NodeTask</b> custom resource.  These are persisted within
    /// the <b>neon-system</b> namespace and can be protected with RBAC roles.
    /// </para>
    /// <para>
    /// Node tasks are intended to run local node tasks that probably won't need secrets.  We recommend 
    /// that you avoid running node tasks that need secrets and perform those operations using normal
    /// Kubernetes pods that obtain secrets from Kubernetes the usual way.
    /// </para>
    /// </note>
    /// <item>
    /// When the command completes without timing out, the agent will set its state to <see cref="NodeTaskState.Finished"/>,
    /// set <see cref="V1NodeTaskStatus.FinishedUtc"/> to the current time and <see cref="V1NodeTaskStatus.ExitCode"/>,
    /// <see cref="V1NodeTaskStatus.Output"/> and <see cref="V1NodeTaskStatus.Error"/> to the command results.
    /// </item>
    /// <note>
    /// The <see cref="V1NodeTask.V1NodeTaskSpec.CaptureOutput"/> property controls whether the standard
    /// output and error streams are captured.  This defaults to <c>true</c>.  <see cref="V1NodeTask"/>
    /// supports only text output encoded as UTF-8 or ASCII.  Binary output is not supported.  You should
    /// set <see cref="V1NodeTask.V1NodeTaskSpec.CaptureOutput"/><c>=false</c> in these cases or when
    /// the output may include secrets.
    /// </note>
    /// <item>
    /// When the command execution times out, the agent will kill the process and set the node task state to
    /// <see cref="NodeTaskState.ExecuteTimeout"/> and set <see cref="V1NodeTaskStatus.FinishedUtc"/> to the
    /// current time.
    /// </item>
    /// <item>
    /// <b>neon-node-agents</b> also look for running tasks that are assigned to its node but include a 
    /// <see cref="V1NodeTaskStatus.AgentId"/> that doesn't match the current agent's ID.  This can
    /// happen when the previous agent pod started executing the command and then was terminated before the
    /// command completed.  The agent will attempt to locate the running pod by its command line and
    /// process ID and terminate when it exists and then set the state to <see cref="NodeTaskState.Orphaned"/>
    /// and <see cref="V1NodeTaskStatus.FinishedUtc"/> to the current time.
    /// </item>
    /// <item>
    /// <b>neon-cluster-operator</b> also monitors these tasks.  It will remove pending tasks that are
    /// older than <see cref="V1NodeTaskSpec.StartLimitUtc"/>, tasks that have been completed for
    /// longer than <see cref="V1NodeTaskSpec.RetainSeconds"/> as well as tasks assigned to nodes
    /// that don't exist.
    /// </item>
    /// </list>
    /// </remarks>
    [KubernetesEntity(Group = Helper.NeonResourceGroup, ApiVersion = "v1alpha1", Kind = "NodeTask", PluralName = "nodetasks")]
#if KUBEOPS
    [KubernetesEntityShortNames]
    [EntityScope(EntityScope.Cluster)]
    [Description("Describes a neonKUBE task to be executed on a specific cluster node.")]
#endif
    public class V1NodeTask : CustomKubernetesEntity<V1NodeTask.V1NodeTaskSpec, V1NodeTask.V1NodeTaskStatus>
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public V1NodeTask()
        {
            ((IKubernetesObject)this).SetMetadata();
        }

        /// <summary>
        /// The node execute task specification.
        /// </summary>
        public class V1NodeTaskSpec
        {
            /// <summary>
            /// Identifies the node where the command will be executed.
            /// </summary>
#if KUBEOPS
            [Required]
#endif
            public string Node { get; set; }

            /// <summary>
            /// Specifies the command and arguments to be executed on the node.
            /// </summary>
#if KUBEOPS
            [Required]
#endif
            public List<string> Command { get; set; }

            /// <summary>
            /// Specifies the time after which the task will be rejected by the node agent. 
            /// This can be used to ensure that node tasks won't remain pending forever.  
            /// This defaults to one day in the future from the time the task was created.
            /// </summary>
            public DateTime StartLimitUtc { get; set; } = DateTime.UtcNow + TimeSpan.FromDays(1);

            /// <summary>
            /// Specifies the maximum time in seconds the command will be allowed to execute.
            /// This defaults to 1800 seconds (30 minutes).
            /// </summary>
            public int TimeoutSeconds { get; set; } = 1800;

            /// <summary>
            /// Specifies the maximum time to retain the task after it has been
            /// ended, for any reason.  <b>neon-cluster-operator</b> will add
            /// this to <see cref="V1NodeTaskStatus.FinishedUtc"/> to determine
            /// when it should delete the task.  This defaults to 600 seconds
            /// (10 minutes).
            /// </summary>
            public int RetainSeconds { get; set; } = 600;

            /// <summary>
            /// <para>
            /// Controls whether the command output is to be captured.  This defaults to <c>true</c>.
            /// </para>
            /// <note>
            /// <see cref="V1NodeTask"/> is designed to capture command output as UTF-8 or
            /// ASCII text.  Binary output or other text encodings are not supported.  You
            /// should set this to <c>false</c> for commands with unsupported output or
            /// when the command output may include secrets.
            /// </note>
            /// </summary>
            public bool CaptureOutput { get; set; } = true;

            /// <summary>
            /// Verifies that the specification properties are valid.
            /// </summary>
            /// <exception cref="CustomResourceException">Thrown when the resource is not valid.</exception>
            public void Validate()
            {
                var specPrefix = $"{nameof(V1NodeTask)}.Spec";

                if (Command == null || Command.Count == 0)
                {
                    throw new CustomResourceException($"[{specPrefix}.{nameof(Command)}]: List be NULL or empty.");
                }

                if (string.IsNullOrEmpty(Command[0]))
                {
                    throw new CustomResourceException($"[{specPrefix}.{nameof(Command)}]: Command cannot be NULL or empty.");
                }

                if (TimeoutSeconds <= 0)
                {
                    throw new CustomResourceException($"[{specPrefix}.{nameof(TimeoutSeconds)}={TimeoutSeconds}]: Must be greater than zero.");
                }

                if (RetainSeconds < 0)
                {
                    throw new CustomResourceException($"[{specPrefix}.{nameof(TimeoutSeconds)}={TimeoutSeconds}]: Cannot be negative.");
                }
            }
        }

        /// <summary>
        /// The node execute task status.
        /// </summary>
        public class V1NodeTaskStatus
        {
            /// <summary>
            /// The globally unique ID of the <b>neon-node-agent</b> instance that executed
            /// the command.  This is used to detect tasks that started executing but didn't
            /// finish before node agent crashed or was otherwise terminated, providing a way
            /// for the next node-agent to clean things up.
            /// </summary>
            public string AgentId { get; set; }

            /// <summary>
            /// Indicates the current state of the task.  This defaules to
            /// <see cref="NodeTaskState.Pending"/> when the task is constructed.
            /// </summary>
            public NodeTaskState State { get; set; } = NodeTaskState.Pending;

            /// <summary>
            /// Indicates when the task started executing. 
            /// </summary>
            public DateTime? StartedUtc { get; set; }

            /// <summary>
            /// Indicates when the task finished executing.
            /// </summary>
            public DateTime? FinishedUtc { get; set;}

            /// <summary>
            /// The command line invoked for the task.  This is used for detecting orphaned tasks.
            /// </summary>
            public string CommandLine { get; set; }

            /// <summary>
            /// Set to the ID of the task process while its running.
            /// </summary>
            public int? ProcessId { get; set; }

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

            /// <summary>
            /// Verifies that the status properties are valid.
            /// </summary>
            /// <exception cref="CustomResourceException">Thrown when the resource is not valid.</exception>
            public void Validate()
            {
            }
        }

        /// <summary>
        /// Verifies that the resource properties are valid.
        /// </summary>
        /// <exception cref="CustomResourceException">Thrown when the resource is not valid.</exception>
        public void Validate()
        {
            Spec?.Validate();
            Status?.Validate();
        }
    }
}
