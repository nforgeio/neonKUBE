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

using Neon.Common;
using Neon.Deployment;

namespace GHTool
{
    /// <summary>
    /// Hosts the program entry point.
    /// </summary>
    public static class Program
    {
        private static string cachedGithubToken = null;

        /// <summary>
        /// The program entry point.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        public static async Task<int> Main(params string[] args)
        {
            string usage = $@"
{Program.Name} [v{Program.Version}]
{Neon.Build.Copyright}

USAGE:

    gh-tool COMMAND [ARG...]

COMMANDS:

    gh-tool action run delete REPO [WORKFLOW-NAME] [--max-age-days=AGE]
";
            try
            {
                ICommand    command;

                CommandLine = new CommandLine(args);

                var validOptions = new HashSet<string>();

                validOptions.Add("--help");

                if (CommandLine.Arguments.Length == 0)
                {
                    Console.WriteLine(usage);
                    Program.Exit(0);
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
                    command     = GetCommand(CommandLine, commands);

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
        /// Exits the program returning the specified process exit code.
        /// </summary>
        /// <param name="exitCode">The exit code.</param>
        public static void Exit(int exitCode)
        {
            throw new ProgramExitException(exitCode);
        }

        /// <summary>
        /// Returns the program name.
        /// </summary>
        public static string Name => "gh-tools";

        /// <summary>
        /// Returns the program version.
        /// </summary>
        public static string Version => "0.1.0";

        /// <summary>
        /// Returns the orignal program <see cref="CommandLine"/>.
        /// </summary>
        public static CommandLine CommandLine { get; private set; }

        /// <summary>
        /// Returns the current user's GITHUB_PAT token.
        /// </summary>
        public static string GitHubPAT
        {
            get
            {
                if (!string.IsNullOrEmpty(cachedGithubToken))
                {
                    return cachedGithubToken;
                }

                var profileClient = new ProfileClient();

                return cachedGithubToken = profileClient.GetSecretPassword("GITHUB_PAT");
            }
        }
    }
}