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
using System.Net;
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
using Neon.Time;

using k8s;
using k8s.Autorest;
using k8s.Models;

using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Finalizer;
using KubeOps.Operator.Rbac;

using Newtonsoft.Json;
using Prometheus;

namespace NeonClusterOperator
{
    /// <summary>
    /// <para>
    /// Removes <see cref="V1NeonNodeTask"/> resources assigned to nodes that don't exist.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// This controller relies on a lease named <b>neon-cluster-operator.nodetask</b>.  
    /// This lease will be persisted in the <see cref="KubeNamespace.NeonSystem"/> namespace
    /// and will be used to a leader to manage these resources.
    /// </para>
    /// <para>
    /// The <b>neon-cluster-operator</b> won't conflict with node agents because we're only 
    /// removing tasks that don't belong to an existing node.
    /// </para>
    /// </remarks>
    [EntityRbac(typeof(V1NeonNodeTask), Verbs = RbacVerb.Get | RbacVerb.List | RbacVerb.Patch | RbacVerb.Watch | RbacVerb.Update)]
    public class NodeTaskController : IResourceController<V1NeonNodeTask>, IExtendedController<V1NeonNodeTask>
    {
        //---------------------------------------------------------------------
        // Static members

        private static readonly INeonLogger log = Program.Service.LogManager.GetLogger<NodeTaskController>();

        private static ResourceManager<V1NeonNodeTask, NodeTaskController>  resourceManager;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static NodeTaskController()
        {
        }

        /// <summary>
        /// Starts the controller.
        /// </summary>
        /// <param name="k8s">The <see cref="IKubernetes"/> client to use.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task StartAsync(IKubernetes k8s)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));
            // Load the configuration settings.

            var leaderConfig = 
                new LeaderElectionConfig(
                    k8s,
                    @namespace: KubeNamespace.NeonSystem,
                    leaseName:        $"{Program.Service.Name}.nodetask",
                    identity:         Pod.Name,
                    promotionCounter: Metrics.CreateCounter($"{Program.Service.MetricsPrefix}nodetask_promoted", "Leader promotions"),
                    demotionCounter:  Metrics.CreateCounter($"{Program.Service.MetricsPrefix}nodetask_demoted", "Leader demotions"),
                    newLeaderCounter: Metrics.CreateCounter($"{Program.Service.MetricsPrefix}nodetask_newLeader", "Leadership changes"));

            var options = new ResourceManagerOptions()
            {
                Mode                       = ResourceManagerMode.Collection,
                IdleInterval               = Program.Service.Environment.Get("NODETASK_IDLE_INTERVAL", TimeSpan.FromSeconds(1)),
                ErrorMinRequeueInterval    = Program.Service.Environment.Get("NODETASK_ERROR_MIN_REQUEUE_INTERVAL", TimeSpan.FromSeconds(15)),
                ErrorMaxRetryInterval      = Program.Service.Environment.Get("NODETASK_ERROR_MAX_REQUEUE_INTERVAL", TimeSpan.FromSeconds(60)),
                ReconcileErrorCounter      = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}nodetask_reconciled_error", "Failed NodeTask reconcile event processing."),
                DeleteErrorCounter         = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}nodetask_deleted_error", "Failed NodeTask deleted event processing."),
                StatusModifiedErrorCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}nodetask_statusmodified_error", "Failed NodeTask status-modified events processing.")
            };

            resourceManager = new ResourceManager<V1NeonNodeTask, NodeTaskController>(
                k8s,
                options:      options,
                leaderConfig: leaderConfig);

            await resourceManager.StartAsync();
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

        /// <summary>
        /// Called for each existing custom resource when the controller starts so that the controller
        /// can maintain the status of all resources and then afterwards, this will be called whenever
        /// a resource is added or has a non-status update.
        /// </summary>
        /// <param name="resource">The new entity or <c>null</c> when nothing has changed.</param>
        /// <returns>The controller result.</returns>
        public async Task<ResourceControllerResult> ReconcileAsync(V1NeonNodeTask resource)
        {
            return await resourceManager.ReconciledAsync(resource,
                async (resource, resources) =>
                {
                    var name = resource?.Name();

                    log.LogInfo($"RECONCILED: {name ?? "[IDLE]"}");

                    if (name == null)
                    {
                        // This is an IDLE event: we'll use this as  the signal to do delete
                        // any node tasks that are not assigned to existing node:
                        //
                        // We're going to handle this by looking at each node task and checking
                        // to see whether the target node actually exists.  Rather than listing
                        // the node first, which would be expensive for a large cluster we'll
                        // fetch and cache node information as we go along.

                        var nodeNameToExists = new Dictionary<string, bool>(StringComparer.InvariantCultureIgnoreCase);

                        foreach (var nodeTask in resources.Values)
                        {
                            var deleteMessage = $"Deleting node task [{nodeTask.Name()}] because it is assigned to the non-existent cluster node [{nodeTask.Spec.Node}].";

                            if (nodeNameToExists.TryGetValue(nodeTask.Spec.Node, out var nodeExists))
                            {
                                // Target node status is known.

                                if (nodeExists)
                                {
                                    continue;
                                }
                                else
                                {
                                    log.LogInfo(deleteMessage);

                                    try
                                    {
                                        await k8s.DeleteClusterCustomObjectAsync(nodeTask);
                                    }
                                    catch (Exception e)
                                    {
                                        log.LogError(e);
                                    }

                                    continue;
                                }
                            }

                            // Determine whether the node exists.

                            try
                            {
                                var node = await k8s.ReadNodeAsync(nodeTask.Spec.Node);

                                nodeExists = true;
                                nodeNameToExists.Add(nodeTask.Spec.Node, nodeExists);
                            }
                            catch (HttpOperationException e)
                            {
                                if (e.Response.StatusCode == HttpStatusCode.NotFound)
                                {
                                    nodeExists = false;
                                    nodeNameToExists.Add(nodeTask.Spec.Node, nodeExists);
                                }
                                else
                                {
                                    log.LogError(e);
                                    continue;
                                }
                            }
                            catch (Exception e)
                            {
                                log.LogError(e);
                                continue;
                            }

                            if (!nodeExists)
                            {
                                log.LogInfo(deleteMessage);

                                try
                                {
                                    await k8s.DeleteClusterCustomObjectAsync(nodeTask);
                                }
                                catch (Exception e)
                                {
                                    log.LogError(e);
                                }
                            }
                        }
                    }

                    return null;
                });
        }

        /// <summary>
        /// Called when a custom resource is removed from the API Server.
        /// </summary>
        /// <param name="task">The deleted entity.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task DeletedAsync(V1NeonNodeTask task)
        {
            Covenant.Requires<ArgumentNullException>(task != null, nameof(task));
            
            await resourceManager.DeletedAsync(task,
                async (name, resources) =>
                {
                    log.LogInfo($"DELETED: {name}");

                    // This is a NOP.

                    await Task.CompletedTask;
                });
        }

        /// <summary>
        /// Called when a custom resource's status has been modified.
        /// </summary>
        /// <param name="task">The updated entity.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task StatusModifiedAsync(V1NeonNodeTask task)
        {
            Covenant.Requires<ArgumentNullException>(task != null, nameof(task));

            await resourceManager.DeletedAsync(task,
                async (name, resources) =>
                {
                    log.LogInfo($"STATUS-MODIFIED: {name}");

                    // This is a NOP.

                    await Task.CompletedTask;
                });
        }

        /// <inheritdoc/>
        public V1NeonNodeTask CreateIgnorable()
        {
            var ignorable = new V1NeonNodeTask();

            ignorable.Spec.IgnoreThis    = true;
            ignorable.Spec.Node          = "ignored";
            ignorable.Spec.BashScript    = "ignored";
            ignorable.Spec.Timeout       = "0s";
            ignorable.Spec.RetentionTime = "0s";
            ignorable.Spec.CaptureOutput = false;

            return ignorable;
        }
    }
}
