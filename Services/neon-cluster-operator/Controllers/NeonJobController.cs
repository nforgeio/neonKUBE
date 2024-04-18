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
using System.Threading;
using System.Threading.Tasks;

using k8s;
using k8s.Models;

using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.K8s;
using Neon.Kube;
using Neon.Kube.Clients;
using Neon.Kube.ClusterDef;
using Neon.Kube.Resources.Cluster;
using Neon.Operator.Attributes;
using Neon.Operator.Controllers;
using Neon.Operator.Rbac;
using Neon.Operator.Util;
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
    [RbacRule<V1Namespace>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster)]
    [RbacRule<V1Pod>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster)]
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

        private const string JobGroup = nameof(NeonJobController);

        /// <summary>
        /// The <see cref="MinWorkerNodeVcpuJob"/> schedule is not present in the <see cref="V1NeonClusterJobs"/>
        /// resource because we don't want the user to be able to disable this.  We're going to fix this to
        /// run every couple hours.
        /// </summary>
        private static readonly JobSchedule minWorkerNodeVcpuSchedule = new JobSchedule(enabled: true, "0 0 0/2 ? * *");

        private static AsyncMutex           asyncLock = new AsyncMutex();
        private static IScheduler           scheduler;
        private static StdSchedulerFactory  schedulerFactory;
        private static bool                 isRunning;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static NeonJobController() 
        {
            schedulerFactory = new StdSchedulerFactory();
        }

        //---------------------------------------------------------------------
        // Instance members

        private readonly IKubernetes                    k8s;
        private readonly ILogger<NeonJobController>     logger;
        private readonly HeadendClient                  headendClient;
        private readonly HarborClient                   harborClient;
        private readonly ClusterInfo                    clusterInfo;

        /// <summary>
        /// Constructor.
        /// </summary>
        public NeonJobController(
            IKubernetes                 k8s,
            ILogger<NeonJobController>  logger,
            HeadendClient               headendClient,
            HarborClient                harborClient,
            ClusterInfo                 clusterInfo)
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
        public override async Task<ResourceControllerResult> ReconcileAsync(V1NeonClusterJobs resource, CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                Tracer.CurrentSpan?.AddEvent("reconcile", attributes => attributes.Add("customresource", nameof(V1NeonClusterJobs)));

                logger?.LogInformationEx(() => $"Reconciling {resource.GetType().FullName} [{resource.Namespace()}/{resource.Name()}].");

                // Ignore all resources except for the one named: KubeService.NeonClusterOperator

                if (resource.Name() != KubeService.NeonClusterOperator)
                {
                    return null;
                }

                await StartSchedulerAsync();

                // The [workerNodeVcpuScheduleJob] uses a hardcoded schedule (1:30am UTC) rather than picking up its
                // schedule from the [V1NeonClusterJobs] resource.

                // $todo(jefflill): figure out why this is broken and causes the controller to barf when reconciling.
                //
                //      https://github.com/nforgeio/neonKUBE/issues/1899

#if TODO
                await ScheduleJobAsync<MinWorkerNodeVcpuJob>(
                    scheduler,
                    k8s,
                    "0 30 1 ? * *");
#endif

                // We're going to accumulate any patches to the job status with the
                // original and resolved CRON schedules.

                var patch = OperatorHelper.CreatePatch<V1NeonClusterJobs>();

                if (resource.Spec.NodeCaCertificateUpdate.Enabled)
                {
                    try
                    {
                        var resolvedSchedule = GetResolvedJobSchedule(resource.Spec.NodeCaCertificateUpdate.Schedule, resource.Status?.NodeCaCertificateUpdate);

                        await ScheduleJobAsync<ClusterCertificateRenewalJob>(
                            scheduler,
                            k8s,
                            resolvedSchedule);

                        patch.Replace(path => path.Status.NodeCaCertificateUpdate.OriginalCronSchedule, resource.Spec.NodeCaCertificateUpdate.Schedule);
                        patch.Replace(path => path.Status.NodeCaCertificateUpdate.ResolvedCronSchedule, resolvedSchedule);
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
                        var resolvedSchedule = GetResolvedJobSchedule(resource.Spec.ControlPlaneCertificateRenewal.Schedule, resource.Status?.ControlPlaneCertificateRenewal);

                        await ScheduleJobAsync<ControlPlaneCertificateRenewalJob>(
                            scheduler,
                            k8s,
                            resolvedSchedule);

                        patch.Replace(path => path.Status.ControlPlaneCertificateRenewal.OriginalCronSchedule, resource.Spec.ControlPlaneCertificateRenewal.Schedule);
                        patch.Replace(path => path.Status.ControlPlaneCertificateRenewal.ResolvedCronSchedule, resolvedSchedule);
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
                        var resolvedSchedule = GetResolvedJobSchedule(resource.Spec.HarborImagePush.Schedule, resource.Status?.HarborImagePush);

                        await ScheduleJobAsync<HarborImagePushJob>(
                            scheduler,
                            k8s,
                            resolvedSchedule,
                            new Dictionary<string, object>()
                            {
                                { "HarborClient", harborClient }
                            });

                        patch.Replace(path => path.Status.HarborImagePush.OriginalCronSchedule, resource.Spec.HarborImagePush.Schedule);
                        patch.Replace(path => path.Status.HarborImagePush.ResolvedCronSchedule, resolvedSchedule);
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
                        var resolvedSchedule = GetResolvedJobSchedule(resource.Spec.TelemetryPing.Schedule, resource.Status?.TelemetryPing);
                         
                        await ScheduleJobAsync<TelemetryPingJob>(
                            scheduler,
                            k8s,
                            resolvedSchedule,
                            new Dictionary<string, object>()
                            {
                                { "AuthHeader", headendClient.DefaultRequestHeaders.Authorization }
                            });

                        patch.Replace(path => path.Status.TelemetryPing.OriginalCronSchedule, resource.Spec.TelemetryPing.Schedule);
                        patch.Replace(path => path.Status.TelemetryPing.ResolvedCronSchedule, resolvedSchedule);
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
                        var resolvedSchedule = GetResolvedJobSchedule(resource.Spec.TerminatedPodGc.Schedule, resource.Status?.TerminatedPodGc);

                        await ScheduleJobAsync<TerminatedPodGcJob>(
                            scheduler,
                            k8s,
                            resolvedSchedule,
                            new Dictionary<string, object>()
                            {
                                { "TerminatedPodGcDelayMilliseconds", resource.Spec.TerminatedPodGcDelayMilliseconds },
                                { "TerminatedPodGcThresholdMinutes", resource.Spec.TerminatedPodGcThresholdMinutes }
                            });

                        patch.Replace(path => path.Status.TerminatedPodGc.OriginalCronSchedule, resource.Spec.TerminatedPodGc.Schedule);
                        patch.Replace(path => path.Status.TerminatedPodGc.ResolvedCronSchedule, resolvedSchedule);
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
                        var resolvedSchedule = GetResolvedJobSchedule(resource.Spec.ClusterCertificateRenewal.Schedule, resource.Status?.ClusterCertificateRenewal);

                        await ScheduleJobAsync<ClusterCertificateRenewalJob>(
                            scheduler, 
                            k8s, 
                            resolvedSchedule,
                            new Dictionary<string, object>()
                            {
                                { "HeadendClient", headendClient },
                                { "ClusterInfo", clusterInfo}
                            });

                        patch.Replace(path => path.Status.ClusterCertificateRenewal.OriginalCronSchedule, resource.Spec.ClusterCertificateRenewal.Schedule);
                        patch.Replace(path => path.Status.ClusterCertificateRenewal.ResolvedCronSchedule, resolvedSchedule);
                    }
                    catch (Exception e)
                    {
                        logger.LogErrorEx(e);
                    }
                }

                if (resource.Spec.LinuxSecurityPatch.Enabled)
                {
                    try
                    {
                        var resolvedSchedule = GetResolvedJobSchedule(resource.Spec.LinuxSecurityPatch.Schedule, resource.Status?.LinuxSecurityPatch);

                        await ScheduleJobAsync<LinuxSecurityPatchJob>(
                            scheduler,
                            k8s,
                            resolvedSchedule);

                        patch.Replace(path => path.Status.LinuxSecurityPatch.OriginalCronSchedule, resource.Spec.LinuxSecurityPatch.Schedule);
                        patch.Replace(path => path.Status.LinuxSecurityPatch.ResolvedCronSchedule, resolvedSchedule);
                    }
                    catch (Exception e)
                    {
                        logger.LogErrorEx(e);
                    }
                }

                // Update [V1NeonClusterJobs.Status] with any changes to job CRON schedule info.

                if (patch.Operations.Count > 0)
                {
                    try
                    {
                        if (resource.Status == null)
                        {
                            patch.Replace(path => path.Status, new V1NeonClusterJobs.NeonClusterJobsStatus());
                        }

                        await k8s.CustomObjects.PatchClusterCustomObjectStatusAsync<V1NeonClusterJobs>(
                            patch: OperatorHelper.ToV1Patch(patch),
                            name:  resource.Name());
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
        public override async Task DeletedAsync(V1NeonClusterJobs resource, CancellationToken cancellationToken = default)
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
                        logger?.LogInformationEx(() => $"START: Quartz scheduler");

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
                    logger?.LogInformationEx(() => $"STOP: Quartz scheduler");

                    await scheduler.Shutdown(waitForJobsToComplete: true);

                    isRunning = false;
                }
            }
        }

        /// <summary>
        /// Returns the CRON schedule (with any random <b>"R"</b> fields resolved)
        /// to be used for a job.  This returns the schedule from the job status
        /// if present or returns the resolved <paramref name="cronSchedule"/> when
        /// there's no Job status or the original CRON schedule has been modified.
        /// </summary>
        /// <param name="cronSchedule">Specifies the CRON schedule from <see cref="V1NeonClusterJobs"/>.</param>
        /// <param name="jobStatus">Specifies the job status for the target job from <see cref="V1NeonClusterJobs"/>  (or <c>null</c>).</param>
        /// <returns>The resolved CRON schedule for the job.</returns>
        public string GetResolvedJobSchedule(string cronSchedule, V1NeonClusterJobs.JobStatus jobStatus)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(cronSchedule), nameof(cronSchedule));

            var resolvedSchedule = NeonExtendedHelper.FromEnhancedCronExpression(cronSchedule);

            if (string.IsNullOrEmpty(jobStatus?.OriginalCronSchedule) || cronSchedule != jobStatus.OriginalCronSchedule)
            {
                return resolvedSchedule;
            }

            Covenant.Assert(!string.IsNullOrEmpty(jobStatus.ResolvedCronSchedule), $"Expected [{nameof(jobStatus.ResolvedCronSchedule)}] to have the original schedule.");

            return jobStatus.ResolvedCronSchedule;
        }

        /// <summary>
        /// Adds a job to a specified Quartz scheduler.
        /// </summary>
        /// <typeparam name="TJob">Specifies the job implementation type.</typeparam>
        /// <param name="scheduler">Specifies the scheduler.</param>
        /// <param name="k8s">Specifies the Kubernetes client.</param>
        /// <param name="cronSchedule">Specifies the schedule.</param>
        /// <param name="data">Optionally specifies a dictionary with additional data.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task ScheduleJobAsync<TJob>(
            IScheduler                  scheduler,
            IKubernetes                 k8s,
            string                      cronSchedule,
            Dictionary<string, object>  data = null)

            where TJob : IJob
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(scheduler != null, nameof(scheduler));
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(cronSchedule), nameof(cronSchedule));

            var jobType = typeof(TJob);
            var jobKey  = new JobKey(jobType.Name, JobGroup);

            //-----------------------------------------------------------------
            // Remove the job from the scheduler, if it's already present.

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                Tracer.CurrentSpan?.AddEvent($"cancel-job: {jobType.Name}/{cronSchedule}");

                try
                {
                    await scheduler.DeleteJob(jobKey);
                }
                catch (NullReferenceException)
                {
                    return;
                }
            }

            //-----------------------------------------------------------------
            // Add the job to the scheduler.

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                Tracer.CurrentSpan?.AddEvent($"schedule-job: {jobType.Name}");

                var job = JobBuilder.Create(jobType)
                    .WithIdentity(jobType.Name, JobGroup)
                    .Build();

                job.JobDataMap.Put("Kubernetes", k8s);

                if (data != null)
                {
                    foreach (var item in data)
                    {
                        job.JobDataMap.Put(item.Key, item.Value);
                    }
                }

                // Trigger the job to run now, and then repeat as scheduled.

                var trigger = TriggerBuilder.Create()
                    .WithIdentity(jobKey.Name, jobKey.Group)
                    .WithCronSchedule(cronSchedule)
                    .Build();

                // Tell quartz to schedule the job using our trigger.

                await scheduler.ScheduleJob(job, trigger);
            }
        }
    }
}
