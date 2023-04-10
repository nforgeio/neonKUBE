//-----------------------------------------------------------------------------
// FILE:	    NeonClusterOperatorController.cs
// CONTRIBUTOR: Marcus Bowyer
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
using Neon.Kube.Clients;
using Neon.Kube.Operator.Finalizer;
using Neon.Kube.Operator.ResourceManager;
using Neon.Kube.Operator.Controller;
using Neon.Kube.Operator.Rbac;
using Neon.Kube.Resources;
using Neon.Kube.Resources.Cluster;
using Neon.Retry;
using Neon.Tasks;
using Neon.Time;

using NeonClusterOperator.Harbor;

using k8s;
using k8s.Autorest;
using k8s.Models;

using Newtonsoft.Json;

using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Prometheus;

using Quartz;
using Quartz.Impl;

using Task    = System.Threading.Tasks.Task;
using Metrics = Prometheus.Metrics;

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
    [RbacRule<V1NeonClusterOperator>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster, SubResources = "status")]
    [RbacRule<V1Node>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster)]
    [RbacRule<V1NeonNodeTask>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster)]
    [RbacRule<V1Secret>(Verbs = RbacVerb.Get | RbacVerb.Update, Scope = EntityScope.Cluster)]
    [RbacRule<V1NeonContainerRegistry>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster)]
    [RbacRule<V1ConfigMap>(Verbs = RbacVerb.Get, Scope = EntityScope.Cluster)]
    public class NeonClusterOperatorController : IResourceController<V1NeonClusterOperator>
    {
        //---------------------------------------------------------------------
        // Static members

        private static IScheduler                       scheduler;
        private static StdSchedulerFactory              schedulerFactory;
        private static bool                             initialized;
        private static UpdateCaCertificates             updateCaCertificates;
        private static CheckControlPlaneCertificates    checkControlPlaneCertificates;
        private static CheckRegistryImages              checkRegistryImages;
        private static SendClusterTelemetry             sendClusterTelemetry;
        private static CheckNeonDesktopCertificate      checkNeonDesktopCert;

        private HeadendClient headendClient;
        private HarborClient  harborClient;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static NeonClusterOperatorController() 
        {
            schedulerFactory              = new StdSchedulerFactory();
            updateCaCertificates          = new UpdateCaCertificates();
            checkControlPlaneCertificates = new CheckControlPlaneCertificates();
            checkRegistryImages           = new CheckRegistryImages();
            sendClusterTelemetry          = new SendClusterTelemetry();
            checkNeonDesktopCert          = new CheckNeonDesktopCertificate();
        }

        //---------------------------------------------------------------------
        // Instance members

        private readonly IKubernetes                              k8s;
        private readonly ILogger<NeonClusterOperatorController>   logger;

        /// <summary>
        /// Constructor.
        /// </summary>
        public NeonClusterOperatorController(
            IKubernetes                              k8s,
            ILogger<NeonClusterOperatorController>   logger,
            HeadendClient                            headendClient,
            HarborClient                             harborClient)
        {
            Covenant.Requires(k8s != null, nameof(k8s));
            Covenant.Requires(logger != null, nameof(logger));
            Covenant.Requires(headendClient != null, nameof(headendClient));
            Covenant.Requires(harborClient != null, nameof(harborClient));

            this.k8s           = k8s;
            this.logger        = logger;
            this.headendClient = headendClient;
            this.harborClient  = harborClient;
        }

        /// <summary>
        /// Called periodically to allow the operator to perform global events.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task IdleAsync()
        {
            await SyncContext.Clear;

            logger?.LogInformationEx("[IDLE]");

            if (!initialized)
            {
                await InitializeSchedulerAsync();
            }
        }

        /// <inheritdoc/>
        public async Task<ResourceControllerResult> ReconcileAsync(V1NeonClusterOperator resource)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                Tracer.CurrentSpan?.AddEvent("reconcile", attributes => attributes.Add("customresource", nameof(V1NeonClusterOperator)));

                logger?.LogInformationEx("[RECONCILING]");

                // Ignore all events when the controller hasn't been started.

                if (resource.Name() != KubeService.NeonClusterOperator)
                {
                    return null;
                }

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
                await checkRegistryImages.AddToSchedulerAsync(
                    scheduler,
                    k8s,
                    containerImageExpression,
                    new Dictionary<string, object>()
                    {
                        { "HarborClient", harborClient }
                    });

                if (resource.Spec.Updates.Telemetry.Enabled)
                {
                    var clusterTelemetryExpression = resource.Spec.Updates.Telemetry.Schedule;

                    CronExpression.ValidateExpression(clusterTelemetryExpression);

                    await sendClusterTelemetry.DeleteFromSchedulerAsync(scheduler);
                    await sendClusterTelemetry.AddToSchedulerAsync(
                        scheduler, 
                        k8s, 
                        clusterTelemetryExpression,
                        new Dictionary<string, object>()
                        {
                            { "AuthHeader", headendClient.DefaultRequestHeaders.Authorization }
                        });
                }

                if (resource.Spec.Updates.NeonDesktopCertificate.Enabled)
                {
                    var neonDesktopCertExpression = resource.Spec.Updates.NeonDesktopCertificate.Schedule;

                    CronExpression.ValidateExpression(neonDesktopCertExpression);

                    await checkNeonDesktopCert.DeleteFromSchedulerAsync(scheduler);
                    await checkNeonDesktopCert.AddToSchedulerAsync(
                        scheduler, 
                        k8s, 
                        neonDesktopCertExpression,
                        new Dictionary<string, object>()
                        {
                            { "HeadendClient", headendClient }
                        });
                }

                logger?.LogInformationEx(() => $"RECONCILED: {resource.Name()}");

                return null;
            }
        }

        /// <inheritdoc/>
        public async Task DeletedAsync(V1NeonClusterOperator resource)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                // Ignore all events when the controller hasn't been started.

                if (resource.Name() != KubeService.NeonClusterOperator)
                {
                    return;
                }
                
                logger?.LogInformationEx(() => $"DELETED: {resource.Name()}");
                await ShutDownAsync();
            }
        }

        /// <inheritdoc/>
        public async Task OnDemotionAsync()
        {
            await SyncContext.Clear;
            await ShutDownAsync();
        }

        private async Task InitializeSchedulerAsync()
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                logger?.LogInformationEx(() => $"Initialize Scheduler");

                scheduler = await schedulerFactory.GetScheduler();

                await scheduler.Start();

                initialized = true;
            }
        }

        private async Task ShutDownAsync()
        {
            await SyncContext.Clear;

            logger?.LogInformationEx(() => $"Shutdown Scheduler");

            await scheduler.Shutdown(waitForJobsToComplete: true);

            initialized = false;
        }
    }
}
