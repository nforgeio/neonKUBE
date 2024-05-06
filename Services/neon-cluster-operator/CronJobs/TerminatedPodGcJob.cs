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
    public class TerminatedPodGcJob : IJob
    {
        //---------------------------------------------------------------------
        // Static members

        private static readonly ILogger logger = TelemetryHub.CreateLogger<TelemetryPingJob>();

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        public TerminatedPodGcJob()
        {
        }

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
                var exceptionCount       = 0;

                stopwatch.Start();

                try
                {
                    logger.LogInformationEx(() => "START: GC terminated pods");

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
                    // NOTE: We're going to mostly ignore removal errors because namespaces
                    //       with terminated pods may be removed out from under us or pods
                    //       could also be removed by the built-in Kubernetes pod GC service
                    //       while we're processing them.

                    var terminatedPodGcThresholdMinutes  = (int)context.MergedJobDataMap["TerminatedPodGcThresholdMinutes"];
                    var terminatedPodGcDelayMilliseconds = (int)context.MergedJobDataMap["TerminatedPodGcDelayMilliseconds"];
                    var namespaces                       = await k8s.CoreV1.ListNamespaceAsync();
                    var maxEligibleTimeUtc               = DateTime.UtcNow - TimeSpan.FromMinutes(terminatedPodGcThresholdMinutes);
                    var delay                            = TimeSpan.FromMilliseconds(terminatedPodGcDelayMilliseconds);

                    //---------------------------------------------------------
                    // Process terminated/failed pods.

                    foreach (var @namespace in namespaces.Items)
                    {
                        try
                        {
                            var terminatedPods = await k8s.CoreV1.ListNamespacedPodAsync(@namespace.Name(), fieldSelector: "status.phase==Failed");

                            foreach (var pod in terminatedPods.Items
                                .Where(pod => IsNameGenerated(pod)))
                            {
                                var lastTransitionTime = pod.Status.Conditions.Max(condition => condition.LastTransitionTime);

                                if (lastTransitionTime <= maxEligibleTimeUtc)
                                {
                                    try
                                    {
                                        await k8s.CoreV1.DeleteNamespacedPodAsync(pod.Name(), pod.Namespace());
                                        failedPodsDeleted++;
                                    }
                                    catch (Exception e)
                                    {
                                        logger?.LogErrorEx(e);
                                        exceptionCount++;

                                        // Bail on processing the namespace if it no longer exists.

                                        try
                                        {
                                            await k8s.CoreV1.ReadNamespaceAsync(@namespace.Name());
                                        }
                                        catch
                                        {
                                            continue;
                                        }
                                    }
                                    finally
                                    {
                                        await Task.Delay(delay);
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            logger?.LogErrorEx(e);
                            exceptionCount++;
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
                            var terminatedPods = await k8s.CoreV1.ListNamespacedPodAsync(@namespace.Name(), fieldSelector: "status.phase==Succeeded");

                            foreach (var pod in terminatedPods.Items
                                .Where(pod => IsNameGenerated(pod)))
                            {
                                var lastTransitionTime = pod.Status.Conditions.Max(condition => condition.LastTransitionTime);

                                if (lastTransitionTime <= maxEligibleTimeUtc)
                                {
                                    try
                                    {
                                        await k8s.CoreV1.DeleteNamespacedPodAsync(pod.Name(), pod.Namespace());
                                        succeededPodsDeleted++;
                                    }
                                    catch (Exception e)
                                    {
                                        logger?.LogErrorEx(e);
                                        exceptionCount++;

                                        // Bail on processing the namespace if it no longer exists.

                                        try
                                        {
                                            await k8s.CoreV1.ReadNamespaceAsync(@namespace.Name());
                                        }
                                        catch
                                        {
                                            continue;
                                        }
                                    }
                                    finally
                                    {
                                        await Task.Delay(delay);
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            logger?.LogErrorEx(e);
                            exceptionCount++;
                        }
                        finally
                        {
                            await Task.Delay(delay);
                        }
                    }

                    var clusterOperator = await k8s.CustomObjects.GetClusterCustomObjectAsync<V1NeonClusterJobConfig>(KubeService.NeonClusterOperator);
                    var patch           = OperatorHelper.CreatePatch<V1NeonClusterJobConfig>();

                    if (clusterOperator.Status == null)
                    {
                        patch.Replace(path => path.Status, new V1NeonClusterJobConfig.NeonClusterJobsStatus());
                    }

                    patch.Replace(path => path.Status.TerminatedPodGc, new V1NeonClusterJobConfig.JobStatus());
                    patch.Replace(path => path.Status.TerminatedPodGc.LastCompleted, DateTime.UtcNow);

                    await k8s.CustomObjects.PatchClusterCustomObjectStatusAsync<V1NeonClusterJobConfig>(
                        patch: OperatorHelper.ToV1Patch<V1NeonClusterJobConfig >(patch),
                        name:  clusterOperator.Name());
                }
                catch (Exception e)
                {
                    logger?.LogErrorEx(e);
                    exceptionCount++;
                }

                // Log a summary of what happened.

                var sbSummary = new StringBuilder();

                sbSummary.AppendLine("FINISH: GC terminated pods");
                sbSummary.AppendLine($"elapsed: {stopwatch.Elapsed}");
                sbSummary.AppendLine($"failedPodsDeleted: {failedPodsDeleted}");
                sbSummary.AppendLine($"succeededPodsDeleted: {succeededPodsDeleted}");
                sbSummary.AppendLine($"exceptions: {exceptionCount}");

                logger?.LogInformationEx(sbSummary.ToString());
            }
        }

        /// <summary>
        /// <para>
        /// Determines whether a pod appears to have a generated name, with a UID suffix.
        /// </para>
        /// <para>
        /// We don't delete pods without a generated name to avoid a potential race condition
        /// where we identify terminated pod to be removed but before we actually remove it,
        /// something else deletes the pod and then deploys another with the same name and
        /// then we end up deleting the new pod by mistake.
        /// </para>
        /// </summary>
        /// <param name="pod">Specifies the pod being checked.</param>
        /// <returns><c>true</c> when the pod has a generated name.</returns>
        private bool IsNameGenerated(V1Pod pod)
        {
            Covenant.Requires<ArgumentNullException>(pod != null, nameof(pod));

            if (string.IsNullOrEmpty(pod.Metadata.GenerateName))
            {
                return false;
            }

            // $note(jefflill):
            //
            // There's a chance that the pod was was deployed with both Name and
            // GeneratedName set and that the name be prefixed with the generated
            // name property.  In this case, this method will treat the pod as
            // having a generated name when it actually doesn't.
            //
            // I'm going to assume that this situation will be very rare and
            // combining this with the unlikely chance that we'll encounter
            // the deletion race condition, that this will be too rare to
            // worry about.

            return pod.Metadata.Name.Length > pod.Metadata.GenerateName.Length &&
                   pod.Metadata.Name.StartsWith(pod.Metadata.GenerateName);
        }
    }
}
