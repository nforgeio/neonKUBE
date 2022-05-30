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
using Microsoft.Extensions.Logging;

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
    [EntityRbac(typeof(V1NodeTask), Verbs = RbacVerb.Get | RbacVerb.List | RbacVerb.Watch | RbacVerb.Update)]
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

        private IKubernetes     k8s = new KubernetesClient(KubernetesClientConfiguration.BuildDefaultConfig(), new HttpClient()); 

        /// <summary>
        /// Coinstructor.
        /// </summary>
        public NodeTaskController()
        {
            // Load the configuration settings the first time a controller instance is created.

            if (!configured)
            {
                reconciledNoChangeInterval = Program.Service.Environment.Get("NODETASK_RECONCILED_NOCHANGE_INTERVAL", TimeSpan.FromMinutes(5));
                errorMinRequeueInterval    = Program.Service.Environment.Get("NODETASK_ERROR_MIN_REQUEUE_INTERVAL", TimeSpan.FromSeconds(15));
                errorMaxRequeueInterval    = Program.Service.Environment.Get("NODETASK_ERROR_MAX_REQUEUE_INTERVAL", TimeSpan.FromMinutes(10));

                var leaderConfig = 
                    new LeaderElectionConfig(
                        k8s,
                        @namespace:       KubeNamespace.NeonSystem,
                        leaseName:        $"{Program.Service.Name}.nodetask-{Node.Name}",
                        identity:         Pod.Name,
                        promotionCounter: promotionCounter,
                        demotionCounter:  demotedCounter,
                        newLeaderCounter: newLeaderCounter);

                resourceManager = new ResourceManager<V1NodeTask>(filter: NodeTaskFilter, leaderConfig: leaderConfig)
                {
                     ReconcileNoChangeInterval = reconciledNoChangeInterval,
                     ErrorMinRequeueInterval   = errorMinRequeueInterval,
                     ErrorMaxRequeueInterval   = errorMaxRequeueInterval
                };

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

            return task.Spec.Node.Equals(Node.Name, StringComparison.InvariantCultureIgnoreCase);
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

                    if (name == null)
                    {
                        // This is a NO-CHANGE event which is a good time to handle any
                        // cleanup related to tasks belonging to this node.

                        await CleanupTasksAsync(resources);
                    }
                    else
                    {
                        // We have a new NodeTask targeting this node.

                        var nodeTask = resources[name];
                        var isOK     = true;

                        // Verify that task is well structured.

                        try
                        {
                            nodeTask.Validate();
                        }
                        catch (Exception e)
                        {
                            log.LogWarn($"Invalid NodeTask: [{name}]", e);
                            log.LogWarn($"Deleting invalid NodeTask: [{name}]");
                            await k8s.DeleteNamespacedCustomObjectAsync<V1NodeTask>(KubeNamespace.NeonSystem, nodeTask.Name());

                            isOK = false;
                        }

                        if (isOK)
                        {
                            await ExecuteTaskAsync(resources[name]);
                        }
                    }

                    return await Task.FromResult<ResourceControllerResult>(null);
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
        /// Handles the management of tasks targeting the current cluster node:
        /// </para>
        /// <list type="bullet">
        /// <item>
        /// Tasks whose <see cref="V1NodeTask.V1NodeTaskStatus.AgentId"/> doesn't match
        /// the ID for the current agent will be marked as <see cref="NodeTaskState.Orphaned"/>
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
                    await k8s.DeleteNamespacedCustomObjectAsync<V1NodeTask>(KubeNamespace.NeonSystem, taskName);
                    continue;
                }

                if (nodeTask.Status.State == NodeTaskState.Running)
                {
                    // Detect and kill orphaned tasks.

                    if (await KillOrphanedTaskAsync(nodeTask))
                    {
                        continue;
                    }

                    // Kill tasks that have been running for too long.

                    if (utcNow - nodeTask.Status.StartedUtc >= TimeSpan.FromSeconds(nodeTask.Spec.TimeoutSeconds))
                    {
                        await KillTaskAsync(nodeTask);
                        continue;
                    }
                }
            }
        }

        /// <summary>
        /// Detects when the node task passed is orphaned, killing it and updating it as required.
        /// </summary>
        /// <param name="nodeTask">The node task.</param>
        /// <returns><c>true</c> when the task is orhpaned, <c>false</c> otherwise.</returns>
        private async Task<bool> KillOrphanedTaskAsync(V1NodeTask nodeTask)
        {
            if (nodeTask.Status.AgentId == Node.AgentId)
            {
                return false;
            }

            if (nodeTask.Status.State == NodeTaskState.Orphaned)
            {
                return true;
            }

            var taskName = nodeTask.Name();

            log.LogWarn($"Orphaned node task [{taskName}]: task [agentID={nodeTask.Status.AgentId}] does not match operator [agentID={Node.AgentId}]");

            // Update the node task status to: orphaned

            nodeTask.Status.State       = NodeTaskState.Orphaned;
            nodeTask.Status.FinishedUtc = DateTime.UtcNow;
            nodeTask.Status.ExitCode    = -1;

            await KillTaskAsync(nodeTask);

            // Update the resource status.

            await k8s.UpsertClusterCustomObjectAsync<V1NodeTask>(nodeTask, taskName);

            return true;
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

            if (nodeTask.Status.State != NodeTaskState.Running)
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

            var result = await Node.ExecuteCaptureAsync($"ps --pid={nodeTask.Status.ProcessId} --format cmd=", timeout: null);

            if (result.ExitCode == 0)
            {
                using (var reader = new StringReader(result.OutputText))
                {
                    var commandLine = reader.Lines().FirstOrDefault();

                    if (commandLine == nodeTask.Status.CommandLine)
                    {
                        // The process ID and command line match, so kill it.

                        result = await Node.ExecuteCaptureAsync("kill", null, "-s", "SIGTERM", nodeTask.Status.ProcessId);

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

            var taskName = nodeTask.Name();

            if (nodeTask.Status.State != NodeTaskState.Pending)
            {
                return;
            }

            // Start and execute the command in the background.  The trick here is
            // that we need the ID of the process launched and we need to update the
            // node task status immediately, return, and then handle execution
            // completion in the background.

            var process       = (Process)null;
            var statusUpdated = false;

            _ = Task.Run(
                async () =>
                {
                    // Start the command process.

                    var task = (Task<ExecuteResponse>)null;

                    try
                    {
                        task = Node.ExecuteCaptureAsync(nodeTask.Spec.Command.First(), _process => process = _process, TimeSpan.FromSeconds(nodeTask.Spec.TimeoutSeconds), nodeTask.Spec.Command.Skip(1));
                    }
                    catch (Exception e)
                    {
                        // It's possible for command execution to fail because the command doesn't exist or
                        // due to permissions issues.  We're going to log this and then mark the task as
                        // finised with an error.

                        log.LogWarn(e);

                        nodeTask.Status.State       = NodeTaskState.Finished;
                        nodeTask.Status.FinishedUtc = DateTime.UtcNow;
                        nodeTask.Status.ExitCode    = -1;
                        nodeTask.Status.Error       = $"EXECUTE FAILED: {e.Message}";

                        await k8s.UpsertClusterCustomObjectAsync<V1NodeTask>(nodeTask, taskName);

                        return;
                    }

                    // Mitegate the possiblity of a race condition updating the node task status
                    // by waiting for the update below to complete first.

                    try
                    {
                        NeonHelper.WaitFor(() => statusUpdated, timeout: TimeSpan.FromSeconds(15), pollInterval: TimeSpan.FromMilliseconds(150));
                    }
                    catch (TimeoutException)
                    {
                        // It's possible that the update below failed for some reason.
                        // In this case, we'll just continue and hope for the best.
                    }

                    // Wait for the command to complete and the update the node task status.

                    try
                    {
                        var result  = (ExecuteResponse)null;
                        var timeout = false;

                        try
                        {
                            result = await task;
                        }
                        catch (TimeoutException)
                        {
                            timeout = true;
                        }

                        var innerNodeTask = await k8s.GetNamespacedCustomObjectAsync<V1NodeTask>(KubeNamespace.NeonSystem, taskName);

                        if (innerNodeTask.Status.State == NodeTaskState.Running)
                        {
                            innerNodeTask.Status.FinishedUtc   = DateTime.UtcNow;
                            innerNodeTask.Status.ExecutionTime = (innerNodeTask.Status.FinishedUtc - innerNodeTask.Status.StartedUtc).ToString();

                            if (timeout)
                            {
                                innerNodeTask.Status.State    = NodeTaskState.Timeout;
                                innerNodeTask.Status.ExitCode = -1;
                            }
                            else
                            {
                                innerNodeTask.Status.State    = NodeTaskState.Finished;
                                innerNodeTask.Status.ExitCode = result.ExitCode;

                                if (innerNodeTask.Spec.CaptureOutput)
                                {
                                    innerNodeTask.Status.Output = result.OutputText;
                                    innerNodeTask.Status.Error = result.ErrorText;
                                }
                            }

                            await k8s.UpsertClusterCustomObjectAsync<V1NodeTask>(innerNodeTask, taskName);
                        }
                    }
                    catch (Exception e)
                    {
                        log.LogWarn(e);
                    }
                });

            // Wait for the process callback action to be called.

            NeonHelper.WaitFor(() => process != null, TimeSpan.FromSeconds(15), pollInterval: TimeSpan.FromMilliseconds(150));

            // Update the task status.

            nodeTask.Status.State      = NodeTaskState.Running;
            nodeTask.Status.StartedUtc = DateTime.UtcNow;
            nodeTask.Status.AgentId    = Node.AgentId;
            nodeTask.Status.ProcessId  = process.Id;

            await k8s.UpsertClusterCustomObjectAsync<V1NodeTask>(nodeTask, taskName);

            // Signal to the background task that the node task status has been updated.

            statusUpdated = true;
        }
    }
}
