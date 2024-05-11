//-----------------------------------------------------------------------------
// FILE:        ClusterIsLockedCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
using Neon.Kube.ClusterMetadata;
using Neon.Kube.Hosting;
using Neon.Kube.Proxy;
using Neon.Net;
using Neon.Retry;
using Neon.SSH;
using Neon.Time;

using Newtonsoft.Json;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>cluster islocked</b> command.
    /// </summary>
    [Command]
    public class ClusterIsLockedCommand : CommandBase
    {
        private const string usage = @"
Determines whether the current NeonKUBE cluster is locked.

USAGE:

    neon cluster islocked

OPTIONS:

    --output=json|yaml          - Optionally specifies the format to print the
    -o=json|yaml                  cluster info

EXITCODE:

    0   - when the cluster is locked
    1   - for errors
    2   - when the cluster is unlocked
";
        /// <inheritdoc/>
        public override string[] Words => new string[] { "cluster", "islocked" };

        /// <inheritdoc/>
        public override string[] ExtendedOptions => new string[]
        {
            "--output",
            "-o"
        };

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

            commandLine.DefineOption("--output", "-o").Default = null;

            var outputFormat = Program.GetOutputFormat(commandLine);
            var context      = KubeHelper.CurrentContext;

            if (context == null)
            {
                Console.Error.WriteLine($"*** ERROR: There is no current cluster.");
                Program.Exit(1);
            }

            using (var cluster = ClusterProxy.Create(KubeHelper.KubeConfig, new HostingManagerFactory()))
            {
                var status    = await cluster.GetClusterHealthAsync();
                var isLocked  = (bool?)null;
                var lockState = ClusterLockState.Unknown;

                switch (status.State)
                {
                    case ClusterState.Healthy:
                    case ClusterState.Unhealthy:

                        isLocked = await cluster.IsLockedAsync();
                        break;

                    default:

                        Console.Error.WriteLine($"*** ERROR: Cluster [{cluster.Name}] is not running.");
                        Program.Exit(1);
                        break;
                }

                if (!isLocked.HasValue)
                {
                    lockState = ClusterLockState.Unknown;
                }
                else
                {
                    lockState = isLocked.Value ? ClusterLockState.Locked : ClusterLockState.Unlocked;
                }

                var clusterLockStatus = new ClusterLockStatus()
                {
                    Cluster = cluster.Name,
                    State   = lockState
                };

                if (!outputFormat.HasValue)
                {
                    if (!isLocked.HasValue)
                    {
                        Console.Error.WriteLine();
                        Console.Error.WriteLine($"*** ERROR: [{cluster.Name}] lock status is unknown.");
                        Program.Exit(1);
                    }

                    if (isLocked.Value)
                    {
                        Console.WriteLine();
                        Console.WriteLine($"[{cluster.Name}]: LOCKED");
                        Program.Exit(0);
                    }
                    else
                    {
                        Console.WriteLine();
                        Console.WriteLine($"[{cluster.Name}]: UNLOCKED");
                        Program.Exit(2);
                    }
                }
                else
                {
                    switch (outputFormat.Value)
                    {
                        case OutputFormat.Json:

                            Console.WriteLine(NeonHelper.JsonSerialize(clusterLockStatus, Formatting.Indented));
                            break;

                        case OutputFormat.Yaml:

                            Console.WriteLine(NeonHelper.YamlSerialize(clusterLockStatus));
                            break;

                        default:

                            throw new NotImplementedException();
                    }

                    switch (lockState)
                    {
                        case ClusterLockState.Locked:

                            Program.Exit(0);
                            break;

                        case ClusterLockState.Unlocked:

                            Program.Exit(2);
                            break;

                        case ClusterLockState.Unknown:

                            Program.Exit(1);
                            break;

                        default:

                            throw new NotImplementedException();
                    }
                }
            }
        }
    }
}
