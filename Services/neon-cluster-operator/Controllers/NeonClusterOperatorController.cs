//-----------------------------------------------------------------------------
// FILE:	    NeonClusterOperatorController.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using KubeOps.Operator.Finalizer;
using KubeOps.Operator.Rbac;

using Newtonsoft.Json;

using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Prometheus;

using Quartz.Impl;
using Quartz;
using IdentityModel;

namespace NeonClusterOperator
{
    /// <summary>
    /// <para>
    /// Removes <see cref="V1NeonClusterOperator"/> resources assigned to nodes that don't exist.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// This controller relies on a lease named <b>neon-cluster-operator.operatorsettings</b>.  
    /// This lease will be persisted in the <see cref="KubeNamespace.NeonSystem"/> namespace
    /// and will be used to a leader to manage these resources.
    /// </para>
    /// <para>
    /// The <b>neon-cluster-operator</b> won't conflict with node agents because we're only 
    /// removing tasks that don't belong to an existing node.
    /// </para>
    /// </remarks>
    [EntityRbac(typeof(V1NeonClusterOperator), Verbs = RbacVerb.Get | RbacVerb.List | RbacVerb.Patch | RbacVerb.Watch | RbacVerb.Update)]
    public class NeonClusterOperatorController : IOperatorController<V1NeonClusterOperator>
    {
        //---------------------------------------------------------------------
        // Static members

        private static readonly ILogger log = TelemetryHub.CreateLogger<NeonClusterOperatorController>();

        private static ResourceManager<V1NeonClusterOperator, NeonClusterOperatorController> resourceManager;

        private static IScheduler                       scheduler;
        private static StdSchedulerFactory              schedulerFactory;
        private static bool                             initialized;
        private static UpdateCaCertificates             updateCaCertificates;
        private static CheckControlPlaneCertificates    checkControlPlaneCertificates;
        private static CheckRegistryImages              checkRegistryImages;
        private static SendClusterTelemetry             sendClusterTelemetry;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static NeonClusterOperatorController() { }

        /// <summary>
        /// Starts the controller.
        /// </summary>
        /// <param name="k8s">The <see cref="IKubernetes"/> client to use.</param>
        /// <param name="serviceProvider">The <see cref="IServiceProvider"/>.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task StartAsync(
            IKubernetes k8s,
            IServiceProvider serviceProvider)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));

            // Load the configuration settings.

            var leaderConfig =
                new LeaderElectionConfig(
                    k8s,
                    @namespace:       KubeNamespace.NeonSystem,
                    leaseName:        $"{Program.Service.Name}.operatorsettings",
                    identity:         Pod.Name,
                    promotionCounter: Metrics.CreateCounter($"{Program.Service.MetricsPrefix}operatorsettings_promoted", "Leader promotions"),
                    demotionCounter:  Metrics.CreateCounter($"{Program.Service.MetricsPrefix}operatorsettings_demoted", "Leader demotions"),
                    newLeaderCounter: Metrics.CreateCounter($"{Program.Service.MetricsPrefix}operatorsettings_newLeader", "Leadership changes"));

            var options = new ResourceManagerOptions()
            {
                ErrorMaxRetryCount       = int.MaxValue,
                ErrorMaxRequeueInterval  = TimeSpan.FromMinutes(10),
                ErrorMinRequeueInterval  = TimeSpan.FromSeconds(60),
                IdleCounter              = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}operatorsettings_idle", "IDLE events processed."),
                ReconcileCounter         = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}operatorsettings_idle", "RECONCILE events processed."),
                DeleteCounter            = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}operatorsettings_idle", "DELETED events processed."),
                StatusModifyCounter      = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}operatorsettings_idle", "STATUS-MODIFY events processed."),
                FinalizeCounter          = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}operatorsettings_finalize", "FINALIZE events processed."),
                IdleErrorCounter         = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}operatorsettings_idle_error", "Failed ClusterOperatorSettings IDLE event processing."),
                ReconcileErrorCounter    = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}operatorsettings_reconcile_error", "Failed ClusterOperatorSettings RECONCILE event processing."),
                DeleteErrorCounter       = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}operatorsettings_delete_error", "Failed ClusterOperatorSettings DELETE event processing."),
                StatusModifyErrorCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}operatorsettings_statusmodify_error", "Failed ClusterOperatorSettings STATUS-MODIFY events processing."),
                FinalizeErrorCounter     = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}operatorsettings_finalize_error", "Failed NodeTask FINALIZE events processing.")
            };

            resourceManager = new ResourceManager<V1NeonClusterOperator, NeonClusterOperatorController>(
                k8s,
                options:      options,
                leaderConfig: leaderConfig,
                serviceProvider: serviceProvider);

            await resourceManager.StartAsync();

            schedulerFactory              = new StdSchedulerFactory();
            updateCaCertificates          = new UpdateCaCertificates();
            checkControlPlaneCertificates = new CheckControlPlaneCertificates();
            checkRegistryImages           = new CheckRegistryImages();
            sendClusterTelemetry          = new SendClusterTelemetry();
        }

        //---------------------------------------------------------------------
        // Instance members

        private readonly IKubernetes k8s;
        private readonly Neon.Kube.Operator.IFinalizerManager<V1NeonClusterOperator> finalizerManager;

        /// <summary>
        /// Constructor.
        /// </summary>
        public NeonClusterOperatorController(
            IKubernetes k8s,
            Neon.Kube.Operator.IFinalizerManager<V1NeonClusterOperator> manager)
        {
            Covenant.Requires(k8s != null, nameof(k8s));
            Covenant.Requires(manager != null, nameof(manager));

            this.k8s = k8s;
            this.finalizerManager = manager;
        }

        /// <summary>
        /// Called periodically to allow the operator to perform global events.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task IdleAsync()
        {
            await SyncContext.Clear;

            log.LogInformationEx("[IDLE]");

            if (!initialized)
            {
                await InitializeSchedulerAsync();
            }
        }

        /// <inheritdoc/>
        public async Task<ResourceControllerResult> ReconcileAsync(V1NeonClusterOperator resource)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource.StartActivity())
            {
                Tracer.CurrentSpan?.AddEvent("reconcile", attributes => attributes.Add("customresource", nameof(V1NeonClusterOperator)));

                // Ignore all events when the controller hasn't been started.

                if (resourceManager == null
                    || resource.Name() != KubeService.NeonClusterOperator)
                {
                    return null;
                }

                await finalizerManager.RegisterAllFinalizersAsync(resource);

                if (!initialized)
                {
                    await InitializeSchedulerAsync();
                }

                var nodeCaExpression = resource.Spec.Updates.NodeCaCertificates.Schedule;

                CronExpression.ValidateExpression(nodeCaExpression);

                await updateCaCertificates.DeleteFromSchedulerAsync(scheduler);
                await updateCaCertificates.AddToSchedulerAsync(scheduler, k8s, nodeCaExpression);

                var controlPlaneCertExpression = resource.Spec.Updates.ControlPlaneCertificates.Schedule;

                CronExpression.ValidateExpression(controlPlaneCertExpression);

                await checkControlPlaneCertificates.DeleteFromSchedulerAsync(scheduler);
                await checkControlPlaneCertificates.AddToSchedulerAsync(scheduler, k8s, controlPlaneCertExpression);

                var containerImageExpression = resource.Spec.Updates.ContainerImages.Schedule;

                CronExpression.ValidateExpression(containerImageExpression);

                await checkRegistryImages.DeleteFromSchedulerAsync(scheduler);

                if (resource.Spec.Updates.Telemetry.Enabled)
                {
                    var clusterTelemetryExpression = resource.Spec.Updates.Telemetry.Schedule;
                    CronExpression.ValidateExpression(clusterTelemetryExpression);

                    await sendClusterTelemetry.DeleteFromSchedulerAsync(scheduler);
                    await sendClusterTelemetry.AddToSchedulerAsync(scheduler, k8s, clusterTelemetryExpression);
                }

                log.LogInformationEx(() => $"RECONCILED: {resource.Name()}");

                return null;
            }
        }

        /// <inheritdoc/>
        public async Task DeletedAsync(V1NeonClusterOperator resource)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource.StartActivity())
            {

                // Ignore all events when the controller hasn't been started.

                if (resourceManager == null || resource.Name() != KubeService.NeonClusterOperator)
                {
                    return;
                }

                log.LogInformationEx(() => $"DELETED: {resource.Name()}");

                await ShutDownAsync();
            }
        }

        /// <inheritdoc/>
        public async Task OnPromotionAsync()
        {
            await SyncContext.Clear;

            log.LogInformationEx(() => $"PROMOTED");
        }

        /// <inheritdoc/>
        public async Task OnDemotionAsync()
        {
            await SyncContext.Clear;

            log.LogInformationEx(() => $"DEMOTED");

            await ShutDownAsync();
        }

        /// <inheritdoc/>
        public async Task OnNewLeaderAsync(string identity)
        {
            await SyncContext.Clear;

            log.LogInformationEx(() => $"NEW LEADER: {identity}");
        }

        private async Task InitializeSchedulerAsync()
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource.StartActivity())
            {
                log.LogInformationEx(() => $"Initialize Scheduler");

                scheduler = await schedulerFactory.GetScheduler();

                await scheduler.Start();

                initialized = true;
            }
        }

        private async Task ShutDownAsync()
        {
            await SyncContext.Clear;

            log.LogInformationEx(() => $"Shutdown Scheduler");

            await scheduler.Shutdown(waitForJobsToComplete: true);

            initialized = false;
        }
    }
}
