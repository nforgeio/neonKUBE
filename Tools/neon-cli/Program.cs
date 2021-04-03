//-----------------------------------------------------------------------------
// FILE:	    Program.cs
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

using Microsoft.Extensions.DependencyInjection;

using Neon;
using Neon.Common;
using Neon.Deployment;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Kube.Xunit;
using Neon.IO;
using Neon.SSH;
using Neon.Windows;

namespace NeonCli
{
    /// <summary>
    /// This tool is used to configure and manage the nodes of a neonKUBE cluster.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// The program version.
        /// </summary>
        public const string Version =
#if ENTERPRISE
            Neon.Cloud.Build.NeonDesktopVersion;
#else
            Neon.Build.NeonKubeVersion;
#endif

        /// <summary>
        /// Returns <c>true</c> if this is the enterprise <b>neon-cli</b> build.
        /// </summary>
        /// <remarks>
        /// We use this to help with managing the source code duplicated for this in the
        /// neonKUBE and neonCLOUD (enterprise) GitHub repositories.
        /// </remarks>
        public const bool IsEnterprise =
#if ENTERPRISE
            true;
#else
            false;
#endif

        /// <summary>
        /// Returns the program name for printing help.  This will be <b>"neon"</b> for the community
        /// version and <b>"neon enterprise"</b> for the enterprise version.
        /// </summary>
        public const string Name =
#if ENTERPRISE
            "neon enterprise";
#else
            "neon";
#endif

        /// <summary>
        /// Program entry point.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <returns>The exit code.</returns>
        public static async Task<int> Main(params string[] args)
        {
            string usage = $@"
{Program.Name} [v{Program.Version}]
{Build.Copyright}

USAGE:

    neon [OPTIONS] COMMAND [ARG...]

COMMAND SUMMARY:

    neon help               COMMAND
    neon cluster prepare    [CLUSTER-DEF]
    neon cluster setup      [CLUSTER-DEF]
    neon generate iso       SOURCE-FOLDER ISO-PATH
    neon login              COMMAND
    neon logout
    neon password           COMMAND
    neon prepare            COMMAND
    neon run                -- COMMAND
    neon vault              COMMAND
    neon version            [-n] [--git] [--minimum=VERSION]

ARGUMENTS:

    CLUSTER-DEF         - Path to a cluster definition file.  This is
                          optional for some commands when logged in
    COMMAND             - Subcommand and arguments.
    NODE                - A node name.

OPTIONS:

    --help                              - Display help

    --insecure                          - Use insecure temporary folder 
                                          (see remarks)
 
    --log-folder=LOG-FOLDER             - Optional log folder path

    -m=COUNT, --max-parallel=COUNT      - Maximum number of nodes to be 
                                          configured in parallel [default=6]

    -q, --quiet                         - Disables operation progress

REMARKS:

By default, any temporary files generated will be written to your 
[$HOME/.neonkube/temp] folder which is encrypted at rest for
Windows 10 PRO and Windows Server workstations.  This encryption
may cause some problems (e.g. a [neon run ...] command that 
needs to mount a decrypted file to a local Docker container.

You can disable the use of this encrypted folder by specifying
[--insecure] for any command.
";
            // Disable any logging that might be performed by library classes.

            LogManager.Default.LogLevel = LogLevel.None;

            // Use the version of Powershell Core installed with the application,
            // if present.

            PowerShell.PwshPath = KubeHelper.PwshPath;

            // Ensure that all of the non-enterprise cluster hosting manager 
            // implementations are loaded.

            new HostingManagerFactory(() => HostingLoader.Initialize());

#if ENTERPRISE
            // Configure the enterprise service depedencies.

            NeonHelper.ServiceContainer.AddSingleton<IEnterpriseHostingLoader>(new EnterpriseHostingLoader());
            NeonHelper.ServiceContainer.AddSingleton<IEnterpriseHelper>(new EnterpriseHelper());
#endif
            // Register a [ProfileClient] so commands will be able to pick
            // up secrets and profile information from [neon-assistant].

            NeonHelper.ServiceContainer.AddSingleton<IProfileClient>(new ProfileClient());

            // Process the command line.

            try
            {
                ICommand command;

                CommandLine     = new CommandLine(args);
                LeftCommandLine = CommandLine.Split("--").Left;

                foreach (var cmdLine in new CommandLine[] { CommandLine, LeftCommandLine })
                {
                    cmdLine.DefineOption("-os").Default = "Ubuntu-20.04";
                    cmdLine.DefineOption("-q", "--quiet");
                    cmdLine.DefineOption("--machine-password");
                    cmdLine.DefineOption("-m", "--max-parallel").Default = "6";
                    cmdLine.DefineOption("-w", "--wait").Default = "60";
                    cmdLine.DefineOption("--log-folder").Default = string.Empty;
                }

                var validOptions = new HashSet<string>();

                validOptions.Add("--debug");
                validOptions.Add("--help");
                validOptions.Add("--insecure");
                validOptions.Add("--log-folder");
                validOptions.Add("--machine-password");
                validOptions.Add("-m");
                validOptions.Add("--max-parallel");
                validOptions.Add("-q");
                validOptions.Add("--quiet");
                validOptions.Add("-w");
                validOptions.Add("--wait");
                validOptions.Add("-b");
                validOptions.Add("--branch");

                if (CommandLine.Arguments.Length == 0)
                {
                    Console.WriteLine(usage);
                    Program.Exit(0);
                }

                if (!CommandLine.HasOption("--insecure"))
                {
                    // Ensure that temporary files are written to the user's temporary folder because
                    // there's a decent chance that this folder will be encrypted at rest.

                    if (KubeTestManager.Current == null)
                    {
                        TempFile.Root   = KubeHelper.TempFolder;
                        TempFolder.Root = KubeHelper.TempFolder;
                    }
                }

                // Scan for enabled commands in the current assembly.

                var commands = new List<ICommand>();

                foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
                {
                    if (!type.Implements<ICommand>())
                    {
                        continue;
                    }

                    var commandAttribute = type.GetCustomAttribute<CommandAttribute>();

                    if (commandAttribute == null || commandAttribute.Disabled)
                    {
                        continue;
                    }

                    commands.Add((ICommand)Activator.CreateInstance(type));
                }

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

                LogPath = LeftCommandLine.GetOption("--log-folder");
                Quiet   = LeftCommandLine.GetFlag("--quiet");

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

                // Handle the other options.

                var maxParallelOption = LeftCommandLine.GetOption("--max-parallel");
                int maxParallel;

                if (!int.TryParse(maxParallelOption, out maxParallel) || maxParallel < 1)
                {
                    Console.Error.WriteLine($"*** ERROR: [--max-parallel={maxParallelOption}] option is not valid.");
                    Program.Exit(1);
                }

                Program.MaxParallel = maxParallel;

                Debug = LeftCommandLine.HasOption("--debug");

                // Ensure that there are no unexpected command line options.

                if (command.CheckOptions)
                {
                    foreach (var optionName in command.ExtendedOptions)
                    {
                        validOptions.Add(optionName);
                    }

                    foreach (var option in LeftCommandLine.Options)
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

                if (command.SplitItem != null)
                {
                    // We don't shift the command line for pass-thru commands 
                    // because we don't want to change the order of any options.

                    await command.RunAsync(CommandLine);
                }
                else
                {
                    await command.RunAsync(CommandLine.Shift(command.Words.Length));
                }
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
        /// Path to the WinSCP program executable.
        /// </summary>
        public static string WinScpPath
        {
            get { return Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles(x86)"), @"WinSCP\WinSCP.exe"); }
        }

        /// <summary>
        /// Path to the PuTTY program executable.
        /// </summary>
        public static string PuttyPath
        {
            get
            {
                // Look for a x64 or x86 version of PuTTY at their default install locations.

                var path = Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles"), @"PuTTY\putty.exe");

                if (File.Exists(path))
                {
                    return path;
                }

                return Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles(x86)"), @"PuTTY\putty.exe");
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
        /// Returns the part of the command line to the left of the [--] splitter
        /// or the entire command line if there is no splitter.
        /// </summary>
        public static CommandLine LeftCommandLine { get; private set; }

        /// <summary>
        /// Returns <c>true</c> if the program was built from the production <b>PROD</b> 
        /// source code branch.
        /// </summary>
#pragma warning disable 0436
        public static bool IsRelease => ThisAssembly.Git.Branch.StartsWith("release-", StringComparison.InvariantCultureIgnoreCase);
#pragma warning restore 0436

        /// <summary>
        /// Returns the log folder path or a <c>null</c> or empty string 
        /// to disable logging.
        /// </summary>
        public static string LogPath { get; set; }

        /// <summary>
        /// The maximum number of nodes to be configured in parallel.
        /// </summary>
        public static int MaxParallel { get; set; }

        /// <summary>
        /// Indicates whether operation progress output is to be suppressed.
        /// </summary>
        public static bool Quiet { get; set; }

        /// <summary>
        /// Runs the command in DEBUG mode.
        /// </summary>
        public static bool Debug { get; set; }

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
    }
}
