//-----------------------------------------------------------------------------
// FILE:	    PasswordRemoveCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using Neon.Cryptography;
using Neon.Kube;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>password remove</b> command.
    /// </summary>
    [Command]
    public class PasswordRemoveCommand : CommandBase
    {
        private const string usage = @"
Removes a specific named password or all passwords.

USAGE:

    neon tool password remove|rm [--force] NAME - Removes the named password
    neon tool password remove|rm [--force] *    - Removes all named passwords

ARGUMENTS:

    NAME        - the password name
    PATH        - path to the source file
    -           - read password from STDIN

OPTIONS:

    --force     - don't prompt to confirm removal.

REMARKS:

This command removes a named password or all passwords.
";

        /// <inheritdoc/>
        public override string[] Words => new string[] { "tool", "password", "remove" }; 

        /// <inheritdoc/>
        public override string[] AltWords => new string[] { "tool", "password", "rm" }; 

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
            if (commandLine.HasHelpOption)
            {
                Console.WriteLine(usage);
                Program.Exit(0);
            }

            Console.WriteLine();

            var nameArg = commandLine.Arguments.ElementAtOrDefault(0);
            var force   = commandLine.HasOption("--force");

            if (nameArg == null)
            {
                Console.Error.WriteLine($"*** ERROR: NAME argument is required.");
                Program.Exit(1);
            }

            if (nameArg == "*")
            {
                if (!force && !Program.PromptYesNo("Are you sure you want to remove all passwords?"))
                {
                    Program.Exit(0);
                }

                foreach (var path in Directory.GetFiles(KubeHelper.PasswordsFolder))
                {
                    File.Delete(path);
                }
            }
            else
            {
                var passwordName = NeonVault.ValidatePasswordName(nameArg);
                var passwordPath = Path.Combine(KubeHelper.PasswordsFolder, passwordName);

                if (!File.Exists(passwordPath))
                {
                    Console.Error.WriteLine($"*** ERROR: The [{passwordName}] password does not exist.");
                    Program.Exit(1);
                }
                else
                {
                    if (!force && !Program.PromptYesNo($"Are you sure you want to remove the [{passwordName}] password?"))
                    {
                        Program.Exit(0);
                    }

                    File.Delete(passwordPath);
                }
            }

            Program.Exit(0);
            await Task.CompletedTask;
        }
    }
}
