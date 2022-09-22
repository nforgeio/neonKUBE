//-----------------------------------------------------------------------------
// FILE:	    KubeSetup.SetupCluster.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Neon.Collections;
using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Retry;
using Neon.SSH;
using Neon.Tasks;

namespace Neon.Kube
{
    public static partial class KubeSetup
    {
        /// <summary>
        /// Returns the cluster definition required to prepare a neonDESKTOP built-in cluster for 
        /// a specific hosting environment.
        /// </summary>
        /// <param name="hostEnvironment">Specifies the target environment.</param>
        /// <param name="deploymentPrefix">
        /// <para>
        /// Optionally specifies a deployment prefix string to be set as <see cref="DeploymentOptions.Prefix"/>
        /// in the cluster definition returned.  This can be used by <b>ClusterFixture</b> and custom tools
        /// to help isolated temporary cluster assets from production clusters.
        /// </para>
        /// <note>
        /// This parameter has no effect unless <see cref="KubeHelper.ClusterspaceMode"/> is set to something
        /// other than <see cref="KubeClusterspaceMode.Disabled"/>.
        /// </note>
        /// </param>
        /// <returns>The cluster definition.</returns>
        public static ClusterDefinition GetBuiltInClusterDefinition(HostingEnvironment hostEnvironment, string deploymentPrefix = null)
        {
            var resourceName = "Neon.Kube.ClusterDefinitions.";

            switch (hostEnvironment)
            {
                case HostingEnvironment.HyperV:

                    resourceName += "neon-desktop.hyperv.cluster.yaml";
                    break;

                case HostingEnvironment.Aws:
                case HostingEnvironment.Azure:
                case HostingEnvironment.BareMetal:
                case HostingEnvironment.Google:
                case HostingEnvironment.XenServer:

                default:

                    throw new NotSupportedException($"[{nameof(hostEnvironment)}={hostEnvironment}].");
            }

            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                using (var reader = new StreamReader(stream, encoding: Encoding.UTF8))
                {
                    var clusterDefinition = ClusterDefinition.FromYaml(reader.ReadToEnd());

                    clusterDefinition.Validate();
                    Covenant.Assert(clusterDefinition.NodeDefinitions.Count == 1, "Built-in cluster definitions must include exactly one node.");

                    if (!string.IsNullOrEmpty(deploymentPrefix))
                    {
                        clusterDefinition.Deployment.Prefix = deploymentPrefix;
                    }

                    return clusterDefinition;
                }
            }
        }

        /// <summary>
        /// Constructs the <see cref="ISetupController"/> to be used for setting up a cluster.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <param name="cloudMarketplace">
        /// <para>
        /// For cloud environments, this specifies whether the cluster should be provisioned
        /// using a VM image from the public cloud marketplace when <c>true</c> or from the
        /// private neonFORGE image gallery for testing when <c>false</c>.  This is ignored
        /// for on-premise environments.
        /// </para>
        /// <note>
        /// Only neonFORGE maintainers will have permission to use the private image.
        /// </note>
        /// </param>
        /// <param name="maxParallel">
        /// Optionally specifies the maximum number of node operations to be performed in parallel.
        /// This <b>defaults to 0</b> which means that we'll use <see cref="IHostingManager.MaxParallel"/>.
        /// </param>
        /// <param name="unredacted">
        /// Optionally indicates that sensitive information <b>won't be redacted</b> from the setup logs 
        /// (typically used when debugging).
        /// </param>
        /// <param name="debugMode">Optionally indicates that the cluster will be prepared in debug mode.</param>
        /// <param name="uploadCharts">
        /// <para>
        /// Optionally specifies that the current Helm charts should be uploaded to replace the charts in the base image.
        /// </para>
        /// <note>
        /// This will be treated as <c>true</c> when <paramref name="debugMode"/> is passed as <c>true</c>.
        /// </note>
        /// </param>
        /// <param name = "clusterspace" > Optionally specifies the clusterspace for the operation.</param>        
        /// <param name="neonCloudHeadendUri">Optionally overrides the neonCLOUD headend service URI.  This defaults to <see cref="KubeEnv.HeadendUri"/>.</param>
        /// <param name="disableConsoleOutput">
        /// Optionally disables status output to the console.  This is typically
        /// enabled for non-console applications.
        /// </param>
        /// <returns>The <see cref="ISetupController"/>.</returns>
        /// <exception cref="NeonKubeException">Thrown when there's a problem.</exception>
        public static ISetupController CreateClusterSetupController(
            ClusterDefinition   clusterDefinition,
            bool                cloudMarketplace,
            int                 maxParallel          = 500,
            bool                unredacted           = false,
            bool                debugMode            = false,
            bool                uploadCharts         = false,
            string              clusterspace         = null,
            string              neonCloudHeadendUri  = null,
            bool                disableConsoleOutput = false)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));
            Covenant.Requires<ArgumentException>(maxParallel > 0, nameof(maxParallel));

            neonCloudHeadendUri ??= KubeEnv.HeadendUri.ToString();

            clusterDefinition.Validate();

            // Determine where the log files should go.

            var logFolder = KubeHelper.LogFolder;

            // Ensure that the [prepare-ok] file in the log folder exists, indicating that
            // the last prepare operation succeeded.

            var prepareOkPath = Path.Combine(logFolder, "prepare-ok");

            if (!File.Exists(prepareOkPath))
            {
                throw new NeonKubeException($"Cannot locate the [{prepareOkPath}] file.  Cluster prepare must have failed.");
            }

            // Clear the log folder except for the [prepare-ok] file.

            if (Directory.Exists(logFolder))
            {
                foreach (var file in Directory.GetFiles(logFolder, "*", SearchOption.TopDirectoryOnly))
                {
                    if (Path.GetFileName(file) != "prepare-ok")
                    {
                        NeonHelper.DeleteFile(file);
                    }
                }
            }
            else
            {
                throw new DirectoryNotFoundException(logFolder);
            }

            // Reload the any KubeConfig file to ensure we're up-to-date.

            KubeHelper.LoadConfig();

            // Do a quick check to ensure that component versions look reasonable.

            var kubernetesVersion = new Version(KubeVersions.Kubernetes);
            var crioVersion       = new Version(KubeVersions.Crio);

            if (crioVersion.Major != kubernetesVersion.Major || crioVersion.Minor != kubernetesVersion.Minor)
            {
                throw new NeonKubeException($"[{nameof(KubeConst)}.{nameof(KubeVersions.Crio)}={KubeVersions.Crio}] major and minor versions don't match [{nameof(KubeConst)}.{nameof(KubeVersions.Kubernetes)}={KubeVersions.Kubernetes}].");
            }

            // Initialize the cluster proxy.

            var contextName = KubeContextName.Parse($"root@{clusterDefinition.Name}");
            var kubeContext = new KubeConfigContext(contextName);

            KubeHelper.InitContext(kubeContext);

            ClusterProxy cluster = null;

            cluster = new ClusterProxy(
                hostingManagerFactory:  new HostingManagerFactory(() => HostingLoader.Initialize()),
                cloudMarketplace:       cloudMarketplace,
                operation:              ClusterProxy.Operation.Setup,
                clusterDefinition:      clusterDefinition,
                nodeProxyCreator:       (nodeName, nodeAddress) =>
                {
                    var logStream      = new FileStream(Path.Combine(logFolder, $"{nodeName}.log"), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
                    var logWriter      = new StreamWriter(logStream);
                    var context        = KubeHelper.CurrentContext;
                    var sshCredentials = context.Extension.SshCredentials ?? SshCredentials.FromUserPassword(KubeConst.SysAdminUser, KubeConst.SysAdminPassword);

                    return new NodeSshProxy<NodeDefinition>(nodeName, nodeAddress, sshCredentials, logWriter: logWriter);
                });

            if (unredacted)
            {
                cluster.SecureRunOptions = RunOptions.None;
            }

            // Load the cluster login information if it exists and when it indicates that
            // setup is still pending, we'll use that information (especially the generated
            // secure SSH password).
            //
            // Otherwise, we'll write (or overwrite) the context file with a fresh context.

            var clusterLoginPath = KubeHelper.GetClusterLoginPath((KubeContextName)$"{KubeConst.RootUser}@{clusterDefinition.Name}");
            var clusterLogin     = ClusterLogin.Load(clusterLoginPath);

            if (clusterLogin == null || !clusterLogin.SetupDetails.SetupPending)
            {
                clusterLogin = new ClusterLogin(clusterLoginPath)
                {
                    ClusterDefinition = clusterDefinition,
                    SshUsername       = KubeConst.SysAdminUser,
                    SetupDetails      = new KubeSetupDetails() { SetupPending = true }
                };

                clusterLogin.Save();
            }

            // Configure the setup controller.

            var controller = new SetupController<NodeDefinition>($"Setup [{cluster.Definition.Name}] cluster", cluster.Nodes, KubeHelper.LogFolder, disableConsoleOutput: disableConsoleOutput)
            {
                MaxParallel     = maxParallel > 0 ? maxParallel: cluster.HostingManager.MaxParallel,
                LogBeginMarker  = "# CLUSTER-BEGIN-SETUP #########################################################",
                LogEndMarker    = "# CLUSTER-END-SETUP-SUCCESS ###################################################",
                LogFailedMarker = "# CLUSTER-END-SETUP-FAILED ####################################################"
            };

            // Configure the setup controller state.

            controller.Add(KubeSetupProperty.Preparing, false);
            controller.Add(KubeSetupProperty.ReleaseMode, KubeHelper.IsRelease);
            controller.Add(KubeSetupProperty.DebugMode, debugMode);
            controller.Add(KubeSetupProperty.MaintainerMode, !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NC_ROOT")));
            controller.Add(KubeSetupProperty.ClusterProxy, cluster);
            controller.Add(KubeSetupProperty.ClusterLogin, clusterLogin);
            controller.Add(KubeSetupProperty.HostingManager, cluster.HostingManager);
            controller.Add(KubeSetupProperty.HostingEnvironment, cluster.HostingManager.HostingEnvironment);
            controller.Add(KubeSetupProperty.ClusterspaceFolder, clusterspace);
            controller.Add(KubeSetupProperty.NeonCloudHeadendClient, new HeadendClient(new HttpClient() { BaseAddress = new Uri(neonCloudHeadendUri) }));
            controller.Add(KubeSetupProperty.Redact, !unredacted);

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

                        if (imageVersion != SemanticVersion.Parse(KubeVersions.NeonKube))
                        {
                            throw new Exception($"Node image version [{imageVersion}] does not match the neonKUBE version [{KubeVersions.NeonKube}] implemented by the current build.");
                        }
                });

            controller.AddNodeStep("disable cloud-init", (controller, node) => node.SudoCommand("touch /etc/cloud/cloud-init.disabled"));
            controller.AddNodeStep("node basics", (controller, node) => node.BaseInitialize(controller, upgradeLinux: false));  // $todo(jefflill): We don't support Linux distribution upgrades yet.
            controller.AddNodeStep("root certificates", (controller, node) => node.UpdateRootCertificates());
            controller.AddNodeStep("setup ntp", (controller, node) => node.SetupConfigureNtp(controller));
            controller.AddNodeStep("cluster metadata", ConfigureMetadataAsync);

            // Perform common configuration for the bootstrap node first.
            // We need to do this so the the package cache will be running
            // when the remaining nodes are configured.

            var configureControlPlaneStepLabel = cluster.Definition.ControlNodes.Count() > 1 ? "setup first control-plane node" : "setup control-plane node";

            controller.AddNodeStep(configureControlPlaneStepLabel,
                (controller, node) =>
                {
                    node.SetupNode(controller, KubeSetup.ClusterManifest);
                },
                (controller, node) => node == cluster.FirstControlNode);

            // Perform common configuration for the remaining nodes (if any).

            if (cluster.Definition.Nodes.Count() > 1)
            {
                controller.AddNodeStep("setup other nodes",
                    (controller, node) =>
                    {
                        node.SetupNode(controller, KubeSetup.ClusterManifest);
                        node.InvokeIdempotent("setup/setup-node-restart", () => node.Reboot(wait: true));
                    },
                    (controller, node) => node != cluster.FirstControlNode);
            }

            if (debugMode)
            {
                controller.AddNodeStep("load images", (controller, node) => node.NodeLoadImagesAsync(controller, downloadParallel: 5, loadParallel: 3));
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

            if (uploadCharts || debugMode)
            {
                controller.AddNodeStep("upload helm charts",
                    (controller, node) =>
                    {
                        cluster.FirstControlNode.SudoCommand($"rm -rf {KubeNodeFolder.Helm}/*");
                        cluster.FirstControlNode.NodeInstallHelmArchive(controller);

                        var zipPath = LinuxPath.Combine(KubeNodeFolder.Helm, "charts.zip");

                        cluster.FirstControlNode.SudoCommand($"unzip {zipPath} -d {KubeNodeFolder.Helm}");
                        cluster.FirstControlNode.SudoCommand($"rm -f {zipPath}");
                    },
                    (controller, node) => node == cluster.FirstControlNode);
            }

            //-----------------------------------------------------------------
            // Cluster setup.

            controller.AddGlobalStep("setup cluster", controller => KubeSetup.SetupClusterAsync(controller));
            controller.AddGlobalStep("persist state",
                controller =>
                {
                    // Indicate that setup is complete.

                    clusterLogin.ClusterDefinition.ClearSetupState();
                    clusterLogin.SetupDetails.SetupPending = false;
                    clusterLogin.Save();
                });

            //-----------------------------------------------------------------
            // Verify the cluster.

            controller.AddNodeStep("check control-plane nodes",
                (controller, node) =>
                {
                    KubeDiagnostics.CheckControlNode(node, cluster.Definition);
                },
                (controller, node) => node.Metadata.IsControlPane);

            if (cluster.Workers.Count() > 0)
            {
                controller.AddNodeStep("check workers",
                    (controller, node) =>
                    {
                        KubeDiagnostics.CheckWorker(node, cluster.Definition);
                    },
                    (controller, node) => node.Metadata.IsWorker);
            }

            cluster.HostingManager.AddPostSetupSteps(controller);

            // We need to dispose this after the setup controller runs.

            controller.AddDisposable(cluster);

            return controller;
        }
    }
}