// -----------------------------------------------------------------------------
// FILE:	    ClusterDeployCommand.cs
// CONTRIBUTOR: NEONFORGE Team
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License").
// You may not use this file except in compliance with the License.
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
using Neon.Kube.ClusterDef;
using Neon.Kube.Config;
using Neon.Kube.Setup;
using Neon.Net;
using Neon.Retry;
using Neon.SSH;
using Neon.Time;

namespace NeonCli.Commands.Cluster
{
    /// <summary>
    /// Implements the <b>cluster deploy</b> command.
    /// </summary>
    [Command]
    public class ClusterDeployCommand : CommandBase
    {
        private const string usage = @"
Deploys a NEONKUBE cluster, based on a cluster definition.

USAGE: 

    neon cluster deploy [OPTIONS] CLUSTER-DEF

ARGUMENTS:

    CLUSTERDEF                  - Path to the cluster definition file.

OPTIONS:

    --max-parallel=#            - Specifies the maximum number of node related operations
                                  to perform in parallel.  This defaults to [6].

    --force                     - Don't prompt before removing existing contexts that
                                  reference the target cluster.

    --quiet                     - Only print the currently executing step rather than
                                  displaying detailed setup status.

    --check                     - Performs development related checks against the cluster
                                  after it's been deployed.

                                  A non-zero exit code will be returned when this option
                                  is specified and one or more checks fail.

    --package-cache=HOST:PORT   - Specifies one or more APT Package cache servers by hostname
                                  and port for use by the new cluster.  Specify multiple
                                  servers by separating the endpoints with spaces.

    --use-staged[=branch]       - MAINTAINERS ONLY: Specifies that the staged node image 
                                  should be used as opposed to the public release image.

                                  [--use-staged] by itself will prepare the cluster using
                                  the staged NEONKUBE node image whose version is a 
                                  combination of the NEONKUBE version along with the 
                                  name of the NEONKUBE branch when the libraries were
                                  built.

                                  [--use-staged=branch] allows you to override the branch
                                  so you can base your cluster off of a specific image
                                  build.

    --no-telemetry              - Disables telemetry uploads for failed cluster deployment,
                                  overriding the NEONKUBE_DISABLE_TELEMETRY environment variable.

REMARKS:

Most users will use the deploy command that combines both commands.  The two
stage process is typically used only by NEONKUBE maintainers.

    neon cluster deploy CLUSTER-DEF

";

        /// <inheritdoc/>
        public override string[] Words => new string[] { "cluster", "deploy" };

        /// <inheritdoc/>
        public override string[] ExtendedOptions => new string[]
        {
            "--check",
            "--force",
            "--max-parallel",
            "--no-telemetry",
            "--package-cache",
            "--quiet",
            "--use-staged",
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
            if (commandLine.Arguments.Length < 1)
            {
                Console.Error.WriteLine("*** ERROR: CLUSTERDEF argument is required.");
                Program.Exit(-1);
            }

            Console.WriteLine();

            // Cluster prepare/setup uses the [ProfileClient] to retrieve secrets and profile values.
            // We need to inject an implementation for [PreprocessReader] so it will be able to
            // perform the lookups.

            NeonHelper.ServiceContainer.AddSingleton<IProfileClient>(new MaintainerProfile());

            var clusterDefPath    = commandLine.Arguments[0];
            var check             = commandLine.HasOption("--check");
            var force             = commandLine.HasOption("--force");
            var packageCaches     = commandLine.GetOption("--package-cache");
            var maxParallelOption = commandLine.GetOption("--max-parallel", "6");
            var noTelemetry       = commandLine.HasOption("--no-telemetry");
            var quiet             = commandLine.HasOption("--quiet");
            var useStaged         = commandLine.HasOption("--use-staged");
            var stageBranch       = commandLine.GetOption("--use-staged", KubeVersions.BuildBranch);

            if (useStaged && string.IsNullOrEmpty(stageBranch))
            {
                stageBranch = KubeVersions.BuildBranch;
            }

            // We're simply going to invoke the [cluster prepare] and [cluster setup]
            // commands directly in process.
            //
            // NOTE: This requires that the cluster prepare and setup to not explicitly
            //       terminate the neon-cli process when they complete successfully.
            //       It's OK if these commands terminate on error.

            var clusterDefinition = ClusterDefinition.FromFile(clusterDefPath);

            //-----------------------------------------------------------------
            // neon cluster prepare ...

            var args = new List<string>() { "cluster", "prepare", clusterDefPath };

            if (!string.IsNullOrEmpty(packageCaches))
            {
                args.Add($"--package-cache={packageCaches}");
            }

            if (noTelemetry)
            {
                args.Add("--no-telemetry");
            }

            args.Add($"--max-parallel={maxParallelOption}");

            if (quiet)
            {
                args.Add($"--quiet");
            }

            if (useStaged)
            {
                args.Add($"--use-staged={stageBranch}");
            }

            var exitcode = await Program.Main(args.ToArray());

            if (exitcode != 0)
            {
                Program.Exit(exitcode);
            }

            //-----------------------------------------------------------------
            // neon cluster setup ...

            args = new List<string>() { "cluster", "setup", $"root@{clusterDefinition.Name}" };

            if (check)
            {
                args.Add("--check");
            }

            if (force)
            {
                args.Add("--force");
            }

            if (noTelemetry)
            {
                args.Add("--no-telemetry");
            }

            if (quiet)
            {
                args.Add($"--quiet");
            }

            if (useStaged)
            {
                args.Add($"--use-staged={stageBranch}");
            }

            args.Add($"--max-parallel={maxParallelOption}");

            exitcode = await Program.Main(args.ToArray());

            Program.Exit(exitcode);
        }
    }
}
