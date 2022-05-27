//-----------------------------------------------------------------------------
// FILE:	    ClusterStatusCommand.cs
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
    /// Implements the <b>cluster status</b> command.
    /// </summary>
    [Command]
    public class ClusterStatusCommand : CommandBase
    {
        private const string usage = @"
Prints the status of the current cluster.
";
        /// <inheritdoc/>
        public override string[] Words => new string[] { "cluster", "status" };

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

            using (var cluster = new ClusterProxy(context, new HostingManagerFactory()))
            {
                var status = await cluster.GetClusterStatusAsync();

                Console.WriteLine($"{cluster.Name}: {status.State.ToString().ToUpperInvariant()}");
                Console.WriteLine();

                var maxNodeNameLength = cluster.Definition.NodeDefinitions.Keys.Max(nodeName => nodeName.Length);

                Console.WriteLine("Master Nodes:");
                Console.WriteLine("-------------");

                foreach (var nodeDefinition in cluster.Definition.SortedMasterNodes)
                {
                    var nodeName = nodeDefinition.Name;
                    var padding  = new string(' ', maxNodeNameLength - nodeName.Length + 4);

                    if (status.Nodes.TryGetValue(nodeDefinition.Name, out var nodeState))
                    {
                        Console.WriteLine($"{nodeName}: {padding}{nodeState.ToString().ToUpperInvariant()}");
                    }
                    else
                    {
                        Console.WriteLine($"{nodeName}: {padding}*NOT-FOUND*");
                    }
                }

                if (cluster.Definition.NodeDefinitions.Values.Any(nodeDefinition => nodeDefinition.IsWorker))
                {
                    Console.WriteLine();
                    Console.WriteLine("Worker Nodes:");
                    Console.WriteLine("-------------");

                    foreach (var nodeDefinition in cluster.Definition.SortedWorkerNodes)
                    {
                        var nodeName = nodeDefinition.Name;
                        var padding  = new string(' ', maxNodeNameLength - nodeName.Length + 4);

                        if (status.Nodes.TryGetValue(nodeDefinition.Name, out var nodeState))
                        {
                            Console.WriteLine($"{nodeName}: {padding}{nodeState.ToString().ToUpperInvariant()}");
                        }
                        else
                        {
                            Console.WriteLine($"{nodeName}: {padding}*NOT-FOUND*");
                        }
                    }
                }
            }
        }
    }
}
