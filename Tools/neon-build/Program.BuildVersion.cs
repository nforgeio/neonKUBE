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
            var kubeVersionPath    = Path.Combine(Environment.GetEnvironmentVariable("NF_ROOT"), "neonKUBE-version.txt");
            var libraryVersionPath = Path.Combine(Environment.GetEnvironmentVariable("NF_ROOT"), "neonLIBRARY-version.txt");
            var buildCsPath        = Path.Combine(Environment.GetEnvironmentVariable("NF_ROOT"), "Lib", "Neon.Common", "Build.cs");
            var kubeVersion        = File.ReadLines(kubeVersionPath, Encoding.UTF8).First();
            var libraryVersion     = File.ReadLines(libraryVersionPath, Encoding.UTF8).First();

            if (string.IsNullOrEmpty(kubeVersionPath))
            {
                Console.Error.WriteLine($"[{kubeVersionPath}] specifies no version.");
                Program.Exit(1);
            }

            if (string.IsNullOrEmpty(libraryVersion))
            {
                Console.Error.WriteLine($"[{libraryVersionPath}] specifies no version.");
                Program.Exit(1);
            }

            kubeVersion = kubeVersion.Trim();

            if (!SemanticVersion.TryParse(kubeVersionPath, out var v))
            {
                Console.Error.WriteLine($"[{kubeVersionPath}] specifies an invalid semantic version: [{kubeVersion}].");
                Program.Exit(1);
            }

            libraryVersion = libraryVersion.Trim();

            if (!SemanticVersion.TryParse(libraryVersion, out v))
            {
                Console.Error.WriteLine($"[{libraryVersionPath}] specifies an invalid semantic version: [{libraryVersion}].");
                Program.Exit(1);
            }

            // Process the lines from the [$/Lib/Neon/Common/Build.cs] file, looking for the one
            // with the [NeonLibraryVersion] constant definitions and replace
            // the value with the appropriate version we loaded above.

            var buildCsLines = File.ReadAllLines(buildCsPath);
            var sbOutput     = new StringBuilder();

            foreach (var line in buildCsLines)
            {
                if (!line.Contains("public const string NeonLibraryVersion"))
                {
                    sbOutput.AppendLine(line);
                    continue;
                }

                int pStartQuote;
                int pEndQuote;

                pStartQuote = line.IndexOf('"');

                if (pStartQuote == -1)
                {
                    Console.Error.WriteLine($"[{buildCsPath}] unexpected [NeonLibraryVersion] definition format.");
                    Program.Exit(1);
                }

                pEndQuote = line.IndexOf('"', pStartQuote + 1);

                if (pStartQuote == -1)
                {
                    Console.Error.WriteLine($"[{buildCsPath}] unexpected [NeonLibraryVersion] definition format.");
                    Program.Exit(1);
                }

                var oldLiteral = line.Substring(pStartQuote, pEndQuote - pStartQuote + 1);
                var newLiteral = $"\"{libraryVersion}\"";

                var newLine = line.Replace(oldLiteral, newLiteral);

                sbOutput.AppendLine(newLine);
            }

            File.WriteAllText(buildCsPath, sbOutput.ToString());

            // Process [$/Lib/Neon/Common/Build.cs] file again to replace
            // the value for [NeonKubeVersion].

            buildCsLines = File.ReadAllLines(buildCsPath);
            sbOutput     = new StringBuilder();

            foreach (var line in buildCsLines)
            {
                if (!line.Contains("public const string NeonKubeVersion"))
                {
                    sbOutput.AppendLine(line);
                    continue;
                }

                int pStartQuote;
                int pEndQuote;

                pStartQuote = line.IndexOf('"');

                if (pStartQuote == -1)
                {
                    Console.Error.WriteLine($"[{buildCsPath}] unexpected [NeonKubeVersion] definition format.");
                    Program.Exit(1);
                }

                pEndQuote = line.IndexOf('"', pStartQuote + 1);

                if (pStartQuote == -1)
                {
                    Console.Error.WriteLine($"[{buildCsPath}] unexpected [NeonKubeVersion] definition format.");
                    Program.Exit(1);
                }

                var oldLiteral = line.Substring(pStartQuote, pEndQuote - pStartQuote + 1);
                var newLiteral = $"\"{kubeVersion}\"";

                var newLine = line.Replace(oldLiteral, newLiteral);

                sbOutput.AppendLine(newLine);
            }

            File.WriteAllText(buildCsPath, sbOutput.ToString());
        }
    }
}
