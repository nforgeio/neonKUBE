//-----------------------------------------------------------------------------
// FILE:	    ClusterIsLockedCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
    /// Implements the <b>cluster islocked</b> command.
    /// </summary>
    [Command]
    public class ClusterIsLockedCommand : CommandBase
    {
        private const string usage = @"
Determines whether the current cluster is locked.

USAGE:

    neon cluster islocked

EXITCODE:

    0   - when the cluster is locked
    1   - for errors
    2   - when the cluster is unlocked
";
        /// <inheritdoc/>
        public override string[] Words => new string[] { "cluster", "islocked" };

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

            using (var cluster = await ClusterProxy.CreateAsync(KubeHelper.KubeConfig, new HostingManagerFactory(), cloudMarketplace: false))   // [cloudMarketplace] arg doesn't matter here.
            {
                var status = await cluster.GetClusterHealthAsync();

                switch (status.State)
                {
                    case ClusterState.Healthy:
                    case ClusterState.Unhealthy:

                        var isLocked = await cluster.IsLockedAsync();

                        if (!isLocked.HasValue)
                        {
                            Console.Error.WriteLine($"*** ERROR: [{cluster.Name}] lock status is unknown.");
                            Program.Exit(1);
                        }

                        if (isLocked.Value)
                        {
                            Console.WriteLine($"[{cluster.Name}]: LOCKED");
                            Program.Exit(0);
                        }
                        else
                        {
                            Console.WriteLine($"[{cluster.Name}]: UNLOCKED");
                            Program.Exit(2);
                        }
                        break;

                    default:

                        Console.Error.WriteLine($"*** ERROR: Cluster is not running.");
                        Program.Exit(1);
                        break;
                }
            }
        }
    }
}
