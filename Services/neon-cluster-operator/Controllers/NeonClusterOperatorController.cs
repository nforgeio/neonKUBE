//-----------------------------------------------------------------------------
// FILE:        NeonClusterOperatorController.cs
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
using System.Diagnostics.Contracts;
using System.Threading.Tasks;

using k8s;
using k8s.Models;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Neon.Diagnostics;
using Neon.Kube;
using Neon.Kube.Clients;
using Neon.Kube.Resources.Cluster;
using Neon.Operator.Attributes;
using Neon.Operator.Controllers;
using Neon.Operator.Rbac;
using Neon.Tasks;

using NeonClusterOperator.Harbor;

using OpenTelemetry.Trace;

using Quartz;
using Quartz.Impl;

using Task = System.Threading.Tasks.Task;

namespace NeonClusterOperator
{
    /// <summary>
    /// Manages global cluster CRON jobes including updating node CA certificates, checking
    /// control-plane certificates, ensuring that required container images are present,
    /// sending cluster telemetry to NEONCLOUD and checking cluster certificates.
    /// </summary>
    [RbacRule<V1NeonClusterOperator>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster, SubResources = "status")]
    [RbacRule<V1Node>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster)]
    [RbacRule<V1NeonNodeTask>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster, SubResources = "status")]
    [RbacRule<V1Secret>(Verbs = RbacVerb.Get | RbacVerb.Update, Scope = EntityScope.Cluster)]
    [RbacRule<V1NeonContainerRegistry>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster, SubResources = "status")]
    [RbacRule<V1ConfigMap>(Verbs = RbacVerb.Get, Scope = EntityScope.Cluster)]
    [ResourceController]
    public class NeonClusterOperatorController : ResourceControllerBase<V1NeonClusterOperator>
    {
        //---------------------------------------------------------------------
        // Static members

        private static IScheduler                       scheduler;
        private static StdSchedulerFactory              schedulerFactory;
        private static bool                             initialized;
        private static UpdateCaCertificatesJob          updateCaCertificatesJob;
        private static CheckControlPlaneCertificatesJob checkControlPlaneCertificatesJob;
        private static CheckRegistryImagesJob           checkRegistryImagesJob;
        private static SendClusterTelemetryJob          sendClusterTelemetryJob;
        private static CheckClusterCertificateJob       checkClusterCertJob;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static NeonClusterOperatorController() 
        {
            schedulerFactory                 = new StdSchedulerFactory();
            updateCaCertificatesJob          = new UpdateCaCertificatesJob();
            checkControlPlaneCertificatesJob = new CheckControlPlaneCertificatesJob();
            checkRegistryImagesJob           = new CheckRegistryImagesJob();
            sendClusterTelemetryJob          = new SendClusterTelemetryJob();
            checkClusterCertJob              = new CheckClusterCertificateJob();
        }

        //---------------------------------------------------------------------
        // Instance members

        private readonly IKubernetes                                k8s;
        private readonly ILogger<NeonClusterOperatorController>     logger;
        private readonly HeadendClient                              headendClient;
        private readonly HarborClient                               harborClient;
        private readonly ClusterInfo                                clusterInfo;

        /// <summary>
        /// Constructor.
        /// </summary>
        public NeonClusterOperatorController(
            IKubernetes                              k8s,
            ILogger<NeonClusterOperatorController>   logger,
            HeadendClient                            headendClient,
            HarborClient                             harborClient,
            ClusterInfo                              clusterInfo)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));
            Covenant.Requires<ArgumentNullException>(logger != null, nameof(logger));
            Covenant.Requires<ArgumentNullException>(headendClient != null, nameof(headendClient));
            Covenant.Requires<ArgumentNullException>(harborClient != null, nameof(harborClient));

            this.k8s           = k8s;
            this.logger        = logger;
            this.headendClient = headendClient;
            this.harborClient  = harborClient;
            this.clusterInfo   = clusterInfo;
        }

        /// <inheritdoc/>
        public override async Task<ResourceControllerResult> ReconcileAsync(V1NeonClusterOperator resource)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                Tracer.CurrentSpan?.AddEvent("reconcile", attributes => attributes.Add("customresource", nameof(V1NeonClusterOperator)));

                logger?.LogInformationEx(() => $"Reconciling {resource.GetType().FullName} [{resource.Namespace()}/{resource.Name()}].");

                // Ignore all events when the controller hasn't been started.

                if (resource.Name() != KubeService.NeonClusterOperator)
                {
                    return null;
                }

                if (!initialized)
                {
                    await InitializeSchedulerAsync();
                }

                if (resource.Spec.Updates.NodeCaCertificates.Enabled)
                {
                    try
                    {
                        var nodeCaSchedule = resource.Spec.Updates.NodeCaCertificates.Schedule;

                        CronExpression.ValidateExpression(nodeCaSchedule);

                        await updateCaCertificatesJob.DeleteFromSchedulerAsync(scheduler);
                        await updateCaCertificatesJob.AddToSchedulerAsync(scheduler, k8s, nodeCaSchedule);
                    }
                    catch (Exception e)
                    {
                        logger.LogErrorEx(e);
                    }
                }

                if (resource.Spec.Updates.ControlPlaneCertificates.Enabled)
                {
                    try
                    {
                        var controlPlaneCertSchedule = resource.Spec.Updates.ControlPlaneCertificates.Schedule;

                        CronExpression.ValidateExpression(controlPlaneCertSchedule);

                        await checkControlPlaneCertificatesJob.DeleteFromSchedulerAsync(scheduler);
                        await checkControlPlaneCertificatesJob.AddToSchedulerAsync(scheduler, k8s, controlPlaneCertSchedule);
                    }
                    catch (Exception e)
                    {
                        logger.LogErrorEx(e);
                    }
                }

                var nodes = await k8s.CoreV1.ListNodeAsync();

                if (resource.Spec.Updates.ContainerImages.Enabled
                    && !clusterInfo.IsDesktop
                    && nodes.Items.Count > 1)
                {
                    try
                    {
                        var containerImageSchedule = resource.Spec.Updates.ContainerImages.Schedule;

                        CronExpression.ValidateExpression(containerImageSchedule);

                        await checkRegistryImagesJob.DeleteFromSchedulerAsync(scheduler);
                        await checkRegistryImagesJob.AddToSchedulerAsync(
                            scheduler,
                            k8s,
                            containerImageSchedule,
                            new Dictionary<string, object>()
                            {
                                { "HarborClient", harborClient }
                            });
                    }
                    catch (Exception e)
                    {
                        logger.LogErrorEx(e);
                    }
                }

                if (resource.Spec.Updates.Telemetry.Enabled)
                {
                    try
                    {
                        var clusterTelemetrySchedule = resource.Spec.Updates.Telemetry.Schedule;

                        CronExpression.ValidateExpression(clusterTelemetrySchedule);

                        await sendClusterTelemetryJob.DeleteFromSchedulerAsync(scheduler);
                        await sendClusterTelemetryJob.AddToSchedulerAsync(
                            scheduler,
                            k8s,
                            clusterTelemetrySchedule,
                            new Dictionary<string, object>()
                            {
                                { "AuthHeader", headendClient.DefaultRequestHeaders.Authorization }
                            });
                    }
                    catch (Exception e)
                    {
                        logger.LogErrorEx(e);
                    }
                }

                if (resource.Spec.Updates.ClusterCertificate.Enabled)
                {
                    try
                    {
                        var neonDesktopCertSchedule = resource.Spec.Updates.ClusterCertificate.Schedule;

                        CronExpression.ValidateExpression(neonDesktopCertSchedule);

                        await checkClusterCertJob.DeleteFromSchedulerAsync(scheduler);
                        await checkClusterCertJob.AddToSchedulerAsync(
                            scheduler, 
                            k8s, 
                            neonDesktopCertSchedule,
                            new Dictionary<string, object>()
                            {
                                { "HeadendClient", headendClient },
                                { "ClusterInfo", clusterInfo}
                            });
                    }
                    catch (Exception e)
                    {
                        logger.LogErrorEx(e);
                    }
                }

                logger?.LogInformationEx(() => $"RECONCILED: {resource.Name()}");

                return null;
            }
        }

        /// <inheritdoc/>
        public override async Task DeletedAsync(V1NeonClusterOperator resource)
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
        public override async Task OnDemotionAsync()
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
