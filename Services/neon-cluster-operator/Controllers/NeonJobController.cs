//-----------------------------------------------------------------------------
// FILE:        NeonJobController.cs
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

using Neon.Common;
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
    /// Manages global cluster CRON jobs including updating node CA certificates, renewing
    /// control-plane certificates, ensuring that required container images are pushed to
    /// Harbor, sending cluster telemetry to NEONCLOUD, and renewing cluster certificates.
    /// </summary>
    [RbacRule<V1NeonClusterJobs>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster, SubResources = "status")]
    [RbacRule<V1Node>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster)]
    [RbacRule<V1NeonNodeTask>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster, SubResources = "status")]
    [RbacRule<V1Secret>(Verbs = RbacVerb.Get | RbacVerb.Update, Scope = EntityScope.Cluster)]
    [RbacRule<V1NeonContainerRegistry>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster, SubResources = "status")]
    [RbacRule<V1ConfigMap>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster)]
    [ResourceController(MaxConcurrentReconciles = 1)]
    public class NeonJobController : ResourceControllerBase<V1NeonClusterJobs>
    {
        //---------------------------------------------------------------------
        // Static members

        private static IScheduler                           scheduler;
        private static StdSchedulerFactory                  schedulerFactory;
        private static bool                                 initialized;
        private static NodeCaCertificatesUpdateJob          nodeCaCertificatesUpdateJob;
        private static ControlPlaneCertificateRenewalJob    renewControlPlaneCertificatesJob;
        private static HarborImagePushJob                   pushHarborImagesJob;
        private static TelemetryPingJob                     telemetryPingJob;
        private static ClusterCertificateRenewalJob         renewClusterCertificateJob;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static NeonJobController() 
        {
            schedulerFactory                 = new StdSchedulerFactory();
            nodeCaCertificatesUpdateJob      = new NodeCaCertificatesUpdateJob();
            renewControlPlaneCertificatesJob = new ControlPlaneCertificateRenewalJob();
            pushHarborImagesJob              = new HarborImagePushJob();
            telemetryPingJob                 = new TelemetryPingJob();
            renewClusterCertificateJob       = new ClusterCertificateRenewalJob();
        }

        //---------------------------------------------------------------------
        // Instance members

        private readonly IKubernetes                                k8s;
        private readonly ILogger<NeonJobController>     logger;
        private readonly HeadendClient                              headendClient;
        private readonly HarborClient                               harborClient;
        private readonly ClusterInfo                                clusterInfo;

        /// <summary>
        /// Constructor.
        /// </summary>
        public NeonJobController(
            IKubernetes                              k8s,
            ILogger<NeonJobController>   logger,
            HeadendClient                            headendClient,
            HarborClient                             harborClient,
            ClusterInfo                             clusterInfo)
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
        public override async Task<ResourceControllerResult> ReconcileAsync(V1NeonClusterJobs resource)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                Tracer.CurrentSpan?.AddEvent("reconcile", attributes => attributes.Add("customresource", nameof(V1NeonClusterJobs)));

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

                if (resource.Spec.NodeCaCertificateUpdate.Enabled)
                {
                    try
                    {
                        var nodeCaSchedule = NeonExtendedHelper.FromEnhancedCronExpression(resource.Spec.NodeCaCertificateUpdate.Schedule);

                        await nodeCaCertificatesUpdateJob.DeleteFromSchedulerAsync(scheduler);
                        await nodeCaCertificatesUpdateJob.AddToSchedulerAsync(scheduler, k8s, nodeCaSchedule);
                    }
                    catch (Exception e)
                    {
                        logger.LogErrorEx(e);
                    }
                }

                if (resource.Spec.ControlPlaneCertificateRenewal.Enabled)
                {
                    try
                    {
                        var controlPlaneCertSchedule = NeonExtendedHelper.FromEnhancedCronExpression(resource.Spec.ControlPlaneCertificateRenewal.Schedule);

                        await renewControlPlaneCertificatesJob.DeleteFromSchedulerAsync(scheduler);
                        await renewControlPlaneCertificatesJob.AddToSchedulerAsync(scheduler, k8s, controlPlaneCertSchedule);
                    }
                    catch (Exception e)
                    {
                        logger.LogErrorEx(e);
                    }
                }

                if (resource.Spec.HarborImagePush.Enabled)
                {
                    try
                    {
                        var containerImageSchedule = NeonExtendedHelper.FromEnhancedCronExpression(resource.Spec.HarborImagePush.Schedule);

                        await pushHarborImagesJob.DeleteFromSchedulerAsync(scheduler);
                        await pushHarborImagesJob.AddToSchedulerAsync(
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

                if (resource.Spec.TelemetryPing.Enabled)
                {
                    try
                    {
                        var clusterTelemetrySchedule = NeonExtendedHelper.FromEnhancedCronExpression(resource.Spec.TelemetryPing.Schedule);

                        await telemetryPingJob.DeleteFromSchedulerAsync(scheduler);
                        await telemetryPingJob.AddToSchedulerAsync(
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

                if (resource.Spec.ClusterCertificateRenewal.Enabled)
                {
                    try
                    {
                        var neonDesktopCertSchedule = NeonExtendedHelper.FromEnhancedCronExpression(resource.Spec.ClusterCertificateRenewal.Schedule);

                        await renewClusterCertificateJob.DeleteFromSchedulerAsync(scheduler);
                        await renewClusterCertificateJob.AddToSchedulerAsync(
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
        public override async Task DeletedAsync(V1NeonClusterJobs resource)
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
