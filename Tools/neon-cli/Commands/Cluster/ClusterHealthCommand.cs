//-----------------------------------------------------------------------------
// FILE:        ClusterHealthCommand.cs
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

using Newtonsoft.Json;

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
    /// Implements the <b>cluster health</b> command.
    /// </summary>
    [Command]
    public class ClusterHealthCommand : CommandBase
    {
        private const string usage = @"
Prints the status of the current NEONKUBE cluster.

USAGE: neon cluster health [OPTIONS]

OPTIONS:

    --output=json|yaml          - Optionally specifies the format to print the
    -o=json|yaml                  cluster health

";
        /// <inheritdoc/>
        public override string[] Words => new string[] { "cluster", "health" };

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

            commandLine.DefineOption("--output", "-o");

            var outputFormat = Program.GetOutputFormat(commandLine);
            var context      = KubeHelper.CurrentContext;

            if (context == null)
            {
                Console.WriteLine();
                Console.Error.WriteLine($"*** ERROR: There is no current cluster.");
                Program.Exit(1);
            }

            using (var cluster = ClusterProxy.Create(KubeHelper.KubeConfig, new HostingManagerFactory()))
            {
                var clusterHealth = await cluster.GetClusterHealthAsync();

                if (!outputFormat.HasValue)
                {
                    outputFormat = OutputFormat.Yaml;
                }

                switch (outputFormat.Value)
                {
                    case OutputFormat.Json:

                        Console.WriteLine(NeonHelper.JsonSerialize(clusterHealth, Formatting.Indented));
                        break;

                    case OutputFormat.Yaml:

                        Console.WriteLine(NeonHelper.YamlSerialize(clusterHealth));
                        break;

                    default:

                        throw new NotImplementedException();
                }
            }
        }
    }
}
