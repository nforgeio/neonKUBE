//-----------------------------------------------------------------------------
// FILE:        LoginListCommand.cs
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
using Neon.Kube.Hosting;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>logins list</b> command.
    /// </summary>
    [Command]
    public class LoginListCommand : CommandBase
    {
        //---------------------------------------------------------------------
        // Private types

        private class LoginInfo
        {
            public string context { get; set; }
            public string @namespace { get; set; }
            public bool current { get; set; }
        }

        //---------------------------------------------------------------------
        // Implementation

        private const string usage = @"
Lists the NeonKUBE contexts.

USAGE:

    neon login list|ls
";

        /// <inheritdoc/>
        public override string[] Words => new string[] { "login", "list" };

        /// <inheritdoc/>
        public override string[] AltWords => new string[] { "login", "ls" };

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
            commandLine.DefineOption("--namespace", "-n");
            commandLine.DefineOption("--output", "-o");

            var outputFormat = Program.GetOutputFormat(commandLine);

            var config  = KubeHelper.KubeConfig;
            var current = KubeHelper.CurrentContext;
            var logins  = KubeHelper.KubeConfig.Contexts
                .Where(context =>
                {
                    var cluster = config.GetCluster(context.Context.Cluster);

                    if (cluster == null)
                    {
                        return false;
                    }
                    else
                    {
                        return cluster.IsNeonKube;
                    }
                })
                .OrderBy(context => context.Name)
                .Select(context => new LoginInfo() { context = context.Name, @namespace = context.Context.Namespace, current = context == current })
                .ToArray();

            Console.WriteLine();

            if (outputFormat.HasValue)
            {
                switch (outputFormat.Value)
                {
                    case OutputFormat.Json:

                        Console.WriteLine(NeonHelper.JsonSerialize(logins, Formatting.Indented));
                        break;

                    case OutputFormat.Yaml:

                        Console.WriteLine(NeonHelper.YamlSerialize(logins));
                        break;

                    default:

                        throw new NotImplementedException();
                }
            }
            else
            {
                if (logins.Length == 0)
                {
                    Console.Error.WriteLine("No NeonKUBE logins.");
                    return;
                }

                var maxLoginNameWidth = logins.Max(login => login.context.Length);

                foreach (var login in logins)
                {
                    if (login.current)
                    {
                        Console.Write(" --> ");
                    }
                    else
                    {
                        Console.Write("     ");
                    }

                    var formattedName = login.context;

                    if (formattedName.Length < maxLoginNameWidth)
                    {
                        formattedName += new string(' ', maxLoginNameWidth - formattedName.Length);
                    }

                    Console.WriteLine($"{formattedName} ({(login.@namespace ?? "default")})");
                }

                Console.WriteLine();
            }

            await Task.CompletedTask;
        }
    }
}
