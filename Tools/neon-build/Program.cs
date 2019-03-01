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
Internal neonKUBE project build related utilities.

neon-build clean
----------------
Deletes all of the [bin] and [obj] folders within the repo and
also clears the [Build] folder.

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
            string              platform;
            KubeSetupHelper     helper;

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
                Program.RepoRootFolder = Environment.GetEnvironmentVariable("NF_ROOT");

                if (string.IsNullOrEmpty(Program.RepoRootFolder) || !Directory.Exists(Program.RepoRootFolder))
                {
                    Console.Error.WriteLine("*** ERROR: NF_ROOT environment variable does not reference the local neonKUBE repostory.");
                    Program.Exit(1);
                }

                Program.DefaultKubernetesVersion = File.ReadAllText(Path.Combine(Program.RepoRootFolder, "kube-version.txt")).Trim();

                // Handle the commands.

                switch (command)
                {
                    case "clean":

                        var buildFolder = Path.Combine(Program.RepoRootFolder, "Build");

                        if (Directory.Exists(buildFolder))
                        {
                            NeonHelper.DeleteFolderContents(Path.Combine(Program.RepoRootFolder, "Build"));
                        }

                        foreach (var folder in Directory.EnumerateDirectories(Program.RepoRootFolder, "bin", SearchOption.AllDirectories))
                        {
                            NeonHelper.DeleteFolder(folder);
                        }

                        foreach (var folder in Directory.EnumerateDirectories(Program.RepoRootFolder, "obj", SearchOption.AllDirectories))
                        {
                            NeonHelper.DeleteFolder(folder);
                        }

                        break;

                    case "installer":

                        platform = commandLine.Arguments.ElementAtOrDefault(1);

                        if (string.IsNullOrEmpty(platform))
                        {
                            Console.Error.WriteLine("*** ERROR: PLATFORM argument is required.");
                            Program.Exit(1);
                        }

                        helper = new KubeSetupHelper(platform, commandLine,
                            outputAction: text => Console.Write(text),
                            errorAction: text => Console.Write(text));

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

                        platform = commandLine.Arguments.ElementAtOrDefault(1);

                        if (string.IsNullOrEmpty(platform))
                        {
                            Console.Error.WriteLine("*** ERROR: PLATFORM argument is required.");
                            Program.Exit(1);
                        }

                        helper = new KubeSetupHelper(platform, commandLine,
                            outputAction: text => Console.Write(text),
                            errorAction: text => Console.Write(text));

                        helper.Clear();
                        break;

                    case "download":

                        platform = commandLine.Arguments.ElementAtOrDefault(1);

                        if (string.IsNullOrEmpty(platform))
                        {
                            Console.Error.WriteLine("*** ERROR: PLATFORM argument is required.");
                            Program.Exit(1);
                        }

                        helper = new KubeSetupHelper(platform, commandLine,
                            outputAction: text => Console.Write(text),
                            errorAction: text => Console.Write(text));

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
        /// Returns the path to the neonKUBE local repository root folder.
        /// </summary>
        public static string RepoRootFolder { get; private set; }

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
