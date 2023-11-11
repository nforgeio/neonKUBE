// -----------------------------------------------------------------------------
// FILE:	    WorkerNodeVcpuJob.cs
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

namespace NeonClusterOperator.CronJobs
{
    /// <summary>
    /// Ensures that worker nodes running in a cloud hosted cluster do not have fewer
    /// than 4 vCPUs by removing any of those nodes from the cluster.  The idea here
    /// is to prevent users from ducking our fees because we don't charge for control
    /// plane nodes with fewer than 4 cores as a somewhat weak competitive response to
    /// cloud native Kubernetes clusters where the control plane is free.
    /// </summary>
    [DisallowConcurrentExecution]
    public class WorkerNodeVcpuJob : CronJob, IJob
    {
        private static readonly ILogger logger = TelemetryHub.CreateLogger<WorkerNodeVcpuJob>();

        /// <summary>
        /// Constructor.
        /// </summary>
        public WorkerNodeVcpuJob()
            : base(typeof(WorkerNodeVcpuJob))
        {
        }

        /// <inheritdoc/>
        public async Task Execute(IJobExecutionContext context)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(context != null, nameof(context));

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                // We're going to fetch the cluster info configmap so we can use its
                // [HostingEnvironment] property to determine whether the cluster is
                // hosted in a cloud.

                // $note(jefflill):
                //
                // In theory, users could defeat the 2-vCPU check by manually editing
                // the cluster info configmap hosting environment to an on-premise
                // alternative.  This would break future cloud features like scaling
                // and perhaps repair, so I'm not going to worry about this now.

                var dataMap     = context.MergedJobDataMap;
                var k8s         = (IKubernetes)dataMap["Kubernetes"];
                var clusterInfo = (await k8s.CoreV1.ReadNamespacedTypedConfigMapAsync<ClusterInfo>(KubeConfigMapName.ClusterInfo, KubeNamespace.NeonStatus)).Data;

                try
                {
                    // Perform this operation only for hosting environments where NEONFORGE collects revenue.

                    if (Neon.Kube.KubeHelper.IsPaidHostingEnvironment(clusterInfo.HostingEnvironment))
                    {
                        return;
                    }

                    logger.LogInformationEx(() => "Check worker node vCPUs.");

                    // We're going to list the cluster nodes and remove any worker nodes
                    // fewer fewer than 4 vCPUs.
                    //
                    // NOTE: We're going to page through the node list so we can support
                    //       very large clusters without having to allocate RAM for all
                    //       node resources at once.

                    var removedCount  = 0;
                    var continueToken = (string)null;

                    do
                    {
                        var nodes = await k8s.CoreV1.ListNodeAsync(continueParameter: continueToken, limit: 10);

                        continueToken = nodes.Continue();

                        foreach (var node in nodes.Items)
                        {
                            // We can identify control plane nodes by checking for the existence of the
                            // well-known [node-role.kubernetes.io/control-plane] label which is configured
                            // by [kubeadm].

                            if (node.Metadata.Labels.ContainsKey("node-role.kubernetes.io/control-plane"))
                            {
                                // Don't check control-plane nodes.

                                continue;
                            }

                            // Remove worker nodes with fewer than 4 vCPUs from the cluster.

                            if (!node.Status.Allocatable.TryGetValue("cpu", out var allocatableCpu))
                            {
                                // Looks like Kublet hasn't reported the number of CPUs yet.

                                continue;
                            }

                            var vCpus = allocatableCpu.ToInt32();

                            if (vCpus < KubeConst.MinWorkerNodeVCpus)
                            {
                                logger.LogCriticalEx(() => $"Removing worker node [{node.Name()}] because it has only [{vCpus}] vCPUs when at least [{KubeConst.MinWorkerNodeVCpus}] are required.");
                                await k8s.CoreV1.DeleteNodeAsync(node.Name());

                                removedCount++;
                            }
                        }

                    } while (continueToken != null);

                    if (removedCount > 0)
                    {
                        logger.LogCriticalEx(() => $"Removed [{removedCount}] worker nodes they don't have at least [{KubeConst.MinWorkerNodeVCpus}] vCPUs.");
                    }
                }
                catch (Exception e)
                {
                    logger?.LogErrorEx(e);
                }
            }
        }
    }
}
