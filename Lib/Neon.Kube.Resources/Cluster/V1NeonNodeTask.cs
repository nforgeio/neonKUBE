//-----------------------------------------------------------------------------
// FILE:        V1NeonNodeTask.cs
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
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json.Serialization;

using Neon.JsonConverters;

using k8s;
using k8s.Models;

namespace Neon.Kube.Resources.Cluster
{
    /// <summary>
    /// <para>
    /// Describes a task to be executed as a Bash script on a node by the <b>neon-node-agent</b> pod
    /// running on the target cluster node.
    /// </para>
    /// <note>
    /// The node agent currently executes one node task at a time in no guaranteed order.
    /// </note>
    /// </summary>
    /// <remarks>
    /// <para>
    /// NEONKUBE clusters deploy the <b>neon-node-agent</b> as a daemonset such that this is running on
    /// every node in the cluster.  This runs as a privileged pod and has full access to the host node's
    /// file system, network, and processes and is typically used for low-level node maintainance activities.
    /// </para>
    /// <para><b>NODETASK SCRIPTS</b></para>
    /// <para>
    /// Node tasks are simply Bash scripts executed on the node by the <b>neon-node-agent</b> daemon running
    /// on the node.  These scripts will be written to the node's file system like:
    /// </para>
    /// <para><b>/var/run/neonkube/node-agent/nodetasks/GUID/task.sh</b></para>
    /// <para>
    /// where GUID is a base-36 encoded GUID generated and assigned to the task by the agent.
    /// </para>
    /// <para>
    /// <b>neon-node-agent</b> adds some variable assignments to the beginning of the deployed script before executing it:
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><b>$NODE_ROOT</b></term>
    ///     <description>
    ///     <para>
    ///     Identifies where the host node's file system is mounted to the <b>neon-node-agent</b> container.
    ///     Since the script is executing in the context of the container, your script will need to use this
    ///     to reference files and directories on the host node.  This currently returns <b>/mnt/host</b> but
    ///     you should always use this variable instead of hardcoding the path.
    ///     </para>
    ///     <note>
    ///     This <b>does not include</b> a terminating <b>"/"</b>
    ///     </note>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>$SCRIPT_DIR</b></term>
    ///     <description>
    ///     <para>
    ///     Set to the directory where the script is executing (like <b>/var/run/neonkube/node-agent/nodetasks/GUID</b>.
    ///     Your scripts should generally store any temporary files here so they will be removed automaticaly by the
    ///     node agent.
    ///     </para>
    ///     <note>
    ///     This <b>does not include</b> a terminating <b>"/"</b>
    ///     </note>
    ///     </description>
    /// </item>
    /// </list>
    /// <para>
    /// </para>
    /// <para><b>LIFECYCLE</b></para>
    /// <para>
    /// Here is the description of a NodeTask lifecycle:
    /// </para>
    /// <list type="number">
    /// <item>
    /// <b>neon-cluster-operator</b> or other entity determines that a script needs to be run on a
    /// specific node and creates a <see cref="V1NeonNodeTask"/> specifiying the name of the target node
    /// as well as the Bash script to be executed.
    /// </item>
    /// <item>
    /// <b>neon-node-agent</b> is running as a daemonset on all cluster nodes and each instance is
    /// watching for node tasks assigned to its node.
    /// </item>
    /// <item>
    /// When a <b>neon-node-agent</b> sees a pending <see cref="V1NeonNodeTask"/> assigned to the
    /// node it's managing, the agent will assign its unique ID to the task status, set the
    /// <see cref="TaskStatus.StartTimestamp"/> to the current time and change the state to
    /// <see cref="Phase.Running"/>.
    /// </item>
    /// <item>
    /// The agent will assign a new UUID to the task and save this in the node task status.  This UUID will
    /// be used to name the script file persisted to the host and will also be used to identify the 
    /// The agent will then execute the script on the node, persisting the process ID to the node task 
    /// status along with the command line used to execute the script.  When the script finishes, the
    /// agent will capture its exit code and standard output and error streams as text.  The command 
    /// execution time will be limited by <see cref="TaskSpec.TimeoutSeconds"/>.
    /// </item>
    /// <note>
    /// <para>
    /// <b>WARNING!</b> You need to recognize that secrets included in a node task command line will
    /// can be observed by examining the <b>NodeTask</b> custom resource.  These are persisted at the
    /// cluster level.  The script itself will be executed in a host folder where only <b>root</b> has
    /// permissions.
    /// </para>
    /// <para>
    /// Node tasks are intended to run local node tasks that probably won't need secrets.  We recommend 
    /// that you avoid running node tasks that need secrets and perform those operations using normal
    /// Kubernetes pods that obtain secrets from Kubernetes the usual way.
    /// </para>
    /// </note>
    /// <item>
    /// When the command completes without timing out, the agent will set its state to <see cref="Phase.Success"/>,
    /// set <see cref="TaskStatus.FinishTimestamp"/> to the current time and <see cref="TaskStatus.ExitCode"/>,
    /// <see cref="TaskStatus.Output"/> and <see cref="TaskStatus.Error"/> to the command results.
    /// </item>
    /// <note>
    /// The <see cref="V1NeonNodeTask.TaskSpec.CaptureOutput"/> property controls whether the standard
    /// output and error streams are captured.  This defaults to <c>true</c>.  <see cref="V1NeonNodeTask"/>
    /// supports only text output encoded as UTF-8 or ASCII.  Binary output is not supported.  You should
    /// set <see cref="V1NeonNodeTask.TaskSpec.CaptureOutput"/><c>=false</c> in these cases or when
    /// the output may include secrets.
    /// </note>
    /// <item>
    /// When the command execution timesout, the agent will kill the process and set the node task state to
    /// <see cref="Phase.Timeout"/> and set <see cref="TaskStatus.FinishTimestamp"/> to the time when the
    /// timeout was detected.
    /// </item>
    /// <item>
    /// <b>neon-node-agents</b> also look for running tasks that are assigned to its node but include a 
    /// <see cref="TaskStatus.AgentId"/> that doesn't match the current agent's ID.  This can
    /// happen when the previous agent pod started executing the command and then was terminated before the
    /// command completed.  The agent will attempt to locate the running pod by its command line and
    /// process ID and terminate when it exists and then set the state to <see cref="Phase.Orphaned"/>
    /// and <see cref="TaskStatus.FinishTimestamp"/> to the current time.
    /// </item>
    /// <item>
    /// Finally, <b>neon-node-agent</b> periodically looks for Bash scripts that don't have corresponding node
    /// tasks and will delete these so they don't accumulate.  This means the a task's script will typically
    /// be deleted shortly after the task retention period has been exceeded.
    /// </item>
    /// <item>
    /// <b>neon-cluster-operator</b> also monitors these tasks.  It will remove tasks assigned to nodes
    /// that don't exist.
    /// </item>
    /// </list>
    /// </remarks>
    [KubernetesEntity(Group = KubeGroup, ApiVersion = KubeApiVersion, Kind = KubeKind, PluralName = KubePlural)]
    [EntityScope(EntityScope.Cluster)]
    public class V1NeonNodeTask : IKubernetesObject<V1ObjectMeta>, ISpec<V1NeonNodeTask.TaskSpec>, IStatus<V1NeonNodeTask.TaskStatus>
    {
        /// <summary>
        /// Object API group.
        /// </summary>
        public const string KubeGroup = ResourceHelper.NeonKubeResourceGroup;

        /// <summary>
        /// Object API version.
        /// </summary>
        public const string KubeApiVersion = "v1alpha1";

        /// <summary>
        /// Object API kind.
        /// </summary>
        public const string KubeKind = "NeonNodeTask";

        /// <summary>
        /// Object plural name.
        /// </summary>
        public const string KubePlural = "neonnodetasks";

        //---------------------------------------------------------------------
        // Local types

        /// <summary>
        /// Enumerates the possible status of a <see cref="V1NeonNodeTask"/>.
        /// </summary>
        public enum Phase
        {
            /// <summary>
            /// The task has been newly submitted.  <b>neon-node-agent</b> will set this
            /// to <see cref="Pending"/> when it sees the task for the first time.
            /// </summary>
            New = 0,

            /// <summary>
            /// The task is waiting to be executed by the <b>neon-node-agent</b>.
            /// </summary>
            Pending,

            /// <summary>
            /// The task is currently running.
            /// </summary>
            Running,

            /// <summary>
            /// The task timed out while executing.
            /// </summary>
            Timeout,

            /// <summary>
            /// The task started executing on one <b>neon-node-agent</b> pod which
            /// crashed or was otherwise terminated and a newly scheduled pod detected
            /// this sutuation.
            /// </summary>
            Orphaned,

            /// <summary>
            /// The task did not execute before its <see cref="V1NeonNodeTask.TaskSpec.StartBeforeTimestamp"/>
            /// property.
            /// </summary>
            Tardy,

            /// <summary>
            /// The task executed but failed with a non-zero exit code.
            /// </summary>
            Failed,

            /// <summary>
            /// The task executed successfully with a zero exit code.
            /// </summary>
            Success
        }

        //---------------------------------------------------------------------
        // Implementation

        /// <summary>
        /// Default constructor.
        /// </summary>
        public V1NeonNodeTask()
        {
            ApiVersion = $"{KubeGroup}/{KubeApiVersion}";
            Kind       = KubeKind;
        }

        /// <summary>
        /// Gets or sets APIVersion defines the versioned schema of this
        /// representation of an object. Servers should convert recognized
        /// schemas to the latest internal value, and may reject unrecognized
        /// values. More info:
        /// https://git.k8s.io/community/contributors/devel/sig-architecture/api-conventions.md#resources
        /// </summary>
        public string ApiVersion { get; set; }

        /// <summary>
        /// Gets or sets kind is a string value representing the REST resource
        /// this object represents. Servers may infer this from the endpoint
        /// the client submits requests to. Cannot be updated. In CamelCase.
        /// More info:
        /// https://git.k8s.io/community/contributors/devel/sig-architecture/api-conventions.md#types-kinds
        /// </summary>
        public string Kind { get; set; }

        /// <summary>
        /// Gets or sets standard object metadata.
        /// </summary>
        public V1ObjectMeta Metadata { get; set; }

        /// <summary>
        /// The spec.
        /// </summary>
        public TaskSpec Spec { get; set; }

        /// <summary>
        /// The spec.
        /// </summary>
        public TaskStatus Status { get; set; } = new TaskStatus();

        /// <summary>
        /// Verifies that the resource properties are valid.
        /// </summary>
        /// <exception cref="CustomResourceException">Thrown when the resource is not valid.</exception>
        public void Validate()
        {
            Spec?.Validate();
            Status?.Validate();
        }



        /// <summary>
        /// The node execute task specification.
        /// </summary>
        public class TaskSpec
        {
            /// <summary>
            /// Identifies the target node where the command will be executed.
            /// </summary>
            [Required]
            public string Node { get; set; }

            /// <summary>
            /// Specifies the Bash script to be executed on the target node.
            /// </summary>
            [Required]
            public string BashScript { get; set; }

            /// <summary>
            /// <para>
            /// Optionally specifies that the task should be started after a specific time.
            /// This is intended to make it easier for operators to schedule tasks across
            /// the cluster nodes while reducing the chance that all of the tasks will
            /// execute at the same time.
            /// </para>
            /// <note>
            /// This property only guarentees that the task will be started <b>after</b> the
            /// specified time, not at that time.  Task execution may happen some minutes 
            /// afterwards.
            /// </note>
            /// </summary>
            [JsonConverter(typeof(JsonNullableDateTimeConverter))]
            public DateTime? StartAfterTimestamp { get; set; }

            /// <summary>
            /// Optionally specifies the time after which the task should not be executed.
            /// This is useful for ensuring that tasks don't accumulate for some reason
            /// and then perhaps, execute all at once.
            /// </summary>
            [JsonConverter(typeof(JsonNullableDateTimeConverter))]
            public DateTime? StartBeforeTimestamp { get; set; }

            /// <summary>
            /// Specifies the maximum time the command will be allowed to execute
            /// in seconds.  Defaults to <b>5 minutes</b>.
            /// </summary>
            [Required]
            public int TimeoutSeconds { get; set; } = 300;

            /// <summary>
            /// Specifies the maximum time in seconds to retain the task after 
            /// it has been ended, for any reason.  <b>neon-cluster-operator</b> will add
            /// this to <see cref="TaskStatus.FinishTimestamp"/> to determine
            /// when it should delete the task.  This defaults to <b>10 minutes</b>.
            /// </summary>
            [Required]
            public int RetentionSeconds { get; set; } = 600;

            /// <summary>
            /// <para>
            /// Controls whether the command output is to be captured.  This defaults to <c>true</c>.
            /// </para>
            /// <note>
            /// <see cref="V1NeonNodeTask"/> is designed to capture command output as UTF-8 or
            /// ASCII text.  Binary output or other text encodings are not supported.  You
            /// should set this to <c>false</c> for commands with unsupported output or
            /// when the command output may include secrets.
            /// </note>
            /// </summary>
            [Required]
            public bool CaptureOutput { get; set; } = true;

            /// <summary>
            /// Verifies that the specification properties are valid.
            /// </summary>
            /// <exception cref="CustomResourceException">Thrown when the resource is not valid.</exception>
            public void Validate()
            {
                var specPrefix = $"{nameof(V1NeonNodeTask)}.Spec";

                if (string.IsNullOrEmpty(BashScript))
                {
                    throw new CustomResourceException($"[{specPrefix}.{nameof(BashScript)}]: cannot be NULL or empty.");
                }

                if (TimeoutSeconds <= 0)
                {
                    throw new CustomResourceException($"[{specPrefix}.{nameof(TimeoutSeconds)}={TimeoutSeconds}]: Must be greater than zero.");
                }

                if (RetentionSeconds < 0)
                {
                    throw new CustomResourceException($"[{specPrefix}.{nameof(RetentionSeconds)}={RetentionSeconds}]: Cannot be negative.");
                }
            }
        }

        /// <summary>
        /// The node execute task status.
        /// </summary>
        public class TaskStatus
        {
            /// <summary>
            /// The globally unique ID of the <b>neon-node-agent</b> instance that executed
            /// the command.  This is used to detect tasks that started executing but didn't
            /// finish before node agent crashed or was otherwise terminated, providing a way
            /// for the next node-agent to clean things up.
            /// </summary>
            public string AgentId { get; set; }

            /// <summary>
            /// Indicates the current task phase.  This defaults to <see cref="Phase.New"/>.
            /// </summary>
            [JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumMemberConverter))]
            public Phase Phase { get; set; } = Phase.New;

            /// <summary>
            /// Indicates when the task started executing. 
            /// </summary>
            [JsonConverter(typeof(JsonNullableDateTimeConverter))]
            public DateTime? StartTimestamp { get; set; }

            /// <summary>
            /// Indicates when the task finished executing.
            /// </summary>
            [JsonConverter(typeof(JsonNullableDateTimeConverter))]
            public DateTime? FinishTimestamp { get; set; }

            /// <summary>
            /// Set to the task execution time.
            /// </summary>
            public int RuntimeSeconds { get; set; }

            /// <summary>
            /// The command line invoked for the task.  This is used for detecting orphaned tasks.
            /// </summary>
            public string CommandLine { get; set; }

            /// <summary>
            /// Set to a UUID identifying the task execution.  This will be used to name the Bash
            /// script when persisted to the host node as well as to help identify the process
            /// when it's running.
            /// </summary>
            public string RunId { get; set; }

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
    }
}
