//-----------------------------------------------------------------------------
// FILE:	    CheckRegistryImages.cs
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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Kube.Resources;

using NeonClusterOperator.Harbor;

using k8s;
using k8s.Models;


using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Prometheus;

using Quartz;

using Task = System.Threading.Tasks.Task;
using Metrics = Prometheus.Metrics;

namespace NeonClusterOperator
{
    /// <summary>
    /// Handles updating of Linux CA certificates on cluster nodes.
    /// </summary>
    public class CheckRegistryImages : CronJob, IJob
    {
        private HarborClient harborClient;
        private IKubernetes k8s;

        /// <summary>
        /// Constructor.
        /// </summary>
        public CheckRegistryImages()
            : base(typeof(CheckRegistryImages))
        {
        }
        
        /// <inheritdoc/>
        public async Task Execute(IJobExecutionContext context)
        {
            Covenant.Requires<ArgumentNullException>(context != null, nameof(context));

            using (var activity = TelemetryHub.ActivitySource.StartActivity())
            {
                Tracer.CurrentSpan?.AddEvent("execute", attributes => attributes.Add("cronjob", nameof(CheckRegistryImages)));

                var dataMap   = context.MergedJobDataMap;
                k8s           = (IKubernetes)dataMap["Kubernetes"];
                harborClient  = (HarborClient)dataMap["HarborClient"];

                await CheckProjectAsync(KubeConst.LocalClusterRegistryProject);

                var nodes     = await k8s.ListNodeAsync();
                var startTime = DateTime.UtcNow.AddSeconds(10);

                var clusterManifestJson = Program.Resources.GetFile("/cluster-manifest.json").ReadAllText();
                var clusterManifest = NeonHelper.JsonDeserialize<ClusterManifest>(clusterManifestJson);

                var masters = await k8s.ListNodeAsync(labelSelector: "node-role.kubernetes.io/control-plane=");

                foreach (var image in clusterManifest.ContainerImages)
                {
                    var node = masters.Items.SelectRandom(1).First();
                    var nodeTask = new V1NeonNodeTask()
                    {
                        Metadata = new V1ObjectMeta()
                        {
                            Name = $"{NeonNodeTaskType.ContainerImageSync}-{NeonHelper.CreateBase36Uuid()}",
                            Labels = new Dictionary<string, string>
                            {
                                { NeonLabel.ManagedBy, KubeService.NeonClusterOperator },
                                { NeonLabel.NodeTaskType, NeonNodeTaskType.ContainerImageSync }
                            }
                        },
                        Spec = new V1NeonNodeTask.TaskSpec()
                        {
                            Node = node.Name(),
                            StartAfterTimestamp = startTime,
                            BashScript = @$"
podman push {image.InternalRef}

retVal=$?
if [ $retVal -ne 0 ]; then
    podman pull {image.SourceRef}
    podman tag {image.SourceRef} {image.InternalRef}
    podman push {image.InternalRef}
fi
",
                            CaptureOutput = true,
                            RetentionSeconds = (int)TimeSpan.FromHours(1).TotalSeconds
                        }
                    };

                    await k8s.CreateClusterCustomObjectAsync<V1NeonNodeTask>(nodeTask, nodeTask.Name());

                    startTime = startTime.AddSeconds(10);
                }
            }
        }

        /// <summary>
        /// Ensure that the specified Harbor project exists.
        /// </summary>
        /// <param name="projectName">Specifies the project name.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task CheckProjectAsync(string projectName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(projectName), nameof(projectName));

            using (var activity = TelemetryHub.ActivitySource.StartActivity())
            {
                Tracer.CurrentSpan?.AddEvent("execute", attributes => attributes.Add("cronjob", nameof(CheckRegistryImages)));

                try
                {
                    await harborClient.HeadProjectAsync(x_Request_Id: null, project_name: projectName);
                }
                catch
                {
                    await harborClient.CreateProjectAsync(null, null, new ProjectReq()
                    {
                        Project_name = projectName,
                        Metadata     = new ProjectMetadata()
                        {
                            Public = "false",
                        }
                    });
                }
            }
        }
    }
}