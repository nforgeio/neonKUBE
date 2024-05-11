//-----------------------------------------------------------------------------
// FILE:        ClusterUnlockCommand.cs
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
using Neon.Kube.Hosting;
using Neon.Kube.Proxy;
using Neon.Net;
using Neon.Retry;
using Neon.SSH;
using Neon.Time;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>cluster unlock</b> command.
    /// </summary>
    [Command]
    public class ClusterUnlockCommand : CommandBase
    {
        private const string usage = @"
Unlocks the current NeonKUBE cluster, enabling dangerous operations like:

    remove, reset, pause and stop

and also to enable [ClusterFixture] to run unit tests on the cluster.

USAGE:

    neon cluster unlock

REMARKS:

All clusters besides NEONDESKTOP clusters are locked by default when they're
deployed.  You can disable this by setting [IsLocked=false] in your cluster
definition.

";
        /// <inheritdoc/>
        public override string[] Words => new string[] { "cluster", "unlock" };

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

            using (var cluster = ClusterProxy.Create(KubeHelper.KubeConfig, new HostingManagerFactory()))
            {
                var status       = await cluster.GetClusterHealthAsync();
                var capabilities = cluster.Capabilities;

                switch (status.State)
                {
                    case ClusterState.Healthy:
                    case ClusterState.Unhealthy:

                        await cluster.UnlockAsync();
                        Console.WriteLine($"{cluster.Name}: UNLOCKED");
                        break;

                    default:

                        Console.Error.WriteLine($"*** ERROR: Cluster [{cluster.Name}] is not running.");
                        Program.Exit(1);
                        break;
                }
            }
        }
    }
}
