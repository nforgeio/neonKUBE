//-----------------------------------------------------------------------------
// FILE:        TelemetryPingJob.cs
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
    /// Handles the transmission of telemetry pings to the headend. 
    /// </summary>
    [DisallowConcurrentExecution]
    public class TelemetryPingJob : IJob
    {
        //---------------------------------------------------------------------
        // Static members

        private static readonly ILogger logger = TelemetryHub.CreateLogger<TelemetryPingJob>();

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        public TelemetryPingJob()
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
                    logger.LogInformationEx(() => "Send cluster telemetry.");

                    var dataMap          = context.MergedJobDataMap;
                    var k8s              = (IKubernetes)dataMap["Kubernetes"];
                    var authHeader       = (AuthenticationHeaderValue)dataMap["AuthHeader"];
                    var clusterTelemetry = new ClusterTelemetry();
                    var nodes            = await k8s.CoreV1.ListNodeAsync();
                    var hasWorkers       = nodes.Items.Count(node => node.Metadata.GetLabel("neonkube.io/node.role") == NodeRole.Worker) > 0;

                    foreach (var k8sNode in nodes) 
                    {
                        var node = new ClusterNodeTelemetry();

                        node.ContainerRuntimeVersion = k8sNode.Status.NodeInfo.ContainerRuntimeVersion;
                        node.CpuArchitecture         = k8sNode.Status.NodeInfo.Architecture;
                        node.KernelVersion           = k8sNode.Status.NodeInfo.KernelVersion;
                        node.KubeletVersion          = k8sNode.Status.NodeInfo.KubeletVersion;
                        node.OperatingSystem         = k8sNode.Status.NodeInfo.OperatingSystem;
                        node.OsImage                 = k8sNode.Status.NodeInfo.OsImage;
                        node.PrivateAddress          = k8sNode.Status.Addresses.FirstOrDefault(address => address.Type == "InternalIP").Address;
                        node.Role                    = k8sNode.Metadata.GetLabel(NodeLabel.LabelRole);

                        if (k8sNode.Status.Capacity.TryGetValue("cpu", out var cores))
                        {
                            node.VCpus = cores.ToInt32();
                        }

                        if (k8sNode.Status.Capacity.TryGetValue("memory", out var memory))
                        {
                            node.Memory = memory.ToString();
                        }

                        clusterTelemetry.Nodes.Add(node);
                    }

                    clusterTelemetry.Details = new ClusterDetails((await k8s.CoreV1.ReadNamespacedTypedConfigMapAsync<ClusterInfo>(KubeConfigMapName.ClusterInfo, KubeNamespace.NeonStatus)).Data);

                    using (var jsonClient = new JsonClient() { BaseAddress = KubeEnv.HeadendUri })
                    {
                        jsonClient.DefaultRequestHeaders.Authorization = authHeader;

                        await jsonClient.PostAsync("/telemetry/cluster?api-version=2023-04-06", clusterTelemetry);
                    }

                    var clusterOperator = await k8s.CustomObjects.GetClusterCustomObjectAsync<V1NeonClusterJobConfig>(KubeService.NeonClusterOperator);
                    var patch           = OperatorHelper.CreatePatch<V1NeonClusterJobConfig>();

                    if (clusterOperator.Status == null)
                    {
                        patch.Replace(path => path.Status, new V1NeonClusterJobConfig.NeonClusterJobsStatus());
                    }

                    patch.Replace(path => path.Status.TelemetryPing, new V1NeonClusterJobConfig.JobStatus());
                    patch.Replace(path => path.Status.TelemetryPing.LastCompleted, DateTime.UtcNow);

                    await k8s.CustomObjects.PatchClusterCustomObjectStatusAsync<V1NeonClusterJobConfig>(
                        patch: OperatorHelper.ToV1Patch<V1NeonClusterJobConfig>(patch),
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
