//-----------------------------------------------------------------------------
// FILE:	    KubeSetup.SetupCluster.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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

using ICSharpCode.SharpZipLib.Zip;
using k8s;
using k8s.Models;
using Microsoft.Rest;
using Newtonsoft.Json.Linq;

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
        /// Returns the cluster definition required to prepare a ready-to-go cluster for 
        /// a specific hosting environment.
        /// </summary>
        /// <param name="hostEnvironment">Specifies the target environment.</param>
        /// <param name="deploymentPrefix">
        /// <para>
        /// Optionally specifies a deployment prefix string to be set as <see cref="DeploymentOptions.Prefix"/>
        /// in the cluster definition returned.  This can be used by <b>KubernetesFixture</b> and custom tools
        /// to help isolated temporary cluster assets from production clusters.
        /// </para>
        /// <note>
        /// This parameter has no effect unless <see cref="KubeHelper.AutomationMode"/> is set to something
        /// other than <see cref="KubeAutomationMode.Disabled"/>.
        /// </note>
        /// </param>
        /// <returns>The cluster definition.</returns>
        /// <exception cref="NotSupportedException">Thrown when the <paramref name="hostEnvironment"/> does not (yet) support ready-to-go.</exception>
        public static ClusterDefinition GetReadyToGoClusterDefinition(HostingEnvironment hostEnvironment, string deploymentPrefix = null)
        {
            // $todo(jefflill):
            //
            // We'll need to flesh this out as we support ready-to-go for more hosting environments.

            var resourceName = "Neon.Kube.ClusterDefinitions.";

            switch (hostEnvironment)
            {
                case HostingEnvironment.HyperV:
                case HostingEnvironment.HyperVLocal:

                    resourceName += "neon-desktop.hyperv.cluster.yaml";
                    break;

                case HostingEnvironment.Wsl2:

                    resourceName += "neon-desktop.wsl2.cluster.yaml";
                    break;

                case HostingEnvironment.Aws:
                case HostingEnvironment.Azure:
                case HostingEnvironment.BareMetal:
                case HostingEnvironment.Google:
                case HostingEnvironment.XenServer:

                default:

                    throw new NotSupportedException($"Ready-To-Go is not yet supported for [{hostEnvironment}].");
            }

            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                using (var reader = new StreamReader(stream, encoding: Encoding.UTF8))
                {
                    var clusterDefinition = ClusterDefinition.FromYaml(reader.ReadToEnd());

                    clusterDefinition.Validate();
                    Covenant.Assert(clusterDefinition.NodeDefinitions.Count == 1, "Ready-to-go cluster definitions must include exactly one node.");

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
        /// <param name="maxParallel">
        /// Optionally specifies the maximum number of node operations to be performed in parallel.
        /// This <b>defaults to 500</b> which is effectively infinite.
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
        /// <param name="automationFolder">
        /// Optionally specifies that the operation is to be performed in <b>automation mode</b> by specifying
        /// the non-default directory where cluster state such as logs, logins, etc. will be written, overriding
        /// the default <b>$(USERPROFILE)\.neonkube</b> directory.
        /// </param>
        /// <param name="readyToGoMode">
        /// Optionally creates a setup controller that prepares and partially sets up a ready-to-go image or completes
        /// the cluster setup for a provisioned ready-to-go cluster.  This defaults to <see cref="ReadyToGoMode.Normal"/>.
        /// </param>
        /// <returns>The <see cref="ISetupController"/>.</returns>
        /// <exception cref="KubeException">Thrown when there's a problem.</exception>
        /// <remarks>
        /// <para>
        /// Node images prepared as <b>ready-to-go</b> can be identified by the presence of a 
        /// <b>/etc/neonkube/image-type</b> file set to <see cref="KubeImageType.ReadyToGo"/>.
        /// is passed.
        /// </para>
        /// </remarks>
        public static ISetupController CreateClusterSetupController(
            ClusterDefinition   clusterDefinition, 
            int                 maxParallel      = 500, 
            bool                unredacted       = false, 
            bool                debugMode        = false, 
            bool                uploadCharts     = false,
            string              automationFolder = null,
            ReadyToGoMode       readyToGoMode    = ReadyToGoMode.Normal)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));
            Covenant.Requires<ArgumentException>(maxParallel > 0, nameof(maxParallel));

            if (debugMode && readyToGoMode != ReadyToGoMode.Normal)
            {
                throw new ArgumentException($"[{nameof(readyToGoMode)}] must be [{ReadyToGoMode.Normal}] when [{nameof(debugMode)}=TRUE].");
            }

            // Create the automation subfolder for the operation if required and determine
            // where the log files should go.

            var logFolder = KubeHelper.LogFolder;

            if (!string.IsNullOrEmpty(automationFolder))
            {
                logFolder = Path.Combine(automationFolder, logFolder);
            }

            // Initialize the cluster proxy.

            var contextName  = KubeContextName.Parse($"root@{clusterDefinition.Name}");
            var kubeContext  = new KubeConfigContext(contextName);

            KubeHelper.InitContext(kubeContext);

            var cluster = new ClusterProxy(
                hostingManagerFactory:  new HostingManagerFactory(() => HostingLoader.Initialize()),
                operation:              ClusterProxy.Operation.Setup,
                clusterDefinition:      clusterDefinition,
                nodeProxyCreator:       (nodeName, nodeAddress, appendToLog) =>
                {
                    var logStream = new FileStream(Path.Combine(logFolder, $"{nodeName}.log"), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);

                    if (appendToLog)
                    {
                        logStream.Seek(0, SeekOrigin.End);
                    }

                    var logWriter      = new StreamWriter(logStream);
                    var sshCredentials = SshCredentials.FromUserPassword(KubeConst.SysAdminUser, KubeConst.SysAdminPassword);

                    return new NodeSshProxy<NodeDefinition>(nodeName, nodeAddress, sshCredentials, logWriter: logWriter);
                });

            if (unredacted)
            {
                cluster.SecureRunOptions = RunOptions.None;
            }

            using (cluster)
            {
                // Configure the setup controller.

                var controller = new SetupController<NodeDefinition>($"Setup [{cluster.Definition.Name}] cluster", cluster.Nodes, KubeHelper.LogFolder)
                {
                    MaxParallel     = maxParallel,
                    LogBeginMarker  = "# CLUSTER-BEGIN-SETUP ############################################################",
                    LogEndMarker    = "# CLUSTER-END-SETUP-SUCCESS ######################################################",
                    LogFailedMarker = "# CLUSTER-END-SETUP-FAILED #######################################################"
                };

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

                // Update the cluster node SSH credentials to use the secure password
                // when we're not preparing a ready-to-go image.

                if (readyToGoMode != ReadyToGoMode.Prepare)
                {
                    var sshCredentials = SshCredentials.FromUserPassword(KubeConst.SysAdminUser, clusterLogin.SshPassword);

                    foreach (var node in cluster.Nodes)
                    {
                        node.UpdateCredentials(sshCredentials);
                    }
                }

                // Configure the setup controller state.

                controller.Add(KubeSetupProperty.ReleaseMode, KubeHelper.IsRelease);
                controller.Add(KubeSetupProperty.DebugMode, debugMode);
                controller.Add(KubeSetupProperty.MaintainerMode, !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NC_ROOT")));
                controller.Add(KubeSetupProperty.ClusterProxy, cluster);
                controller.Add(KubeSetupProperty.ClusterLogin, clusterLogin);
                controller.Add(KubeSetupProperty.HostingManager, cluster.HostingManager);
                controller.Add(KubeSetupProperty.HostingEnvironment, cluster.HostingManager.HostingEnvironment);
                controller.Add(KubeSetupProperty.AutomationFolder, automationFolder);
                controller.Add(KubeSetupProperty.ReadyToGoMode, readyToGoMode);

                // Configure the setup steps.

                controller.AddGlobalStep("resource requirements", KubeSetup.CalculateResourceRequirements);
                controller.AddGlobalStep("download binaries", async controller => await KubeSetup.InstallWorkstationBinariesAsync(controller));
                controller.AddWaitUntilOnlineStep("connect nodes");
                controller.AddNodeStep("check node OS", (controller, node) => node.VerifyNodeOS());

                controller.AddNodeStep("check image version",
                    (state, node) =>
                    {
                        // Ensure that the node image version matches the current neonKUBE (build) version.

                        var imageVersion = node.ImageVersion;

                        if (imageVersion == null)
                        {
                            throw new Exception("Node image is not stamped with the image version.  You'll need to regenerate the node image.");
                        }

                        if (imageVersion != SemanticVersion.Parse(KubeConst.NeonKubeVersion))
                        {
                            throw new Exception($"Node image version [{imageVersion}] does not match the neonKUBE version [{KubeConst.NeonKubeVersion}] implemented by the current build.");
                        }
                    });

                if (readyToGoMode == ReadyToGoMode.Setup)
                {
                    controller.AddNodeStep("verify ready-to-go image", (controller, node) => node.VerifyImageIsReadyToGo(controller));
                }

                controller.AddNodeStep("node basics", (controller, node) => node.BaseInitialize(controller, upgradeLinux: false));  // $todo(jefflill): We don't support Linux distribution upgrades yet.
                controller.AddNodeStep("root certificates", (controller, node) => node.UpdateRootCertificates());
                controller.AddNodeStep("setup ntp", (controller, node) => node.SetupConfigureNtp(controller));

                // Perform common configuration for the bootstrap node first.
                // We need to do this so the the package cache will be running
                // when the remaining nodes are configured.

                var configureFirstMasterStepLabel = cluster.Definition.Masters.Count() > 1 ? "setup first master" : "setup master";

                controller.AddNodeStep(configureFirstMasterStepLabel,
                    (controller, node) =>
                    {
                        node.SetupNode(controller);
                        //exitnode.InvokeIdempotent("setup/setup-node-restart", () => node.Reboot(wait: true));
                    },
                    (controller, node) => node == cluster.FirstMaster);
            
                // Perform common configuration for the remaining nodes (if any).

                if (cluster.Definition.Nodes.Count() > 1)
                {
                    controller.AddNodeStep("setup other nodes",
                        (controller, node) =>
                        {
                            node.SetupNode(controller);
                            node.InvokeIdempotent("setup/setup-node-restart", () => node.Reboot(wait: true));
                        },
                        (controller, node) => node != cluster.FirstMaster);
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

                if (uploadCharts || debugMode)
                {
                    controller.AddNodeStep("upload helm charts",
                        (controller, node) =>
                        {
                            cluster.FirstMaster.SudoCommand($"rm -rf {KubeNodeFolders.Helm}/*");
                            cluster.FirstMaster.NodeInstallHelmArchive(controller);

                            var zipPath = LinuxPath.Combine(KubeNodeFolders.Helm, "charts.zip");

                            cluster.FirstMaster.SudoCommand($"unzip {zipPath} -d {KubeNodeFolders.Helm}");
                            cluster.FirstMaster.SudoCommand($"rm -f {zipPath}");
                        },
                        (controller, node) => node == cluster.FirstMaster);
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

                controller.AddNodeStep("check masters",
                    (controller, node) =>
                    {
                        KubeDiagnostics.CheckMaster(node, cluster.Definition);
                    },
                    (controller, node) => node.Metadata.IsMaster);

                if (cluster.Workers.Count() > 0)
                {
                    controller.AddNodeStep("check workers",
                        (controller, node) =>
                        {
                            KubeDiagnostics.CheckWorker(node, cluster.Definition);
                        },
                        (controller, node) => node.Metadata.IsWorker);
                }

                return controller;
            }
        }
    }
}
