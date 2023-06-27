//-----------------------------------------------------------------------------
// FILE:        SendClusterTelemetry.cs
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
using Neon.Kube;
using Neon.Kube.Operator.Util;
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
    /// Handles checking for expired 
    /// </summary>
    [DisallowConcurrentExecution]
    public class SendClusterTelemetry : CronJob, IJob
    {
        private static readonly ILogger logger = TelemetryHub.CreateLogger<SendClusterTelemetry>();

        /// <summary>
        /// Constructor.
        /// </summary>
        public SendClusterTelemetry()
            : base(typeof(SendClusterTelemetry))
        {
        }

        /// <inheritdoc/>
        public async Task Execute(IJobExecutionContext context)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                try
                {
                    logger.LogInformationEx(() => "Sending cluster telemetry.");

                    var dataMap          = context.MergedJobDataMap;
                    var k8s              = (IKubernetes)dataMap["Kubernetes"];
                    var authHeader       = (AuthenticationHeaderValue)dataMap["AuthHeader"];
                    var clusterTelemetry = new ClusterTelemetry();
                    var nodes            = await k8s.CoreV1.ListNodeAsync();

                    foreach (var k8sNode in nodes) 
                    {
                        var node                     = new Node();
                        node.KernelVersion           = k8sNode.Status.NodeInfo.KernelVersion;
                        node.OsImage                 = k8sNode.Status.NodeInfo.OsImage;
                        node.ContainerRuntimeVersion = k8sNode.Status.NodeInfo.ContainerRuntimeVersion;
                        node.KubeletVersion          = k8sNode.Status.NodeInfo.KubeletVersion;
                        node.KubeProxyVersion        = k8sNode.Status.NodeInfo.KubeProxyVersion;
                        node.OperatingSystem         = k8sNode.Status.NodeInfo.OperatingSystem;
                        node.CpuArchitecture         = k8sNode.Status.NodeInfo.Architecture;
                        node.Role                    = k8sNode.Metadata.GetLabel("neonkube.io/node.role");

                        if (k8sNode.Status.Capacity.TryGetValue("cpu", out var cores))
                        {
                            node.Cores = cores.ToInt32();
                        }

                        if (k8sNode.Status.Capacity.TryGetValue("memory", out var memory))
                        {
                            node.Memory = memory.ToString();
                        }

                        clusterTelemetry.Nodes.Add(node);
                    }

                    clusterTelemetry.ClusterInfo             = (await k8s.CoreV1.ReadNamespacedTypedConfigMapAsync<ClusterInfo>(KubeConfigMapName.ClusterInfo, KubeNamespace.NeonStatus)).Data;
                    clusterTelemetry.ClusterInfo.Description = null;

                    using (var jsonClient = new JsonClient() 
                    { 
                        BaseAddress = KubeEnv.HeadendUri 
                    })
                    {
                        jsonClient.DefaultRequestHeaders.Authorization = authHeader;
                        await jsonClient.PostAsync("/telemetry/cluster?api-version=2023-04-06", clusterTelemetry);
                    }

                    var clusterOperator = await k8s.CustomObjects.ReadClusterCustomObjectAsync<V1NeonClusterOperator>(KubeService.NeonClusterOperator);
                    var patch           = OperatorHelper.CreatePatch<V1NeonClusterOperator>();

                    if (clusterOperator.Status == null)
                    {
                        patch.Replace(path => path.Status, new V1NeonClusterOperator.OperatorStatus());
                    }

                    patch.Replace(path => path.Status.Telemetry, new V1NeonClusterOperator.UpdateStatus());
                    patch.Replace(path => path.Status.Telemetry.LastCompleted, DateTime.UtcNow);

                    await k8s.CustomObjects.PatchClusterCustomObjectStatusAsync<V1NeonClusterOperator>(
                        patch: OperatorHelper.ToV1Patch<V1NeonClusterOperator>(patch),
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
