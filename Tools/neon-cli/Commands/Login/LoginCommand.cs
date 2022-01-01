//-----------------------------------------------------------------------------
// FILE:	    LoginCommand.cs
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using k8s;
using Newtonsoft;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Kube;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>login</b> command.
    /// </summary>
    [Command]
    public class LoginCommand : CommandBase
    {
        private const string usage = @"
Manages Kubernetes contexts for the user on the local workstation.

USAGE:

    neon login              USER@CLUSTER[/NAMESPACE]
    neon login export       USER@CLUSTER[/NAMESPACE] [PATH]
    neon login import       PATH
    neon login list|ls
    neon login remove|rm    USER@CLUSTER[/NAMESPACE]

    neon logout

ARGUMENTS:

    PATH                        - Path to an exported login file.
    USER@CLUSTER[/NAMESPACE]    - Kubernetes user, cluster and optional namespace
";

        /// <inheritdoc/>
        public override string[] Words => new string[] { "login" }; 

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override async Task RunAsync(CommandLine commandLine)
        {
            if (commandLine.HasHelpOption || commandLine.Arguments.Length == 0)
            {
                Console.WriteLine(usage);
                Program.Exit(0);
            }

            Console.Error.WriteLine();

            var currentContext = KubeHelper.CurrentContext;
            var newContextName = KubeContextName.Parse(commandLine.Arguments.First());

            // Ensure that the new context exists.

            if (KubeHelper.Config.GetContext(newContextName) == null)
            {
                Console.Error.WriteLine($"*** Context [{newContextName}] not found.");
                Program.Exit(1);
            }

            // Check whether we're already logged into the cluster.

            if (KubeHelper.CurrentContext != null && newContextName == KubeContextName.Parse(KubeHelper.CurrentContext.Name))
            {
                Console.Error.WriteLine($"*** You are already logged into: {newContextName}");
                Program.Exit(0);
            }

            // Logout of the current cluster.

            if (currentContext != null)
            {
                Console.Error.WriteLine($"Logging out of [{currentContext.Name}].");
                KubeHelper.SetCurrentContext((string)null);
            }

            // Log into the new context and then send a simple command to ensure
            // that cluster is ready.

            var orgContext = KubeHelper.CurrentContext;

            KubeHelper.SetCurrentContext(newContextName);
            Console.WriteLine($"Logging into [{newContextName}]...");
            Console.WriteLine();

            try
            {
                using (var k8s = new Kubernetes(KubernetesClientConfiguration.BuildConfigFromConfigFile()))
                {
                    await k8s.ListNamespaceAsync();
                }
            }
            catch (Exception e)
            {
                KubeHelper.SetCurrentContext(orgContext?.Name);

                Console.WriteLine("*** ERROR: Cluster is not responding.");
                Console.WriteLine();
                Console.WriteLine(NeonHelper.ExceptionError(e));
                Console.WriteLine();
                Program.Exit(1);
            }

            Console.WriteLine($"Logged into [{newContextName}].");
            Console.WriteLine();

            await Task.CompletedTask;
        }
    }
}
