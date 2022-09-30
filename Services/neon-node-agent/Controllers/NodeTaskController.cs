//-----------------------------------------------------------------------------
// FILE:	    NodeTaskController.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Operator;
using Neon.Kube.ResourceDefinitions;
using Neon.Retry;
using Neon.Tasks;
using Neon.Time;

using k8s;
using k8s.Models;

using KubeOps.Operator.Controller;
using KubeOps.Operator.Finalizer;
using KubeOps.Operator.Rbac;

using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Prometheus;

namespace NeonNodeAgent
{
    /// <summary>
    /// Manages <see cref="V1NeonNodeTask"/> command execution on cluster nodes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This controller relies on a lease named like <b>neon-node-agent.nodetask-NODENAME</b> where <b>NODENAME</b>
    /// is the name of the node where the <b>neon-node-agent</b> operator is running.  This lease will be
    /// persisted in the <see cref="KubeNamespace.NeonSystem"/> namespace and will be used to
    /// elect a leader for the node in case there happens to be two agents running on the same
    /// node for some reason.
    /// </para>
    /// <note>
    /// This controller provides limited functionality when running on Windows to facilitate debugging.
    /// Node tasks on the host node will be simulated in this case by simply returning a zero exit code
    /// and empty output and error streams.
    /// </note>
    /// </remarks>
    [EntityRbac(typeof(V1NeonNodeTask), Verbs = RbacVerb.Get | RbacVerb.List | RbacVerb.Patch | RbacVerb.Watch | RbacVerb.Update)]
    public class NodeTaskController : IOperatorController<V1NeonNodeTask>
    {
        //---------------------------------------------------------------------
        // Static members

        private static readonly ILogger                                     logger = TelemetryHub.CreateLogger<NodeTaskController>();
        private static ResourceManager<V1NeonNodeTask, NodeTaskController>  resourceManager;

        // Paths to relevant folders in the host file system.

        private static readonly string      hostNeonRunFolder;
        private static readonly string      hostNeonTasksFolder;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static NodeTaskController()
        {
            hostNeonRunFolder   = Path.Combine(Node.HostMount, KubeNodeFolder.NeonRun.Substring(1));
            hostNeonTasksFolder = Path.Combine(hostNeonRunFolder, "node-tasks");
        }

        /// <summary>
        /// Starts the controller.
        /// </summary>
        /// <param name="k8s">The <see cref="IKubernetes"/> client to use.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task StartAsync(IKubernetes k8s)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));

            if (NeonHelper.IsLinux)
            {
                // Ensure that the [/var/run/neonkube/node-tasks] folder exists on the node.

                var scriptPath = Path.Combine(Node.HostMount, $"tmp/node-agent-folder-{NeonHelper.CreateBase36Uuid()}.sh");
                var script      =
$@"#!/bin/bash

set -euo pipefail

# Ensure that the nodetask runtime folders exist and have the correct permissions.

if [ ! -d {hostNeonRunFolder} ]; then

mkdir -p {hostNeonRunFolder}
chmod 700 {hostNeonRunFolder}
fi

if [ ! -d {hostNeonTasksFolder} ]; then

mkdir -p {hostNeonTasksFolder}
chmod 700 {hostNeonTasksFolder}
fi

# Remove this script.

rm $0
";
                File.WriteAllText(scriptPath, NeonHelper.ToLinuxLineEndings(script));
                try
                {
                    (await Node.BashExecuteCaptureAsync(scriptPath)).EnsureSuccess();
                }
                finally
                {
                    NeonHelper.DeleteFile(scriptPath);
                }
            }

            // Load the configuration settings.

            var leaderConfig = 
                new LeaderElectionConfig(
                    k8s,
                    @namespace:       KubeNamespace.NeonSystem,
                    leaseName:        $"{Program.Service.Name}.nodetask-{Node.Name}",
                    identity:         Pod.Name,
                    promotionCounter: Metrics.CreateCounter($"{Program.Service.MetricsPrefix}nodetask_promoted", "Leader promotions"),
                    demotionCounter:  Metrics.CreateCounter($"{Program.Service.MetricsPrefix}nodetask_demoted", "Leader demotions"),
                    newLeaderCounter: Metrics.CreateCounter($"{Program.Service.MetricsPrefix}nodetask_newLeader", "Leadership changes"));

            var options = new ResourceManagerOptions()
            {
                IdleInterval             = Program.Service.Environment.Get("NODETASK_IDLE_INTERVAL", TimeSpan.FromSeconds(60)),
                ErrorMinRequeueInterval  = Program.Service.Environment.Get("NODETASK_ERROR_MIN_REQUEUE_INTERVAL", TimeSpan.FromSeconds(15)),
                ErrorMaxRequeueInterval    = Program.Service.Environment.Get("NODETASK_ERROR_MAX_REQUEUE_INTERVAL", TimeSpan.FromSeconds(60)),
                IdleCounter              = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}nodetask_idle", "IDLE events processed."),
                ReconcileCounter         = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}nodetask_reconcile", "RECONCILE events processed."),
                DeleteCounter            = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}nodetask_delete", "DELETED events processed."),
                IdleErrorCounter         = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}nodetask_idle_error", "Failed NodeTask IDLE event processing."),
                ReconcileErrorCounter    = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}nodetask_reconcile_error", "Failed NodeTask RECONCILE event processing."),
                DeleteErrorCounter       = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}nodetask_delete_error", "Failed NodeTask DELETE event processing."),
                StatusModifyErrorCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}nodetask_statusmodify_error", "Failed NodeTask STATUS-MODIFY events processing.")
            };

            resourceManager = new ResourceManager<V1NeonNodeTask, NodeTaskController>(
                k8s,
                options:      options,
                filter:       NodeTaskFilter,
                leaderConfig: leaderConfig);

            await resourceManager.StartAsync();
        }

        /// <summary>
        /// Selects only tasks assigned to the current node to be handled by the resource manager.
        /// </summary>
        /// <param name="task">The task being filtered.</param>
        /// <returns><b>true</b> if the task is assigned to the current node.</returns>
        private static bool NodeTaskFilter(V1NeonNodeTask task)
        {
            Covenant.Requires<ArgumentNullException>(task != null, nameof(task));

            // Handle all tasks when debugging.

            if (!NeonHelper.IsLinux)
            {
                return true;
            }

            // ...otherwise, just for the tasks assigned to the host node.

            return task.Spec.Node.Equals(Node.Name, StringComparison.InvariantCultureIgnoreCase);
        }

        //---------------------------------------------------------------------
        // Instance members

        private readonly IKubernetes k8s;

        /// <summary>
        /// Constructor.
        /// </summary>
        public NodeTaskController(IKubernetes k8s)
        {
            Covenant.Requires(k8s != null, nameof(k8s));
            Covenant.Requires<InvalidOperationException>(resourceManager != null, $"[{nameof(NodeTaskController)}] must be started before KubeOps.");

            this.k8s = k8s;
        }

        /// <inheritdoc/>
        public async Task IdleAsync()
        {
            logger.LogInformationEx("[IDLE]");

            // Handle execution of scheduled tasks.

            // $todo(jefflill):
            //
            // I'm implementing this here because even though this would be better
            // implemented via requeued events.  Unfortunately, we haven't implemented
            // that yet.

            var utcNow = DateTime.UtcNow;

            foreach (var scheduledTask in (await k8s.ListClusterCustomObjectAsync<V1NeonNodeTask>()).Items
                .Where(task => NodeTaskFilter(task))
                .Where(task => task.Status != null && task.Status.Phase == V1NeonNodeTask.Phase.Pending)
                .Where(task => !task.Spec.StartAfterTimestamp.HasValue || task.Spec.StartAfterTimestamp <= utcNow)
                .Where(task => !task.Spec.StartBeforeTimestamp.HasValue || task.Spec.StartBeforeTimestamp < utcNow)
                .OrderByDescending(task => task.Metadata.CreationTimestamp))
            {
                await ExecuteTaskAsync(scheduledTask);
            }

            // Manage tasks by deleting finished tasks after their retention period,
            // detecting and deleting orphanded tasks, as well as detecting tardy tasks
            // that have missed their scheduling window.

            await CleanupTasksAsync();
        }

        /// <inheritdoc/>
        public async Task<ResourceControllerResult> ReconcileAsync(V1NeonNodeTask resource)
        {
            // Ignore all events when the controller hasn't been started.

            if (resourceManager == null)
            {
                return null;
            }

            var name = resource.Name();

            logger.LogInformationEx(() => $"RECONCILED: {name}");

            // We have a new node task targeting the host node:
            //
            //      1. Ensure that it's valid, delete if bad
            //      2. Add a status property as necessary
            //      3. Remove the task if it's been retained long enough
            //      4. Execute the task if it's pending

            var nodeTask = resource;

            // Verify that task is valid.

            try
            {
                nodeTask.Validate();
            }
            catch (Exception e)
            {
                logger.LogWarningEx(e, () => $"Invalid NodeTask: [{name}]");
                logger.LogWarningEx(() => $"Deleting invalid NodeTask: [{name}]");
                await k8s.DeleteClusterCustomObjectAsync(nodeTask);

                return null;
            }

            // For new tasks, update the status to PENDING and also add the
            // node's owner reference to the object.
                        
            if (nodeTask.Status.Phase == V1NeonNodeTask.Phase.New)
            {
                var patch = OperatorHelper.CreatePatch<V1NeonNodeTask>();

                patch.Replace(path => path.Status, new V1NeonNodeTask.TaskStatus());
                patch.Replace(path => path.Status.Phase, V1NeonNodeTask.Phase.Pending);

                nodeTask = await k8s.PatchClusterCustomObjectStatusAsync<V1NeonNodeTask>(OperatorHelper.ToV1Patch<V1NeonNodeTask>(patch), nodeTask.Name());

                var nodeOwnerReference = await Node.GetOwnerReferenceAsync(k8s);

                if (nodeOwnerReference != null)
                {
                    if (nodeTask.Metadata.OwnerReferences == null)
                    {
                        nodeTask.Metadata.OwnerReferences = new List<V1OwnerReference>();
                    }

                    nodeTask.Metadata.OwnerReferences.Add(await Node.GetOwnerReferenceAsync(k8s));
                }

                nodeTask = await k8s.ReplaceClusterCustomObjectAsync<V1NeonNodeTask>(nodeTask, nodeTask.Name());
            }

            if (nodeTask.Status.FinishTimestamp.HasValue)
            {
                var retentionTime = DateTime.UtcNow - nodeTask.Status.FinishTimestamp;

                if (retentionTime >= TimeSpan.FromSeconds(nodeTask.Spec.RetentionSeconds))
                {
                    logger.LogInformationEx(() => $"NodeTask [{name}] retained for [{retentionTime}] (deleting now).");
                    await k8s.DeleteClusterCustomObjectAsync(nodeTask);

                    return null;
                }
            }

            // Execute the task if it's pending and it hasn't missed the scheduling window.

            if (nodeTask.Status.Phase == V1NeonNodeTask.Phase.Pending)
            {
                var utcNow = DateTime.UtcNow;

                // Abort if we missed the end the scheduled window.

                if (nodeTask.Spec.StartBeforeTimestamp.HasValue && nodeTask.Spec.StartBeforeTimestamp < utcNow)
                {
                    logger.LogWarningEx(() => $"Detected tardy [nodetask={nodeTask.Name()}]: task execution didn't start before [{nodeTask.Spec.StartBeforeTimestamp}].");

                    // Update the node task status to: TARDY

                    var patch = OperatorHelper.CreatePatch<V1NeonNodeTask>();

                    patch.Replace(path => path.Status.Phase, V1NeonNodeTask.Phase.Tardy);
                    patch.Replace(path => path.Status.FinishTimestamp, utcNow);
                    patch.Replace(path => path.Status.ExitCode, -1);

                    await k8s.PatchClusterCustomObjectStatusAsync<V1NeonNodeTask>(OperatorHelper.ToV1Patch<V1NeonNodeTask>(patch), nodeTask.Name());

                    return null;
                }

                // Don't start before a scheduled time.

                // $todo(jefflill):
                //
                // We should requeue the event for the remaining time here, instead of letting
                // the IDLE handler execute the delayed task.

                if (nodeTask.Status.Phase == V1NeonNodeTask.Phase.Pending)
                {
                    if (nodeTask.Spec.StartAfterTimestamp.HasValue && nodeTask.Spec.StartAfterTimestamp.Value <= utcNow)
                    {
                        await ExecuteTaskAsync(nodeTask);

                        return null;
                    }
                    else
                    {
                        return null;
                    }
                }

                await ExecuteTaskAsync(nodeTask);
            }

            return null;
        }

        /// <summary>
        /// <para>
        /// Handles the cleanup of tasks targeting the current cluster node:
        /// </para>
        /// <list type="bullet">
        /// <item>
        /// Tasks whose <see cref="V1NeonNodeTask.TaskStatus.AgentId"/> doesn't match
        /// the ID for the current agent will be marked as <see cref="V1NeonNodeTask.Phase.Orphaned"/>
        /// and the finish time will be set to now.  This sets the task up for eventual
        /// deletion.
        /// </item>
        /// <item>
        /// Tasks with a finish time that is older than <see cref="V1NeonNodeTask.TaskSpec.RetentionTime"/>
        /// will be removed.
        /// </item>
        /// <item>
        /// Scheduled tasks that missed their scheduling window will be marked as TARDY and
        /// will be retained for a while before being deleted.
        /// </item>
        /// </list>
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task CleanupTasksAsync()
        {
            var utcNow    = DateTime.UtcNow;
            var nodeTasks = (await k8s.ListClusterCustomObjectAsync<V1NeonNodeTask>()).Items
                .Where(tasks => NodeTaskFilter(tasks))
                .ToArray();

            foreach (var nodeTask in nodeTasks)
            {
                var taskName = nodeTask.Name();

                //-------------------------------------------------------------
                // Remove invalid tasks.

                try
                {
                    nodeTask.Validate();
                }
                catch (Exception e)
                {
                    logger.LogWarningEx(e, () => $"Invalid NodeTask: [{taskName}]");
                    logger.LogWarningEx(() => $"Deleting invalid NodeTask: [{taskName}]");
                    await k8s.DeleteClusterCustomObjectAsync(nodeTask);
                    continue;
                }

                if (nodeTask.Status.Phase == V1NeonNodeTask.Phase.Running)
                {
                    //---------------------------------------------------------
                    // Detect and kill orphaned tasks.

                    if (nodeTask.Status.AgentId != Node.AgentId)
                    {
                        logger.LogWarningEx(() => $"Detected orphaned [nodetask={taskName}]: task [agentID={nodeTask.Status.AgentId}] does not match operator [agentID={Node.AgentId}]");

                        // Update the node task status to: ORPHANED

                        var patch = OperatorHelper.CreatePatch<V1NeonNodeTask>();

                        patch.Replace(path => path.Status.Phase, V1NeonNodeTask.Phase.Orphaned);
                        patch.Replace(path => path.Status.FinishTimestamp, utcNow);
                        patch.Replace(path => path.Status.ExitCode, -1);

                        await k8s.PatchClusterCustomObjectStatusAsync<V1NeonNodeTask>(OperatorHelper.ToV1Patch<V1NeonNodeTask>(patch), nodeTask.Name());

                        await KillTaskAsync(nodeTask);
                        continue;
                    }

                    //---------------------------------------------------------
                    // Kill tasks that have been running for too long.

                    if (nodeTask.Status.Phase == V1NeonNodeTask.Phase.Running &&
                        utcNow - nodeTask.Status.StartTimestamp >= TimeSpan.FromSeconds(nodeTask.Spec.TimeoutSeconds))
                    {
                        logger.LogWarningEx(() => $"Execution timeout [nodetask={taskName}]: execution time exceeds [{nodeTask.Spec.TimeoutSeconds}].");
                        await KillTaskAsync(nodeTask);

                        // Update the node task status to: TIMEOUT

                        var patch = OperatorHelper.CreatePatch<V1NeonNodeTask>();

                        patch.Replace(path => path.Status.Phase, V1NeonNodeTask.Phase.Timeout);
                        patch.Replace(path => path.Status.FinishTimestamp, utcNow);
                        patch.Replace(path => path.Status.RuntimeSeconds, (int)Math.Ceiling((utcNow - nodeTask.Status.StartTimestamp.Value).TotalSeconds));
                        patch.Replace(path => path.Status.ExitCode, -1);

                        await k8s.PatchClusterCustomObjectStatusAsync<V1NeonNodeTask>(OperatorHelper.ToV1Patch<V1NeonNodeTask>(patch), nodeTask.Name());
                        continue;
                    }

                    //---------------------------------------------------------
                    // Detect that missed their scheduling window and mark them as tardy 

                    if (nodeTask.Status.Phase == V1NeonNodeTask.Phase.Pending &&
                        nodeTask.Spec.StartBeforeTimestamp.HasValue && nodeTask.Spec.StartBeforeTimestamp <= utcNow)
                    {
                        logger.LogWarningEx(() => $"Detected tardy [nodetask={taskName}]: task execution didn't start before [{nodeTask.Spec.StartBeforeTimestamp}].");

                        // Update the node task status to: TARDY

                        var patch = OperatorHelper.CreatePatch<V1NeonNodeTask>();

                        patch.Replace(path => path.Status.Phase, V1NeonNodeTask.Phase.Tardy);
                        patch.Replace(path => path.Status.FinishTimestamp, utcNow);
                        patch.Replace(path => path.Status.ExitCode, -1);

                        await k8s.PatchClusterCustomObjectStatusAsync<V1NeonNodeTask>(OperatorHelper.ToV1Patch<V1NeonNodeTask>(patch), nodeTask.Name());
                        continue;
                    }
                }
            }

            //-----------------------------------------------------------------
            // Remove tasks that have been retained long enough.

            foreach (var nodeTask in nodeTasks
                .Where(task => task.Status.Phase != V1NeonNodeTask.Phase.New && task.Status.Phase != V1NeonNodeTask.Phase.Running)
                .Where(task => (utcNow - task.Status.FinishTimestamp) >= TimeSpan.FromSeconds(task.Spec.RetentionSeconds)))
            {
                logger.LogWarningEx(() => $"[nodetask={nodeTask.Name()}]: has been retained for [{nodeTask.Spec.RetentionSeconds}] (deleting now).");
                await k8s.DeleteClusterCustomObjectAsync(nodeTask);
            }

            //-----------------------------------------------------------------
            // Remove any script folders whose node task no longer exists.

            if (NeonHelper.IsLinux)
            {
                var nodeTaskExecuteIds = new HashSet<string>();

                foreach (var nodeTask in nodeTasks.Where(task => !string.IsNullOrEmpty(task.Status.RunId)))
                {
                    nodeTaskExecuteIds.Add(nodeTask.Status.RunId);
                }

                foreach (var scriptFolderPath in Directory.GetDirectories(hostNeonTasksFolder, "*", SearchOption.TopDirectoryOnly))
                {
                    var scriptFolderName = LinuxPath.GetFileName(scriptFolderPath);

                    if (!nodeTaskExecuteIds.Contains(scriptFolderName))
                    {
                        logger.LogWarningEx(() => $"Removing node task host script folder: {scriptFolderName}");
                        NeonHelper.DeleteFolder(scriptFolderPath);
                    }
                }
            }
        }

        /// <summary>
        /// Kills the node task's process if it is running.
        /// </summary>
        /// <param name="nodeTask">The node task.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task KillTaskAsync(V1NeonNodeTask nodeTask)
        {
            Covenant.Requires<ArgumentNullException>(nodeTask != null, nameof(nodeTask));

            if (!NeonHelper.IsLinux)
            {
                return;
            }

            var taskName = nodeTask.Name();

            if (nodeTask.Status != null && nodeTask.Status.Phase != V1NeonNodeTask.Phase.Running)
            {
                return;
            }

            // Try to locate the task process by process ID and command line.  Note that
            // we can't use the process ID by itself because it possible that the process
            // ID has been recycled and is currently assigned to an entirely unrelated
            // process.
            //
            // We're going to use the [ps --pid=PROCESSID --format cmd=] command.  This
            // will return an empty line when the process doesn't exist and a single line
            // with the process command line when the process exists.

            var result = await Node.ExecuteCaptureAsync("ps", new object[] { $"--pid {nodeTask.Status.ProcessId}", "--format cmd=" });

            if (result.ExitCode == 0)
            {
                using (var reader = new StringReader(result.OutputText))
                {
                    var commandLine = reader.Lines().FirstOrDefault();

                    if (commandLine == nodeTask.Status.CommandLine)
                    {
                        // The process ID and command line match, so kill it.

                        result = await Node.ExecuteCaptureAsync("kill", new object[] { "-s", "SIGTERM", nodeTask.Status.ProcessId });

                        if (result.ExitCode != 0)
                        {
                            logger.LogWarningEx(() => $"[NodeTask: {taskName}]: Cannot kill orphaned task process [{nodeTask.Status.ProcessId}]. [exitcode={result.ExitCode}]");
                            return;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Initiates execution of a node task in the background when the task is still pending.
        /// </summary>
        /// <param name="nodeTask">The node task to be executed.</param>
        /// <param name="resources">The existing tasks.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task ExecuteTaskAsync(V1NeonNodeTask nodeTask)
        {
            Covenant.Requires<ArgumentNullException>(nodeTask != null, nameof(nodeTask));

            var taskName = nodeTask.Name();

            if (nodeTask.Status.Phase != V1NeonNodeTask.Phase.Pending)
            {
                return;
            }

            // Start and execute the command.  The trick here is that we need the
            // ID of the process launched before we can update the status.

            int? processId = null;

            // Generate the execution UUID and determine where the script will be located.

            var executionId = Guid.NewGuid().ToString("d");

            var taskFolder     = LinuxPath.Combine(hostNeonTasksFolder, executionId);
            var hostTaskFolder = LinuxPath.Combine(KubeNodeFolder.NeonRun, "node-tasks", executionId);
            var scriptPath     = LinuxPath.Combine(taskFolder, "task.sh");
            var hostScriptPath = LinuxPath.Combine(hostTaskFolder, "task.sh");

            // Prepend the script to be deployed with code that sets the special
            // environment variables.

            var deployedScript =
$@"
#------------------------------------------------------------------------------
# neon-node-task: Initialze special script variables

export NODE_ROOT={Node.HostMount}
export SCRIPT_DIR={hostTaskFolder}

#------------------------------------------------------------------------------

{nodeTask.Spec.BashScript}
";
            if (NeonHelper.IsLinux)
            {
                Directory.CreateDirectory(taskFolder);
                File.WriteAllText(scriptPath, NeonHelper.ToLinuxLineEndings(deployedScript));
            }

            // Start the command process.

            var task = (Task<ExecuteResponse>)null;

            if (!NeonHelper.IsLinux)
            {
                processId = 1234;
                
                task = Task.Run<ExecuteResponse>(
                    async () =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1));

                        return new ExecuteResponse(0);
                    });
            }
            else
            {
                try
                {
                    // This callback will be executed once the [Node.ExecuteCaptureAsync()]
                    // call has the process details.  We'll save the details, update the node task
                    // status and persist the status changes to the API server.

                    var processCallback =
                        (Process newProcess) =>
                        {
                            processId = newProcess.Id;

                            logger.LogInformationEx(() => $"Starting [nodetask={taskName}]: [command={Node.GetBashCommandLine(hostScriptPath)}] [processID={processId}]");
                        };

                    task = Node.BashExecuteCaptureAsync(
                        path:            hostScriptPath,
                        host:            true,
                        timeout:         TimeSpan.FromSeconds(nodeTask.Spec.TimeoutSeconds),
                        processCallback: processCallback);
                }
                catch (Exception e)
                {
                    // We shouldn't ever see an error here because [/bin/bash] should always
                    // exist, but we'll log something just in case.

                    logger.LogWarningEx(e);

                    var failedPatch = OperatorHelper.CreatePatch<V1NeonNodeTask>();

                    failedPatch.Replace(path => path.Status.Phase, V1NeonNodeTask.Phase.Failed);
                    failedPatch.Replace(path => path.Status.FinishTimestamp, DateTime.UtcNow);
                    failedPatch.Replace(path => path.Status.ExitCode, -1);
                    failedPatch.Replace(path => path.Status.Error, $"EXECUTE FAILED: {e.Message}");

                    await k8s.PatchClusterCustomObjectStatusAsync<V1NeonNodeTask>(OperatorHelper.ToV1Patch<V1NeonNodeTask>(failedPatch), nodeTask.Name());
                    return;
                }
            }

            // We need to wait for the [Node.BashExecuteCaptureAsync()] call above to 
            // report the process for the executed script.

            try
            {
                NeonHelper.WaitFor(() => processId != null, timeout: TimeSpan.FromSeconds(15), pollInterval: TimeSpan.FromMilliseconds(150));
            }
            catch (TimeoutException e)
            {
                // It's possible but unlikely that the update above failed for some reason.
                // We'll log this and then hope for the best.

                logger.LogWarningEx(e);
            }

            // Update the node task status to: RUNNING

            var patch = OperatorHelper.CreatePatch<V1NeonNodeTask>();

            patch.Replace(path => path.Status.Phase, V1NeonNodeTask.Phase.Running);
            patch.Replace(path => path.Status.StartTimestamp, DateTime.UtcNow);
            patch.Replace(path => path.Status.AgentId, Node.AgentId);
            patch.Replace(path => path.Status.ProcessId, processId);
            patch.Replace(path => path.Status.CommandLine, Node.GetBashCommandLine(hostScriptPath).Trim());
            patch.Replace(path => path.Status.RunId, executionId);

            nodeTask = await k8s.PatchClusterCustomObjectStatusAsync<V1NeonNodeTask>(OperatorHelper.ToV1Patch<V1NeonNodeTask>(patch), nodeTask.Name());

            // Wait for the command to complete and then update the node task status.

            try
            {
                var result  = (ExecuteResponse)null;
                var timeout = false;

                try
                {
                    result = await task;
                    logger.LogInformationEx(() => $"Finished [nodetask={taskName}]: [command={nodeTask.Status.CommandLine}] [exitcode={result.ExitCode}]");
                }
                catch (TimeoutException)
                {
                    timeout = true;

                    logger.LogWarningEx(() => $"Timeout [nodetask={taskName}]");
                }

                var utcNow = DateTime.UtcNow;

                if (nodeTask.Status.Phase == V1NeonNodeTask.Phase.Running)
                {
                    patch = OperatorHelper.CreatePatch<V1NeonNodeTask>();

                    patch.Replace(path => path.Status.FinishTimestamp, utcNow);

                    if (timeout)
                    {
                        patch.Replace(path => path.Status.Phase, V1NeonNodeTask.Phase.Timeout);
                        patch.Replace(path => path.Status.RuntimeSeconds, (int)Math.Ceiling((utcNow - nodeTask.Status.StartTimestamp.Value).TotalSeconds));
                        patch.Replace(path => path.Status.ExitCode, -1);
                    }
                    else
                    {
                        patch.Replace(path => path.Status.Phase, result.ExitCode == 0 ? V1NeonNodeTask.Phase.Success : V1NeonNodeTask.Phase.Failed);
                        patch.Replace(path => path.Status.RuntimeSeconds, (int)Math.Ceiling((utcNow - nodeTask.Status.StartTimestamp.Value).TotalSeconds));
                        patch.Replace(path => path.Status.ExitCode, result.ExitCode);

                        if (nodeTask.Spec.CaptureOutput)
                        {
                            patch.Replace(path => path.Status.Output, result.OutputText);
                            patch.Replace(path => path.Status.Error, result.ErrorText);
                        }
                    }

                    nodeTask = await k8s.PatchClusterCustomObjectStatusAsync<V1NeonNodeTask>(OperatorHelper.ToV1Patch<V1NeonNodeTask>(patch), nodeTask.Name());
                }
            }
            catch (Exception e)
            {
                logger.LogWarningEx(e);
            }
        }
    }
}
