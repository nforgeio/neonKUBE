//-----------------------------------------------------------------------------
// FILE:	    NodeTaskController.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
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
using Neon.Kube.Resources;
using Neon.Retry;
using Neon.Tasks;

using k8s.Models;

using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Finalizer;
using KubeOps.Operator.Rbac;

using Prometheus;
using Tomlyn;

namespace NeonNodeAgent
{
    /// <summary>
    /// Manages <see cref="V1NodeTask"/> resources on the Kubernetes API Server.
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

        //---------------------------------------------------------------------
        // Instance members

        private bool    cleanupTaskRunning = false;

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

                resourceManager = new ResourceManager<V1NodeTask>(filter: NodeTaskFilter)
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
            await SyncContext.ClearAsync;

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
                        // We have a new NodeTask targeting this node that will need
                        // to be executed.

                        await ExecuteTaskAsync(resources[name]);
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
            await SyncContext.ClearAsync;

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
            await SyncContext.ClearAsync;

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
        /// Handles the management of tasks targeting the current cluster node where:
        /// </para>
        /// <list type="bullet">
        /// <item>
        /// Tasks whose <see cref="V1NodeTask.V1NodeTaskStatus.AgentId"/> doesn't match
        /// the ID for the current agent will be marked as <see cref="NodeTaskState.Orphaned"/>
        /// and the finish time will be set to now.  This sets the task up for eventual
        /// deletion.
        /// </item>
        /// <item>
        /// Tasks whose <see cref="V1NodeTask.V1NodeTaskSpec.StartLimitUtc"/> has been exceeded
        /// will be marked as <see cref="NodeTaskState.PendingTimeout"/> and the finish time will 
        /// be set to now.  This sets the task up for eventual deletion.
        /// </item>
        /// <item>
        /// Tasks with a finish time that is older than <see cref="V1NodeTask.V1NodeTaskSpec.RetainSeconds"/>
        /// will be removed.
        /// </item>
        /// </list>
        /// <note>
        /// This method starts the cleanup in a separate <see cref="Task"/> and then returns
        /// immediately.  Only one of these tasks will be allowed to execute at any time,
        /// so this method will just return when a cleanup task is already running.
        /// </note>
        /// </summary>
        /// <param name="resources">The current known tasks.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task CleanupTasksAsync(IReadOnlyDictionary<string, V1NodeTask> resources)
        {
            await SyncContext.ClearAsync;

            var tasks = resourceManager.CloneResourcesAsync(resources);

            if (cleanupTaskRunning)
            {
                await Task.CompletedTask;
                return;
            }

            _ = Task.Run(
                () =>
                {
                    try
                    {
                        cleanupTaskRunning = true;


                    }
                    catch (Exception e)
                    {
                        log.LogError(e);
                    }
                    finally
                    {
                        cleanupTaskRunning = false;
                    }
                });
        }

        /// <summary>
        /// Executes a node task.
        /// </summary>
        /// <param name="task">The node task.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task ExecuteTaskAsync(V1NodeTask task)
        {
            await SyncContext.ClearAsync;
            throw new NotImplementedException("$todo(jefflill)");
        }
    }
}
