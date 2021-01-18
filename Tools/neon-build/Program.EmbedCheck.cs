//-----------------------------------------------------------------------------
// FILE:	    Program.Shfb.cs
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
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

using Neon.Common;

namespace NeonBuild
{
    public partial class Program
    {
        /// <summary>
        /// Ensures that all of the files within a specified directory are also 
        /// explicitly referenced as <b>embedded resources</b> in a C# project
        /// file.
        /// </summary>
        /// <param name="commandLine">The command line.</param>
        public static void EmbedCheck(CommandLine commandLine)
        {
            const string usage =
@"
neon-build embed-check PROJECT EMBED-FOLDER

ARGUMENTS:

    PROJECT         - Path to a [*.csproj] file
    EMBED-FOLDER    - Path to the folder with embedded resource files

Verifies that a C# project file includes embedded resource file references
to all of the files within EMBED-FOLDER (recurively).  This is handy for
ensuring that no files are present that aren't being embeded.
";

            if (commandLine.Arguments.Count() == 0)
            {
                Console.Error.WriteLine(usage);
                Program.Exit(0);
            }
            else if (commandLine.Arguments.Count() != 3)
            {
                Console.WriteLine(usage);
                Program.Exit(1);
            }

            Console.WriteLine(commandLine);
            Console.WriteLine("neon-build embed-check: Ensure that resource files will be embedded.");

            var projectPath   = Path.GetFullPath(commandLine.Arguments[1]);
            var projectFolder = projectPath.Substring(0, projectPath.Length - Path.GetFileName(projectPath).Length);
            var folderPath    = Path.GetFullPath(commandLine.Arguments[2]);
            var projectText   = File.ReadAllText(projectPath);
            var badFiles      = new List<string>();

            if (!Path.GetExtension(projectPath).Equals(".csproj", StringComparison.InvariantCultureIgnoreCase))
            {
                Console.Error.WriteLine($"neon-build embed-check: *** ERROR: Only [*.csproj] project files are supported.");
                Program.Exit(1);
            }

            // We're only going to support checking folders that are beneath
            // the project folder.

            if (!folderPath.StartsWith(projectFolder, StringComparison.InvariantCultureIgnoreCase))
            {
                Console.Error.WriteLine($"neon-build embed-check: *** ERROR: target folder [{folderPath}] is not within the [{projectFolder}] project folder.");
                Program.Exit(1);
            }

            foreach (var file in Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories))
            {
                var relativePath = file.Substring(projectFolder.Length);
                var embedElement = $"<EmbeddedResource Include=\"{relativePath}\"";

                if (!projectText.Contains(embedElement, StringComparison.InvariantCultureIgnoreCase))
                {
                    badFiles.Add(relativePath);
                }
            }

            if (badFiles.Count > 0)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("neon-build embed-check: *** ERROR: One or more files are not marked as embedded:");
                Console.Error.WriteLine("--------------------------------------------------------------------------------");

                foreach (var file in badFiles)
                {
                    Console.Error.WriteLine(file);
                }
            }

            Console.WriteLine("neon-build embed-check: All resource files are embedded.");
            Program.Exit(0);
        }
    }
}
