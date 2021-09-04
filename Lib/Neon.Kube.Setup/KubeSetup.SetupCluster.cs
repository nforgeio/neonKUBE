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
        /// <param name="automate">
        /// Optionally specifies that the operation is to be performed in <b>automation mode</b>, where the
        /// current neonDESKTOP state will not be impacted.
        /// </param>
        /// <returns>The <see cref="ISetupController"/>.</returns>
        /// <exception cref="KubeException">Thrown when there's a problem.</exception>
        public static ISetupController CreateClusterSetupController(
            ClusterDefinition   clusterDefinition, 
            int                 maxParallel  = 500, 
            bool                unredacted   = false, 
            bool                debugMode    = false, 
            bool                uploadCharts = false,
            bool                automate     = false)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));
            Covenant.Requires<ArgumentException>(maxParallel > 0, nameof(maxParallel));

            // Create the automation subfolder for the operation if required and determine
            // where the log files should go.

            var automationFolder = (string)null;
            var logFolder        = KubeHelper.LogFolder;

            if (automate)
            {
                automationFolder = KubeHelper.CreateAutomationFolder();
                logFolder        = Path.Combine(automationFolder, logFolder);
            }

            // Initialize the cluster proxy.

            var cluster = new ClusterProxy(
                clusterDefinition:  clusterDefinition,
                nodeProxyCreator:   (nodeName, nodeAddress, appendToLog) =>
                {
                    var logWriter      = new StreamWriter(new FileStream(Path.Combine(logFolder, $"{nodeName}.log"), FileMode.Create, appendToLog ? FileAccess.Write : FileAccess.ReadWrite));
                    var sshCredentials = SshCredentials.FromUserPassword(KubeConst.SysAdminUser, KubeConst.SysAdminPassword);

                    return new NodeSshProxy<NodeDefinition>(nodeName, nodeAddress, sshCredentials, logWriter: logWriter);
                });

            if (unredacted)
            {
                cluster.SecureRunOptions = RunOptions.None;
            }

            // Configure the setup controller.

            var controller = new SetupController<NodeDefinition>($"Setup [{cluster.Definition.Name}] cluster", cluster.Nodes, KubeHelper.LogFolder)
            {
                MaxParallel     = maxParallel,
                LogBeginMarker  = "# CLUSTER-BEGIN-SETUP ############################################################",
                LogEndMarker    = "# CLUSTER-END-SETUP-SUCCESS ######################################################",
                LogFailedMarker = "# CLUSTER-END-SETUP-FAILED #######################################################"
            };

            // Configure the hosting manager.

            var hostingManager = cluster.GetHostingManager(new HostingManagerFactory(() => HostingLoader.Initialize()), ClusterProxy.Operation.Setup);

            if (hostingManager == null)
            {
                throw new KubeException($"No hosting manager for the [{cluster.Definition.Hosting.Environment}] environment could be located.");
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

            // Update the cluster node SSH credentials to use the secure password.

            var sshCredentials = SshCredentials.FromUserPassword(KubeConst.SysAdminUser, clusterLogin.SshPassword);

            foreach (var node in cluster.Nodes)
            {
                node.UpdateCredentials(sshCredentials);
            }

            // Configure the setup controller state.

            controller.Add(KubeSetupProperty.ReleaseMode, KubeHelper.IsRelease);
            controller.Add(KubeSetupProperty.DebugMode, debugMode);
            controller.Add(KubeSetupProperty.MaintainerMode, !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NC_ROOT")));
            controller.Add(KubeSetupProperty.ClusterProxy, cluster);
            controller.Add(KubeSetupProperty.ClusterLogin, clusterLogin);
            controller.Add(KubeSetupProperty.HostingManager, hostingManager);
            controller.Add(KubeSetupProperty.HostingEnvironment, hostingManager.HostingEnvironment);
            controller.Add(KubeSetupProperty.AutomationFolder, automationFolder);

            // Configure the setup steps.

            controller.AddGlobalStep("resource requirements", KubeSetup.CalculateResourceRequirements);
            controller.AddGlobalStep("download binaries", async controller => await KubeSetup.InstallWorkstationBinariesAsync(controller));
            controller.AddWaitUntilOnlineStep("connect nodes");
            controller.AddNodeStep("verify os", (controller, node) => node.VerifyNodeOS());
            controller.AddNodeStep("node basics", (controller, node) => node.BaseInitialize(controller, upgradeLinux: false));  // $todo(jefflill): We don't support Linux distribution upgrades yet.
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

            // We need to dispose this after the setup controller runs.

            controller.AddDisposable(hostingManager);

            return controller;
        }
    }
}
