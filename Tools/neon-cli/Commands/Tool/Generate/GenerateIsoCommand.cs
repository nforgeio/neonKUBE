//-----------------------------------------------------------------------------
// FILE:	    GenerateIsoCommand.cs
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
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;

using Neon.ModelGen;
using Neon.Common;
using Neon.Kube;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>generate iso</b> command.
    /// </summary>
    [Command]
    public class GenerateIsoCommand : CommandBase
    {
        private const string usage = @"
Generates an ISO file from files in a folder.

USAGE:

    neon tool generate iso SOURCE-FOLDER SOURCE-FOLDER

ARGUMENTS:

    SOURCE-FOLDER   - Path to the input folder.
    ISO-PATH        - Path to the output ISO file.

OPTIONS:

    --linux         - Process all source files by converting all line
                      endings to Linux format before creating the ISO.

    --label=VALUE   - Specifies the volume label
";

        /// <inheritdoc/>
        public override string[] Words => new string[] { "tool", "generate", "iso" }; 

        /// <inheritdoc/>
        public override string[] ExtendedOptions => new string[] { "--linux" };

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override async Task RunAsync(CommandLine commandLine)
        {
            if (commandLine.Arguments.Length != 2)
            {
                Help();
                Program.Exit(1);
            }

            var sourceFolder = commandLine.Arguments.ElementAtOrDefault(0);
            var isoPath      = commandLine.Arguments.ElementAtOrDefault(1);
            var linux        = commandLine.GetFlag("--linux");
            var label        = commandLine.GetOption("--label");

            if (string.IsNullOrEmpty(sourceFolder) || string.IsNullOrEmpty(isoPath))
            {
                Help();
                Program.Exit(1);
            }

            if (linux)
            {
                foreach (var file in Directory.EnumerateFiles(sourceFolder))
                {
                    var text = File.ReadAllText(file);

                    text = NeonHelper.ToLinuxLineEndings(text);

                    File.WriteAllText(file, text);
                }
            }

            KubeHelper.CreateIsoFile(sourceFolder, isoPath, label);

            Program.Exit(0);
            await Task.CompletedTask;
        }
    }
}
