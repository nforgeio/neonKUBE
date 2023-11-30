//-----------------------------------------------------------------------------
// FILE:        VersionCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
    /// Implements the <b>version</b> command.
    /// </summary>
    [Command]
    public class VersionCommand : CommandBase
    {
        private const string usage = @"
Prints the [neon-cli] version or compares the version to an argument.

USAGE:

    neon neon-version [OPTIONS]

OPTIONS:

    --git               - Include the Git branch/commit information.

    --minimum=VERSION   - Compares the version passed against the current
                          tool version and returns a non-zero exit code
                          if the tool is older than VERSION.
    -n                  - Don't write a newline after version.

";
        /// <inheritdoc/>
        public override string[] Words => new string[] { "neon-version" }; 

        /// <inheritdoc/>
        public override string[] ExtendedOptions => new string[] { "-n", "--git", "--minimum" };

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

            if (commandLine.Arguments.Length > 0)
            {
                Console.Error.WriteLine($"*** ERROR: Unexpected command line argument.");
                Program.Exit(-1);
            }

            var minVersion = commandLine.GetOption("--minimum");

            if (!string.IsNullOrEmpty(minVersion))
            {
                if (!SemanticVersion.TryParse(minVersion, out var minSemanticVersion))
                {
                    Console.Error.WriteLine($"*** ERROR: [{minVersion}] is not a valid semantic version.");
                    Program.Exit(-1);
                }

                var toolSemanticVersion = SemanticVersion.Parse(Program.Version);

                if (toolSemanticVersion < minSemanticVersion)
                {
                    Console.Error.WriteLine($"*** ERROR: [{Program.Name} v{Program.Version}] is older than the required version [{minSemanticVersion}].");
                    Console.Error.WriteLine();
                    Console.Error.WriteLine($"You can obtain the latest releases from here:");
                    Console.Error.WriteLine();
                    Console.Error.WriteLine($"    https://github.com/nforgeio/neonKUBE/releases");
                    Console.Error.WriteLine();
                    Program.Exit(1);
                }
            }
            else
            {
                if (commandLine.HasOption("--git"))
                {
                    Console.Write($"{Program.Version}/{Program.GitVersion}");
                }
                else
                {
                    Console.Write(Program.Version);
                }

                if (!commandLine.HasOption("-n"))
                {
                    Console.WriteLine();
                }
            }

            await Task.CompletedTask;
        }
    }
}
