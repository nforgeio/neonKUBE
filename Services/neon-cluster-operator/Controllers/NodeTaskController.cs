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

namespace NeonClusterOperator
{
    /// <summary>
    /// <para>
    /// Manages <see cref="V1NodeTask"/> resources on the Kubernetes API Server.
    /// </para>
    /// <note>
    /// This controller relies on a lease named <b>neon-cluster-operator.nodetask</b> so that only
    /// one <b>neon-cluster-operator</b> pod will perform node task management.  This lease will be
    /// persisted in the <see cref="KubeNamespace.NeonSystem"/> namespace.
    /// </note>
    /// </summary>
    /// <remarks>
    /// This controller handles removal of node tasks targeting nodes that don't exist.  
    /// See <see cref="V1NodeTask"/> for more details.
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
                        leaseName:        $"{Program.Service.Name}.nodetask",
                        identity:         Pod.Name,
                        promotionCounter: promotionCounter,
                        demotionCounter:  demotedCounter,
                        newLeaderCounter: newLeaderCounter);

                resourceManager = new ResourceManager<V1NodeTask>(leaderConfig: leaderConfig)
                {
                     ReconcileNoChangeInterval = reconciledNoChangeInterval,
                     ErrorMinRequeueInterval   = errorMinRequeueInterval,
                     ErrorMaxRequeueInterval   = errorMaxRequeueInterval
                };

                configured = true;
            }
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

                        var tasks = await resourceManager.CloneResourcesAsync(resources);
                        var nodes = new Dictionary<string, V1Node>(StringComparer.InvariantCultureIgnoreCase);

                        foreach (var node in (await k8s.ListNodeAsync()).Items)
                        {
                            nodes.Add(node.Name(), node);
                        }

                        foreach (var nodeTask in tasks.Values)
                        {
                            var taskName = nodeTask.Name();

                            // Remove tasks assigned to nodes that don't exist.

                            var utcNow = DateTime.UtcNow;

                            if (!nodes.ContainsKey(taskName))
                            {
                                try
                                {
                                    await k8s.DeleteNamespacedCustomObjectAsync<V1NodeTask>(KubeNamespace.NeonSystem, taskName);
                                }
                                catch (Exception e)
                                {
                                    log.LogWarn(e);
                                }
                            }
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
    }
}
