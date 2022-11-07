//-----------------------------------------------------------------------------
// FILE:	    SendClusterTelemetry.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using Neon.Kube.Resources;
using Neon.Tasks;

using k8s;
using k8s.Models;

using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Prometheus;

using Quartz;
using static IdentityModel.OidcConstants;
using Grpc.Core;
using Neon.Net;

namespace NeonClusterOperator
{
    /// <summary>
    /// Handles checking for expired 
    /// </summary>
    public class SendClusterTelemetry : CronJob, IJob
    {
        private ILogger logger;

        /// <summary>
        /// Constructor.
        /// </summary>
        public SendClusterTelemetry()
            : base(typeof(SendClusterTelemetry))
        {
            logger = TelemetryHub.CreateLogger<SendClusterTelemetry>();
        }

        /// <inheritdoc/>
        public async Task Execute(IJobExecutionContext context)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource.StartActivity())
            {
                logger.LogInformationEx(() => "Sending cluster telemetry.");

                var dataMap = context.MergedJobDataMap;
                var k8s = (IKubernetes)dataMap["Kubernetes"];

                var clusterTelemetry = new ClusterTelemetry();

                var nodes = await k8s.ListNodeAsync();
                clusterTelemetry.Nodes = nodes.Items.ToList();

                var configMap = await k8s.ReadNamespacedConfigMapAsync(KubeConfigMapName.ClusterInfo, KubeNamespace.NeonStatus);
                clusterTelemetry.ClusterInfo = TypeSafeConfigMap<ClusterInfo>.From(configMap).Config;

                using (var jsonClient = new JsonClient()
                {
                    BaseAddress = KubeEnv.HeadendUri
                })
                {
                    await jsonClient.PostAsync("/telemetry/cluster", clusterTelemetry);
                }

            }
        }
    }
}
