//-----------------------------------------------------------------------------
// FILE:	    ClusterResetCommand.cs
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
    /// Implements the <b>cluster reset</b> command.
    /// </summary>
    [Command]
    public class ClusterResetCommand : CommandBase
    {
        private const string usage = @"
Resets the current NEONKUBE cluster to its factory new condition.

USAGE:

    neon cluster reset [--force]

OPTIONS:

    --auth              - Resets authentication (Dex, Glauth)

    --crio              - Resets referenced container registeries to the 
                          cluster definition specifications and removes
                          any non-system container images

    --force              - Forces cluster stop without user confirmation
                           or verifying unlocked status

    --harbor            - Resets Harbor components

    --keep-namespaces   - Comma separated list of non-system namespaces
                          to be retained or ""*"" to retain all non-system
                          namespaces

    --minio             - Resets Minio

    --monitoring        - Clears monitoring data as well as non-system
                          dashboards and alerts

REMARKS:

This command works by removing all non-system namespaces including [default],
along with any resources within.  The [default] namespace will be recreated
afterwards, restoring it to its original empty condition.  You can specify
namespaces to be retained via [--namespace-exclude], passing a comma separated
list of namespaces.

The command also resets Harbor, Minio, CRIO-O, Dex and the monitoring components
to their defaults.  All components are reset by default, but you can you can
control whether some components are reset by passing one or more of the options.

EXAMPLES:

Reset cluster with confirmation prompt:

    neon cluster reset

Reset cluster without confirmation prompt:

    neon cluster reset --force

Reset ckuster while retaining the ""foo"" and ""bar"" namespaces:

    neon cluster reset --keep-namespaces=foo,bar

Reset cluster, excluding all non-system namespaces:

    neon cluster reset --keep-namespaces=*

Reset cluster Minio and Harbor as well as removing all non-system namespaces:

    neon cluster reset --minio --harbor

NOTE:

This command will not work on a locked clusters as a safety measure.

All clusters besides NEONDESKTOP clusters are locked by default when they're
deployed.  You can disable this by setting [IsLocked=false] in your cluster
definition or by executing this command on your cluster:

    neon cluster unlock

";
        /// <inheritdoc/>
        public override string[] Words => new string[] { "cluster", "reset" };

        /// <inheritdoc/>
        public override string[] ExtendedOptions => new string[] { "--force", "--auth", "--crio", "--harbor", "--minio", "--monitoring", "--keep-namespaces" };

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

            var force = commandLine.HasOption("--force");

            // Determine the individual components to be reset.  We'll default to
            // resetting all of them when none of these options are specified.

            var crio       = commandLine.HasOption("--crio");
            var auth       = commandLine.HasOption("--auth");
            var harbor     = commandLine.HasOption("--harbor");
            var minio      = commandLine.HasOption("--minio");
            var monitoring = commandLine.HasOption("--monitoring");

            if (!crio && !auth && !harbor && !minio && monitoring)
            {
                crio       = true;
                auth       = true;
                harbor     = true;
                minio      = true;
                monitoring = true;
            }

            // Obtain the namespaces to be retained.

            var keep           = commandLine.GetOption("--keep-namespaces");
            var keepNamespaces = new List<string>();

            if (!string.IsNullOrEmpty(keep))
            {
                foreach (var @namespace in keep.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    keepNamespaces.Add(@namespace);
                }
            }

            using (var cluster = ClusterProxy.Create(KubeHelper.KubeConfig, new HostingManagerFactory()))
            {
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

                            if (!Program.PromptYesNo($"Are you sure you want to reset: {cluster.Name}?"))
                            {
                                Program.Exit(0);
                            }
                        }

                        Console.WriteLine("Resetting cluster (this may take a while)...");

                        await cluster.ResetAsync(
                            new ClusterResetOptions()
                            {
                                KeepNamespaces  = keepNamespaces,
                                ResetCrio       = crio,
                                ResetAuth       = auth,
                                ResetHarbor     = harbor,
                                ResetMinio      = minio,
                                ResetMonitoring = monitoring
                            },
                            progressMessage => Console.WriteLine(progressMessage));

                        Console.WriteLine($"RESET: {cluster.Name}");
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
