//-----------------------------------------------------------------------------
// FILE:	    LoginImportCommand.cs
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Kube;
using Neon.Kube.Config;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>login import</b> command.
    /// </summary>
    [Command]
    public class LoginImportCommand : CommandBase
    {
        private const string usage = @"
Imports an exported NEONKUBE context from a file.

USAGE:

    neon login import [--no-login] [--force] PATH

ARGUMENTS:

    PATH        - Path to the context file.

OPTIONS:

    --force     - Don't prompt for permission to replace an existing context

    --no-login  - Don't login to the new context

REMARKS:

This command logs into the new context by default.  Use [--no-login]
to disable this behavior and just import the context.
";

        /// <inheritdoc/>
        public override string[] Words => new string[] { "login", "import" }; 

        /// <inheritdoc/>
        public override string[] ExtendedOptions => new string[] { "--no-login", "--force" };

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
                Console.Error.WriteLine("*** ERROR: PATH is required.");
                Program.Exit(-1);
            }

            Console.WriteLine();

            var newLogin        = NeonHelper.YamlDeserialize<ClusterLoginExport>(File.ReadAllText(commandLine.Arguments.First()));
            var existingContext = KubeHelper.KubeConfig.GetContext(newLogin.Context.Name);

            // Add/replace the context.

            if (existingContext != null)
            {
                if (!commandLine.HasOption("--force") && !Program.PromptYesNo($"*** Are you sure you want to replace [{existingContext.Name}]?"))
                {
                    return;
                }

                KubeHelper.KubeConfig.RemoveContext(existingContext);
            }

            KubeHelper.KubeConfig.Contexts.Add(newLogin.Context);

            // Add/replace the cluster.

            var existingCluster = KubeHelper.KubeConfig.GetCluster(newLogin.Context.Context.Cluster);

            if (existingCluster != null)
            {
                KubeHelper.KubeConfig.Clusters.Remove(existingCluster);
            }

            KubeHelper.KubeConfig.Clusters.Add(newLogin.Cluster);

            // Add/replace the user.

            var existingUser = KubeHelper.KubeConfig.GetUser(newLogin.Context.Context.User);

            if (existingUser != null)
            {
                KubeHelper.KubeConfig.Users.Remove(existingUser);
            }

            KubeHelper.KubeConfig.Users.Add(newLogin.User);
            KubeHelper.KubeConfig.Save();

            Console.Error.WriteLine($"Imported: {newLogin.Context.Name}");

            if (commandLine.GetOption("--no-login") == null)
            {
                Console.Error.WriteLine($"Logging into: {newLogin.Context.Name}");
                KubeHelper.KubeConfig.CurrentContext = newLogin.Context.Name;
                KubeHelper.KubeConfig.Save();
            }

            await Task.CompletedTask;
        }
    }
}
