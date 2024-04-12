//-----------------------------------------------------------------------------
// FILE:        NeonClusterJobController.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
using System.Threading;
using System.Threading.Tasks;

using k8s;
using k8s.Models;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Kube.Clients;
using Neon.Kube.ClusterDef;
using Neon.Kube.Resources.Cluster;
using Neon.Operator.Attributes;
using Neon.Operator.Controllers;
using Neon.Operator.Rbac;
using Neon.Tasks;

using NeonClusterOperator.CronJobs;
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
    [RbacRule<V1NeonClusterJobConfig>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster, SubResources = "status")]
    [RbacRule<V1Node>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster)]
    [RbacRule<V1NeonNodeTask>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster, SubResources = "status")]
    [RbacRule<V1Secret>(Verbs = RbacVerb.Get | RbacVerb.Update, Scope = EntityScope.Cluster)]
    [RbacRule<V1NeonContainerRegistry>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster, SubResources = "status")]
    [RbacRule<V1ConfigMap>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster)]
    [ResourceController(MaxConcurrentReconciles = 1)]
    public class NeonClusterJobController : ResourceControllerBase<V1NeonClusterJobConfig>
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// The <see cref="MinWorkerNodeVcpuJob"/> schedule is not present in the <see cref="V1NeonClusterJobConfig"/>
        /// resource because we don't want the user to be able to disable this.  We're going to fix this to
        /// run every couple hours.
        /// </summary>
        private static readonly JobSchedule minWorkerNodeVcpuSchedule = new JobSchedule(enabled: true, "0 0 0/2 ? * *");

        private static AsyncMutex                           asyncLock = new AsyncMutex();
        private static IScheduler                           scheduler;
        private static StdSchedulerFactory                  schedulerFactory;
        private static bool                                 isRunning;
        private static NodeCaCertificatesUpdateJob          nodeCaCertificatesUpdateJob;
        private static ControlPlaneCertificateRenewalJob    renewControlPlaneCertificatesJob;
        private static HarborImagePushJob                   pushHarborImagesJob;
        private static TelemetryPingJob                     telemetryPingJob;
        private static ClusterCertificateRenewalJob         renewClusterCertificateJob;
        private static MinWorkerNodeVcpuJob                 minWorkerNodeVcpuJob;
        private static TerminatedPodGcJob                   terminatedPodGcJob;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static NeonClusterJobController() 
        {
            schedulerFactory                 = new StdSchedulerFactory();
            nodeCaCertificatesUpdateJob      = new NodeCaCertificatesUpdateJob();
            renewControlPlaneCertificatesJob = new ControlPlaneCertificateRenewalJob();
            pushHarborImagesJob              = new HarborImagePushJob();
            telemetryPingJob                 = new TelemetryPingJob();
            renewClusterCertificateJob       = new ClusterCertificateRenewalJob();
            minWorkerNodeVcpuJob             = new MinWorkerNodeVcpuJob();
            terminatedPodGcJob               = new TerminatedPodGcJob();
        }

        //---------------------------------------------------------------------
        // Instance members

        private readonly IKubernetes                        k8s;
        private readonly ILogger<NeonClusterJobController>  logger;
        private readonly HeadendClient                      headendClient;
        private readonly HarborClient                       harborClient;
        private readonly ClusterInfo                        clusterInfo;

        /// <summary>
        /// Constructor.
        /// </summary>
        public NeonClusterJobController(
            IKubernetes                         k8s,
            ILogger<NeonClusterJobController>   logger,
            HeadendClient                       headendClient,
            HarborClient                        harborClient,
            ClusterInfo                         clusterInfo)
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
        public override async Task<ResourceControllerResult> ReconcileAsync(V1NeonClusterJobConfig resource, CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                Tracer.CurrentSpan?.AddEvent("reconcile", attributes => attributes.Add("customresource", nameof(V1NeonClusterJobConfig)));

                logger?.LogInformationEx(() => $"Reconciling {resource.GetType().FullName} [{resource.Namespace()}/{resource.Name()}].");

                // Ignore all events when the controller hasn't been started.

                if (resource.Name() != V1NeonClusterJobConfig.SingularName)
                {
                    logger?.LogInformationEx(() => $"Ignorning resource [{resource.Name()}].  Only [{V1NeonClusterJobConfig.SingularName}] is recognized.");
                    return null;
                }

                await StartSchedulerAsync();

                // The [workerNodeVcpuScheduleJob] uses a hardcoded schedule rather than gpicking up its
                // schedule from the [V1NeonClusterJobConfig] resource, so we're going to schedule the job
                // only on the first reconcile callback.

                // $todo(jefflill): figure out why this is broken and causes the controller to barf when reconciling
                //
                //      https://github.com/nforgeio/neonKUBE/issues/1899

#if TODO
                if (!startedWorkerNodeVcpuSchedule)
                {
                    await minWorkerNodeVcpuJob.AddToSchedulerAsync(scheduler, k8s, minWowe canrkerNodeVcpuSchedule.Schedule);

                    startedWorkerNodeVcpuSchedule = true;
                }
#endif

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

                if (resource.Spec.TerminatedPodGc.Enabled)
                {
                    try
                    {
                        var terminatedPodGcSchedule = NeonExtendedHelper.FromEnhancedCronExpression(resource.Spec.TerminatedPodGc.Schedule);

                        terminatedPodGcJob.TerminatedPodGcDelayMilliseconds = resource.Spec.TerminatedPodGcDelayMilliseconds;
                        terminatedPodGcJob.TerminatedPodGcThresholdMinutes  = resource.Spec.TerminatedPodGcThresholdMinutes;

                        await terminatedPodGcJob.DeleteFromSchedulerAsync(scheduler);
                        await terminatedPodGcJob.AddToSchedulerAsync(
                            scheduler,
                            k8s,
                            terminatedPodGcSchedule,
                            new Dictionary<string, object>());
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

                return ResourceControllerResult.Ok();
            }
        }

        /// <inheritdoc/>
        public override async Task DeletedAsync(V1NeonClusterJobConfig resource, CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                logger?.LogInformationEx(() => $"DELETED: {resource.Name()}");
                await StopSchedulerAsync();
            }
        }

        /// <inheritdoc/>
        public override async Task OnDemotionAsync(CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;
            await StopSchedulerAsync();
        }

        /// <summary>
        /// Initializes the job scheduler if it's not running.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task StartSchedulerAsync()
        {
            await SyncContext.Clear;

            using (await asyncLock.AcquireAsync())
            {
                if (!isRunning)
                {
                    using (var activity = TelemetryHub.ActivitySource?.StartActivity())
                    {
                        logger?.LogInformationEx(() => $"Start Quartz scheduler");

                        scheduler = await schedulerFactory.GetScheduler();

                        await scheduler.Start();

                        isRunning = true;
                    }
                }
            }
        }

        /// <summary>
        /// Stops the job scheduler if it's running.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task StopSchedulerAsync()
        {
            await SyncContext.Clear;

            using (await asyncLock.AcquireAsync())
            {
                if (isRunning)
                {
                    logger?.LogInformationEx(() => $"Shutdown Quartz scheduler");

                    await scheduler.Shutdown(waitForJobsToComplete: true);

                    isRunning = false;
                }
            }
        }
    }
}
