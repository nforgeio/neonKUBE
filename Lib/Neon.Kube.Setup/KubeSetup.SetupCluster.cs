//-----------------------------------------------------------------------------
// FILE:	    KubeSetup.SetupCluster.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using k8s;
using k8s.Models;

using Neon.Collections;
using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Clients;
using Neon.Kube.ClusterDef;
using Neon.Kube.Config;
using Neon.Kube.Hosting;
using Neon.Kube.Proxy;
using Neon.Kube.Setup;
using Neon.Kube.SSH;
using Neon.Retry;
using Neon.SSH;
using Neon.Tasks;

namespace Neon.Kube.Setup
{
    public static partial class KubeSetup
    {
        /// <summary>
        /// Constructs the <see cref="ISetupController"/> to be used for setting up a cluster.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <param name="cloudMarketplace">
        /// <para>
        /// For cloud environments, this specifies whether the cluster should be provisioned
        /// using a VM image from the public cloud marketplace when <c>true</c> or from the
        /// private NEONFORGE image gallery for testing when <c>false</c>.  This is ignored
        /// for on-premise environments.
        /// </para>
        /// <note>
        /// Only NEONFORGE maintainers will have permission to use the private image.
        /// </note>
        /// </param>
        /// <param name="options">Specifies the cluster setup options.</param>
        /// <returns>The <see cref="ISetupController"/>.</returns>
        /// <exception cref="NeonKubeException">Thrown when there's a problem.</exception>
        public static async Task<ISetupController> CreateClusterSetupControllerAsync(
            ClusterDefinition   clusterDefinition,
            bool                cloudMarketplace,
            SetupClusterOptions options)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));
            Covenant.Requires<ArgumentNullException>(options != null, nameof(options));
            Covenant.Requires<ArgumentException>(options.MaxParallel > 0, nameof(options.MaxParallel));

            options.NeonCloudHeadendUri ??= KubeEnv.HeadendUri.ToString();

            clusterDefinition.Validate();

            // Determine where the log files should go.

            var logFolder = KubeHelper.LogFolder;

            // Ensure that the cluster's setup state file exists and that it
            // indicates that tyhe cluster was prepared.

            var contextName    = KubeContextName.Parse($"root@{clusterDefinition.Name}");
            var setupStatePath = KubeSetupState.GetPath((string)contextName);
            var setupState     = (KubeSetupState)null;

            if (!File.Exists(setupStatePath))
            {
                throw new NeonKubeException($"Cannot locate the [{setupStatePath}] file.  Cluster hasn't been prepared.");
            }

            try
            {
                setupState = KubeSetupState.Load((string)contextName);
            }
            catch (Exception e)
            {
                throw new NeonKubeException($"[{setupStatePath}] file is invalid.", e);
            }

            // Clear the log folder.

            if (Directory.Exists(logFolder))
            {
                NeonHelper.DeleteFolderContents(logFolder);
            }

            // Reload the the KubeConfig file to ensure we're up-to-date.

            KubeHelper.LoadConfig();

            // Do a quick check to ensure that component versions look reasonable.

            var kubernetesVersion = new Version(KubeVersions.Kubernetes);
            var crioVersion       = new Version(KubeVersions.Crio);

            if (crioVersion.Major != kubernetesVersion.Major || crioVersion.Minor != kubernetesVersion.Minor)
            {
                throw new NeonKubeException($"[{nameof(KubeConst)}.{nameof(KubeVersions.Crio)}={KubeVersions.Crio}] major and minor versions don't match [{nameof(KubeConst)}.{nameof(KubeVersions.Kubernetes)}={KubeVersions.Kubernetes}].");
            }

            // Initialize the cluster proxy.

            var cluster = ClusterProxy.Create(
                hostingManagerFactory: new HostingManagerFactory(() => HostingLoader.Initialize()),
                cloudMarketplace:      cloudMarketplace,
                operation:             ClusterProxy.Operation.Setup,
                setupState:            setupState,
                nodeProxyCreator:      (nodeName, nodeAddress) =>
                {
                    var logStream      = new FileStream(Path.Combine(logFolder, $"{nodeName}.log"), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
                    var logWriter      = new StreamWriter(logStream);
                    var context        = KubeHelper.CurrentContext;
                    var sshCredentials = setupState.SshCredentials ?? SshCredentials.FromUserPassword(KubeConst.SysAdminUser, KubeConst.SysAdminPassword);

                    return new NodeSshProxy<NodeDefinition>(nodeName, nodeAddress, sshCredentials, logWriter: logWriter);
                });

            if (options.Unredacted)
            {
                cluster.SecureRunOptions = RunOptions.None;
            }

            // Configure the setup controller.

            var controller = new SetupController<NodeDefinition>($"Setup [{cluster.Name}] cluster", cluster.Nodes, KubeHelper.LogFolder, disableConsoleOutput: options.DisableConsoleOutput)
            {
                MaxParallel     = options.MaxParallel > 0 ? options.MaxParallel: cluster.HostingManager.MaxParallel,
                LogBeginMarker  = "# CLUSTER-BEGIN-SETUP #########################################################",
                LogEndMarker    = "# CLUSTER-END-SETUP-SUCCESS ###################################################",
                LogFailedMarker = "# CLUSTER-END-SETUP-FAILED ####################################################"
            };

            // Create a [DesktopService] proxy so setup can perform some privileged operations 
            // from a non-privileged process.

            var desktopServiceProxy = new DesktopServiceProxy();

            // Configure the setup controller state.

            controller.Add(KubeSetupProperty.Preparing, false);
            controller.Add(KubeSetupProperty.ReleaseMode, KubeHelper.IsRelease);
            controller.Add(KubeSetupProperty.DebugMode, options.DebugMode);
            controller.Add(KubeSetupProperty.MaintainerMode, !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NC_ROOT")));
            controller.Add(KubeSetupProperty.ClusterProxy, cluster);
            controller.Add(KubeSetupProperty.HostingManager, cluster.HostingManager);
            controller.Add(KubeSetupProperty.HostingEnvironment, cluster.HostingManager.HostingEnvironment);
            controller.Add(KubeSetupProperty.NeonCloudHeadendClient, HeadendClient.Create());
            controller.Add(KubeSetupProperty.Redact, !options.Unredacted);
            controller.Add(KubeSetupProperty.DesktopReadyToGo, options.DesktopReadyToGo);
            controller.Add(KubeSetupProperty.DesktopServiceProxy, desktopServiceProxy);

            // Configure the setup steps.

            controller.AddGlobalStep("resource requirements", KubeSetup.CalculateResourceRequirements);

            cluster.HostingManager.AddSetupSteps(controller);

            controller.AddWaitUntilOnlineStep("connect nodes");
            controller.AddNodeStep("check node OS", (controller, node) => node.VerifyNodeOS());

            controller.AddNodeStep("check image version",
                (controller, node) =>
                {
                    // Ensure that the node image version matches the current neonKUBE (build) version.

                    var imageVersion = node.ImageVersion;

                        if (imageVersion == null)
                        {
                            throw new Exception($"Node image is not stamped with the image version file: {KubeConst.ImageVersionPath}");
                        }

                        if (!imageVersion.ToString().StartsWith(KubeVersions.NeonKube))
                        {
                            throw new Exception($"Node image version [{imageVersion}] does not match the neonKUBE version [{KubeVersions.NeonKube}] implemented by the current build.");
                        }
                });

            controller.AddNodeStep("disable cloud-init", (controller, node) => node.SudoCommand("touch /etc/cloud/cloud-init.disabled"));
            controller.AddNodeStep("node basics", (controller, node) => node.BaseInitialize(controller, upgradeLinux: false));
            controller.AddNodeStep("root certificates", (controller, node) => node.UpdateRootCertificates());
            controller.AddNodeStep("setup ntp", (controller, node) => node.SetupConfigureNtp(controller));
            controller.AddNodeStep("cluster manifest", ConfigureMetadataAsync);

            // Perform common configuration for the bootstrap node first.
            // We need to do this so the the package cache will be running
            // when the remaining nodes are configured.

            controller.AddNodeStep("setup control-plane",
                (controller, node) =>
                {
                    node.SetupNode(controller, KubeSetup.ClusterManifest);
                },
                (controller, node) => node == cluster.DeploymentControlNode);

            // Perform common configuration for the remaining nodes (if any).

            if (cluster.SetupState.ClusterDefinition.Nodes.Count() > 1)
            {
                controller.AddNodeStep("setup other nodes",
                    (controller, node) =>
                    {
                        node.SetupNode(controller, KubeSetup.ClusterManifest);
                        node.InvokeIdempotent("setup/setup-node-restart", () => node.Reboot(wait: true));
                    },
                    (controller, node) => node != cluster.DeploymentControlNode);
            }

            if (options.DebugMode)
            {
                controller.AddNodeStep("load container images", (controller, node) => node.NodeLoadImagesAsync(controller, downloadParallel: 5, loadParallel: 3));
            }

            controller.AddNodeStep("install helm",
                (controller, node) =>
                {
                    node.NodeInstallHelm(controller);
                });

            controller.AddNodeStep("install kustomize",
                (controller, node) =>
                {
                    node.NodeInstallKustomize(controller);
                });

            if (options.UploadCharts || options.DebugMode)
            {
                controller.AddNodeStep("upload helm charts",
                    (controller, node) =>
                    {
                        cluster.DeploymentControlNode.SudoCommand($"rm -rf {KubeNodeFolder.Helm}/*");
                        cluster.DeploymentControlNode.NodeInstallHelmArchive(controller);

                        var zipPath = LinuxPath.Combine(KubeNodeFolder.Helm, "charts.zip");

                        cluster.DeploymentControlNode.SudoCommand($"unzip {zipPath} -d {KubeNodeFolder.Helm}");
                        cluster.DeploymentControlNode.SudoCommand($"rm -f {zipPath}");
                    },
                    (controller, node) => node == cluster.DeploymentControlNode);
            }

            //-----------------------------------------------------------------
            // Cluster setup.

            controller.AddGlobalStep("setup cluster", controller => KubeSetup.SetupClusterAsync(controller));
            controller.AddGlobalStep("persist state",
                controller =>
                {
                    // Indicate that setup is complete.

                    setupState.DeploymentStatus = ClusterDeploymentStatus.Ready;
                    setupState.Save();

                    // Persist additional cluster setup details to the log folder.

                    CaptureClusterState(controller);
                });

            //-----------------------------------------------------------------
            // Give the hosting manager a chance to perform and post setup steps.

            cluster.HostingManager.AddPostSetupSteps(controller);

            //-----------------------------------------------------------------
            // Ensure that required pods are running for [neon-desktop] clusters.
            // This is required because no pods will be running when the [neon-desktop]
            // image first boots and it may take some time for the pods to start
            // and stablize.

            if (options.DesktopReadyToGo)
            {
                controller.AddGlobalStep("wait for stable cluster...", StabilizeClusterAsync);
            }

            // We need to dispose these after the setup controller runs.

            controller.AddDisposable(cluster);
            controller.AddDisposable(desktopServiceProxy);

            // Add a [Finished] event handler that uploads cluster deployment logs and
            // details to the headend for manalysis.  Note that we don't do this when telemetry
            // is disabled or when the cluster was deployed without redaction.

            controller.Finished += (s, a) => UploadDeploymentLogs((ISetupController)s, a);

            // Add a [Finished] event handler that removes the cluster setup state
            // when cluster setup completed successfully.

            controller.Finished +=
                (a, s) =>
                {
                    if (setupState.DeploymentStatus == ClusterDeploymentStatus.Ready)
                    {
                        setupState.Delete();
                    }
                };

            return controller;
        }

        /// <summary>
        /// Captures additional information about the cluster including things like cluster pod status
        /// and logs from failed cluster pod containers and persisting that to the logs folder.
        /// </summary>
        private static void CaptureClusterState(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            const string header = "===============================================================================";

            var clusterProxy              = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var redactedClusterDefinition = clusterProxy.SetupState.ClusterDefinition.Redact();
            var logFolder                 = KubeHelper.LogFolder;
            var logDetailsFolder          = KubeHelper.LogDetailsFolder;

            Directory.CreateDirectory(logDetailsFolder);

            //-----------------------------------------------------------------
            // FILE: pods.txt (output from: kubectl get pods -A

            var result = clusterProxy.DeploymentControlNode.SudoCommand("kubectl", "get", "pods", "-A");

            using (var stream = File.Create(Path.Combine(logDetailsFolder, "all-pods.txt")))
            {
                using (var writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    writer.WriteLine(header);
                    writer.WriteLine($"# kubectl get pods -A");
                    writer.WriteLine($"# EXITCODE: {result.ExitCode}");
                    writer.WriteLine();

                    using (var reader = new StringReader(result.AllText))
                    {
                        foreach (var line in reader.Lines())
                        {
                            writer.WriteLine(line);
                        }
                    }
                }
            }

            if (!result.Success)
            {
                // Don't bother with capturing any additional status if the [kubectl] command failed.

                return;
            }

            //-----------------------------------------------------------------
            // Query the pod status and write files detailing information about
            // failed pods and their pod and containers.
            //
            //      pod-NAMESPACE-PODNAME.yaml  - pod spec and status
            //      pod-NAMESPACE-PODNAME.log   -lists the failed pod containers and their logs
            //
            // The log files will include logs from any failed pod containers.

            using (var k8s = KubeHelper.GetKubernetesClient())
            {
                var pods = k8s.CoreV1.ListAllPodsAsync().Result;

                foreach (var failedPod in pods.Items.Where(pod => pod.Status.Phase == "Error"))
                {
                    // Write the [pod-NAMESPACE-PODNAME.yaml] file with the pod spec/status.

                    File.WriteAllText(Path.Combine(logDetailsFolder, $"pod-{failedPod.Namespace()}-{failedPod.Name()}.yaml"), NeonHelper.YamlSerialize(failedPod));

                    // Write the [pod-NAMESPACE-PODNAME.log] with logs from any failed pod containers.

                    using (var stream = File.Create(Path.Combine(logDetailsFolder, $"pod-{failedPod.Namespace()}-{failedPod.Name()}.log")))
                    {
                        using (var writer = new StreamWriter(stream, Encoding.UTF8))
                        {
                            writer.WriteLine(header);
                            writer.WriteLine($"# FAILED POD: {failedPod.Namespace()}/{failedPod.Name()}");

                            var failedPodStatuses = failedPod.Status.ContainerStatuses.Where(status => !status.Ready).ToList();

                            if (failedPodStatuses.Count == 0)
                            {
                                writer.WriteLine("# All pod containers are READY.");
                            }
                            else
                            {
                                writer.WriteLine($"# [{failedPodStatuses.Count}] containers are NOT READY.");

                                foreach (var failedContainerStatus in failedPodStatuses)
                                {
                                    writer.WriteLine();
                                    writer.WriteLine(header);
                                    writer.WriteLine($"# FAILED CONTAINER: name={failedContainerStatus.Name} image={failedContainerStatus.Image}");
                                    writer.WriteLine();

                                    using (var logStream = k8s.CoreV1.ReadNamespacedPodLog(failedPod.Name(), failedPod.Namespace(), failedContainerStatus.Name))
                                    {
                                        using (var reader = new StreamReader(logStream, Encoding.UTF8))
                                        {
                                            foreach (var line in reader.Lines())
                                            {
                                                writer.WriteLine(line);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
