//-----------------------------------------------------------------------------
// FILE:        ClusterStopCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using Neon.Kube.Hosting;
using Neon.Kube.Proxy;
using Neon.Net;
using Neon.Retry;
using Neon.SSH;
using Neon.Time;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>cluster stop</b> command.
    /// </summary>
    [Command]
    public class ClusterStopCommand : CommandBase
    {
        private const string usage = @"
Stops the current NEONKUBE cluster.

USAGE:

    neon cluster stop [--turnoff] [--force]

OPTIONS:

    --force     - Don't prompt for permission or require the the cluster
                  be unlocked before stopping

    --turnoff   - Turns the nodes off immediately without waiting for
                  a graceful shutdown.  This may cause data loss.

REMARKS:

NOTE: This command requires that the cluster be unlocked by default as
a safety measure.

All clusters besides NEONDESKTOP clusters are locked by default when
they're deployed.  You can disable this by setting [IsLocked=false]
in your cluster definition or by executing this command:

    neon cluster unlock

";
        /// <inheritdoc/>
        public override string[] Words => new string[] { "cluster", "stop" };

        /// <inheritdoc/>
        public override string[] ExtendedOptions => new string[] { "--turnoff", "--force" };

        /// <inheritdoc/>
        public override bool NeedsHostingManager => true;

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

            Console.WriteLine();

            var context = KubeHelper.CurrentContext;

            if (context == null)
            {
                Console.Error.WriteLine($"*** ERROR: There is no current cluster.");
                Program.Exit(1);
            }

            var turnoff = commandLine.HasOption("--turnoff");
            var force   = commandLine.HasOption("--force");

            using (var cluster = ClusterProxy.Create(KubeHelper.KubeConfig, new HostingManagerFactory()))
            {
                var capabilities = cluster.Capabilities;

                if ((capabilities & HostingCapabilities.Stoppable) == 0)
                {
                    Console.Error.WriteLine($"*** ERROR: Cluster does not support start/stop.");
                    Program.Exit(1);
                }

                var status = await cluster.GetClusterHealthAsync();

                switch (status.State)
                {
                    case ClusterState.Healthy:
                    case ClusterState.Unhealthy:

                        if (!force)
                        {
                            var isLocked = await cluster.IsLockedAsync();

                            if (!isLocked.HasValue)
                            {
                                Console.Error.WriteLine($"*** ERROR: [{cluster.Name}] lock status is unknown.");
                                Program.Exit(1);
                            }

                            if (isLocked.Value)
                            {
                                Console.Error.WriteLine($"*** ERROR: [{cluster.Name}] is locked.");
                                Program.Exit(1);
                            }

                            if (!Program.PromptYesNo($"Are you sure you want to stop: {cluster.Name}?"))
                            {
                                Program.Exit(0);
                            }
                        }

                        try
                        {
                            if (turnoff)
                            {
                                Console.WriteLine($"Turning Off: {cluster.Name}");
                            }
                            else
                            {
                                Console.WriteLine($"Stopping: {cluster.Name}...");
                            }

                            await cluster.StopAsync(turnoff ? StopMode.TurnOff : StopMode.Graceful);
                            Console.WriteLine($"Stopped:  {cluster.Name}");
                        }
                        catch (TimeoutException)
                        {
                            Console.WriteLine();
                            Console.WriteLine($"*** ERROR: Timeout waiting for cluster.");
                        }
                        break;

                    default:

                        Console.Error.WriteLine($"*** ERROR: Cluster is already stopped.");
                        Program.Exit(1);
                        break;
                }
            }
        }
    }
}
