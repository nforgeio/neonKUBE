//-----------------------------------------------------------------------------
// FILE:	    ClusterRemoveCommand.cs
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
using Neon.Kube;
using Neon.Net;
using Neon.SSH;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>cluster remove</b> command.
    /// </summary>
    [Command]
    public class ClusterRemoveCommand : CommandBase
    {
        private const string usage = @"
Deprovisions the specified cluster.

USAGE:

    neon cluster remove [OPTIONS] LOGIN-PATH

ARGUMENTS:

    LOGIN-PATH      - Specifies the path to the cluster login file for the
                      cluster to be removed.

OPTIONS:

    --force         - Do not prompt for confirmation

    --unredacted    - Runs commands with potential secrets without  redacting logs.
                      This is useful for debugging  cluster setup issues.  Do not 
                      use for production clusters.
";
        private const string    logBeginMarker  = "# CLUSTER-BEGIN-PREPARE ##########################################################";
        private const string    logEndMarker    = "# CLUSTER-END-PREPARE-SUCCESS ####################################################";
        private const string    logFailedMarker = "# CLUSTER-END-PREPARE-FAILED #####################################################";

        /// <inheritdoc/>
        public override string[] Words => new string[] { "cluster", "prepare" };

        /// <inheritdoc/>
        public override string[] ExtendedOptions => new string[] { "--package-caches", "--unredacted", "--remove-templates", "--debug", "--base-image-name" };

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

            // Implement the command.

            if (commandLine.Arguments.Length == 0)
            {
                Console.Error.WriteLine($"*** ERROR: LOGIN-PATH expected.");
                Program.Exit(1);
            }

            var loginPath    = commandLine.Arguments[0];
            var clusterLogin = ClusterLogin.Load(loginPath);
            var unredacted   = commandLine.HasOption("--unredacted");

            // Create and run the cluster remove controller.

            var controller = KubeSetup.CreateClusterRemoveController(
                clusterLogin,
                maxParallel:    Program.MaxParallel,
                unredacted:     unredacted);

            controller.StatusChangedEvent +=
                status =>
                {
                    status.WriteToConsole();
                };

            if (controller.Run())
            {
                Console.WriteLine();
                Console.WriteLine($" [{clusterDefinition.Name}] cluster is prepared.");
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
