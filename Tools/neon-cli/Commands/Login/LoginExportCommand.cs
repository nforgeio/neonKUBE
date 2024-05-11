//-----------------------------------------------------------------------------
// FILE:        LoginExportCommand.cs
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
    /// Implements the <b>logins export</b> command.
    /// </summary>
    [Command]
    public class LoginExportCommand : CommandBase
    {
        private const string usage = @"
Exports a NeonKUBE context to standard output or a file.

USAGE:

    neon login export [OPTIONS] [PATH]

ARGUMENTS:

    PATH                    - Optional output file (defaults to STDOUT)

OPTIONS:

    --context=CONTEXT-NAME  - Optionally identifies a specific context
                              to be exported, rather than the current
                              context

REMARKS:

IMPORTANT: Exported NeonKUBE contexts include cluster credentials and
           should be protected.

This command is used to obtain a NeonKUBE context for a cluster so
it can be saved (perhaps in a password manager) and possibly shared with
other cluster users.  The current context (if there is one) is obtained
by default, but you can use the [--context=CONTEXT-NAME] option to obtain
a specific context.

The context information is written to STDOUT by default.  Use the PATH
argument to save to a file instead.

Use the [neon login import] command to import an exported context.
";

        /// <inheritdoc/>
        public override string[] Words => new string[] { "login", "export" }; 

        /// <inheritdoc/>
        public override string[] ExtendedOptions => new string[] { "--force", "--context" };

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
            KubeContextName contextName = null;

            var path    = commandLine.Arguments.FirstOrDefault();
            var rawName = commandLine.GetOption("--context");

            if (rawName != null)
            {
                contextName = KubeContextName.Parse(rawName);

                if (!contextName.IsNeonKube)
                {
                    Console.Error.WriteLine($"*** ERROR: [{contextName}] is not a NeonKUBE context.");
                    Program.Exit(1);
                }
            }
            else
            {
                contextName = KubeHelper.CurrentContextName;

                if (contextName == null)
                {
                    Console.Error.WriteLine($"*** ERROR: You are not logged into a NeonKUBE cluster.");
                    Program.Exit(1);
                }
            }

            var context = KubeHelper.KubeConfig.GetContext(contextName);

            if (context == null)
            {
                Console.Error.WriteLine($"*** ERROR: Context [{contextName}] not found.");
                Program.Exit(1);
            }

            var cluster = KubeHelper.KubeConfig.GetCluster(context.Context.Cluster);
            var user    = KubeHelper.KubeConfig.GetUser(context.Context.User);

            if (context == null)
            {
                Console.Error.WriteLine($"*** ERROR: Context [{contextName}] not found.");
                Program.Exit(1);
            }

            if (user == null)
            {
                Console.Error.WriteLine($"*** ERROR: User [{context.Context.User}] not found.");
                Program.Exit(1);
            }

            var login = new ClusterLoginExport()
            {
                Cluster = cluster,
                Context = context,
                User    = user
            };

            var yaml = NeonHelper.YamlSerialize(login);

            if (path == null)
            {
                Console.WriteLine(yaml);
            }
            else
            {
                File.WriteAllText(path, yaml);
            }

            await Task.CompletedTask;
        }
    }
}
