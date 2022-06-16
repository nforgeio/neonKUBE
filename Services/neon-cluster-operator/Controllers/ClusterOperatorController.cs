//-----------------------------------------------------------------------------
// FILE:	    ClusterOperatorController.cs
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
    /// Manages the <see cref="V1NeonClusterOperator"/> resource on the Kubernetes API Server.
    /// </para>
    /// </summary>
    /// <remarks>
    /// This controller relies on a lease named <b>neon-cluster-operator.clusteroperator</b>.  
    /// This lease will be persisted in the <see cref="KubeNamespace.NeonSystem"/> namespace
    /// and will be used to a leader to manage these resources.
    /// </remarks>
    [EntityRbac(typeof(V1NeonClusterOperator), Verbs = RbacVerb.Get | RbacVerb.Patch | RbacVerb.List | RbacVerb.Watch | RbacVerb.Update)]
    public class ClusterOperatorController : IResourceController<V1NeonClusterOperator>, IExtendedController<V1NeonContainerRegistry>
    {
        //---------------------------------------------------------------------
        // Static members

        private static readonly INeonLogger log = Program.Service.LogManager.GetLogger<ClusterOperatorController>();

        private static ResourceManager<V1NeonClusterOperator, ClusterOperatorController> resourceManager;

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
                    @namespace:       KubeNamespace.NeonSystem,
                    leaseName:        $"{Program.Service.Name}.clusteroperator",
                    identity:         Pod.Name,
                    promotionCounter: Metrics.CreateCounter($"{Program.Service.MetricsPrefix}clusteroperator_promoted", "Leader promotions"),
                    demotionCounter:  Metrics.CreateCounter($"{Program.Service.MetricsPrefix}clusteroperator_demoted", "Leader demotions"),
                    newLeaderCounter: Metrics.CreateCounter($"{Program.Service.MetricsPrefix}clusteroperator_newLeader", "Leadership changes"));

            var options = new ResourceManagerOptions()
            {
                Mode                       = ResourceManagerMode.Normal,
                IdleInterval               = Program.Service.Environment.Get("CLUSTEROPERATOR_IDLE_INTERVAL", TimeSpan.FromMinutes(5)),
                ErrorMinRequeueInterval    = Program.Service.Environment.Get("CLUSTEROPERATOR_ERROR_MIN_REQUEUE_INTERVAL", TimeSpan.FromSeconds(15)),
                ErrorMaxRetryInterval      = Program.Service.Environment.Get("CLUSTEROPERATOR_ERROR_MAX_REQUEUE_INTERVAL", TimeSpan.FromSeconds(60)),
                ReconcileCounter           = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}clusteroperator_reconciled_changes", "Processed ContainerRegistry reconcile events due to change."),
                DeleteCounter              = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}clusteroperator_deleted_received", "Received ContainerRegistry deleted events."),
                StatusModifiedCounter      = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}clusteroperator_statusmodified_received", "Received ContainerRegistry status-modified events."),
                ReconcileErrorCounter      = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}clusteroperator_reconciled_error", "Failed NodeTask reconcile event processing."),
                DeleteErrorCounter         = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}clusteroperator_deleted_error", "Failed NodeTask deleted event processing."),
                StatusModifiedErrorCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}clusteroperator_statusmodified_error", "Failed NodeTask status-modified events processing.")
            };

            resourceManager = new ResourceManager<V1NeonClusterOperator, ClusterOperatorController>(
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
        public ClusterOperatorController(IKubernetes k8s)
        {
            Covenant.Requires(k8s != null, nameof(k8s));
            Covenant.Requires<InvalidOperationException>(resourceManager != null, $"[{nameof(ClusterOperatorController)}] must be started before KubeOps.");

            this.k8s = k8s;
        }

        /// <summary>
        /// Called for each existing custom resource when the controller starts so that the controller
        /// can maintain the status of all resources and then afterwards, this will be called whenever
        /// a resource is added or has a non-status update.
        /// </summary>
        /// <param name="resource">The new entity or <c>null</c> when nothing has changed.</param>
        /// <returns>The controller result.</returns>
        public async Task<ResourceControllerResult> ReconcileAsync(V1NeonClusterOperator resource)
        {
            return await resourceManager.ReconciledAsync(resource,
                async (resource, resources) =>
                {
                    var name = resource?.Name();

                    log.LogInfo($"RECONCILED: {name ?? "[IDLE]"}");

                    return await Task.FromResult< ResourceControllerResult>(null);
                });
        }

        /// <summary>
        /// Called when a custom resource is removed from the API Server.
        /// </summary>
        /// <param name="resource">The deleted entity.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task DeletedAsync(V1NeonClusterOperator resource)
        {
            await resourceManager.DeletedAsync(resource,
                async (name, resources) =>
                {
                    log.LogInfo($"DELETED: {name}");

                    await Task.CompletedTask;
                });
        }

        /// <summary>
        /// Called when a custom resource's status has been modified.
        /// </summary>
        /// <param name="resource">The updated entity.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task StatusModifiedAsync(V1NeonClusterOperator resource)
        {
            await resourceManager.StatusModifiedAsync(resource,
                async (name, resources) =>
                {
                    // This is a NO-OP

                    await Task.CompletedTask;
                });
        }

        /// <inheritdoc/>
        public V1NeonContainerRegistry CreateIgnorable()
        {
            var ignorable = new V1NeonContainerRegistry();

            ignorable.Spec.IgnoreThis = true;

            return ignorable;
        }
    }
}
