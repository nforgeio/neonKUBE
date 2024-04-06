// -----------------------------------------------------------------------------
// FILE:	    TerminatedPodGcJob.cs
// CONTRIBUTOR: NEONFORGE Team
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License").
// You may not use this file except in compliance with the License.
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
using System.Net.Http.Headers;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using k8s;
using k8s.Models;

using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.K8s;
using Neon.Kube;
using Neon.Kube.ClusterDef;
using Neon.Operator.Util;
using Neon.Net;
using Neon.Kube.Resources.Cluster;
using Neon.Tasks;

using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Prometheus;

using Quartz;

namespace NeonClusterOperator
{
    /// <summary>
    /// Handles the removal of pods that have been terminated successfully or unsuccessfully
    /// longer than a threshold period of time.
    /// </summary>
    [DisallowConcurrentExecution]
    public class TerminatedPodGcJob : CronJob, IJob
    {
        private static readonly ILogger logger = TelemetryHub.CreateLogger<TelemetryPingJob>();

        /// <summary>
        /// Constructor.
        /// </summary>
        public TerminatedPodGcJob()
            : base(typeof(TerminatedPodGcJob))
        {
        }

        /// <summary>
        /// Specifies the delay in milliseconds the terminated pod removal job
        /// will pause after scanning a namespace for terminated jobs and also
        /// after each job removal to reduce pressure on the API Server.
        /// </summary>
        public int TerminatedPodGcDelayMilliseconds { get; set; }

        /// <summary>
        /// Specifies the number of minutes after a pod terminates sucessfully or not before it
        /// becomes eligible for removal by the <b>neon-cluster-operator</b>.
        /// </summary>
        public int TerminatedPodGcThresholdMinutes { get; set; }

        /// <inheritdoc/>
        public async Task Execute(IJobExecutionContext context)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(context != null, nameof(context));

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                var stopwatch            = new Stopwatch();
                var failedPodsDeleted    = 0;
                var succeededPodsDeleted = 0;

                stopwatch.Start();

                try
                {
                    logger.LogInformationEx(() => "GC terminated jobs.");

                    var dataMap = context.MergedJobDataMap;
                    var k8s     = (IKubernetes)dataMap["Kubernetes"];

                    // We're going to list all failed pods first and then remove those
                    // that have been terminated long enough and then we'll do the same
                    // for successfully terminated pods.
                    //
                    // NOTE: We're going to pause (1 second by default) after every pod
                    //       removal and processed namespace so we don't overwhelm the API
                    //       Server.  This means that it'll take 3+ hours to remove the
                    //       default maximum of 12,500 terminated pods Kubernetes may have
                    //       accumulated.  This shouldn't really happen unless this job is
                    //       enabled after being disabled for a long period of time so this
                    //       seems like a reasonable tradeoff.
                    //
                    // NOTE: We're going to ignore removal errors because namespaces
                    //       with terminated pods may be removed out from under us or pods
                    //       could also be removed by the built-in Kubernetes pod GC service
                    //       while we're processing pods.

                    var namespaces         = await k8s.CoreV1.ListNamespaceAsync();
                    var maxEligibleTimeUtc = DateTime.UtcNow - TimeSpan.FromMinutes(TerminatedPodGcThresholdMinutes);
                    var delay              = TimeSpan.FromMilliseconds(TerminatedPodGcDelayMilliseconds);

                    //---------------------------------------------------------
                    // Process terminated/failed pods.

                    foreach (var @namespace in namespaces.Items)
                    {
                        try
                        {
                            var terminatedPods = await k8s.CoreV1.ListNamespacedPodAsync(@namespace.Name(), fieldSelector: "status.phase: Failed");

                            foreach (var pod in terminatedPods.Items)
                            {
                                var lastTransitionTime = pod.Status.Conditions.Max(condition => condition.LastTransitionTime);

                                if (lastTransitionTime <= maxEligibleTimeUtc)
                                {
                                    await k8s.CoreV1.DeleteNamespacedPodAsync(pod.Name(), pod.Namespace());
                                    await Task.Delay(delay);

                                    failedPodsDeleted++;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            logger?.LogErrorEx(e);
                        }
                        finally
                        {
                            await Task.Delay(delay);
                        }
                    }

                    //---------------------------------------------------------
                    // Process terminated/succeeded pods.

                    foreach (var @namespace in namespaces.Items)
                    {
                        try
                        {
                            var terminatedPods = await k8s.CoreV1.ListNamespacedPodAsync(@namespace.Name(), fieldSelector: "status.phase: Succeeded");

                            foreach (var pod in terminatedPods.Items)
                            {
                                var lastTransitionTime = pod.Status.Conditions.Max(condition => condition.LastTransitionTime);

                                if (lastTransitionTime <= maxEligibleTimeUtc)
                                {
                                    await k8s.CoreV1.DeleteNamespacedPodAsync(pod.Name(), pod.Namespace());
                                    await Task.Delay(delay);

                                    succeededPodsDeleted++;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            logger?.LogErrorEx(e);
                        }
                        finally
                        {
                            await Task.Delay(delay);
                        }
                    }
                }
                catch (Exception e)
                {
                    logger?.LogErrorEx(e);
                }

                logger?.LogInformationEx(() => $"elapsed={stopwatch.Elapsed}, failedPodsDeleted={failedPodsDeleted}, succeededPodsDeleted={succeededPodsDeleted}");
            }
        }
    }
}
