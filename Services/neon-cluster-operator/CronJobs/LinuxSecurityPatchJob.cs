// -----------------------------------------------------------------------------
// FILE:	    LinuxSecurityPatchJob.cs
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
    /// Handles applying of Linux security patches to the cluster nodes.
    /// </summary>
    [DisallowConcurrentExecution]
    public class LinuxSecurityPatchJob : IJob
    {
        //---------------------------------------------------------------------
        // Static members

        private static readonly ILogger logger = TelemetryHub.CreateLogger<TelemetryPingJob>();

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        public LinuxSecurityPatchJob()
        {
        }

        /// <inheritdoc/>
        public async Task Execute(IJobExecutionContext context)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(context != null, nameof(context));

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                try
                {
                    logger.LogInformationEx(() => "Appling Linux security patches.");

                    var dataMap = context.MergedJobDataMap;
                    var k8s     = (IKubernetes)dataMap["Kubernetes"];
                    var nodes   = await k8s.CoreV1.ListNodeAsync();

                    // We're going to schedule node tasks to perform the updates in parallel.
                    // This shouldn't impact node availability.
                    //
                    // NOTE: We're only going to patch READY nodes.

                    foreach (var node in nodes.Items)
                    {
                        var lastCondition = node.Status.Conditions.LastOrDefault();

                        if (lastCondition != null &&
                            lastCondition.Status == "True" &&
                            lastCondition.Type == "Ready")
                        {
                            var nodeTask = new V1NeonNodeTask();

                            nodeTask.Metadata = new V1ObjectMeta();
                            nodeTask.Metadata.SetLabel(NeonLabel.RemoveOnClusterReset);

                            nodeTask.Spec            = new V1NeonNodeTask.TaskSpec();
                            nodeTask.Spec.Node       = node.Name();
                            nodeTask.Spec.BashScript =
$@"
set -euo pipefail

{KubeConst.SafeAptGetToolPath} update
{KubeConst.SafeAptGetToolPath} upgrade -yq
";
                            await k8s.CustomObjects.CreateClusterCustomObjectAsync<V1NeonNodeTask>(nodeTask, name: $"node-security-patch-{Guid.NewGuid().ToString("d")}");
                        }
                    }

                    var clusterOperator = await k8s.CustomObjects.GetClusterCustomObjectAsync<V1NeonClusterJobs>(KubeService.NeonClusterOperator);
                    var patch           = OperatorHelper.CreatePatch<V1NeonClusterJobs>();

                    if (clusterOperator.Status == null)
                    {
                        patch.Replace(path => path.Status, new V1NeonClusterJobs.NeonClusterJobsStatus());
                    }

                    patch.Replace(path => path.Status.LinuxSecurityPatch, new V1NeonClusterJobs.JobStatus());
                    patch.Replace(path => path.Status.LinuxSecurityPatch.LastCompleted, DateTime.UtcNow);

                    await k8s.CustomObjects.PatchClusterCustomObjectStatusAsync<V1NeonClusterJobs>(
                        patch: OperatorHelper.ToV1Patch<V1NeonClusterJobs>(patch),
                        name:  clusterOperator.Name());
                }
                catch (Exception e)
                {
                    logger?.LogErrorEx(e);
                }
            }
        }
    }
}
