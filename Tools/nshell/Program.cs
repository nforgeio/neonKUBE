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
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Neon;
using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Windows;

namespace NShell
{
    /// <summary>
    /// Program information.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// The program version.
        /// </summary>
        public const string Version = Build.ProductVersion;

        /// <summary>
        /// Program entry point.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        public static int Main(params string[] args)
        {
            string usage = $@"
neonKUBE Shell Utilities: nshell [v{Program.Version}]
{Build.Copyright}

USAGE:

    nshell help         COMMAND         - Help for a command
    nshell file         COMMAND         - Manages file encryption
    nshell version      [-n] [--git]    - Prints version
    nshell password     COMMAND         - Manages passwords
    nshell run --       COMMAND         - Runs a command with secrets
";

            // Disable any logging that might be performed by library classes.

            LogManager.Default.LogLevel = LogLevel.None;

            // Use the version of Powershell Core installed with the application,
            // if present.

            PowerShell.PwshPath = KubeHelper.PwshPath;

            // Process the command line.

            try
            {
                ICommand command;

                CommandLine     = new CommandLine(args);
                LeftCommandLine = CommandLine.Split("--").Left;

                if (CommandLine.Arguments.Length == 0)
                {
                    Console.WriteLine(usage);
                    Program.Exit(0);
                }

                var commands = new List<ICommand>()
                {
                    new FileCommand(),
                    new FileCreateCommand(),
                    new FileDecryptCommand(),
                    new FileEditCommand(),
                    new FileEncryptCommand(),
                    new FilePasswordCommand(),
                    new VersionCommand()
                };

                // Short-circuit the help command.

                if (CommandLine.Arguments[0] == "help")
                {
                    if (CommandLine.Arguments.Length == 1)
                    {
                        Console.WriteLine(usage);
                        Program.Exit(0);
                    }

                    CommandLine = CommandLine.Shift(1);

                    command = GetCommand(CommandLine, commands);

                    if (command == null)
                    {
                        Console.Error.WriteLine($"*** ERROR: Unexpected [{CommandLine.Arguments[0]}] command.");
                        Program.Exit(1);
                    }

                    command.Help();
                    Program.Exit(0);
                }

                // Lookup the command.

                command = GetCommand(CommandLine, commands);

                if (command == null)
                {
                    Console.Error.WriteLine($"*** ERROR: Unexpected [{CommandLine.Arguments[0]}] command.");
                    Program.Exit(1);
                }

                // Run the command.

                if (command.SplitItem != null)
                {
                    // We don't shift the command line for pass-thru commands 
                    // because we don't want to change the order of any options.

                    command.Run(CommandLine);
                }
                else
                {
                    command.Run(CommandLine.Shift(command.Words.Length));
                }

                Program.Exit(0);
            }
            catch (ProgramExitException e)
            {
                if (ProgramRunner.Current != null)
                {
                    return e.ExitCode;
                }
                else
                {
                    Environment.Exit(e.ExitCode);
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"*** ERROR: {NeonHelper.ExceptionError(e)}");
                Console.Error.WriteLine(string.Empty);

                if (ProgramRunner.Current != null)
                {
                    return 1;
                }
                else
                {
                    Environment.Exit(1);
                }
            }

            if (ProgramRunner.Current != null)
            {
                return 0;
            }
            else
            {
                Environment.Exit(0);
                return 0;
            }
        }

        /// <summary>
        /// Returns the orignal program <see cref="CommandLine"/>.
        /// </summary>
        public static CommandLine CommandLine { get; private set; }

        /// <summary>
        /// Returns the part of the command line to the left of the [--] splitter
        /// or the entire command line if there is no splitter.
        /// </summary>
        public static CommandLine LeftCommandLine { get; private set; }
        
        /// <summary>
        /// Returns <c>true</c> if the program was built from the production <b>PROD</b> 
        /// source code branch.
        /// </summary>
        public static bool IsRelease => ThisAssembly.Git.Branch.StartsWith("release-", StringComparison.InvariantCultureIgnoreCase);

        /// <summary>
        /// Returns the program version as the Git branch and commit and an optional
        /// indication of whether the program was build from a dirty branch.
        /// </summary>
        public static string GitVersion
        {
            get
            {
                var version = $"{ThisAssembly.Git.Branch}-{ThisAssembly.Git.Commit}";

#pragma warning disable 162 // Unreachable code

                //if (ThisAssembly.Git.IsDirty)
                //{
                //    version += "-DIRTY";
                //}

#pragma warning restore 162 // Unreachable code

                return version;
            }
        }

        /// <summary>
        /// Attempts to match the command line to the <see cref="ICommand"/> to be used
        /// to implement the command.
        /// </summary>
        /// <param name="commandLine">The command line.</param>
        /// <param name="commands">The commands.</param>
        /// <returns>The command instance or <c>null</c>.</returns>
        private static ICommand GetCommand(CommandLine commandLine, List<ICommand> commands)
        {
            // Sort the commands in decending order by number of words in the
            // command (we want to match the longest sequence).

            foreach (var command in commands.OrderByDescending(c => c.Words.Length))
            {
                if (command.Words.Length > commandLine.Arguments.Length)
                {
                    // Not enough arguments to match the command.

                    continue;
                }

                var matches = true;

                for (int i = 0; i < command.Words.Length; i++)
                {
                    if (!string.Equals(command.Words[i], commandLine.Arguments[i]))
                    {
                        matches = false;
                        break;
                    }
                }

                if (!matches && command.AltWords != null)
                {
                    matches = true;

                    for (int i = 0; i < command.AltWords.Length; i++)
                    {
                        if (!string.Equals(command.AltWords[i], commandLine.Arguments[i]))
                        {
                            matches = false;
                            break;
                        }
                    }
                }

                if (matches)
                {
                    return command;
                }
            }

            // No match.

            return null;
        }

        /// <summary>
        /// Creates a <see cref="SshProxy{TMetadata}"/> for the specified host and server name,
        /// configuring logging and the credentials as specified by the global command
        /// line options.
        /// </summary>
        /// <param name="name">The node name.</param>
        /// <param name="publicAddress">The node's public IP address or FQDN.</param>
        /// <param name="privateAddress">The node's private IP address.</param>
        /// <param name="appendToLog">
        /// Pass <c>true</c> to append to an existing log file (or create one if necessary)
        /// or <c>false</c> to replace any existing log file with a new one.
        /// </param>
        /// <typeparam name="TMetadata">Defines the metadata type the command wishes to associate with the server.</typeparam>
        /// <returns>The <see cref="SshProxy{TMetadata}"/>.</returns>
        public static SshProxy<TMetadata> CreateNodeProxy<TMetadata>(string name, string publicAddress, IPAddress privateAddress, bool appendToLog)
            where TMetadata : class
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            var sshCredentials = KubeHelper.CurrentContext.Extension.SshCredentials; ;

            return new SshProxy<TMetadata>(name, publicAddress, privateAddress, sshCredentials);
        }

        /// <summary>
        /// Returns a <see cref="ClusterProxy"/> for the current Kubernetes context.
        /// </summary>
        /// <returns>The <see cref="ClusterProxy"/>.</returns>
        /// <remarks>
        /// <note>
        /// This method will terminate the program with an error message when not logged
        /// into a neonKUBE cluster.
        /// </note>
        /// </remarks>
        public static ClusterProxy GetCluster()
        {
            if (KubeHelper.CurrentContext == null)
            {
                Console.Error.WriteLine("*** ERROR: You are not logged into a cluster.");
                Program.Exit(1);
            }
            else if (KubeHelper.CurrentContext == null)
            {
                Console.Error.WriteLine("*** ERROR: You are not logged into a neonKUBE cluster.");
                Program.Exit(1);
            }

            return new ClusterProxy(KubeHelper.CurrentContext, Program.CreateNodeProxy<NodeDefinition>);
        }

        /// <summary>
        /// Executes the neonKUBE installed version of <b>kubectl</b> passing 
        /// the argument string.
        /// </summary>
        /// <param name="args">The argumuments.</param>
        /// <returns>The <see cref="ExecuteResponse"/>.</returns>
        public static ExecuteResponse Kubectl(string args)
        {
            // $todo(jeff.lill):
            //
            // For now, we're going to assume that the correct version 
            // of KUBECTL is on the PATH.

            return NeonHelper.ExecuteCapture("kubectl", args);
        }

        /// <summary>
        /// Executes the neonKUBE installed version of <b>kubectl</b> passing 
        /// individual arguments..
        /// </summary>
        /// <param name="args">The argumuments.</param>
        /// <returns>The <see cref="ExecuteResponse"/>.</returns>
        public static ExecuteResponse Kubectl(params object[] args)
        {
            return Kubectl(NeonHelper.NormalizeExecArgs(args));
        }

        /// <summary>
        /// Searches the directory holding a file as well as any ancestor directories
        /// for the first <b>.password-name</b> file specifying a default password name.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <returns>The default password name if one was found or <c>null</c>.</returns>
        public static string GetDefaultPasswordName(string filePath)
        {
            var folderPath = Path.GetDirectoryName(Path.GetFullPath(filePath));

            try
            {
                while (true)
                {
                    var passwordNamePath = Path.Combine(folderPath, ".password-name");

                    if (File.Exists(passwordNamePath))
                    {
                        var passwordName = File.ReadLines(passwordNamePath).First().Trim();

                        if (passwordName == string.Empty)
                        {
                            // An empty [.password-name] file will block further searching.

                            return null;
                        }

                        return passwordName;
                    }

                    if (Path.GetPathRoot(folderPath) == folderPath)
                    {
                        // We're at the file system root.

                        return null;
                    }

                    // Advance to the parent folder.

                    folderPath = Path.GetFullPath(Path.Combine(folderPath, ".."));
                }
            }
            catch (UnauthorizedAccessException)
            {
                // We will see this if the current user doesn't have permissions to
                // walk the file directories all the way up to the root of the
                // file system.  We'll just return NULL in this case.

                return null;
            }
        }

        /// <summary>
        /// Returns a password based on its name.
        /// </summary>
        /// <param name="passwordName">The password name.</param>
        /// <returns>The password or <c>null</c> if the named password doesn't exist.</returns>
        public static string LookupPassword(string passwordName)
        {
            var passwordPath = Path.Combine(KubeHelper.PasswordsFolder, passwordName);

            if (File.Exists(passwordPath))
            {
                return File.ReadLines(passwordPath).First().Trim();
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Exits the program returning the specified process exit code.
        /// </summary>
        /// <param name="exitCode">The exit code.</param>
        public static void Exit(int exitCode)
        {
            throw new ProgramExitException(exitCode);
        }
    }
}
