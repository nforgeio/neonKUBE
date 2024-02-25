// -----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: NEONFORGE Team
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
//
// The contents of this repository are for private use by NEONFORGE, LLC. and may not be
// divulged or used for any purpose by other organizations or individuals without a
// formal written and signed agreement with NEONFORGE, LLC.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Neon;
using Neon.Common;

using Neon.Kube.Helm;

using YamlDotNet.Serialization;

namespace HelmMunger
{
    /// <summary>
    /// Hosts the program entry point.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Returns the program name.
        /// </summary>
        public const string Name = "helm-munger";

        /// <summary>
        /// Returns the program version.
        /// </summary>
        public const string Version = "1.0.0";

        /// <summary>
        /// Returns the program command line.
        /// </summary>
        public static CommandLine CommandLine { get; private set; }

        /// <summary>
        /// Implements the program entry point.
        /// </summary>
        /// <param name="args">Specifies the command line arguments.</param>
        public static async Task<int> Main(string[] args)
        {
            var usage = $@"
{Program.Name} v{Program.Version}
{Build.Copyright}

Used by NEONKUBE Helm chart upgrade scripts to munge cluster Helm charts.

USAGE:

    # Removes the named dependency from the [Chart.yaml] file in the specified
    # chart folder and then removes the [charts/NAME] subchart folder.

    helm-munger dependency remove CHART-FOLDER NAME

    # Removes all [repository] properties from any dependencies within the
    # root [Chart.yaml] file within the specified chart folder as well as
    # recusively for any subcharts.

    helm-munger dependency remove-repositories CHART-FOLDER
";
            try
            {
                ICommand command;

                CommandLine = new CommandLine(args);

                if (CommandLine.HasHelpOption || CommandLine.Items.Length == 0)
                {
                    // Output our standard usage help.

                    Console.WriteLine(usage);
                    Program.Exit(CommandLine.HasHelpOption ? 0 : -1);
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

                if (CommandLine.Arguments.ElementAtOrDefault(0) == "help")
                {
                    CommandLine = CommandLine.Shift(1);
                    command     = GetCommand(CommandLine, commands);

                    if (command != null)
                    {
                        command.Help();
                    }
                    else
                    {
                        // Output our standard usage help.

                        Console.WriteLine(usage);
                    }

                    Program.Exit(0);
                }

                // Lookup the command.

                command = GetCommand(CommandLine, commands);

                if (command == null)
                {
                    Console.Error.WriteLine($"*** ERROR: Unexpected [{CommandLine.Arguments[0]}] command.");
                    Program.Exit(-1);
                }

                // This is one of our commands, so ensure that there are no unexpected
                // command line options when the command enables option checks.

                if (command.CheckOptions)
                {
                    var validOptions = new HashSet<string>();

                    validOptions.Add("--help"); // All commands support "--help"

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

                            Console.Error.WriteLine($"*** ERROR: [{commandWords}] command does not support the [{option.Key}] option.");
                            Program.Exit(1);
                        }
                    }
                }

                // Run the command.

                Console.WriteLine();
                await command.RunAsync(CommandLine.Shift(command.Words.Length));
                Console.WriteLine();
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
        /// Returns a YAML serializer suitable for serializing Helm chart YAML.
        /// </summary>
        /// <returns>The <see cref="ISerializer"/>.</returns>
        public static ISerializer YamlSerializer()
        {
            return new SerializerBuilder()
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitDefaults | DefaultValuesHandling.OmitEmptyCollections)
                .Build();
        }

        /// <summary>
        /// Exits the program returning the specified process exit code.
        /// </summary>
        /// <param name="exitCode">The exit code.</param>
        [DoesNotReturn]
        public static void Exit(int exitCode)
        {
            throw new ProgramExitException(exitCode);
        }
    }
}
