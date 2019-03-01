//-----------------------------------------------------------------------------
// FILE:	    Program.cs
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

using Neon.Common;

namespace NeonBuild
{
    /// <summary>
    /// Hosts the program entrypoint.
    /// </summary>
    public static class Program
    {
        private const string usage =
@"
Internal KSETUP project build related utilities.

Builds a neonKUBE Installer
---------------------------
neon-build build-installer PLATFORM [--kube-version=VERSION]

Removes cached components
-------------------------
neon-build clear PLATFORM

Downloads KUBE PLATFORM components (if not already present)
-----------------------------------------------------------
neon-build download PLATFORM [--kube-version=VERSION]

ARGUMENTS:

    PLATFORM        - specifies the target platform, one of:

                        windows, osx

OPTIONS:

    --kube-version  - optionally specifies the Kubernetes version
                      to be installed.  This defaults to the version
                      read from [$/kube-version.txt].
";
        private static CommandLine commandLine;

        /// <summary>
        /// This is the program entrypoint.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        public static void Main(string[] args)
        {
            commandLine = new CommandLine(args);

            var command = commandLine.Arguments.FirstOrDefault();

            if (command != null)
            {
                command = command.ToLowerInvariant();
            }

            if (commandLine.Arguments.Length == 0 || commandLine.HasHelpOption || command == "help")
            {
                Console.WriteLine(usage);
                Program.Exit(0);
            }

            try
            {
                var platform = commandLine.Arguments.ElementAtOrDefault(1);

                if (string.IsNullOrEmpty(platform))
                {
                    Console.Error.WriteLine("*** ERROR: PLATFORM argument is required.");
                    Program.Exit(1);
                }

                Program.DefaultKubernetesVersion = File.ReadAllText(Path.Combine(Environment.GetEnvironmentVariable("NF_ROOT"), "kube-version.txt")).Trim();

                var helper = new KubeSetupHelper(platform, commandLine,
                    outputAction: text => Console.Write(text),
                    errorAction:  text => Console.Write(text));

                // Handle the commands.

                switch (command)
                {
                    case "build-installer":

                        EnsureOption("--kube-version", Program.DefaultKubernetesVersion);

                        switch (helper.Platform)
                        {
                            case KubeClientPlatform.Windows:

                                new WinInstallBuilder(helper).Run();
                                break;

                            case KubeClientPlatform.Osx:

                                throw new NotImplementedException();
                        }
                        break;

                    case "clear":

                        helper.Clear();
                        break;

                    case "download":

                        EnsureOption("--kube-version", Program.DefaultKubernetesVersion);
                        helper.Download();
                        break;

                    default:

                        Console.Error.WriteLine($"*** ERROR: Unexpected command [{command}].");
                        Program.Exit(1);
                        break;
                }

                Program.Exit(0);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"*** ERROR: {e.Message}");
                Program.Exit(1);
            }
        }

        /// <summary>
        /// Returns the default version of Kubernetes to be installed.
        /// </summary>
        public static string DefaultKubernetesVersion { get; private set; }

        /// <summary>
        /// Ensures that a command line option is present.
        /// </summary>
        /// <param name="option">The option name.</param>
        /// <param name="defValue">Optionally specifies the default value.</param>
        private static void EnsureOption(string option, string defValue = null)
        {
            if (string.IsNullOrEmpty(commandLine.GetOption(option, defValue)))
            {
                Console.Error.WriteLine($"*** ERROR: Command line option [{option}] is invalid.");
                Program.Exit(1);
            }
        }

        /// <summary>
        /// Terminates the program with a specified exit code.
        /// </summary>
        /// <param name="exitCode">The exit code.</param>
        public static void Exit(int exitCode)
        {
            Environment.Exit(exitCode);
        }
    }
}
