//-----------------------------------------------------------------------------
// FILE:	    CheckRegistryImages.cs
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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Kube.Operator.Util;
using Neon.Kube.Resources;
using Neon.Kube.Resources.Cluster;

using NeonClusterOperator.Harbor;

using k8s;
using k8s.Models;

using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Prometheus;
using Quartz;

using Task    = System.Threading.Tasks.Task;
using Metrics = Prometheus.Metrics;

namespace NeonClusterOperator
{
    /// <summary>
    /// Handles loading of neonKUBE container images into Harbor by assigning node tasks to
    /// specific cluster nodes to upload missing images that are already cached locally by
    /// CRI-O or by fetching these from our public container registry and pushing them to
    /// Harbor.
    /// </summary>
    [DisallowConcurrentExecution]
    public class CheckRegistryImages : CronJob, IJob
    {
        //---------------------------------------------------------------------
        // Static members

        private static readonly ILogger logger = TelemetryHub.CreateLogger<CheckRegistryImages>();

        //---------------------------------------------------------------------
        // Instance members

        private HarborClient    harborClient;
        private IKubernetes     k8s;

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

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                Tracer.CurrentSpan?.AddEvent("execute", attributes => attributes.Add("cronjob", nameof(CheckRegistryImages)));

                var dataMap  = context.MergedJobDataMap;

                k8s          = (IKubernetes)dataMap["Kubernetes"];
                harborClient = (HarborClient)dataMap["HarborClient"];

                await CheckProjectAsync(KubeConst.LocalClusterRegistryProject);

                var nodes                        = await k8s.CoreV1.ListNodeAsync();
                var startTime                    = DateTime.UtcNow.AddSeconds(10);
                var rawClusterManifestConfigMap  = await k8s.CoreV1.ReadNamespacedConfigMapAsync(KubeConfigMapName.ClusterManifest, KubeNamespace.NeonSystem);
                var safeClusterManifestConfigmap = TypeSafeConfigMap<ClusterManifest>.From(rawClusterManifestConfigMap);
                var masters                      = await k8s.CoreV1.ListNodeAsync(labelSelector: "node-role.kubernetes.io/control-plane=");

                foreach (var image in safeClusterManifestConfigmap.Config.ContainerImages)
                {
                    var tag       = image.InternalRef.Split(':').Last();
                    var imageName = image.InternalRef.Split('/').Last().Split(':').First();
                    var node      = masters.Items.SelectRandom(1).First();
                    var tempDir   = $"/tmp/{NeonHelper.CreateBase36Uuid()}";
                    var labels    = new Dictionary<string, string>
                    {
                        { NeonLabel.ManagedBy, KubeService.NeonClusterOperator },
                        { NeonLabel.NodeTaskType, NeonNodeTaskType.ContainerImageSync },
                        { "project", KubeConst.LocalClusterRegistryProject },
                        { "image", imageName },
                        { "tag", tag },
                    };

                    if (await HarborHoldsContainerImageAsync(KubeConst.LocalClusterRegistryProject, imageName, tag))
                    {
                        logger?.LogDebugEx(() => $"Image {KubeConst.LocalClusterRegistryProject}/{imageName}:{tag} exists.");
                        continue;
                    }

                    if (await IsAnyNodeTaskPendingAsync(labels))
                    {
                        logger?.LogDebugEx(() => $"Image {KubeConst.LocalClusterRegistryProject}/{imageName}:{tag} has node task pending.");
                        continue;
                    }

                    var nodeTask = new V1NeonNodeTask()
                    {
                        Metadata = new V1ObjectMeta()
                        {
                            Name   = $"{NeonNodeTaskType.ContainerImageSync}-{NeonHelper.CreateBase36Uuid()}",
                            Labels = labels
                        },
                        Spec = new V1NeonNodeTask.TaskSpec()
                        {
                            Node                = node.Name(),
                            StartAfterTimestamp = startTime,
                            BashScript          = @$"
podman save --format oci-dir --output {tempDir} {image.InternalRef}

retVal=$?
if [ $retVal -ne 0 ]; then
    podman pull {image.SourceRef}
    podman save --format oci-dir --output {tempDir} {image.SourceRef}
fi

skopeo copy --retry-times 5 oci:{tempDir} docker://{image.InternalRef}
rm -rf {tempDir}
",
                            CaptureOutput    = true,
                            RetentionSeconds = (int)TimeSpan.FromHours(1).TotalSeconds
                        }
                    };

                    await k8s.CustomObjects.CreateClusterCustomObjectAsync<V1NeonNodeTask>(nodeTask, nodeTask.Name());

                    startTime = startTime.AddSeconds(10);
                }

                var clusterOperator = await k8s.CustomObjects.ReadClusterCustomObjectAsync<V1NeonClusterOperator>(KubeService.NeonClusterOperator);
                var patch           = OperatorHelper.CreatePatch<V1NeonClusterOperator>();

                if (clusterOperator.Status == null)
                {
                    patch.Replace(path => path.Status, new V1NeonClusterOperator.OperatorStatus());
                }

                patch.Replace(path => path.Status.ContainerImages, new V1NeonClusterOperator.UpdateStatus());
                patch.Replace(path => path.Status.ContainerImages.LastCompleted, DateTime.UtcNow);
                
                await k8s.CustomObjects.PatchClusterCustomObjectStatusAsync<V1NeonClusterOperator>(
                    patch: OperatorHelper.ToV1Patch<V1NeonClusterOperator>(patch), 
                    name:  clusterOperator.Name());
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

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
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

        /// <summary>
        /// Determines whether a neonKuber container exists in Harbor.
        /// </summary>
        /// <param name="projectName">Specifies the target Harbor project name.</param>
        /// <param name="imageName">Specifies the container image name.</param>
        /// <param name="tag">Specifies the container image tag.</param>
        /// <returns><c>true</c> when the container image exists in Harbor.</returns>
        private async Task<bool> HarborHoldsContainerImageAsync(string projectName, string imageName, string tag)
        {
            var exists = false;

            try
            {
                var result = await harborClient.ListTagsAsync(
                    x_Request_Id:          null,
                    project_name:          projectName,
                    repository_name:       imageName,
                    reference:             tag,
                    q:                     null,
                    sort:                  null,
                    page:                  null,
                    page_size:             null,
                    with_signature:        null,
                    with_immutable_status: null);

                if (result.Count > 0)
                {
                    exists = true;
                }
            }
            catch
            {
                // doesn't exist
            }

            return exists;
        }

        /// <summary>
        /// Detetermines whether any <see cref="V1NeonNodeTask"/> with the specified labels
        /// is still pending.
        /// </summary>
        /// <param name="labels">The target node task labels.</param>
        /// <returns><c>true</c> when any matching tasks are pending.</returns>
        private async Task<bool> IsAnyNodeTaskPendingAsync(Dictionary<string, string> labels)
        {
            var selector       = labels.Keys.Select(key => $"{key}={labels[key]}");
            var selectorString = string.Join(",", selector.ToArray());

            var tasks = await k8s.CustomObjects.ListClusterCustomObjectAsync<V1NeonNodeTask>(labelSelector: selectorString);

            return tasks.Items.Any(nodeTask => nodeTask.Status == null || nodeTask.Status?.Phase <=  V1NeonNodeTask.Phase.Running);
        }
    }
}