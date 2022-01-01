//------------------------------------------------------------------------------
// FILE:        Service.cs
// CONTRIBUTOR: Marcus Bowyer, Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Net.Sockets;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

using Neon.Common;
using Neon.Data;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Net;
using Neon.Retry;
using Neon.Service;

using k8s;
using k8s.Models;
using KubeOps.Operator;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;
using YamlDotNet.RepresentationModel;

namespace NeonClusterOperator
{
    /// <summary>
    /// Implements the <b>neon-cluster-operator</b> service.
    /// </summary>
    public partial class Service : NeonService
    {
        private const string StateTable = "state";
        
        private static KubernetesWithRetry k8s;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <param name="serviceMap">Optionally specifies the service map.</param>
        public Service(string name, ServiceMap serviceMap = null)
            : base(name, version: KubeVersions.NeonKube, serviceMap: serviceMap)
        {
            k8s = new KubernetesWithRetry(KubernetesClientConfiguration.BuildDefaultConfig());

            k8s.RetryPolicy = new ExponentialRetryPolicy(
                e => true,
                maxAttempts:          int.MaxValue,
                initialRetryInterval: TimeSpan.FromSeconds(0.25),
                maxRetryInterval:     TimeSpan.FromSeconds(10),
                timeout:              TimeSpan.FromMinutes(5));
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        /// <inheritdoc/>
        protected async override Task<int> OnRunAsync()
        {
            // Start the operator controllers.  Note that we're not going to await
            // this and will use the termination signal to known when to exit.

            _ = Host.CreateDefaultBuilder()
                    .ConfigureWebHostDefaults(builder => { builder.UseStartup<Startup>(); })
                    .Build()
                    .RunOperatorAsync(Array.Empty<string>());

            // Let Kubernetes know that we're running.

            await StartedAsync();

            // Launch the sub-tasks.  These will run until the service is terminated.

            while (true)
            {
                await Task.Delay(TimeSpan.FromMinutes(5));

                // $todo(jefflill):
                // 
                // We're disabling all activities until we code proper operators.

                // await CheckNodeImagesAsync();
            }
        }

        /// <summary>
        /// Responsible for making sure cluster container images are present in the local
        /// cluster registry.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task CheckNodeImagesAsync()
        {
            // check busybox doesn't already exist

            var pods = await k8s.ListNamespacedPodAsync(KubeNamespaces.NeonSystem);

            if (pods.Items.Any(p => p.Metadata.Name == "check-node-images-busybox"))
            {
                Log.LogInfo($"[check-node-images] Removing existing busybox pod.");
                
                await k8s.DeleteNamespacedPodAsync("check-node-images-busybox", KubeNamespaces.NeonSystem);

                await NeonHelper.WaitForAsync(
                    async () =>
                    {
                        pods = await k8s.ListNamespacedPodAsync(KubeNamespaces.NeonSystem);

                        return !pods.Items.Any(p => p.Metadata.Name == "check-node-images-busybox");
                    }, 
                    timeout:      TimeSpan.FromSeconds(60),
                    pollInterval: TimeSpan.FromSeconds(2));
            }

            Log.LogInfo($"[check-node-images] Creating busybox pod.");

            var busybox = await k8s.CreateNamespacedPodAsync(
                new V1Pod()
                {
                    Metadata = new V1ObjectMeta()
                    {
                        Name              = "check-node-images-busybox",
                        NamespaceProperty = KubeNamespaces.NeonSystem
                    },
                    Spec = new V1PodSpec()
                    {
                        Tolerations = new List<V1Toleration>()
                        {
                            { new V1Toleration() { Effect = "NoSchedule", OperatorProperty = "Exists" } },
                            { new V1Toleration() { Effect = "NoExecute", OperatorProperty = "Exists" } }
                        },
                        HostNetwork = true,
                        HostPID     = true,
                        HostIPC     = true,
                        Volumes     = new List<V1Volume>()
                        {
                            new V1Volume()
                            {
                                Name     = "noderoot",
                                HostPath = new V1HostPathVolumeSource()
                                {
                                    Path = "/",
                                }
                            }
                        },
                        Containers = new List<V1Container>()
                        {
                            new V1Container()
                            {
                                Name            = "check-node-images-busybox",
                                Image           = $"{KubeConst.LocalClusterRegistry}/busybox:{KubeVersions.Busybox}",
                                Command         = new List<string>() {"sleep", "infinity" },
                                ImagePullPolicy = "IfNotPresent",
                                SecurityContext = new V1SecurityContext()
                                {
                                    Privileged = true
                                },
                                VolumeMounts = new List<V1VolumeMount>()
                                {
                                    new V1VolumeMount()
                                    {
                                        Name      = "noderoot",
                                        MountPath = "/host"
                                    }
                                }
                            }
                        },
                        RestartPolicy      = "Always",
                        ServiceAccount     = KubeService.NeonClusterOperator,
                        ServiceAccountName = KubeService.NeonClusterOperator
                    }
                }, KubeNamespaces.NeonSystem);

            await NeonHelper.WaitForAsync(
                async () =>
                {
                    pods = await k8s.ListNamespacedPodAsync(KubeNamespaces.NeonSystem);

                    return pods.Items.Any(p => p.Metadata.Name == "check-node-images-busybox");
                },
                timeout:      TimeSpan.FromSeconds(60),
                pollInterval: TimeSpan.FromSeconds(2));

            Log.LogInfo($"[check-node-images] Loading cluster manifest.");

            var clusterManifestJson = Program.Resources.GetFile("/cluster-manifest.json").ReadAllText();
            var clusterManifest     = NeonHelper.JsonDeserialize<ClusterManifest>(clusterManifestJson);

            Log.LogInfo($"[check-node-images] Getting images currently on node.");

            var crioOutput = NeonHelper.JsonDeserialize<dynamic>(await ExecInPodAsync("check-node-images-busybox", KubeNamespaces.NeonSystem, $@"crictl images --output json",  retry: true));
            var nodeImages = ((IEnumerable<dynamic>)crioOutput.images).Select(image => image.repoTags).SelectMany(x => (JArray)x);

            foreach (var image in clusterManifest.ContainerImages)
            {
                if (nodeImages.Contains(image.InternalRef))
                {
                    Log.LogInfo($"[check-node-images] Image [{image.InternalRef}] exists. Pushing to registry.");
                    await ExecInPodAsync("check-node-images-busybox", KubeNamespaces.NeonSystem, $@"podman push {image.InternalRef}", retry: true);
                } 
                else
                {
                    Log.LogInfo($"[check-node-images] Image [{image.InternalRef}] doesn't exist. Pulling from [{image.SourceRef}].");
                    await ExecInPodAsync("check-node-images-busybox", KubeNamespaces.NeonSystem, $@"podman pull {image.SourceRef}", retry: true);
                    await ExecInPodAsync("check-node-images-busybox", KubeNamespaces.NeonSystem, $@"podman tag {image.SourceRef} {image.InternalRef}");

                    Log.LogInfo($"[check-node-images] Pushing [{image.InternalRef}] to cluster registry.");
                    await ExecInPodAsync("check-node-images-busybox", KubeNamespaces.NeonSystem, $@"podman push {image.InternalRef}", retry: true);
                }
            }

            Log.LogInfo($"[check-node-images] Removing busybox.");
            await k8s.DeleteNamespacedPodAsync("check-node-images-busybox", KubeNamespaces.NeonSystem);

            Log.LogInfo($"[check-node-images] Finished.");
        }

        /// <summary>
        /// Helper method for running node commands via a busybox container.
        /// </summary>
        /// <param name="podName"></param>
        /// <param name="namespace"></param>
        /// <param name="command"></param>
        /// <param name="containerName"></param>
        /// <param name="retry"></param>
        /// <returns>The command output as lines of text.</returns>
        public async Task<string> ExecInPodAsync(
            string      podName,
            string      @namespace,
            string      command,
            string      containerName = null,
            bool        retry         = false)
        {
            var podCommand = new string[]
            {
                "chroot",
                "/host",
                "/bin/bash",
                "-c",
                command
            };

            var pod = await k8s.ReadNamespacedPodAsync(podName, @namespace);

            if (string.IsNullOrEmpty(containerName))
            {
                containerName = pod.Spec.Containers.FirstOrDefault().Name;
            }

            string stdOut = "";
            string stdErr = "";

            var handler = new ExecAsyncCallback(async (_stdIn, _stdOut, _stdError) =>
            {
                stdOut = Encoding.UTF8.GetString(await _stdOut.ReadToEndAsync());
                stdErr = Encoding.UTF8.GetString(await _stdError.ReadToEndAsync());
            });

            var exitcode = await k8s.NamespacedPodExecAsync(podName, @namespace, containerName, podCommand, true, handler, CancellationToken.None);

            if (exitcode != 0)
            {
                throw new KubernetesException($@"{stdOut}

{stdErr}");
            }

            var result = new StringBuilder();
            foreach (var line in stdOut.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
            {
                result.AppendLine(line);
            }

            return result.ToString();
        }
    }
}