//-----------------------------------------------------------------------------
// FILE:        ClusterCheckCommand.cs
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
using Neon.Kube.Config;
using Neon.Net;
using Neon.Retry;
using Neon.SSH;
using Neon.Time;

// $todo(jefflill): We need to check for failed pods here too.

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>cluster check</b> command.
    /// </summary>
    [Command]
    public class ClusterCheckCommand : CommandBase
    {
        private const string usage = @"
Performs various checks on the current NeonKUBE cluster.  These checks are
used by NeonKUBE maintainers to verify that cluster setup worked correctly.
This does the same thing as the [neon cluster setup --check] option.

USAGE: 

    neon cluster check [OPTIONS]

OPTIONS:

    --all               - Performs all checks.  This is implied when no other
                          options are present

    --details           - Includes additional information for some of the 
                          checks even when there are no errors

    --container-images  - Verifies that all container images running on the
                          cluster are included in the cluster manifest and
                          that they are all sourced from the local registry

    --priority-class    - Verifies that all running pods have a non-zero
                          PriorityClass

    --resources         - Verifies that all pod containers specify resource
                          request and limits

REMARKS:

This command returns a non-zero exit code when one or more checks fail.
";

        /// <inheritdoc/>
        public override string[] Words => new string[] { "cluster", "check" };

        /// <inheritdoc/>
        public override string[] ExtendedOptions => new string[] { "--all", "--container-images", "--priority-class", "--resources", "--details" };

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
            if (commandLine.Arguments.Length > 0)
            {
                Console.Error.WriteLine("*** ERROR: Unexpected argument.");
                Program.Exit(-1);
            }

            Console.WriteLine();

            var context = KubeHelper.CurrentContext;

            if (context == null)
            {
                Console.Error.WriteLine($"*** ERROR: No NeonKUBE cluster is selected.");
                Program.Exit(1);
            }

            // Handle the command line options.

            var all             = commandLine.HasOption("--all");
            var containerImages = commandLine.HasOption("--container-images");
            var priorityClass   = commandLine.HasOption("--priority-class");
            var resources       = commandLine.HasOption("--resources");
            var details         = commandLine.HasOption("--details");

            if (all || (!containerImages && !priorityClass && !resources))
            {
                containerImages = true;
                priorityClass   = true;
                resources       = true;
            }

            // Perform the requested checks.

            var k8s   = KubeHelper.CreateKubernetesClient(kubeConfigPath: KubeHelper.KubeConfigPath);
            var error = false;

            if (containerImages && !await ClusterChecker.CheckContainerImagesAsync(k8s, details: details))
            {
                error = true;
            }

            if (priorityClass && !await ClusterChecker.CheckPodPrioritiesAsync(k8s, details: details))
            {
                error = true;
            }

            if (resources && !await ClusterChecker.CheckResourcesAsync(k8s, details: details))
            {
                error = true;
            }

            if (error)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("*** ERROR: Cluster check failed with one or more errors.");
                Program.Exit(1);
            }

            await Task.CompletedTask;
        }
    }
}
