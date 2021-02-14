//-----------------------------------------------------------------------------
// FILE:	    LoginRemoveCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>login remove</b> command.
    /// </summary>
    [Command]
    public class LoginRemoveCommand : CommandBase
    {
        private const string usage = @"
Removes a Kubernetes context from the local computer.

USAGE:

    neon login rm       [--force] [ USER@CLUSTER[/NAMESPACE] ]
    neon login remove   [--force] [ USER@CLUSTER[/NAMESPACE] ]

ARGUMENTS:

    USER@CLUSTER[/NAMESPACE]    - Kubernetes user, cluster and optional namespace

OPTIONS:

    --force             - Don't prompt, just remove.

REMARKS:

By default, this comman will remove the current login when 
USER@CLUSTER[/NAMESPACE is not specified.
";

        /// <inheritdoc/>
        public override string[] Words => new string[] { "login", "remove" }; 

        /// <inheritdoc/>
        public override string[] AltWords => new string[] { "login", "rm" }; 

        /// <inheritdoc/>
        public override string[] ExtendedOptions => new string[] { "--force" }; 

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override async Task RunAsync(CommandLine commandLine)
        {
            KubeConfigContext   context     = null;
            KubeContextName     contextName = null;

            if (commandLine.Arguments.Length > 0)
            {
                contextName = KubeContextName.Parse(commandLine.Arguments.FirstOrDefault());
            }

            if (contextName != null)
            {
                context = KubeHelper.Config.GetContext(contextName);

                if (context == null)
                {
                    Console.Error.WriteLine($"*** ERROR: Context [{contextName}] not found.");
                    Program.Exit(1);
                }
            }
            else
            {
                context = KubeHelper.CurrentContext;

                if (context == null)
                {
                    Console.Error.WriteLine($"*** ERROR: You are not logged into a cluster.");
                    Program.Exit(1);
                }

                contextName = (KubeContextName)context.Name;
            }

            if (!commandLine.HasOption("--force") && !Program.PromptYesNo($"*** Are you sure you want to remove: {contextName}?"))
            {
                return;
            }

            if (KubeHelper.CurrentContextName == contextName)
            {
                Console.WriteLine($"Logging out of: {contextName}");
                KubeHelper.Desktop.Logout().Wait();
            }

            KubeHelper.Config.RemoveContext(context);
            Console.WriteLine($"Removed: {contextName}");

            // Notify the desktop application.

            await KubeHelper.Desktop.UpdateUIAsync();
        }
    }
}
