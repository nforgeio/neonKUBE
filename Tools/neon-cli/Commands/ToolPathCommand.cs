//-----------------------------------------------------------------------------
// FILE:        ToolPathCommand.cs
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
    /// Implements the <b>toolpath</b> command.
    /// </summary>
    [Command]
    public class ToolPathCommand : CommandBase
    {
        private const string usage = @"
Returns the path to a neon related tool binary, like: helm

USAGE:

    neon toolpath TOOLNAME

ARGUMENTS:

    TOOLNAME    - specifies the desired tool, one of: helm

REMARKS:

This command returns the full qualified path to the tool binary.
For normal users, this will be the path to the tool in the install
folder.  For maintainers, this command will attempt to download
and cache the tool and return its path from the cache folder.

";

        /// <inheritdoc/>
        public override string[] Words => new string[] { "toolpath" };

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override async Task RunAsync(CommandLine commandLine)
        {
            if (commandLine.Items.Length == 0)
            {
                Help();
                return;
            }

            var toolName = commandLine.Arguments.ElementAtOrDefault(0);

            if (toolName == null)
            {
                Console.Error.WriteLine("TOOLNAME argument expected.");
                Program.Exit(1);
            }

            switch (toolName)
            {
                case "helm":

                    Console.WriteLine(Program.HelmPath);
                    break;

                default:

                    Console.Error.WriteLine($"Unknown tool: {toolName}");
                    Program.Exit(1);
                    break;
            }

            await Task.CompletedTask;
        }
    }
}
