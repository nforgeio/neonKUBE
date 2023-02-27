//-----------------------------------------------------------------------------
// FILE:	    SendClusterTelemetry.cs
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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Kube.Kube;
using Neon.Kube.Operator.Util;
using Neon.Net;
using Neon.Kube.Resources.Cluster;
using Neon.Tasks;

using k8s;
using k8s.Models;

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
                    var clusterTelemetry = new ClusterTelemetry();
                    var nodes            = await k8s.CoreV1.ListNodeAsync();

                    clusterTelemetry.Nodes       = nodes.Items.ToList();
                    clusterTelemetry.ClusterInfo = (await k8s.CoreV1.ReadNamespacedTypedConfigMapAsync<ClusterInfo>(KubeConfigMapName.ClusterInfo, KubeNamespace.NeonStatus)).Data;

                    using (var jsonClient = new JsonClient() { BaseAddress = KubeEnv.HeadendUri })
                    {
                        await jsonClient.PostAsync("/telemetry/cluster", clusterTelemetry);
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
