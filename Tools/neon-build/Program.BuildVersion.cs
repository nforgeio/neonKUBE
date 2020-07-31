//-----------------------------------------------------------------------------
// FILE:	    Program.Shfb.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.IO;
using System.Linq;
using System.Text;

using Neon.Common;

namespace NeonBuild
{
    public static partial class Program
    {
        /// <summary>
        /// Updates the build version in [$/Lib/Neon.Common/Build.cs]
        /// </summary>
        /// <param name="commandLine">The command line.</param>
        private static void BuildVersion(CommandLine commandLine)
        {
            var neonDesktopVersionPath = Path.Combine(Environment.GetEnvironmentVariable("NF_ROOT"), "neonDESKTOP-version.txt");
            var neonLibraryVersionPath = Path.Combine(Environment.GetEnvironmentVariable("NF_ROOT"), "neonLIBRARY-version.txt");
            var neonKubeVersionPath    = Path.Combine(Environment.GetEnvironmentVariable("NF_ROOT"), "neonKUBE-version.txt");
            var neonDesktopVersion     = File.ReadLines(neonDesktopVersionPath, Encoding.UTF8).First();
            var neonLibraryVersion     = File.ReadLines(neonLibraryVersionPath, Encoding.UTF8).First();
            var neonKubeVersion        = File.ReadLines(neonKubeVersionPath, Encoding.UTF8).First();
            var buildCsPath            = Path.Combine(Environment.GetEnvironmentVariable("NF_ROOT"), "Lib", "Neon.Common", "Build.cs");

            if (string.IsNullOrEmpty(neonDesktopVersionPath))
            {
                Console.Error.WriteLine($"[{neonDesktopVersionPath}] specifies no version.");
                Program.Exit(1);
            }

            if (string.IsNullOrEmpty(neonLibraryVersion))
            {
                Console.Error.WriteLine($"[{neonLibraryVersionPath}] specifies no version.");
                Program.Exit(1);
            }

            if (string.IsNullOrEmpty(neonKubeVersionPath))
            {
                Console.Error.WriteLine($"[{neonKubeVersionPath}] specifies no version.");
                Program.Exit(1);
            }

            neonDesktopVersion = neonDesktopVersion.Trim();

            if (!SemanticVersion.TryParse(neonDesktopVersion, out var v))
            {
                Console.Error.WriteLine($"[{neonDesktopVersionPath}] specifies an invalid semantic version: [{neonDesktopVersion}].");
                Program.Exit(1);
            }

            neonLibraryVersion = neonLibraryVersion.Trim();

            if (!SemanticVersion.TryParse(neonLibraryVersion, out v))
            {
                Console.Error.WriteLine($"[{neonLibraryVersionPath}] specifies an invalid semantic version: [{neonLibraryVersion}].");
                Program.Exit(1);
            }

            neonKubeVersion = neonKubeVersion.Trim();

            if (!SemanticVersion.TryParse(neonKubeVersion, out v))
            {
                Console.Error.WriteLine($"[{neonKubeVersionPath}] specifies an invalid semantic version: [{neonKubeVersion}].");
                Program.Exit(1);
            }

            // Update the [$/Lib/Neon/Common/Build.cs] file with the current build versions.

            MungeBuildCs(buildCsPath, "NeonLibraryVersion", neonLibraryVersion);
            MungeBuildCs(buildCsPath, "NeonDesktopVersion", neonDesktopVersion);
            MungeBuildCs(buildCsPath, "NeonKubeVersion", neonKubeVersion);
        }

        /// <summary>
        /// Munges the solution's <b>Build.cs</b> source file to update one of the named
        /// version constants.
        /// </summary>
        /// <param name="buildCsPath">Path to the <b>Build.cs</b> file.</param>
        /// <param name="versionName">Name of the target version constant.</param>
        /// <param name="version">The new version.</param>
        private static void MungeBuildCs(string buildCsPath, string versionName, string version)
        {
            var buildCsLines = File.ReadAllLines(buildCsPath);
            var sbOutput     = new StringBuilder();

            foreach (var line in buildCsLines)
            {
                if (!line.Contains($"public const string {versionName}"))
                {
                    sbOutput.AppendLine(line);
                    continue;
                }

                int pStartQuote;
                int pEndQuote;

                pStartQuote = line.IndexOf('"');

                if (pStartQuote == -1)
                {
                    Console.Error.WriteLine($"[{buildCsPath}] unexpected [{versionName}] definition format.");
                    Program.Exit(1);
                }

                pEndQuote = line.IndexOf('"', pStartQuote + 1);

                if (pStartQuote == -1)
                {
                    Console.Error.WriteLine($"[{buildCsPath}] unexpected [{versionName}] definition format.");
                    Program.Exit(1);
                }

                var oldLiteral = line.Substring(pStartQuote, pEndQuote - pStartQuote + 1);
                var newLiteral = $"\"{version}\"";

                var newLine = line.Replace(oldLiteral, newLiteral);

                sbOutput.AppendLine(newLine);
            }

            File.WriteAllText(buildCsPath, sbOutput.ToString());
        }
    }
}
