//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using Neon.IO;
using Neon.Kube;
using Neon.Windows;
using Neon.SSH;

namespace NeonImage
{
    /// <summary>
    /// This tool is used to build the base Neon VM images.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// The program version.
        /// </summary>
        public const string Version = "0.0.1";
    
        /// <summary>
        /// Program entry point.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <returns>The exit code.</returns>
        public async static Task<int> Main(params string[] args)
        {
            string usage = $@"
neonKUBE VM image generation: neon-image [v{Program.Version}]
{Build.Copyright}

COMMAND SUMMARY:

    neon-image help         COMMAND
    neon-image pull
    neon-image version      [-n] [--git] [--minimum=VERSION]

ARGUMENTS:

    COMMAND             - Subcommand and arguments.

OPTIONS:

    --help                              - Display help

    --log-folder=LOG-FOLDER             - Optional log folder path

    --machine-password=PASSWORD         - Overrides default initial machine
                                          password for the [sysadmin] account.
                                          This defaults to: sysadmin0000
";
            // Disable any logging that might be performed by library classes.

            LogManager.Default.LogLevel = LogLevel.None;

            // Process the command line.

            try
            {
                ICommand command;

                CommandLine = new CommandLine(args);

                CommandLine.DefineOption("--log-folder").Default = string.Empty;
                CommandLine.DefineOption("--machine-password");

                var validOptions = new HashSet<string>();

                validOptions.Add("--help");
                validOptions.Add("--log-folder");
                validOptions.Add("--machine-password");

                if (CommandLine.Arguments.Length == 0)
                {
                    Console.WriteLine(usage);
                    Program.Exit(0);
                }

                var commands = new List<ICommand>()
                {
                    new AnalyzeCommand(),
                    new PullCommand(),
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

                // Handle the logging options.

                LogPath = CommandLine.GetOption("--log-folder");

                if (!string.IsNullOrEmpty(LogPath))
                {
                    LogPath = Path.GetFullPath(LogPath);

                    Directory.CreateDirectory(LogPath);
                }
                else
                {
                    LogPath = KubeHelper.LogFolder;

                    // We can clear this folder because we know that there shouldn't be
                    // any other files in here.

                    NeonHelper.DeleteFolderContents(LogPath);
                }

                //-------------------------------------------------------------
                // Process the standard command line options.

                // Load the password from the command line options, if present.

                MachinePassword = CommandLine.GetOption("--machine-password", KubeConst.VmTemplatePassword);

                // Ensure that there are no unexpected command line options.

                if (command.CheckOptions)
                {
                    foreach (var optionName in command.ExtendedOptions)
                    {
                        validOptions.Add(optionName);
                    }

                    foreach (var option in CommandLine.Options)
                    {
                        if (!validOptions.Contains(option.Key))
                        {
                            var commandWords = string.Empty;

                            foreach (var word in command.Words)
                            {
                                if (commandWords.Length > 0)
                                {
                                    commandWords += " ";
                                }

                                commandWords += word;
                            }

                            Console.Error.WriteLine($"*** ERROR: [{commandWords}] command does not support [{option.Key}].");
                            Program.Exit(1);
                        }
                    }
                }

                // Run the command.

                if (command.NeedsSshCredentials(CommandLine) && string.IsNullOrEmpty(MachinePassword))
                {
                    Console.WriteLine();
                    Console.WriteLine($"    Enter cluster SSH password for [{KubeConst.SysAdminUsername}]:");
                    Console.WriteLine($"    ------------------------------------------");

                    while (string.IsNullOrEmpty(MachinePassword))
                    {
                        MachinePassword = NeonHelper.ReadConsolePassword("    password: ");
                    }
                }

                await command.RunAsync(CommandLine.Shift(command.Words.Length));
            }
            catch (ProgramExitException e)
            {
                return e.ExitCode;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"*** ERROR: {NeonHelper.ExceptionError(e)}");
                Console.Error.WriteLine(e.StackTrace);
                Console.Error.WriteLine(string.Empty);
                return 1;
            }

            return 0;
        }

        /// <summary>
        /// Message written then a user is not logged into a cluster.
        /// </summary>
        public const string MustLoginMessage = "*** ERROR: You must first log into a cluster.";

        /// <summary>
        /// Returns the Git source code branch.
        /// </summary>
#pragma warning disable 0436
        public static string GitBranch => ThisAssembly.Git.Branch;
#pragma warning restore 0436

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
        /// Returns the program version as the Git branch and commit and an optional
        /// indication of whether the program was build from a dirty branch.
        /// </summary>
        public static string GitVersion
        {
            get
            {
#pragma warning disable 0436
                var version = $"{ThisAssembly.Git.Branch}-{ThisAssembly.Git.Commit}";
#pragma warning restore 0436

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
        /// Exits the program returning the specified process exit code.
        /// </summary>
        /// <param name="exitCode">The exit code.</param>
        public static void Exit(int exitCode)
        {
            // Ensure that all sensitive files and folders are encrypted at rest.  We're 
            // running this after every command just to be super safe.

            KubeHelper.EncryptSensitiveFiles();
            throw new ProgramExitException(exitCode);
        }

        /// <summary>
        /// Returns the orignal program <see cref="CommandLine"/>.
        /// </summary>
        public static CommandLine CommandLine { get; private set; }

        /// <summary>
        /// Returns <c>true</c> if the program was built from the production <b>PROD</b> 
        /// source code branch.
        /// </summary>
#pragma warning disable 0436
        public static bool IsRelease => ThisAssembly.Git.Branch.StartsWith("release-", StringComparison.InvariantCultureIgnoreCase);
#pragma warning restore 0436

        /// <summary>
        /// The password used to secure the cluster nodes before they are setup.  This defaults
        /// to <b>sysadmin0000</b> which is used for the cluster machine templates.
        /// </summary>
        public static string MachinePassword { get; set; } = KubeConst.VmTemplatePassword;

        /// <summary>
        /// Returns the log folder path or a <c>null</c> or empty string 
        /// to disable logging.
        /// </summary>
        public static string LogPath { get; set; }
        
        /// <summary>
        /// Creates a <see cref="NodeSshProxy{TMetadata}"/> for the specified host and server name,
        /// configuring logging and the credentials as specified by the global command
        /// line options.
        /// </summary>
        /// <param name="name">The node name.</param>
        /// <param name="address">The node's private IP address.</param>
        /// <param name="appendToLog">
        /// Pass <c>true</c> to append to an existing log file (or create one if necessary)
        /// or <c>false</c> to replace any existing log file with a new one.
        /// </param>
        /// 
        /// <typeparam name="TMetadata">Defines the metadata type the command wishes to associate with the server.</typeparam>
        /// <returns>The <see cref="NodeSshProxy{TMetadata}"/>.</returns>
        public static NodeSshProxy<TMetadata> CreateNodeProxy<TMetadata>(string name, IPAddress address, bool appendToLog)
            where TMetadata : class
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            var logWriter = (TextWriter)null;

            if (!string.IsNullOrEmpty(LogPath))
            {
                var path = Path.Combine(LogPath, name + ".log");

                logWriter = new StreamWriter(new FileStream(path, appendToLog ? FileMode.Append : FileMode.Create, appendToLog ? FileAccess.Write : FileAccess.ReadWrite));
            }

            SshCredentials sshCredentials;

            if (!string.IsNullOrEmpty(KubeConst.SysAdminUsername) && !string.IsNullOrEmpty(Program.MachinePassword))
            {
                sshCredentials = SshCredentials.FromUserPassword(KubeConst.SysAdminUsername, Program.MachinePassword);
            }
            else if (KubeHelper.CurrentContext != null)
            {
                sshCredentials = KubeHelper.CurrentContext.Extension.SshCredentials;
            }
            else
            {
                Console.Error.WriteLine("*** ERROR: Expected some node credentials.");
                Program.Exit(1);

                return null;
            }

            return new NodeSshProxy<TMetadata>(name, address, sshCredentials, logWriter: logWriter);
        }

        /// <summary>
        /// Presents the user with a yes/no question and waits for a response.
        /// </summary>
        /// <param name="prompt">The question prompt.</param>
        /// <returns><c>true</c> if the answer is yes, <b>false</b> for no.</returns>
        public static bool PromptYesNo(string prompt)
        {
            try
            {
                while (true)
                {
                    Console.WriteLine();
                    Console.Write($"{prompt} [y/n]: ");

                    var key = Console.ReadKey().KeyChar;

                    Console.WriteLine();

                    if (key == 'y' || key == 'Y')
                    {
                        return true;
                    }
                    else if (key == 'n' || key == 'N')
                    {
                        return false;
                    }
                }
            }
            finally
            {
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Executes a command on the local operating system, writing an error and
        /// existing the program if the command fails.
        /// </summary>
        /// <param name="programPath">The program.</param>
        /// <param name="args">The arguments.</param>
        public static void Execute(string programPath, params object[] args)
        {
            var sbArgs = new StringBuilder();

            foreach (var arg in args)
            {
                var argString = arg.ToString();

                if (argString.Contains(" "))
                {
                    argString = "\"" + argString + "\"";
                }

                sbArgs.AppendWithSeparator(argString);
            }

            try
            {
                var result = NeonHelper.ExecuteCapture(programPath, sbArgs.ToString());

                if (result.ExitCode != 0)
                {
                    Console.Error.Write(result.AllText);
                    Program.Exit(result.ExitCode);
                }
            }
            catch (Win32Exception)
            {
                Console.Error.WriteLine($"*** ERROR: Cannot launch [{programPath}].");
                Program.Exit(1);
            }
        }

        /// <summary>
        /// Verify that the current user has administrator privileges, exiting
        /// the application if this is not the case.
        /// </summary>
        /// <param name="message">Optional message.</param>
        public static void VerifyAdminPrivileges(string message = null)
        {
            if (message == null)
            {
                message = "*** ERROR: This command requires elevated administrator privileges.";
            }
            else
            {
                if (!message.StartsWith("*** ERROR: "))
                {
                    message = $"** ERROR: {message}";
                }
            }

            if (!KubeHelper.InToolContainer)
            {
                if (NeonHelper.IsWindows)
                {
                    var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());

                    if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
                    {
                        Console.Error.WriteLine(message);
                        Program.Exit(1);
                    }
                }
                else if (NeonHelper.IsOSX)
                {
                    // $todo(jefflill): Implement this?
                }
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
        /// Pulls all of the required images into the local Docker container image cache.
        /// </summary>
        public static void PullImages()
        {
            foreach (var image in ContainerImages.Required)
            {
                Console.WriteLine($"Pulling: {image}");

                var response = NeonHelper.ExecuteCapture("docker.exe", new object[] { "pull", image });

                response.EnsureSuccess();
            }
        }
    }
}
