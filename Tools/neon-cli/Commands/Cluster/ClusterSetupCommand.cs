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
                          use for production hives.

    --force             - Don't prompt before removing existing contexts
                          that reference the target cluster.

    --upload-charts     - Upload helm charts to node before setup. This
                          is useful when developing.
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
        public override string[] ExtendedOptions => new string[] { "--unredacted", "--force", "--upload-charts" };

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

            var contextName = KubeContextName.Parse(commandLine.Arguments[0]);
            var kubeCluster = KubeHelper.Config.GetCluster(contextName.Cluster);

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

            // Get on with cluster setup.

            var failed = false;

            try
            {
                await KubeHelper.Desktop.StartOperationAsync($"Setting up [{cluster.Name}]");

                // Configure global options.

                if (commandLine.HasOption("--unredacted"))
                {
                    cluster.SecureRunOptions = RunOptions.None;
                }

                // Perform the setup operations.

                var setupController =
                    new SetupController<NodeDefinition>(new string[] { "cluster", "setup", $"[{cluster.Name}]" }, cluster.Nodes)
                    {
                        ShowStatus  = !Program.Quiet,
                        MaxParallel = Program.MaxParallel,
                        ShowElapsed = true
                    };

                // Configure the setup controller state.

                setupController.Add(KubeSetup.ClusterProxyProperty, cluster);
                setupController.Add(KubeSetup.ClusterLoginProperty, clusterLogin);
                setupController.Add(KubeSetup.HostingManagerProperty, hostingManager);
                setupController.Add(KubeSetup.HostingEnvironmentProperty, hostingManager.HostingEnvironment);

                // Configure the setup steps.

                setupController.AddGlobalStep("download binaries", async state => await KubeSetup.InstallWorkstationBinariesAsync(state));
                setupController.AddWaitUntilOnlineStep("connect");
                setupController.AddNodeStep("verify OS", (state, node) => node.VerifyNodeOS());
                setupController.AddNodeStep("setup NTP", (state, node) => node.SetupConfigureNtp());

                // Write the operation begin marker to all cluster node logs.

                cluster.LogLine(logBeginMarker);

                // Perform common configuration for the bootstrap node first.
                // We need to do this so the the package cache will be running
                // when the remaining nodes are configured.

                var configureFirstMasterStepLabel = cluster.Definition.Masters.Count() > 1 ? "setup first master" : "setup master";

                setupController.AddNodeStep(configureFirstMasterStepLabel,
                    (state, node) =>
                    {
                        node.SetupNode(setupController);
                        //node.InvokeIdempotent("setup/common-restart", () => node.RebootAndWait(state));
                    },
                    (state, node) => node == cluster.FirstMaster);

                // Perform common configuration for the remaining nodes (if any).

                if (cluster.Definition.Nodes.Count() > 1)
                {
                    setupController.AddNodeStep("setup other nodes",
                        (state, node) =>
                        {
                            node.SetupNode(setupController);
                            node.InvokeIdempotent("setup/common-restart", () => node.RebootAndWait(setupController));
                        },
                        (state, node) => node != cluster.FirstMaster);
                }

                if (commandLine.HasOption("--upload-charts"))
                {
                    setupController.AddNodeStep("upload Helm charts",
                        (state, node) =>
                        {
                            cluster.FirstMaster.SudoCommand($"rm -rf {KubeNodeFolders.Helm}/*");
                            cluster.FirstMaster.NodeInstallHelmArchive(state, message => Console.WriteLine(message));

                            var zipPath = LinuxPath.Combine(KubeNodeFolders.Helm, "charts.zip");

                            cluster.FirstMaster.SudoCommand($"unzip {zipPath} -d {KubeNodeFolders.Helm}");
                            cluster.FirstMaster.SudoCommand($"rm -f {zipPath}");
                        },
                        (state, node) => node == cluster.FirstMaster);
                }

                //-----------------------------------------------------------------
                // Kubernetes configuration.

                setupController.AddNodeStep("install kubernetes", (setupState, node) => node.NodeInstallKubernetes(setupState));
                setupController.AddGlobalStep("setup cluster", setupState => KubeSetup.SetupClusterAsync(setupState));

                //-----------------------------------------------------------------
                // Verify the cluster.

                setupController.AddNodeStep("check masters",
                    (state, node) =>
                    {
                        KubeDiagnostics.CheckMaster(node, cluster.Definition);
                    },
                    (state, node) => node.Metadata.IsMaster);

                setupController.AddNodeStep("check workers",
                    (state, node) =>
                    {
                        KubeDiagnostics.CheckWorker(node, cluster.Definition);
                    },
                    (state, node) => node.Metadata.IsWorker);

                // Start setup.

                if (!setupController.Run())
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
            }
            catch
            {
                failed = true;
                throw;
            }
            finally
            {
                if (!failed)
                {
                    await KubeHelper.Desktop.EndOperationAsync($"Cluster [{cluster.Name}] is ready for use.");
                }
                else
                {
                    await KubeHelper.Desktop.EndOperationAsync($"Cluster [{cluster.Name}] setup failed.", failed: true);
                }
            }

            Console.WriteLine();
        }
    }
}