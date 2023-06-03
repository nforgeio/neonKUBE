//-----------------------------------------------------------------------------
// FILE:	    LoginDeleteCommand.cs
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
    /// Implements the <b>login delete</b> command.
    /// </summary>
    [Command]
    public class LoginDeleteCommand : CommandBase
    {
        private const string usage = @"
Removes a NEONKUBE context from the local worstation.

USAGE:

    neon login delete|rm [--force] [CONTEXT-NAME]

ARGUMENTS:

    CONTEXT-NAME    - Optionally specifies the context to be removed.  The
                      current context is removed by default.

OPTIONS:

    --force         - Don't prompt for permission and also ignore missing contexts

REMARKS:

This command removes the current login when CONTEXT-NAME is not specified.
";

        /// <inheritdoc/>
        public override string[] Words => new string[] { "login", "delete" };

        /// <inheritdoc/>
        public override string[] AltWords => new string[] { "login", "rm" };

        /// <inheritdoc/>
        public override string[] ExtendedOptions => new string[] { "--force" };

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
            KubeConfigContext   context     = null;
            KubeContextName     contextName = null;

            Console.WriteLine();

            var force = commandLine.HasOption("--force");

            if (commandLine.Arguments.Length > 0)
            {
                contextName = KubeContextName.Parse(commandLine.Arguments.FirstOrDefault());

                if (!contextName.IsNeonKube)
                {
                    Console.Error.WriteLine($"*** ERROR: [{contextName}] is not a NEONKUBE context.");
                    Program.Exit(1);
                }
            }

            if (contextName != null)
            {
                context = KubeHelper.KubeConfig.GetContext(contextName);

                if (context == null)
                {
                    if (!force)
                    {
                        Console.Error.WriteLine($"*** ERROR: Context [{contextName}] not found.");
                        Program.Exit(1);
                    }
                    else
                    {
                        KubeHelper.KubeConfig.RemoveContext(contextName);
                        Program.Exit(0);
                    }
                }
            }
            else
            {
                context = KubeHelper.CurrentContext;

                if (context == null)
                {
                    if (!force)
                    {
                        Console.Error.WriteLine($"*** ERROR: You are not logged into a NEONKUBE cluster.");
                        Program.Exit(1);
                    }
                    else
                    {
                        Program.Exit(0);
                    }
                }

                contextName = (KubeContextName)context.Name;
            }

            if (!force && !Program.PromptYesNo($"*** Are you sure you want to remove: {contextName}?"))
            {
                return;
            }

            if (KubeHelper.CurrentContextName == contextName)
            {
                Console.WriteLine($"Logging out of: {contextName}");
            }

            // Remove the SSH key from the user directory if present.

            if (KubeHelper.CurrentContextName != null)
            {
                var sshKeyPath = Path.Combine(NeonHelper.UserHomeFolder, ".ssh", (string)KubeHelper.CurrentContextName);

                NeonHelper.DeleteFile(sshKeyPath);
            }

            // Remove the login and kubecontext

            KubeHelper.KubeConfig.RemoveContext(context);
            Console.WriteLine($"Removed: {contextName}");
            Console.WriteLine();

            await Task.CompletedTask;
        }
    }
}
