//-----------------------------------------------------------------------------
// FILE:	    LoginExportCommand.cs
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
    /// Implements the <b>logins export</b> command.
    /// </summary>
    [Command]
    public class LoginExportCommand : CommandBase
    {
        private const string usage = @"
Exports an extended Kubernetes context to standard output.

USAGE:

    neon login export --context=USER@CLUSTER[/NAMESPACE] ] [PATH]

ARGUMENTS:

    USER@CLUSTER[/NAMESPACE]    - Kubernetes user, cluster and optional namespace
    PATH                        - Optional output file (defaults to STDOUT)

REMARKS:

    The output includes the Kubernetes context information along
    with additional neonKUBE cluster login information.  This is 
    intended to be used for distributing a login to other cluster 
    login to their workstation.
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
                    Console.Error.WriteLine($"*** ERROR: [{contextName}] is not a neonKUBE context.");
                    Program.Exit(1);
                }
            }
            else
            {
                contextName = KubeHelper.CurrentContextName;

                if (contextName == null)
                {
                    Console.Error.WriteLine($"*** ERROR: You are not logged into a neonKUBE cluster.");
                    Program.Exit(1);
                }
            }

            var context = KubeHelper.Config.GetContext(contextName);

            if (context == null)
            {
                Console.Error.WriteLine($"*** ERROR: Context [{contextName}] not found.");
                Program.Exit(1);
            }

            var cluster = KubeHelper.Config.GetCluster(context.Config.Cluster);
            var user    = KubeHelper.Config.GetUser(context.Config.User);

            if (context == null)
            {
                Console.Error.WriteLine($"*** ERROR: Context [{contextName}] not found.");
                Program.Exit(1);
            }

            if (user == null)
            {
                Console.Error.WriteLine($"*** ERROR: User [{context.Config.User}] not found.");
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
