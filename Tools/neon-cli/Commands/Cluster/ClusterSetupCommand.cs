//-----------------------------------------------------------------------------
// FILE:	    ClusterSetupCommand.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Kube;
using Neon.Net;
using Neon.Retry;
using Neon.SSH;
using Neon.Time;

using k8s;
using k8s.Models;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>cluster setup</b> command.
    /// </summary>
    [Command]
    public class ClusterSetupCommand : CommandBase
    {
        //---------------------------------------------------------------------
        // Implementation

        private const string usage = @"
Configures a neonKUBE cluster as described in the cluster definition file.

USAGE: 

    neon cluster setup [OPTIONS] sysadmin@CLUSTER-NAME  

OPTIONS:

    --unredacted        - Runs Vault and other commands with potential
                          secrets without redacting logs.  This is useful 
                          for debugging cluster setup  issues.  Do not
                          use for production clusters.

    --force             - Don't prompt before removing existing contexts
                          that reference the target cluster.

    --upload-charts     - Upload helm charts to node before setup. This
                          is useful when debugging.

    --debug             - Implements cluster setup from the base rather
                          than the node image.  This mode is useful while
                          developing and debugging cluster setup.  This
                          implies [--upload-charts].

                          NOTE: This mode is not supported for cloud and
                                bare-metal environments.

    --base-image-name   - Specifies the base image name to use when operating
                          in [--debug] mode.  This will be the name of the base
                          image file as published to our public S3 bucket for
                          the target hosting manager.  Examples:

                                Hyper-V:   ubuntu-20.04.1.hyperv.vhdx
                                WSL2:      ubuntu-20.04.20210206.wsl2.tar
                                XenServer: ubuntu-20.04.1.xenserver.xva

                          NOTE: This is required for [--debug]
";
        private const string        logBeginMarker  = "# CLUSTER-BEGIN-SETUP ############################################################";
        private const string        logEndMarker    = "# CLUSTER-END-SETUP-SUCCESS ######################################################";
        private const string        logFailedMarker = "# CLUSTER-END-SETUP-FAILED #######################################################";

        private KubeConfigContext   kubeContext;
        private ClusterLogin        clusterLogin;
        private ClusterProxy        cluster;
        private HostingManager      hostingManager; 

        /// <inheritdoc/>
        public override string[] Words => new string[] { "cluster", "setup" };

        /// <inheritdoc/>
        public override string[] ExtendedOptions => new string[] { "--unredacted", "--force", "--upload-charts", "--debug", "--base-image-name" };

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override async Task RunAsync(CommandLine commandLine)
        {
            if (commandLine.Arguments.Length < 1)
            {
                Console.Error.WriteLine("*** ERROR: [sysadmin@CLUSTER-NAME] argument is required.");
                Program.Exit(1);
            }

            var contextName   = KubeContextName.Parse(commandLine.Arguments[0]);
            var kubeCluster   = KubeHelper.Config.GetCluster(contextName.Cluster);
            var debug         = commandLine.HasOption("--debug");
            var uploadCharts  = commandLine.HasOption("--upload-charts") || debug;

            clusterLogin = KubeHelper.GetClusterLogin(contextName);

            if (clusterLogin == null)
            {
                Console.Error.WriteLine($"*** ERROR: Be sure to prepare the cluster first via [neon cluster prepare...].");
                Program.Exit(1);
            }

            if (string.IsNullOrEmpty(clusterLogin.SshPassword))
            {
                Console.Error.WriteLine($"*** ERROR: No cluster node SSH password found.");
                Program.Exit(1);
            }

            if (kubeCluster != null && !clusterLogin.SetupDetails.SetupPending)
            {
                if (commandLine.GetOption("--force") == null && !Program.PromptYesNo($"One or more logins reference [{kubeCluster.Name}].  Do you wish to delete these?"))
                {
                    Program.Exit(0);
                }

                // Remove the cluster from the kubeconfig and remove any 
                // contexts that reference it.

                KubeHelper.Config.Clusters.Remove(kubeCluster);

                var delList = new List<KubeConfigContext>();

                foreach (var context in KubeHelper.Config.Contexts)
                {
                    if (context.Properties.Cluster == kubeCluster.Name)
                    {
                        delList.Add(context);
                    }
                }

                foreach (var context in delList)
                {
                    KubeHelper.Config.Contexts.Remove(context);
                }

                if (KubeHelper.CurrentContext != null && KubeHelper.CurrentContext.Properties.Cluster == kubeCluster.Name)
                {
                    KubeHelper.Config.CurrentContext = null;
                }

                KubeHelper.Config.Save();
            }

            kubeContext = new KubeConfigContext(contextName);

            KubeHelper.InitContext(kubeContext);

            // Initialize the cluster proxy and the hbosting manager.

            cluster = new ClusterProxy(kubeContext, Program.CreateNodeProxy<NodeDefinition>, appendToLog: true, defaultRunOptions: RunOptions.LogOutput | RunOptions.FaultOnError);

            hostingManager = new HostingManagerFactory(() => HostingLoader.Initialize()).GetManager(cluster, Program.LogPath);

            if (hostingManager == null)
            {
                Console.Error.WriteLine($"*** ERROR: No hosting manager for the [{cluster.Definition.Hosting.Environment}] environment could be located.");
                Program.Exit(1);
            }

#if ENTERPRISE
            if (hostingManager.HostingEnvironment == HostingEnvironment.Wsl2)
            {
                var wsl2Proxy = new Wsl2Proxy(KubeConst.Wsl2MainDistroName, KubeConst.SysAdminUser);
                
                cluster.FirstMaster.Address = IPAddress.Parse(wsl2Proxy.Address);
            }
#endif

            // Update the cluster node SSH credentials to use the secure password.

            var sshCredentials = SshCredentials.FromUserPassword(KubeConst.SysAdminUser, clusterLogin.SshPassword);

            foreach (var node in cluster.Nodes)
            {
                node.UpdateCredentials(sshCredentials);
            }

            // Configure global options.

            if (commandLine.HasOption("--unredacted"))
            {
                cluster.SecureRunOptions = RunOptions.None;
            }

            // Perform the setup operations.

            var controller =
                new SetupController<NodeDefinition>(new string[] { "cluster", "setup", $"[{cluster.Name}]" }, cluster.Nodes)
                {
                    ShowStatus  = !Program.Quiet,
                    MaxParallel = Program.MaxParallel,
                    ShowElapsed = true
                };

            // Configure the setup controller state.

            controller.Add(KubeSetup.DebugModeProperty, debug);
            controller.Add(KubeSetup.ReleaseModeProperty, Program.IsRelease);
            controller.Add(KubeSetup.MaintainerModeProperty, !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NC_ROOT")));
            controller.Add(KubeSetup.ClusterProxyProperty, cluster);
            controller.Add(KubeSetup.ClusterLoginProperty, clusterLogin);
            controller.Add(KubeSetup.HostingManagerProperty, hostingManager);
            controller.Add(KubeSetup.HostingEnvironmentProperty, hostingManager.HostingEnvironment);

            // Configure the setup steps.

            controller.AddGlobalStep("download binaries", async controller => await KubeSetup.InstallWorkstationBinariesAsync(controller));
            controller.AddWaitUntilOnlineStep("connect");
            controller.AddNodeStep("verify OS", (controller, node) => node.VerifyNodeOS());

            // $todo(jefflill): We don't support Linux distribution upgrades yet.
            controller.AddNodeStep("node basics", (controller, node) => node.BaseInitialize(controller, upgradeLinux: false));

            controller.AddNodeStep("setup NTP", (controller, node) => node.SetupConfigureNtp(controller));

            // Write the operation begin marker to all cluster node logs.

            cluster.LogLine(logBeginMarker);

            // Perform common configuration for the bootstrap node first.
            // We need to do this so the the package cache will be running
            // when the remaining nodes are configured.

            var configureFirstMasterStepLabel = cluster.Definition.Masters.Count() > 1 ? "setup first master" : "setup master";

            controller.AddNodeStep(configureFirstMasterStepLabel,
                (controller, node) =>
                {
                    node.SetupNode(controller);
                    //node.InvokeIdempotent("setup/setup-node-restart", () => node.Reboot(wait: true));
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

            if (commandLine.HasOption("--debug"))
            {
                controller.AddNodeStep("load images", (controller, node) => node.NodeLoadImagesAsync(controller, downloadParallel: 5, loadParallel: 3));
            }

            controller.AddNodeStep("install helm",
                (controller, node) =>
                {
                    node.NodeInstallHelm(controller);
                });

            if (commandLine.HasOption("--upload-charts") || debug)
            {
                controller.AddNodeStep("upload Helm charts",
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

            // Start setup.

            if (!controller.Run())
            {
                // Write the operation end/failed marker to all cluster node logs.

                cluster.LogLine(logFailedMarker);

                Console.Error.WriteLine("*** ERROR: One or more configuration steps failed.");
                Program.Exit(1);
            }

            // Indicate that setup is complete.

            clusterLogin.ClusterDefinition.ClearSetupState();
            clusterLogin.SetupDetails.SetupPending = false;
            clusterLogin.Save();

            // Write the operation end marker to all cluster node logs.

            cluster.LogLine(logEndMarker);

            Console.WriteLine();

            await Task.CompletedTask;
        }
    }
}