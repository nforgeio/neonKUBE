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

using k8s;
using k8s.Models;

using Microsoft.Extensions.DependencyInjection;

using Neon.Common;
using Neon.Cryptography;
using Neon.Deployment;
using Neon.IO;
using Neon.Kube;
using Neon.Net;
using Neon.Retry;
using Neon.SSH;
using Neon.Time;

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

    --automate          - Indicates that the command must not impact neonDESKTOP
                          by changing the current login or Kubernetes config or
                          other files like cluster deployment logs.  This is
                          used for automated deployments that can proceed while
                          neonDESKTOP is doing other things.
";
        private const string        logBeginMarker  = "# CLUSTER-BEGIN-SETUP ############################################################";
        private const string        logEndMarker    = "# CLUSTER-END-SETUP-SUCCESS ######################################################";
        private const string        logFailedMarker = "# CLUSTER-END-SETUP-FAILED #######################################################";

        private KubeConfigContext   kubeContext;
        private ClusterLogin        clusterLogin;

        /// <inheritdoc/>
        public override string[] Words => new string[] { "cluster", "setup" };

        /// <inheritdoc/>
        public override string[] ExtendedOptions => new string[] { "--unredacted", "--force", "--upload-charts", "--debug", "--automate" };

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

            // Cluster prepare/setup uses the [ProfileClient] to retrieve secrets and profile values.
            // We need to inject an implementation for [PreprocessReader] so it will be able to
            // perform the lookups.

            NeonHelper.ServiceContainer.AddSingleton<IProfileClient>(new ProfileClient());

            var contextName  = KubeContextName.Parse(commandLine.Arguments[0]);
            var kubeCluster  = KubeHelper.Config.GetCluster(contextName.Cluster);
            var unredacted   = commandLine.HasOption("--unredacted");
            var debug        = commandLine.HasOption("--debug");
            var uploadCharts = commandLine.HasOption("--upload-charts") || debug;
            var automate     = commandLine.HasOption("--automate");

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

            // Create and run the cluster setup controller.

            var clusterDefinition = clusterLogin.ClusterDefinition;

#if ENTERPRISE
            if (clusterDefinition.Hosting.Environment == HostingEnvironment.Wsl2)
            {
                var distro = new Wsl2Proxy(KubeConst.NeonDesktopWsl2Distro, KubeConst.SysAdminUser);

                clusterDefinition.Masters.FirstOrDefault().Address = distro.Address;
            }
#endif

            var controller = KubeSetup.CreateClusterSetupController(
                clusterDefinition,
                maxParallel:    Program.MaxParallel,
                unredacted:     unredacted,
                debugMode:      debug,
                uploadCharts:   uploadCharts,
                automate:       automate);

            controller.StatusChangedEvent +=
                status =>
                {
                    status.WriteToConsole();
                };

            if (controller.Run())
            {
                Console.WriteLine();
                Console.WriteLine($" [{clusterDefinition.Name}] cluster is ready.");
                Console.WriteLine();
                Program.Exit(0);
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine(" *** ERROR: One or more configuration steps failed.");
                Console.WriteLine();
                Program.Exit(1);
            }

            await Task.CompletedTask;
        }
    }
}