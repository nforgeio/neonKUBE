//-----------------------------------------------------------------------------
// FILE:	    PasswordCommand.cs
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
using Neon.Kube;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>password</b> command.
    /// </summary>
    [Command]
    public class PasswordCommand : CommandBase
    {
        private const string usage = @"
Manages neonKUBE passwords.

USAGE:

    neon tool password
    neon tool password export PATH NAME...
    neon tool password export PATH *
    neon tool password generate [LENGTH]
    neon tool password import PATH
    neon tool password list|ls
    neon tool password remove|rm NAME
    neon tool password remove|rm *
    neon tool password set NAME [PATH|-]

ARGUMENTS:

    LENGTH      - Length of the desired password (default=20)
    NAME        - Password name
    PATH        - Input or output file path
    -           - Read from standard input
    *           - Process all passwords
";

        /// <inheritdoc/>
        public override string[] Words => new string[] { "tool", "password" }; 

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

            if (commandLine.Arguments.Length > 0)
            {
                Console.Error.WriteLine($"*** ERROR: Unexpected [{commandLine.Arguments[0]}] command.");
                Program.Exit(1);
            }

            await Task.CompletedTask;
        }
    }
}
