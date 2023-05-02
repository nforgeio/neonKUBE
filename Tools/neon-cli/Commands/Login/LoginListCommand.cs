//-----------------------------------------------------------------------------
// FILE:	    LoginListCommand.cs
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
using Neon.Kube.Hosting;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>logins list</b> command.
    /// </summary>
    [Command]
    public class LoginListCommand : CommandBase
    {
        private const string usage = @"
Lists the neonKUBE contexts available on the local computer.

USAGE:

    neon login list
    neon login ls
";

        /// <inheritdoc/>
        public override string[] Words => new string[] { "login", "list" }; 

        /// <inheritdoc/>
        public override string[] AltWords => new string[] { "login", "ls" };

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
            var config  = KubeHelper.KubeConfig;
            var current = KubeHelper.CurrentContext;
            var logins  = new List<string>();

            foreach (var context in KubeHelper.KubeConfig.Contexts
                .Where(context =>
                {
                    var cluster = config.GetCluster(current.Cluster);

                    if (cluster == null)
                    {
                        return false;
                    }
                    else
                    {
                        return cluster.IsNeonKube;
                    }
                })
                .OrderBy(context => context.Name))
            {
                logins.Add(context.Name);
            }

            Console.WriteLine();

            if (logins.Count == 0)
            {
                Console.Error.WriteLine("*** No neonKUBE logins.");
            }
            else
            {
                var maxLoginNameWidth = logins.Max(login => login.Length);

                foreach (var login in logins.OrderBy(login => login))
                {
                    if (current != null && login == current.Name)
                    {
                        Console.Write(" --> ");
                    }
                    else
                    {
                        Console.Write("     ");
                    }

                    Console.WriteLine(login);
                }

                Console.WriteLine();
            }

            await Task.CompletedTask;
        }
    }
}
