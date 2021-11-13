//-----------------------------------------------------------------------------
// FILE:	    ClusterPrepareCommand.cs
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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Neon.Collections;
using Neon.Common;
using Neon.Cryptography;
using Neon.Deployment;
using Neon.IO;
using Neon.Kube;
using Neon.Net;
using Neon.SSH;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>cluster prepare</b> command.
    /// </summary>
    [Command]
    public class ClusterPrepareCommand : CommandBase
    {
        private const string usage = @"
Configures cloud platform virtual machines so that they are prepared 
to host a Kubernetes cluster.

USAGE:

    neon cluster prepare [OPTIONS] CLUSTER-DEF

ARGUMENTS:

    CLUSTER-DEF     - Path to the cluster definition file or ""WSL2"" to deploy
                      the standard neonKUBE WSL2 distribution.

OPTIONS:

    --node-image-uri            - Overrides the default node image URI.

                                  NOTE: This is ignored for [--debug] mode.

                                  NOTE: This is ignored when [--node-image-path] is present.

    --node-image-path=PATH      - Uses the node image at the PATH specified rather than
                                  downloading the node image from GitHub Releases.  This
                                  is useful for debugging node image changes.

    --ready-to-go               - Optionally indicates that the default ready-to-go image
                                  should be used when [--node-image-uri] and [--node-image-path]
                                  aren't specified.  This is not compatible with [--debug].

    --package-caches=HOST:PORT  - Optionally specifies one or more APT Package cache
                                  servers by hostname and port for use by the new cluster. 
                                  Specify multiple servers by separating the endpoints 
                                  with spaces.

    --unredacted                - Runs commands with potential secrets without 
                                  redacting logs.  This is useful for debugging 
                                  cluster setup issues.  Do not use for production
                                  clusters.

    --remove-templates          - Removes any cached local virtual machine 
                                  templates without actually setting up a 
                                  cluster.  You can use this to ensure that 
                                  cluster will be created from the most recent
                                  template.

    --debug                     - Implements cluster setup from the base rather
                                  than the node image.  This mode is useful while
                                  developing and debugging cluster setup.  This
                                  implies [--upload-charts].

                                  NOTE: This mode is not supported for cloud and
                                        bare-metal environments.

    --base-image-name           - Specifies the base image name to use when operating
                                  in [--debug] mode.  This will be the name of the base
                                  image file as published to our public S3 bucket for
                                  the target hosting manager.  Examples:

                                        Hyper-V:   ubuntu-20.04.1.hyperv.vhdx
                                        WSL2:      ubuntu-20.04.20210206.wsl2.tar
                                        XenServer: ubuntu-20.04.1.xenserver.xva

                                  NOTE: This is required for [--debug]

    --automation-folder         - Indicates that the command must not impact normal clusters
                                  by changing the current login, Kubernetes config or
                                  other files like cluster deployment logs.  This is
                                  used for automated CI/CD or unit test cluster deployments 
                                  while not disrupting the built-in neonDESKTOP or
                                  other normal clusters.

    --headend-uri               - Set the URI for the headend service.

Server Requirements:
--------------------

    * Ubuntu 20.04.x (server)
    * Known [sysadmin] sudoer user
    * OpenSSH installed
";
        private const string    logBeginMarker  = "# CLUSTER-BEGIN-PREPARE ##########################################################";
        private const string    logEndMarker    = "# CLUSTER-END-PREPARE-SUCCESS ####################################################";
        private const string    logFailedMarker = "# CLUSTER-END-PREPARE-FAILED #####################################################";

        /// <inheritdoc/>
        public override string[] Words => new string[] { "cluster", "prepare" };

        /// <inheritdoc/>
        public override string[] ExtendedOptions => new string[] { "--node-image-uri", "--node-image-path", "--ready-to-go",  "--package-caches", "--unredacted", "--remove-templates", "--debug", "--base-image-name", "--automation-folder", "--headend-uri" };

        /// <inheritdoc/>
        public override bool NeedsSshCredentials(CommandLine commandLine) => !commandLine.HasOption("--remove-templates");

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }
        
        /// <inheritdoc/>
        public override async Task RunAsync(CommandLine commandLine)
        {
            if (commandLine.HasHelpOption)
            {
                Help();
                Program.Exit(0);
            }

            // Cluster prepare/setup uses the [ProfileClient] to retrieve secrets and profile values.
            // We need to inject an implementation for [PreprocessReader] so it will be able to
            // perform the lookups.

            NeonHelper.ServiceContainer.AddSingleton<IProfileClient>(new ProfileClient());

            // Handle the [--remove-templates] option.

            if (commandLine.HasOption("--remove-templates"))
            {
                Console.WriteLine("Removing cached virtual machine templates.");

                foreach (var fileName in Directory.GetFiles(KubeHelper.NodeImageFolder, "*.*", SearchOption.TopDirectoryOnly))
                {
                    File.Delete(fileName);
                }
            }
           
            var nodeImageUri     = commandLine.GetOption("--node-image-uri");
            var nodeImagePath    = commandLine.GetOption("--node-image-path");
            var readyToGo        = commandLine.HasOption("--ready-to-go");
            var debug            = commandLine.HasOption("--debug");
            var baseImageName    = commandLine.GetOption("--base-image-name");
            var automationFolder = commandLine.GetOption("--automation-folder");
            var headendUri       = commandLine.GetOption("--headend-uri") ?? "https://headend.neoncloud.io";

            if (debug && string.IsNullOrEmpty(baseImageName))
            {
                Console.Error.WriteLine($"*** ERROR: [--base-image-name] is required for [--debug] mode.");
                Program.Exit(1);
            }

            if (debug && readyToGo)
            {
                Console.Error.WriteLine($"*** ERROR: [--ready-to-go] is not compatible with [--debug] mode.");
                Program.Exit(1);
            }

            // Implement the command.

            if (KubeHelper.CurrentContext != null)
            {
                Console.Error.WriteLine("*** ERROR: You are logged into a cluster.  You need to logout before preparing another.");
                Program.Exit(1);
            }

            if (commandLine.Arguments.Length == 0)
            {
                Console.Error.WriteLine($"*** ERROR: CLUSTER-DEF expected.");
                Program.Exit(1);
            }

            // Obtain the cluster definition.

            var clusterDefPath    = commandLine.Arguments[0];
            var clusterDefinition = (ClusterDefinition)null;            

            if (clusterDefPath.Equals("WSL2", StringComparison.InvariantCultureIgnoreCase))
            {
                // This special-case argument indicates that we should use the built-in 
                // WSL2 cluster definition.

                clusterDefinition = KubeSetup.GetReadyToGoClusterDefinition(HostingEnvironment.Wsl2);
            }
            else
            {
                ClusterDefinition.ValidateFile(clusterDefPath, strict: true);

                clusterDefinition = ClusterDefinition.FromFile(clusterDefPath, strict: true);
            }

            // Use the default node image for the hosting environment unless [--node-image-uri]
            // or [--node-image-path] was specified.

            if (string.IsNullOrEmpty(nodeImageUri) && string.IsNullOrEmpty(nodeImagePath))
            {
                nodeImageUri = KubeDownloads.GetDefaultNodeImageUri(clusterDefinition.Hosting.Environment, readyToGo: readyToGo);
            }

            // Ensure that the cluster only has a single node for ready-to-go mode.

            if (readyToGo && clusterDefinition.NodeDefinitions.Count > 1)
            {
                Console.Error.WriteLine("*** ERROR: Only single node clusters can be deployed for [--ready-to-go] mode.");
                Program.Exit(1);
            }

            // Parse any specified package cache endpoints.

            var packageCaches         = commandLine.GetOption("--package-caches", null);
            var packageCacheEndpoints = new List<IPEndPoint>();

            if (!string.IsNullOrEmpty(packageCaches))
            {
                foreach (var item in packageCaches.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!NetHelper.TryParseIPv4Endpoint(item, out var endpoint))
                    {
                        Console.Error.WriteLine($"*** ERROR: [{item}] is not a valid package cache IPv4 endpoint.");
                        Program.Exit(1);
                    }

                    packageCacheEndpoints.Add(endpoint);
                }
            }

            // Create and run the cluster prepare controller.

            var controller = KubeSetup.CreateClusterPrepareController(
                clusterDefinition, 
                nodeImageUri:           nodeImageUri,
                nodeImagePath:          nodeImagePath,
                maxParallel:            Program.MaxParallel,
                packageCacheEndpoints:  packageCacheEndpoints,
                unredacted:             commandLine.HasOption("--unredacted"),
                debugMode:              debug,
                baseImageName:          baseImageName,
                automationFolder:       automationFolder,
                headendUri:             headendUri,
                readyToGoMode:          readyToGo ? ReadyToGoMode.Setup : ReadyToGoMode.Normal);

            controller.StatusChangedEvent +=
                status =>
                {
                    status.WriteToConsole();
                };

            switch (await controller.RunAsync())
            {
                case SetupDisposition.Succeeded:

                    Console.WriteLine();
                    Console.WriteLine($" [{clusterDefinition.Name}] cluster is prepared.");
                    Console.WriteLine();
                    Program.Exit(0);
                    break;

                case SetupDisposition.Cancelled:

                    Console.WriteLine();
                    Console.WriteLine(" *** CANCELLED: Cluster prepare was cancelled.");
                    Console.WriteLine();
                    Program.Exit(1);
                    break;

                case SetupDisposition.Failed:

                    Console.WriteLine();
                    Console.WriteLine(" *** ERROR: Cluster prepare has failed.");
                    Console.WriteLine();
                    Program.Exit(1);
                    break;

                default:

                    throw new NotImplementedException();
            }

            await Task.CompletedTask;
        }
    }
}
