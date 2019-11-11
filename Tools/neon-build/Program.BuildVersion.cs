//-----------------------------------------------------------------------------
// FILE:	    Program.Shfb.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
            var productVersionPath = Path.Combine(Environment.GetEnvironmentVariable("NF_ROOT"), "product-version.txt");
            var buildCsPath        = Path.Combine(Environment.GetEnvironmentVariable("NF_ROOT"), "Lib", "Neon.Common", "Build.cs");
            var version            = File.ReadLines(productVersionPath, Encoding.UTF8).First();

            if (string.IsNullOrEmpty(version))
            {
                Console.Error.WriteLine($"[{productVersionPath}] specifies an empty version.");
                Program.Exit(1);
            }

            version = version.Trim();

            if (!SemanticVersion.TryParse(version, out var v))
            {
                Console.Error.WriteLine($"[{productVersionPath}] specifies an invalid semantic version: [{version}].");
                Program.Exit(1);
            }

            // Process the lines from the [$/Lib/Neon/Common/Build.cs] file, looking for the one
            // with the [ProductVersion] constant definition.  We're going to replace the string
            // with the product version we retrieved above and the rewrite the source file.
            //
            // Note that this is somewhat fragile because we're depending on the constant definition
            // being on a single line (which is has been for at least 14 years).

            var buildCsLines = File.ReadAllLines(buildCsPath);
            var sbOutput     = new StringBuilder();

            foreach (var line in buildCsLines)
            {
                if (!line.Contains("public const string ProductVersion"))
                {
                    sbOutput.AppendLine(line);
                    continue;
                }

                int pStartQuote;
                int pEndQuote;

                pStartQuote = line.IndexOf('"');

                if (pStartQuote == -1)
                {
                    Console.Error.WriteLine($"[{buildCsPath}] unexpected [ProductVersion] definition format.");
                    Program.Exit(1);
                }

                pEndQuote = line.IndexOf('"', pStartQuote + 1);

                if (pStartQuote == -1)
                {
                    Console.Error.WriteLine($"[{buildCsPath}] unexpected [ProductVersion] definition format.");
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
