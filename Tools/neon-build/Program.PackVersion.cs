//-----------------------------------------------------------------------------
// FILE:	    Program.PackVersion.cs
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
        /// Reads a Nuget package version string from the first line of a text file and
        /// then updates the version section in a CSPROJ file or NUSPEC with the version.  
        /// This is useful for batch publishing multiple libraries.
        /// </summary>
        /// <param name="commandLine">The command line.</param>
        private static void PackVersion(CommandLine commandLine)
        {
            commandLine = commandLine.Shift(1);

            if (commandLine.Arguments.Length != 2)
            {
                Console.WriteLine(usage);
                Program.Exit(1);
            }

            var solutionVersionPath = Environment.ExpandEnvironmentVariables(commandLine.Arguments[0]);
            var csprojPath          = Environment.ExpandEnvironmentVariables(commandLine.Arguments[1]);
            var localVersionPath    = Path.Combine(Path.GetDirectoryName(csprojPath), "prerelease.txt");
            var rawSolutionVersion  = File.ReadAllLines(solutionVersionPath).FirstOrDefault();

            if (string.IsNullOrWhiteSpace(rawSolutionVersion))
            {
                Console.Error.WriteLine($"*** ERROR: [{solutionVersionPath}] does not specify a version.");
                Program.Exit(1);
            }

            var solutionVersion = SemanticVersion.Parse(rawSolutionVersion.Trim());
            var localPrerelease = (string)null;

            if (File.Exists(localVersionPath))
            {
                localPrerelease = File.ReadAllLines(localVersionPath).FirstOrDefault();

                if (!string.IsNullOrEmpty(localPrerelease))
                {
                    localPrerelease.Trim();
                }

                if (localPrerelease.StartsWith("-"))
                {
                    localPrerelease = localPrerelease.Substring(1);
                }

                if (string.IsNullOrEmpty(localPrerelease))
                {
                    localPrerelease = null;
                }

                localPrerelease = localPrerelease.ToLowerInvariant();
            }

            string version = null;

            if (solutionVersion.Prerelease != null && (string.IsNullOrEmpty(localPrerelease) || solutionVersion.Prerelease.ToLowerInvariant().CompareTo(localPrerelease) < 0))
            {
                // The solution version specifies a pre-release identifier which is less than
                // the local version or there is no local version.

                version = solutionVersion.ToString();
            }
            else if (!string.IsNullOrEmpty(localPrerelease))
            {
                // This project has a local [prerelease.txt] file so we'll append the
                // contents as the release identifier to the solution version for this
                // project.

                version = $"{solutionVersion}-{localPrerelease}";
            }
            else
            {
                // There is no local pre-release version, so we'll use the
                // solution version.

                version = solutionVersion.ToString();
            }

            // Ensure that the local version is valid.

            SemanticVersion.Parse(version);

            var csproj = File.ReadAllText(csprojPath);
            var pos    = csproj.IndexOf("<Version>", StringComparison.OrdinalIgnoreCase);

            pos += "<Version>".Length;

            if (pos == -1)
            {
                Console.Error.WriteLine($"*** ERROR: [{csprojPath}] does not have: <version>...</version>");
                Program.Exit(1);
            }

            var posEnd = csproj.IndexOf("</Version>", pos, StringComparison.OrdinalIgnoreCase);

            if (posEnd == -1)
            {
                Console.Error.WriteLine($"*** ERROR: [{csprojPath}] does not have: <version>...</version>");
                Program.Exit(1);
            }

            csproj = csproj.Substring(0, pos) + version + csproj.Substring(posEnd);

            File.WriteAllText(csprojPath, csproj);

            Program.Exit(0);
        }
    }
}
