//-----------------------------------------------------------------------------
// FILE:	    NodeTaskController.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.Extensions.Logging;

using JsonDiffPatch;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Operator;
using Neon.Kube.ResourceDefinitions;
using Neon.Retry;
using Neon.Tasks;

using k8s;
using k8s.Models;

using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Finalizer;
using KubeOps.Operator.Rbac;

using Newtonsoft.Json;
using Prometheus;

namespace NeonNodeAgent
{
    /// <summary>
    /// <para>
    /// Manages <see cref="V1NodeTask"/> resources on the Kubernetes API Server.
    /// </para>
    /// <note>
    /// This controller relies on a lease named like <b>neon-node-agent.nodetask-NODENAME</b> where <b>NODENAME</b>
    /// is the name of the node where the <b>neon-node-agent</b> operator is running.  This lease will be
    /// persisted in the <see cref="KubeNamespace.NeonSystem"/> namespace and will be used to
    /// elect a leader for the node in case there happens to be two agents running on the same
    /// node for some reason.
    /// </note>
    /// </summary>
    /// <remarks>
    /// This controller handles command executions on the local cluster node.  See
    /// <see cref="V1NodeTask"/> for more details.
    /// </remarks>
    [EntityRbac(typeof(V1NodeTask), Verbs = RbacVerb.Get | RbacVerb.List | RbacVerb.Patch | RbacVerb.Watch | RbacVerb.Update)]
    public class NodeTaskController : IResourceController<V1NodeTask>
    {
        //---------------------------------------------------------------------
        // Static members

        private static readonly INeonLogger                             log = Program.Service.LogManager.GetLogger<NodeTaskController>();
        private static ResourceManager<V1NodeTask, NodeTaskController>  resourceManager;

        // Paths to relevant folders in the host file system.

        private static readonly string hostNeonRunFolder;
        private static readonly string hostAgentFolder;
        private static readonly string hostAgentTasksFolder;

        // Configuration settings

        private static TimeSpan     reconciledNoChangeInterval;
        private static TimeSpan     errorMinRequeueInterval;
        private static TimeSpan     errorMaxRequeueInterval;

        // Metrics counters

        private static readonly Counter reconciledReceivedCounter      = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}nodetask_reconciled_received", "Received NodeTask reconcile events.");
        private static readonly Counter deletedReceivedCounter         = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}nodetask_deleted_received", "Received NodeTask deleted events.");
        private static readonly Counter statusModifiedReceivedCounter  = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}nodetask_statusmodified_received", "Received NodeTask status-modified events.");

        private static readonly Counter reconciledProcessedCounter     = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}nodetask_reconciled_changes", "Processed NodeTask reconcile events due to change.");
        private static readonly Counter deletedProcessedCounter        = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}nodetask_deleted_changes", "Processed NodeTask deleted events due to change.");
        private static readonly Counter statusModifiedProcessedCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}nodetask_statusmodified_changes", "Processed NodeTask status-modified events due to change.");

        private static readonly Counter reconciledErrorCounter         = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}nodetask_reconciled_error", "Failed NodeTask reconcile event processing.");
        private static readonly Counter deletedErrorCounter            = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}nodetask_deleted_error", "Failed NodeTask deleted event processing.");
        private static readonly Counter statusModifiedErrorCounter     = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}nodetask_statusmodified_error", "Failed NodeTask status-modified events processing.");

        private static readonly Counter promotionCounter               = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}nodetask_promoted", "Leader promotions");
        private static readonly Counter demotedCounter                 = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}nodetask_demoted", "Leader demotions");
        private static readonly Counter newLeaderCounter               = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}nodetask_newLeader", "Leadership changes");

        /// <summary>
        /// Static constructor.
        /// </summary>
        static NodeTaskController()
        {
            hostNeonRunFolder    = Path.Combine(Node.HostMount, KubeNodeFolder.NeonRun.Substring(1));
            hostAgentFolder      = Path.Combine(hostNeonRunFolder, "node-agent");
            hostAgentTasksFolder = Path.Combine(hostAgentFolder, "node-tasks");
        }

        /// <summary>
        /// Starts the controller.
        /// </summary>
        /// <param name="k8s">The <see cref="IKubernetes"/> client to use.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task StartAsync(IKubernetes k8s)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));

            // Ensure that the [/var/run/neonkube/neon-node-agent/nodetask] folder exists on the node.

            var scriptPath = Path.Combine(Node.HostMount, $"tmp/node-agent-folder-{NeonHelper.CreateBase36Guid()}.sh");
            var script     =
$@"#!/bin/bash

set -euo pipefail

# Ensure that the node runtime folders exist and have the correct permissions.

if [ ! -d {hostNeonRunFolder} ]; then

mkdir -p {hostNeonRunFolder}
chmod 700 {hostNeonRunFolder}
fi

if [ ! -d {hostAgentFolder} ]; then

mkdir -p {hostAgentFolder}
chmod 700 {hostAgentFolder}
fi

if [ ! -d {hostAgentTasksFolder} ]; then

mkdir -p {hostAgentTasksFolder}
chmod 700 {hostAgentTasksFolder}
fi

# Remove this script.

rm $0
";
            File.WriteAllText(scriptPath, NeonHelper.ToLinuxLineEndings(script));
            try
            {
                Node.BashExecuteCapture(scriptPath).EnsureSuccess();
            }
            finally
            {
                NeonHelper.DeleteFile(scriptPath);
            }

            // Load the configuration settings.

            reconciledNoChangeInterval = Program.Service.Environment.Get("NODETASK_RECONCILED_NOCHANGE_INTERVAL", TimeSpan.FromMinutes(1));
            errorMinRequeueInterval    = Program.Service.Environment.Get("NODETASK_ERROR_MIN_REQUEUE_INTERVAL", TimeSpan.FromSeconds(15));
            errorMaxRequeueInterval    = Program.Service.Environment.Get("NODETASK_ERROR_MAX_REQUEUE_INTERVAL", TimeSpan.FromMinutes(10));

            var leaderConfig = 
                new LeaderElectionConfig(
                    k8s,
                    @namespace: KubeNamespace.NeonSystem,
                    leaseName:        $"{Program.Service.Name}.nodetask-{Node.Name}",
                    identity:         Pod.Name,
                    promotionCounter: promotionCounter,
                    demotionCounter:  demotedCounter,
                    newLeaderCounter: newLeaderCounter);

            resourceManager = new ResourceManager<V1NodeTask, NodeTaskController>(
                k8s,
                filter:                    NodeTaskFilter,
                leaderConfig:              leaderConfig,
                reconcileNoChangeInterval: reconciledNoChangeInterval,
                errorMinRequeueInterval:   errorMinRequeueInterval,
                errorMaxRequeueInterval:   errorMaxRequeueInterval);

            await resourceManager.StartAsync();
        }

        /// <summary>
        /// Selects only tasks assigned to the current node to be handled by the resource manager.
        /// </summary>
        /// <param name="task">The task being filtered.</param>
        /// <returns><b>true</b> if the task is assigned to the current node.</returns>
        private static bool NodeTaskFilter(V1NodeTask task)
        {
            Covenant.Requires<ArgumentNullException>(task != null, nameof(task));

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

            this.k8s = k8s;
        }

        /// <summary>
        /// Called for each existing custom resource when the controller starts so that the controller
        /// can maintain the status of all resources and then afterwards, this will be called whenever
        /// a resource is added or has a non-status update.
        /// </summary>
        /// <param name="task">The new entity or <c>null</c> when nothing has changed.</param>
        /// <returns>The controller result.</returns>
        public async Task<ResourceControllerResult> ReconcileAsync(V1NodeTask task)
        {
            reconciledReceivedCounter.Inc();

            await resourceManager.ReconciledAsync(task,
                async (name, resources) =>
                {
                    log.LogInfo($"RECONCILED: {name ?? "[NO-CHANGE]"}");
                    reconciledProcessedCounter.Inc();
log.LogDebug($"*** RECONCILE: 0");

                    if (name == null)
                    {
                        // This is a NO-CHANGE event: we'll use this as a signal to do any cleanup.

                        // Execute the youngest node task that's pending (if there is one).

log.LogDebug($"*** RECONCILE: 1");
                        await CleanupTasksAsync(resources);
log.LogDebug($"*** RECONCILE: 2");
                    }
                    else
                    {
log.LogDebug($"*** RECONCILE: 3");
                        // We have a new node task targeting this node:
                        //
                        //      1. Ensure that it's valid, delete if bad
                        //      2. Add a status property as necessary
                        //      3. Remove the task if it's been retained long enough
                        //      4. Execute the task if it's pending

                        var nodeTask = resources[name];
var statusIsNull = nodeTask.Status == null ? "STATUS IS NULL" : "STATUS != NULL";
log.LogDebug($"*** RECONCILE: 4A: {statusIsNull}");
log.LogDebug($"*** RECONCILE: 4B: state={nodeTask.Status?.Phase}");

                        // Verify that task is well structured.

                        try
                        {
                            nodeTask.Validate();
log.LogDebug($"*** RECONCILE: 5");
                        }
                        catch (Exception e)
                        {
                            log.LogWarn($"Invalid NodeTask: [{name}]", e);
                            log.LogWarn($"Deleting invalid NodeTask: [{name}]");
                            await k8s.DeleteClusterCustomObjectAsync<V1NodeTask>(nodeTask.Name());
log.LogDebug($"*** RECONCILE: 6");

                            return null;
                        }

                        // For new tasks, update the status to: PENDING
                        
log.LogDebug($"*** RECONCILE: 7");
                        if (nodeTask.Status.Phase == V1NodeTask.Phase.New)
                        {
log.LogDebug($"*** RECONCILE: 8A");
                            //nodeTask.Status.State = V1NodeTask.NodeTaskState.Pending;

log.LogDebug($"*** RECONCILE: 8B");
                            var patch = OperatorHelper.CreatePatch<V1NodeTask>();

                            patch.Replace(path => path.Status, new V1NodeTask.TaskStatus());
                            patch.Replace(path => path.Status.Phase, V1NodeTask.Phase.Pending);

                            nodeTask = await k8s.PatchClusterCustomObjectStatusAsync<V1NodeTask>(OperatorHelper.ToV1Patch<V1NodeTask>(patch), nodeTask.Name());
                            log.LogDebug($"*** RECONCILE: 9");
                        }
log.LogDebug($"*** RECONCILE: 10");

                        if (nodeTask.Status.FinishTimestamp.HasValue)
                        {
log.LogDebug($"*** RECONCILE: 11");
                            var retentionTime = DateTime.UtcNow - nodeTask.Status.FinishTimestamp;

                            if (retentionTime >= TimeSpan.FromSeconds(nodeTask.Spec.RetainSeconds))
                            {
log.LogDebug($"*** RECONCILE: 12");
                                log.LogInfo($"NodeTask [{name}] retained for [{retentionTime}] (deleting now).");
                                await k8s.DeleteClusterCustomObjectAsync<V1NodeTask>(nodeTask.Name());
log.LogDebug($"*** RECONCILE: 13");

                                return null;
                            }
                        }

                        // Execute the task if it's pending.

log.LogDebug($"*** RECONCILE: 14: phase = {nodeTask.Status.Phase}");
                        if (nodeTask.Status.Phase == V1NodeTask.Phase.Pending)
                        {
log.LogDebug($"*** RECONCILE: 15");
                            await ExecuteTaskAsync(nodeTask);
log.LogDebug($"*** RECONCILE: 16");
                        }
                    }

                    return null;
                },
                errorCounter: reconciledErrorCounter);

            return ResourceControllerResult.RequeueEvent(errorMinRequeueInterval);
        }

        /// <summary>
        /// Called when a custom resource is removed from the API Server.
        /// </summary>
        /// <param name="task">The deleted entity.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task DeletedAsync(V1NodeTask task)
        {
            Covenant.Requires<ArgumentNullException>(task != null, nameof(task));

            deletedReceivedCounter.Inc();

            await resourceManager.DeletedAsync(task,
                async (name, resources) =>
                {
                    log.LogInfo($"DELETED: {name}");
                    deletedProcessedCounter.Inc();

                    // This is a NOP.

                    return await Task.FromResult<ResourceControllerResult>(null);
                },
                errorCounter: deletedErrorCounter);
        }

        /// <summary>
        /// Called when a custom resource's status has been modified.
        /// </summary>
        /// <param name="task">The updated entity.</param>
        /// <returns>The controller result.</returns>
        public async Task<ResourceControllerResult> StatusModifiedAsync(V1NodeTask task)
        {
            Covenant.Requires<ArgumentNullException>(task != null, nameof(task));

            statusModifiedReceivedCounter.Inc();

            await resourceManager.DeletedAsync(task,
                async (name, resources) =>
                {
                    log.LogInfo($"STATUS-MODIFIED: {name}");
                    statusModifiedProcessedCounter.Inc();

                    // This is a NOP.

                    return await Task.FromResult<ResourceControllerResult>(null);
                },
                errorCounter: statusModifiedErrorCounter);

            return ResourceControllerResult.RequeueEvent(errorMinRequeueInterval);
        }

        /// <summary>
        /// <para>
        /// Handles the cleanup of tasks targeting the current cluster node:
        /// </para>
        /// <list type="bullet">
        /// <item>
        /// Tasks whose <see cref="V1NodeTask.TaskStatus.AgentId"/> doesn't match
        /// the ID for the current agent will be marked as <see cref="V1NodeTask.Phase.Orphaned"/>
        /// and the finish time will be set to now.  This sets the task up for eventual
        /// deletion.
        /// </item>
        /// <item>
        /// Tasks with a finish time that is older than <see cref="V1NodeTask.TaskSpec.RetainSeconds"/>
        /// will be removed.
        /// </item>
        /// </list>
        /// </summary>
        /// <param name="resources">The current known tasks.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task CleanupTasksAsync(IReadOnlyDictionary<string, V1NodeTask> resources)
        {
            Covenant.Requires<ArgumentNullException>(resources != null, nameof(resources));

            var tasks = await resourceManager.CloneResourcesAsync(resources);

            //-----------------------------------------------------------------
            // Terminate orphaned tasks as well as any tasks that have been executing past their timeout.

            foreach (var nodeTask in tasks.Values)
            {
                var taskName = nodeTask.Name();
                var utcNow   = DateTime.UtcNow;

                // Remove invalid tasks.

                try
                {
                    nodeTask.Validate();
                }
                catch (Exception e)
                {
                    log.LogWarn($"Invalid NodeTask: [{taskName}]", e);
                    log.LogWarn($"Deleting invalid NodeTask: [{taskName}]");
                    await k8s.DeleteClusterCustomObjectAsync<V1NodeTask>(nodeTask.Name());
                    continue;
                }

                if (nodeTask.Status.Phase == V1NodeTask.Phase.Running)
                {
                    // Detect and kill orphaned tasks.

                    if (nodeTask.Status.AgentId != Node.AgentId)
                    {
                        log.LogWarn($"Detected orphaned [nodetask={taskName}]: task [agentID={nodeTask.Status.AgentId}] does not match operator [agentID={Node.AgentId}]");
                        await KillTaskAsync(nodeTask);

                        // Update the node task status to: ORPHANED

                        var patch = OperatorHelper.CreatePatch<V1NodeTask>();

                        patch.Replace(path => path.Status.Phase, V1NodeTask.Phase.Orphaned);
                        patch.Replace(path => path.Status.FinishTimestamp, utcNow);
                        patch.Replace(path => path.Status.ExitCode, -1);

                        await k8s.PatchClusterCustomObjectStatusAsync<V1NodeTask>(OperatorHelper.ToV1Patch<V1NodeTask>(patch), nodeTask.Name());
                        continue;
                    }

                    // Kill tasks that have been running for too long.

                    if (utcNow - nodeTask.Status.StartTimestamp >= TimeSpan.FromSeconds(nodeTask.Spec.TimeoutSeconds))
                    {
                        log.LogWarn($"Detected timeout [nodetask={taskName}]: execution time exceeds [{nodeTask.Spec.TimeoutSeconds}].");
                        await KillTaskAsync(nodeTask);

                        // Update the node task status to: TIMEOUT

                        var patch = OperatorHelper.CreatePatch<V1NodeTask>();

                        patch.Replace(path => path.Status.Phase, V1NodeTask.Phase.Timeout);
                        patch.Replace(path => path.Status.FinishTimestamp, utcNow);
                        patch.Replace(path => path.Status.ExecutionSeconds, (utcNow - nodeTask.Status.StartTimestamp.Value).TotalSeconds);
                        patch.Replace(path => path.Status.ExitCode, -1);

                        await k8s.PatchClusterCustomObjectStatusAsync<V1NodeTask>(OperatorHelper.ToV1Patch<V1NodeTask>(patch), nodeTask.Name());
                        continue;
                    }
                }
            }

            //-----------------------------------------------------------------
            // Remove any script folders whose node task no longer exists.

            var nodeTaskExecuteIds = new HashSet<string>();

            foreach (var nodeTask in tasks.Values.Where(task => !string.IsNullOrEmpty(task.Status.ExecutionId)))
            {
                nodeTaskExecuteIds.Add(nodeTask.Status.ExecutionId);
            }

            foreach (var scriptFolderPath in Directory.GetDirectories(hostAgentTasksFolder, "*", SearchOption.TopDirectoryOnly))
            {
                var scriptFolderName = LinuxPath.GetFileName(scriptFolderPath);

                if (!nodeTaskExecuteIds.Contains(scriptFolderName))
                {
                    log.LogWarn($"Removing node task host script folder: {scriptFolderName}");
                    NeonHelper.DeleteFolder(scriptFolderPath);
                }
            }
        }

        /// <summary>
        /// Kills the node task's process if it is running.
        /// </summary>
        /// <param name="nodeTask">The node task.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task KillTaskAsync(V1NodeTask nodeTask)
        {
            Covenant.Requires<ArgumentNullException>(nodeTask != null, nameof(nodeTask));

            var taskName = nodeTask.Name();

            if (nodeTask.Status.Phase != V1NodeTask.Phase.Running)
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

            var result = await Node.ExecuteCaptureAsync("ps", new object[] { $"--pid={nodeTask.Status.ProcessId}", "--format cmd=" });
                
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
                            log.LogWarn($"[NodeTask: {taskName}]: Cannot kill orphaned task process [{nodeTask.Status.ProcessId}]. [exitcode={result.ExitCode}]");
                            return;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Initiates execution of a node task in the background when the task is still pending.
        /// </summary>
        /// <param name="nodeTask">The node task.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task ExecuteTaskAsync(V1NodeTask nodeTask)
        {
            Covenant.Requires<ArgumentNullException>(nodeTask != null, nameof(nodeTask));

log.LogDebug($"*** EXECUTE: 0");
            var taskName = nodeTask.Name();

            if (nodeTask.Status.Phase != V1NodeTask.Phase.Pending)
            {
log.LogDebug($"*** EXECUTE: 1");
                return;
            }
log.LogDebug($"*** EXECUTE: 2");

            // Start and execute the command.  The trick here is that we need the
            // ID of the process launched before we can update the status.

            var process = (Process)null;

            // Generate the execution UUID and determine where the script will be located.

            var executionId = NeonHelper.CreateBase36Guid();
            var taskFolder  = LinuxPath.Combine(hostAgentTasksFolder, executionId);
            var scriptPath  = LinuxPath.Combine(taskFolder, "task.sh");
log.LogDebug($"*** EXECUTE: 3");

            // Prepend the script to be deployed with code that sets the special
            // environment variables.

            var deployedScript =
$@"
#------------------------------------------------------------------------------
# neon-node-task: Initialze special script variables

export NODE_ROOT={Node.HostMount}
export SCRIPT_DIR={taskFolder}

#------------------------------------------------------------------------------

{nodeTask.Spec.BashScript}
";
            Directory.CreateDirectory(taskFolder);
            File.WriteAllText(scriptPath, NeonHelper.ToLinuxLineEndings(deployedScript));

            // Start the command process.

            var task = (Task<ExecuteResponse>)null;

            try
            {
                // This callback will be executed once the [Node.ExecuteCaptureAsync()]
                // call has the process details.  We'll save the details, update the node task
                // status and persist the status changes to the API server.

log.LogDebug($"*** EXECUTE: 5");
                var processCallback =
                    (Process newProcess) =>
                    {
log.LogDebug($"*** EXECUTE: 6A: processID is NULL: {newProcess == null}");
log.LogDebug($"*** EXECUTE: 6B: processID = {newProcess.Id}");
                        process = newProcess;

log.LogDebug($"*** EXECUTE: 7");
                        log.LogInfo($"Starting [nodetask={taskName}]: [command={nodeTask.Status.CommandLine}] [processID={process.Id}]");
                    };

log.LogDebug($"*** EXECUTE: 9");
                task = Node.BashExecuteCaptureAsync(
                    path:            scriptPath, 
                    timeout:         TimeSpan.FromSeconds(nodeTask.Spec.TimeoutSeconds), 
                    processCallback: processCallback);
log.LogDebug($"*** EXECUTE: 10");
            }
            catch (Exception e)
            {
log.LogDebug($"*** EXECUTE: 11A");
                // We shouldn't ever see an error here because [/bin/bash] should always
                // exist, but we'll log something just in case.

                log.LogWarn(e);

                var failedPatch = OperatorHelper.CreatePatch<V1NodeTask>();

                failedPatch.Replace(path => path.Status.Phase, V1NodeTask.Phase.Failed);
                failedPatch.Replace(path => path.Status.FinishTimestamp, DateTime.UtcNow);
                failedPatch.Replace(path => path.Status.ExitCode, -1);
                failedPatch.Replace(path => path.Status.Error, $"EXECUTE FAILED: {e.Message}");

log.LogDebug($"*** EXECUTE: 11B");
                await k8s.PatchClusterCustomObjectStatusAsync<V1NodeTask>(OperatorHelper.ToV1Patch<V1NodeTask>(failedPatch), nodeTask.Name());
log.LogDebug($"*** EXECUTE: 12");
                return;
            }

            // We need to wait for the [Node.BashExecuteCaptureAsync()] call above to 
            // report the process for the executed script.

            try
            {
                NeonHelper.WaitFor(() => process != null, timeout: TimeSpan.FromSeconds(15), pollInterval: TimeSpan.FromMilliseconds(150));
            }
            catch (TimeoutException e)
            {
                // It's possible but unlikely that the update above failed for some reason.
                // We'll log this and then hope for the best.

                log.LogWarn(e);
            }
log.LogDebug($"*** EXECUTE: 13");

            // Update the node task status to: RUNNING

            var patch = OperatorHelper.CreatePatch<V1NodeTask>();

            patch.Replace(path => path.Status.Phase, V1NodeTask.Phase.Running);
            patch.Replace(path => path.Status.StartTimestamp, DateTime.UtcNow);
            patch.Replace(path => path.Status.AgentId, Node.AgentId);
            patch.Replace(path => path.Status.CommandLine, Node.GetBashCommandLine(scriptPath));
            patch.Replace(path => path.Status.ExecutionId, executionId);

            nodeTask = await k8s.PatchClusterCustomObjectStatusAsync<V1NodeTask>(OperatorHelper.ToV1Patch<V1NodeTask>(patch), nodeTask.Name());

            // Wait for the command to complete and the update the node task status.

            try
            {
                var result  = (ExecuteResponse)null;
                var timeout = false;

                try
                {
                    result = await task;
log.LogDebug($"*** EXECUTE: 14");

                    log.LogInfo($"Finished [nodetask={taskName}]: [command={nodeTask.Status.CommandLine}] [exitcode={result.ExitCode}]");
                }
                catch (TimeoutException)
                {
log.LogDebug($"*** EXECUTE: 15");
                    timeout = true;

                    log.LogWarn($"Timeout [nodetask={taskName}]");
                }

                if (nodeTask.Status.Phase == V1NodeTask.Phase.Running)
                {
log.LogDebug($"*** EXECUTE: 16");
                    nodeTask.Status.FinishTimestamp  = DateTime.UtcNow;
                    nodeTask.Status.ExecutionSeconds = (nodeTask.Status.FinishTimestamp.Value - nodeTask.Status.StartTimestamp.Value).TotalSeconds;

                    if (timeout)
                    {
log.LogDebug($"*** EXECUTE: 17");
                        nodeTask.Status.Phase    = V1NodeTask.Phase.Timeout;
                        nodeTask.Status.ExitCode = -1;
                    }
                    else
                    {
log.LogDebug($"*** EXECUTE: 18");
                        nodeTask.Status.Phase    = result.ExitCode == 0 ? V1NodeTask.Phase.Finished : V1NodeTask.Phase.Failed;
                        nodeTask.Status.ExitCode = result.ExitCode;

                        if (nodeTask.Spec.CaptureOutput)
                        {
                            nodeTask.Status.Output = result.OutputText;
                            nodeTask.Status.Error  = result.ErrorText;
                        }
                    }

log.LogDebug($"*** EXECUTE: 19");
                    patch = OperatorHelper.CreatePatch<V1NodeTask>();

                    patch.Replace(path => path.Status.FinishTimestamp, DateTime.UtcNow);

                    if (timeout)
                    {
                        patch.Replace(path => path.Status.Phase, V1NodeTask.Phase.Timeout);
                        patch.Replace(path => path.Status.ExitCode, -1);
                    }
                    else if (result.ExitCode != 0)
                    {
                        patch.Replace(path => path.Status.Phase, V1NodeTask.Phase.Failed);
                        patch.Replace(path => path.Status.ExecutionSeconds, (nodeTask.Status.FinishTimestamp.Value - nodeTask.Status.StartTimestamp.Value).TotalSeconds);
                        patch.Replace(path => path.Status.ExitCode, result.ExitCode);
                    }
                    else
                    {
                        patch.Replace(path => path.Status.Phase, V1NodeTask.Phase.Finished);
                        patch.Replace(path => path.Status.ExecutionSeconds, (nodeTask.Status.FinishTimestamp.Value - nodeTask.Status.StartTimestamp.Value).TotalSeconds);

                        if (nodeTask.Spec.CaptureOutput)
                        {
                            patch.Replace(path => path.Status.Output, result.OutputText);
                            patch.Replace(path => path.Status.Error, result.ErrorText);
                            patch.Replace(path => path.Status.ExitCode, result.ExitCode);
                        }
                    }

log.LogDebug($"*** EXECUTE: 20");
                    nodeTask = await k8s.PatchClusterCustomObjectStatusAsync<V1NodeTask>(OperatorHelper.ToV1Patch<V1NodeTask>(patch), nodeTask.Name());
log.LogDebug($"*** EXECUTE: 21");
                }
            }
            catch (Exception e)
            {
                log.LogWarn(e);
            }
        }
    }
}
