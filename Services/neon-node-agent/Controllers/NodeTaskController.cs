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

        private static readonly INeonLogger         log = Program.Service.LogManager.GetLogger<NodeTaskController>();
        private static ResourceManager<V1NodeTask>  resourceManager;

        // Configuration settings

        private static bool         configured = false;
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

log.LogDebug($"*** NODETASK-CONTROLLER: 0");
            // Load the configuration settings the first time a controller instance is created.

            if (!configured)
            {
log.LogDebug($"*** NODETASK-CONTROLLER: 1");
                reconciledNoChangeInterval = Program.Service.Environment.Get("NODETASK_RECONCILED_NOCHANGE_INTERVAL", TimeSpan.FromMinutes(5));
                errorMinRequeueInterval    = Program.Service.Environment.Get("NODETASK_ERROR_MIN_REQUEUE_INTERVAL", TimeSpan.FromSeconds(15));
                errorMaxRequeueInterval    = Program.Service.Environment.Get("NODETASK_ERROR_MAX_REQUEUE_INTERVAL", TimeSpan.FromMinutes(10));
log.LogDebug($"*** NODETASK-CONTROLLER: 2");

                var leaderConfig = 
                    new LeaderElectionConfig(
                        this.k8s,
                        @namespace: KubeNamespace.NeonSystem,
                        leaseName:        $"{Program.Service.Name}.nodetask-{HostNode.Name}",
                        identity:         Pod.Name,
                        promotionCounter: promotionCounter,
                        demotionCounter:  demotedCounter,
                        newLeaderCounter: newLeaderCounter);
log.LogDebug($"*** NODETASK-CONTROLLER: 3");

                resourceManager = new ResourceManager<V1NodeTask>(filter: NodeTaskFilter, leaderConfig: leaderConfig)
                {
                     ReconcileNoChangeInterval = reconciledNoChangeInterval,
                     ErrorMinRequeueInterval   = errorMinRequeueInterval,
                     ErrorMaxRequeueInterval   = errorMaxRequeueInterval
                };
log.LogDebug($"*** NODETASK-CONTROLLER: 4");

                configured = true;
            }
        }

        /// <summary>
        /// Selects only tasks assigned to the current node to be handled by the resource manager.
        /// </summary>
        /// <param name="task">The task being filtered.</param>
        /// <returns><b>true</b> if the task is assigned to the current node.</returns>
        private bool NodeTaskFilter(V1NodeTask task)
        {
            Covenant.Requires<ArgumentNullException>(task != null, nameof(task));

            return task.Spec.Node.Equals(HostNode.Name, StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Called for each existing custom resource when the controller starts so that the controller
        /// can maintain the status of all resources and then afterwards, this will be called whenever
        /// a resource is added or has a non-status update.
        /// </summary>
        /// <param name="task">The new entity.</param>
        /// <returns>The controller result.</returns>
        public async Task<ResourceControllerResult> ReconcileAsync(V1NodeTask task)
        {
            Covenant.Requires<ArgumentNullException>(task != null, nameof(task));

            reconciledReceivedCounter.Inc();

            await resourceManager.ReconciledAsync(task,
                async (name, resources) =>
                {
                    log.LogInfo($"RECONCILED: {name ?? "[NO-CHANGE]"}");
                    reconciledProcessedCounter.Inc();
log.LogDebug($"*** RECONCILE: 0A");
try
{
    await k8s.ListClusterCustomObjectAsync<V1NodeTask>();
}
catch (Exception e)
{
log.LogError("#############################################################");
log.LogError(e);
log.LogError("#############################################################");
return null;
}
log.LogDebug($"*** RECONCILE: 0B");

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
log.LogDebug($"*** RECONCILE: 4B: state={nodeTask.Status?.State}");

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
                        if (nodeTask.Status.State == V1NodeTaskState.New)
                        {
log.LogDebug($"*** RECONCILE: 8A");
                            //nodeTask.Status.State = V1NodeTaskState.Pending;

log.LogDebug($"*** RECONCILE: 8B");
try
{
                                var patchString =
$@"
{{
    ""spec"": {{
        ""state"": ""{ NeonHelper.EnumToString(nodeTask.Status.State) }""
    }}
}}
";
                                var patch = OperatorHelper.CreatePatch<V1NodeTask>();

                                patch.Replace(path => path.Status, new V1NodeTask.V1NodeTaskStatus());
                                patch.Replace(path => path.Status.State, V1NodeTaskState.Pending);

                                nodeTask = await k8s.PatchClusterCustomObjectStatusAsync<V1NodeTask>(OperatorHelper.ToV1Patch<V1NodeTask>(patch), nodeTask.Name());
                            }
catch (Exception e)
{
log.LogDebug(e);
log.LogDebug("===============================================");
log.LogDebug(e.Message);
log.LogDebug("===============================================");
log.LogDebug($"Base URI: {k8s.BaseUri}");
log.LogDebug("===============================================");
}
                            log.LogDebug($"*** RECONCILE: 9");
                        }
log.LogDebug($"*** RECONCILE: 10");

                        if (nodeTask.Status.FinishedUtc.HasValue)
                        {
log.LogDebug($"*** RECONCILE: 11");
                            var retentionTime = DateTime.UtcNow - nodeTask.Status.FinishedUtc;

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

log.LogDebug($"*** RECONCILE: 14");
                        if (nodeTask.Status.State == V1NodeTaskState.Pending)
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
        /// Tasks whose <see cref="V1NodeTask.V1NodeTaskStatus.AgentId"/> doesn't match
        /// the ID for the current agent will be marked as <see cref="V1NodeTaskState.Orphaned"/>
        /// and the finish time will be set to now.  This sets the task up for eventual
        /// deletion.
        /// </item>
        /// <item>
        /// Tasks with a finish time that is older than <see cref="V1NodeTask.V1NodeTaskSpec.RetainSeconds"/>
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

                if (nodeTask.Status.State == V1NodeTaskState.Running)
                {
                    // Detect and kill orphaned tasks.

                    if (nodeTask.Status.AgentId != HostNode.AgentId)
                    {
                        log.LogWarn($"Detected orphaned [nodetask={taskName}]: task [agentID={nodeTask.Status.AgentId}] does not match operator [agentID={HostNode.AgentId}]");
                        await KillTaskAsync(nodeTask);

                        // Update the node task status to: ORPHANED

                        nodeTask.Status.State         = V1NodeTaskState.Orphaned;
                        nodeTask.Status.FinishedUtc   = DateTime.UtcNow;
                        nodeTask.Status.ExecutionTime = (nodeTask.Status.StartedUtc - nodeTask.Status.FinishedUtc).ToString();
                        nodeTask.Status.ExitCode      = -1;

                        await k8s.UpsertClusterCustomObjectAsync<V1NodeTask>(nodeTask, taskName);
                        continue;
                    }

                    // Kill tasks that have been running for too long.

                    if (utcNow - nodeTask.Status.StartedUtc >= TimeSpan.FromSeconds(nodeTask.Spec.TimeoutSeconds))
                    {
                        log.LogWarn($"Detected timeout [nodetask={taskName}]: execution time exceeds [{nodeTask.Spec.TimeoutSeconds}].");
                        await KillTaskAsync(nodeTask);

                        // Update the node task status to: TIMEOUT

                        nodeTask.Status.State         = V1NodeTaskState.Timeout;
                        nodeTask.Status.FinishedUtc   = DateTime.UtcNow;
                        nodeTask.Status.ExecutionTime = (nodeTask.Status.StartedUtc - nodeTask.Status.FinishedUtc).ToString();
                        nodeTask.Status.ExitCode      = -1;

                        await k8s.UpsertClusterCustomObjectAsync<V1NodeTask>(nodeTask, taskName);
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

            foreach (var scriptFolderPath in Directory.GetDirectories(LinuxPath.Combine(HostNode.HostMount, KubeNodeFolder.NodeTasks), "*", SearchOption.TopDirectoryOnly))
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

            if (nodeTask.Status.State != V1NodeTaskState.Running)
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

            var result = await HostNode.ExecuteCaptureAsync($"ps --pid={nodeTask.Status.ProcessId} --format cmd=", timeout: null);

            if (result.ExitCode == 0)
            {
                using (var reader = new StringReader(result.OutputText))
                {
                    var commandLine = reader.Lines().FirstOrDefault();

                    if (commandLine == nodeTask.Status.CommandLine)
                    {
                        // The process ID and command line match, so kill it.

                        result = await HostNode.ExecuteCaptureAsync("kill", null, "-s", "SIGTERM", nodeTask.Status.ProcessId);

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

            if (nodeTask.Status.State != V1NodeTaskState.Pending)
            {
log.LogDebug($"*** EXECUTE: 1");
                return;
            }
log.LogDebug($"*** EXECUTE: 2");

            // Start and execute the command.  The trick here is that we need the
            // ID of the process launched.

            var process       = (Process)null;
            var statusUpdated = false;

            // Generate the execution UUID and write the script to the host node.

            var executionId  = Guid.NewGuid().ToString("d");
            var scriptFolder = LinuxPath.Combine(HostNode.HostMount, KubeNodeFolder.NodeTasks, executionId);
            var scriptPath   = LinuxPath.Combine(scriptFolder, executionId);
log.LogDebug($"*** EXECUTE: 3");

            Directory.CreateDirectory(scriptFolder);
            File.WriteAllText(scriptPath, NeonHelper.ToLinuxLineEndings(nodeTask.Spec.BashScript));

            nodeTask.Status.State       = V1NodeTaskState.Running;
            nodeTask.Status.StartedUtc  = DateTime.UtcNow;
            nodeTask.Status.AgentId     = HostNode.AgentId;
            nodeTask.Status.CommandLine = $"/bin/bash {scriptPath}";
            nodeTask.Status.ProcessId   = process.Id;
            nodeTask.Status.ExecutionId = executionId;
log.LogDebug($"*** EXECUTE: 4");

            // Start the command process.

            var task = (Task<ExecuteResponse>)null;

            try
            {
                // This callback will be executed once the [HostNode.ExecuteCaptureAsync()]
                // call has the process details.  We'll save the details, update the node task
                // status and persist the status changes to the API server.

log.LogDebug($"*** EXECUTE: 5");
                var processCallback =
                    (Process _process) =>
                    {
log.LogDebug($"*** EXECUTE: 6: processID = {process.Id}");
                        process                   = _process;
                        nodeTask.Status.ProcessId = process.Id;

log.LogDebug($"*** EXECUTE: 7");
                        nodeTask = k8s.UpsertClusterCustomObjectAsync<V1NodeTask>(nodeTask, taskName).Result;
log.LogDebug($"*** EXECUTE: 8");

                        log.LogInfo($"Starting [nodetask={taskName}]: [command={nodeTask.Status.CommandLine}] [processID={process.Id}]");

                        statusUpdated = true;
                    };

log.LogDebug($"*** EXECUTE: 9");
                task = HostNode.ExecuteCaptureAsync(LinuxPath.Combine(HostNode.HostMount, "/bin/bash"), processCallback, TimeSpan.FromSeconds(nodeTask.Spec.TimeoutSeconds), scriptPath);
log.LogDebug($"*** EXECUTE: 10");
            }
            catch (Exception e)
            {
                // We shouldn't ever see an error here because [/bin/bash] should always
                // exist, but we'll log something just in case.

                log.LogWarn(e);

                nodeTask.Status.State         = V1NodeTaskState.Finished;
                nodeTask.Status.FinishedUtc   = DateTime.UtcNow;
                nodeTask.Status.ExecutionTime = (nodeTask.Status.StartedUtc - nodeTask.Status.FinishedUtc).ToString();
                nodeTask.Status.ExitCode      = -1;
                nodeTask.Status.Error         = $"EXECUTE FAILED: {e.Message}";

log.LogDebug($"*** EXECUTE: 11");
                await k8s.UpsertClusterCustomObjectAsync<V1NodeTask>(nodeTask, taskName);
log.LogDebug($"*** EXECUTE: 12");
                return;
            }

            // Mitigate the possiblity of a race condition updating the node task status
            // by waiting for the update above to complete first.

            try
            {
                NeonHelper.WaitFor(() => statusUpdated, timeout: TimeSpan.FromSeconds(15), pollInterval: TimeSpan.FromMilliseconds(150));
            }
            catch (TimeoutException e)
            {
                // It's possible but unlikely that the update above failed for some reason.
                // We'll log this hope for the best.

                log.LogWarn(e);
            }
log.LogDebug($"*** EXECUTE: 13");

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

                if (nodeTask.Status.State == V1NodeTaskState.Running)
                {
log.LogDebug($"*** EXECUTE: 16");
                    nodeTask.Status.FinishedUtc   = DateTime.UtcNow;
                    nodeTask.Status.ExecutionTime = (nodeTask.Status.FinishedUtc - nodeTask.Status.StartedUtc).ToString();

                    if (timeout)
                    {
log.LogDebug($"*** EXECUTE: 17");
                        nodeTask.Status.State    = V1NodeTaskState.Timeout;
                        nodeTask.Status.ExitCode = -1;
                    }
                    else
                    {
log.LogDebug($"*** EXECUTE: 18");
                        nodeTask.Status.State    = V1NodeTaskState.Finished;
                        nodeTask.Status.ExitCode = result.ExitCode;

                        if (nodeTask.Spec.CaptureOutput)
                        {
                            nodeTask.Status.Output = result.OutputText;
                            nodeTask.Status.Error  = result.ErrorText;
                        }
                    }

log.LogDebug($"*** EXECUTE: 19");
                    await k8s.UpsertClusterCustomObjectAsync<V1NodeTask>(nodeTask, taskName);
log.LogDebug($"*** EXECUTE: 20");
                }
            }
            catch (Exception e)
            {
                log.LogWarn(e);
            }
        }
    }
}
