//-----------------------------------------------------------------------------
// FILE:        Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Grpc.Net.Client;

using k8s;
using k8s.Models;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Neon;
using Neon.Common;
using Neon.Deployment;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.BuildInfo;
using Neon.Kube.GrpcProto;
using Neon.Kube.GrpcProto.Desktop;
using Neon.Kube.Hosting;
using Neon.SSH;
using Neon.Windows;

using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;

using ProtoBuf.Grpc.Client;

namespace NeonCli
{
    /// <summary>
    /// This tool is used to configure and manage the nodes of a NeonKUBE cluster.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// The program version.
        /// </summary>
        public const string Version =
#if PREMIUM
            Neon.Cloud.Build.NeonDesktopVersion;
#else
            Neon.Kube.KubeVersion.Kubernetes;
#endif

        /// <summary>
        /// Returns <c>true</c> if this is the premium <b>NeonCLIENT</b> build.
        /// </summary>
        /// <remarks>
        /// We use this to help with managing the source code duplicated for this in the
        /// NeonKUBE and NeonCLOUD (premium) GitHub repositories.
        /// </remarks>
        public const bool IsPremium =
#if PREMIUM
            true;
#else
            false;
#endif

        /// <summary>
        /// Returns the program name for printing help.  This will be <b>"neon"</b> for the community
        /// version and <b>"neon (premium)"</b> for the premium version.
        /// </summary>
        public const string Name =
#if PREMIUM
            "neon (premium)";
#else
            "neon";
#endif

        /// <summary>
        /// Returns the application name used for telemetry.
        /// </summary>
        public const string TelemetryName = "neon-cli";

        private static ILoggerFactory   loggerFactory;
        private static TracerProvider   tracerProvider;

        /// <summary>
        /// Returns the folder path where the program binary is located.
        /// </summary>
        private static readonly string BinaryFolder = NeonHelper.GetApplicationFolder();

        /// <summary>
        /// Returns the path to the standard tool folder when <b>NeonCLIENT</b> has been fully installed.
        /// </summary>
        private static readonly string InstalledToolFolder = Path.Combine(BinaryFolder, "tools");

        /// <summary>
        /// Returns the orignal program <see cref="CommandLine"/>.
        /// </summary>
        public static CommandLine CommandLine { get; private set; }

        /// <summary>
        /// Returns the fully qualified path to the <b>helm</b> binary.
        /// </summary>
        public static string HelmPath { get; private set; }

        /// <summary>
        /// <para>
        /// The NeonDESKTOP Service gRPC channel.
        /// </para>
        /// <note>
        /// This will be <c>null</c> when the <b>neon-desktop-service</b> is not running.
        /// </note>
        /// </summary>
        private static GrpcChannel DesktopServiceChannel { get; set; }

        /// <summary>
        /// <para>
        /// The NeonDESKTOP Service gRPC client.
        /// </para>
        /// <note>
        /// This will be <c>null</c> when the <b>neon-desktop-service</b> isn't running.
        /// </note>
        /// </summary>
        public static IGrpcDesktopService DesktopService { get; private set; }

        /// <summary>
        /// Returns the <see cref="ILogger"/> the application will use for logging.
        /// </summary>
        public static ILogger Logger { get; private set; }

        /// <summary>
        /// Returns the <see cref="System.Diagnostics.ActivitySource"/> used to record traces.
        /// Note that <see cref="TelemetryHub.ActivitySource"/> is set to the same value.
        /// </summary>
        public static ActivitySource ActivitySource { get; private set; }

        /// <summary>
        /// Program entry point.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <returns>The exit code.</returns>
        public static async Task<int> Main(string[] args)
        {
            var usage = $@"
{Program.Name} [v{Program.Version}]
{Build.Copyright}

USAGE:

    neon cluster check
    neon cluster dashboard
    neon cluster health
    neon cluster info
    neon cluster islocked
    neon cluster lock
    neon cluster prepare    CLUSTER-DEF
    neon cluster pause      [OPTIONS]
    neon cluster delete     [OPTIONS]
    neon cluster reset      [OPTIONS]
    neon cluster setup      [OPTIONS] sysadmin@CLUSTER-NAME
    neon cluster start
    neon cluster stop       [OPTIONS]
    neon cluster unlock
    neon cluster validate   [CLUSTER-DEF]

    neon login              COMMAND
    neon logout

    neon toolpath           TOOLNAME

    neon version            [OPTIONS]

ARGUMENTS:

    CLUSTER-DEF         - Path to a cluster definition file.  This is
                          optional for some commands when logged in

    TOOLNAME            - Identifies a related tool, one of: helm

NOTE: Command line arguments and options may include references to 
      profile values, secrets and environment variables, like:

      ${{profile:NAME}}                   - profile value
      ${{secret:NAME}}                    - ""password"" property value of NAME secret
      ${{secret:NAME:SOURCE}}             - ""password""  property value of NAME secret at SOURCE
      ${{secret:NAME[PROPERTY}}           - PROPERTY value from NAME secret
      ${{secret:NAME[PROPERTY]:SOURCE}}   - PROPERTY value from NAME secret at SOURCE
      ${{env:NAME}}                       - environment variable

      For Linux, you'll need to surround these references with single quotes
      to prevent Bash from interpreting them as Bash variable references.

===============================================================================
";
            // Configure the neon-desktop-server gRPC client.

            DesktopServiceChannel = NeonGrpcServices.CreateDesktopServiceChannel();

            if (DesktopServiceChannel != null)
            {
                DesktopService = DesktopServiceChannel.CreateGrpcService<IGrpcDesktopService>();
            }

            // Configure the telemetry log and trace pipelines.

            if (!KubeEnv.IsTelemetryDisabled || DesktopService == null)
            {
                Logger = new NullLogger();
            }
            else
            {
                //-------------------------------------------------------------
                // Logging pipeline:
                //
                // We're not configuring the standard [AddOtlpExporter] here.  Instead, we're
                // using a custom exporter that fowards log batches to the [neon-desktop-service]
                // via gRPC which will relay them to the headend.

                loggerFactory = LoggerFactory.Create(
                    builder =>
                    {
                        builder.AddOpenTelemetry(
                            options =>
                            {
                                options.ParseStateValues        = true;
                                options.IncludeFormattedMessage = true;
                                options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName: TelemetryName, serviceVersion: Program.Version.ToString()));
                                options.AddLogAsTraceProcessor(
                                    options =>
                                    {
                                        options.LogLevel = LogLevel.Information;
                                    });

                                options.AddProcessor(new SimpleLogRecordExportProcessor(new GrpcLogExporter(DesktopService)));
                            });
                    });

                var logAttributes = new LogAttributes();

                Logger = loggerFactory.CreateLogger(typeof(Program).Name)
                    .AddAttributes(
                        attributes =>
                        {
                            foreach (var tag in KubeHelper.TelemetryTags)
                            {
                                attributes.Add(tag.Key, tag.Value);
                                logAttributes.Add(tag.Key, tag.Value);
                            }
                        });

                TelemetryHub.LoggerFactory = loggerFactory;
                TelemetryHub.LogAttributes = logAttributes;

                //-------------------------------------------------------------
                // Tracing pipeline:

                tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .AddSource(TelemetryName)
                    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName: TelemetryName, serviceVersion: Program.Version.ToString()))
                    .AddProcessor(new SimpleActivityExportProcessor(new GrpcTraceExporter(DesktopService)))
                    .Build();

                ActivitySource              = new ActivitySource(name: TelemetryName, version: Program.Version.ToString());
                TelemetryHub.ActivitySource = ActivitySource;
            }

            // We're going to log when the application starts and exits.

            Logger.LogInformationEx(() => $"starting: {Name}");

            // Register a [ProfileClient] so commands will be able to pick
            // up secrets and profile information from [neon-assistant].

            NeonHelper.ServiceContainer.AddSingleton<IProfileClient>(new MaintainerProfile());

            // Fetch the paths to the [helm] binary.  Note that this
            // will be downloaded them when it's not already present.

            HelmPath = GetHelmPath();

            // Process the command line.

            Activity traceActivity = null;

            // Initialize k8s json

            KubeHelper.InitializeJson();

            try
            {
                ICommand command;

                // $hack(jefflill):
                //
                // We hardcoding our own profile client for the time being.  Eventually,
                // we'll need to support custom or retail profile clients.
                //
                // This is required by: CommandLine.Preprocess()

                NeonHelper.ServiceContainer.AddSingleton<IProfileClient>(new MaintainerProfile());

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

                // Start a trace for the command.

                traceActivity = ActivitySource?.CreateActivity(
                    "command",
                    ActivityKind.Internal,
                    parentId: null, 
                    tags:     new KeyValuePair<string, object>[] { new KeyValuePair<string, object>("cmd", CommandLine.ToString()) });

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

                // Ensure that all of the non-premium cluster hosting manager 
                // implementations are loaded, when required.

                if (command.NeedsHostingManager)
                {
                    _ = new HostingManagerFactory(() => HostingLoader.Initialize());
                }

                // Run the command.

                await command.RunAsync(CommandLine.Shift(command.Words.Length));
            }
            catch (ProgramExitException e)
            {
                Logger.LogErrorEx(exception: e);
                Logger.LogInformationEx(() => $"done: {Name} with [exitcode={e.ExitCode}]");
                FlushTelemetry(traceActivity);
                return e.ExitCode;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"*** ERROR: {NeonHelper.ExceptionError(e)}");
                Console.Error.WriteLine(e.StackTrace);
                Console.Error.WriteLine(string.Empty);
                Logger.LogErrorEx(exception: e);
                Logger.LogInformationEx(() => $"done: {Name} with [exitcode=1]");
                FlushTelemetry(traceActivity);
                return 1;
            }

            Logger.LogInformationEx(() => $"done: {Name} with [exitcode=0]");
            FlushTelemetry(traceActivity);
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
        /// Looks for a <b>--output=FORMAT</b> or <b>-o=FORMAT</b> option on the command line
        /// passed and returns the format or <n>null</n> when neither option is present.
        /// </summary>
        /// <param name="commandLine"></param>
        /// <returns>The format specified or <c>null</c>.</returns>
        public static OutputFormat? GetOutputFormat(CommandLine commandLine)
        {
            Covenant.Requires<ArgumentNullException>(commandLine != null, nameof(commandLine));

            var formatString = commandLine.GetOption("--output");

            if (formatString == null)
            {
                formatString = commandLine.GetOption("-o");
            }

            if (string.IsNullOrEmpty(formatString))
            {
                return null;
            }

            switch (formatString.ToLowerInvariant())
            {
                case "json":

                    return OutputFormat.Json;

                case "yaml":

                    return OutputFormat.Yaml;

                default:

                    Console.Error.WriteLine($"*** ERROR: [{formatString}] is not a supported output format.");
                    Program.Exit(1);
                    break;
            }

            // We should never reach this.

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
        [DoesNotReturn]
        public static void Exit(int exitCode)
        {
            throw new ProgramExitException(exitCode);
        }

        /// <summary>
        /// Disposes the current trace span if any and flushes any pending logs
        /// and traces to the <b>neon-desktop-service</b> to they can be uploaded
        /// to the headend.  This is called just before the tool exits.
        /// </summary>
        /// <param name="traceActivity">Pass as the current trace activity, if any.</param>
        private static void FlushTelemetry(Activity traceActivity)
        {
            traceActivity?.Dispose();
            tracerProvider?.Dispose();
            loggerFactory?.Dispose();
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

        /// <summary>
        /// Returns the path to <b>helm</b> tool binary to be used by <b>NeonCLIENT</b>.
        /// </summary>
        /// <returns>The fully qualified tool path.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the tool cannot be located.</exception>
        /// <remarks>
        /// <para>
        /// Installed versions of <b>NeonCLIENT</b> expect the <b>helm</b> toolto be located
        /// in the <b>tools</b> subfolder where <b>NeonCLIENT</b> itself is installed, like:
        /// </para>
        /// <code>
        /// C:\Program Files\NEONFORGE\NEONDESKTOP\
        ///     neon.exe
        ///     neon-cli.exe
        ///     tools\
        ///         helm.exe
        /// </code>
        /// <para>
        /// If this folder exists and the tool binary exists within that folder, then we'll simply
        /// return the path to the binary.
        /// </para>
        /// <para>
        /// If the tool folder or binary does not exist, then the user is probably a developer running
        /// an uninstalled version of the tool, perhaps in the debugger.  In this case, we're going to
        /// cache these binaries in the special tools folder: <see cref="KubeHelper.ToolsFolder"/>.
        /// </para>
        /// <para>
        /// If the tool folder and/or ther equested tool binary doesn't exist or the tool version doesn't
        /// match what's specified in <see cref="KubeVersion"/>, then this method will attempt to download
        /// the binary to <see cref="KubeHelper.ToolsFolder"/>, indicating that this is happening on the
        /// console.
        /// </para>
        /// </remarks>
        public static string GetHelmPath()
        {
            return KubeHelper.GetHelmPath(InstalledToolFolder, userToolsFolder: true);
        }
    }
}
