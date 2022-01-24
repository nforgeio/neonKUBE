//-----------------------------------------------------------------------------
// FILE:	    ClusterCheckCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
    /// Implements the <b>cluster check</b> command.
    /// </summary>
    [Command]
    public class ClusterCheckCommand : CommandBase
    {
        //---------------------------------------------------------------------
        // Implementation

        private const string usage = @"
Performs various checks on the current cluster.  These checks are targeted at 
neonKUBE maintainers to verify that cluster setup worked correctly.  This does
the same thing as the [neon cluster setup --check] option.

USAGE: 

    neon cluster check [OPTIONS]

OPTIONS:

    --all               - Implied when no other options are specified

    --container-images  - Verifies that all container images running on the
                          cluster are included in the cluster manifest

    --local-images      - Verifies that all images referenced by running pods
                          are being pulled from the local Harbor registry

    --priority-class    - Verifies that all running pod templates specify a
                          non-zero PriorityClass.

    --list              - Lists information for some of the checks even when
                          there are no errors.

REMARKS:

This command returns a non-zero exit code when one or more checks fail.
";

        private KubeConfigContext   k8s;
        private ClusterLogin        clusterLogin;

        /// <inheritdoc/>
        public override string[] Words => new string[] { "cluster", "check" };

        /// <inheritdoc/>
        public override string[] ExtendedOptions => new string[] { "--all", "--container-images", "--local-images", "--priority-class", "--list" };

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override async Task RunAsync(CommandLine commandLine)
        {
            if (commandLine.Arguments.Length > 0)
            {
                Console.Error.WriteLine("*** ERROR: Unexpected argument.");
                Program.Exit(1);
            }

            // Handle the command line options.

            var all = commandLine.HasOption("--all");
            var containerImages = commandLine.HasOption("--container-images");
            var localImages = commandLine.HasOption("--local-images");
            var priorityClass = commandLine.HasOption("--priority-class");
            var list = commandLine.HasOption("--list");

            if (all || (!containerImages && !priorityClass && !localImages))
            {
                containerImages = true;
                localImages = true;
                priorityClass = true;
            }

            // Perform the requested checks.

            var k8s = new Kubernetes(KubernetesClientConfiguration.BuildConfigFromConfigFile());
            var error = false;

            if (containerImages && !await ClusterChecker.CheckContainerImagesAsync(k8s))
            {
                error = true;
            }

            if (localImages && !await ClusterChecker.CheckPodLocalImagesAsync(k8s, displayAlways: list))
            {
                error = true;
            }

            if (priorityClass && !await ClusterChecker.CheckPodPrioritiesAsync(k8s, displayAlways: list))
            {
                error = true;
            }

            if (error)
            {
                Console.Error.Write("*** ERROR: Cluster check failed with one or more errors.");
                Program.Exit(1);
            }

            await Task.CompletedTask;
        }
    }
}