//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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
using Neon.Cluster;
using Neon.Common;
using Neon.Diagnostics;

namespace NeonCli
{
    /// <summary>
    /// This tool is used to configure the nodes of a Neon Docker Swarm cluster.
    /// See <b>$/Doc/Ubuntu-16.04 Cluster Deploy.docx</b> for more information.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// The <b>neon-cli</b> version.
        /// </summary>
        public const string Version = "1.2.50";

        /// <summary>
        /// Program entry point.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        public static void Main(string[] args)
        {
            string usage = $@"
Neon Cluster Configuration Tool: neon [v{Program.Version}]
{Build.Copyright}

USAGE:

    neon [OPTIONS] COMMAND [ARG...]

COMMAND SUMMARY:

    neon help               COMMAND

    neon ansible exec       ARGS
    neon ansible galaxy     ARGS
    neon ansible play       ARGS
    neon ansible vault      ARGS
    neon cluster add        LOGIN-PATH
    neon cluster example
    neon cluster get        VALUE-EXPR
    neon cluster prepare    [CLUSTER-DEF]
    neon cluster setup      [CLUSTER-DEF]
    neon cluster verify     [CLUSTER-DEF]
    neon couchbase          CMD...
    neon cert               CMD...
    neon consul             ARGS
    neon create cypher
    neon create password    [--length=#]
    neon create uuid
    neon dashboard          DASHBOARD
    neon docker             ARGS
    neon download           SOURCE TARGET [NODE]
    neon exec               BASH-CMD
    neon file               create|decrypt|edit|encrypt|view PATH PASSWORD-NAME
    neon folder             FOLDER
    neon get                [VALUE-EXPR]
    neon login              [--no-vpn] USER@CLUSTER
    neon login export       USER@CLUSTER
    neon login import       PATH
    neon login list
    neon login ls
    neon login remove       USER@CLUSTER
    neon login rm           USER@CLUSTER
    neon login status
    neon proxy              CMD...
    neon reboot             NODE...
    neon run                -- CMD...
    neon scp                [NODE]
    neon ssh                [NODE]
    neon validate           CLUSTER-DEF
    neon version            [-n] [-git]
    neon upload             SOURCE TARGET [NODE...]
    neon vault              ARGS
    neon zip create         SOURCE ARCHIVE
    neon zip extract        ARCHIVE FOLDER

ARGUMENTS:

    ARGS                - Command pass-thru arguments.
    BASH-CMD            - Bash command.
    CLUSTER             - Names the cluster to be selected for subsequent
                          operations.
    CLUSTER-DEF         - Path to a cluster definition file.  This is
                          optional for some commands when logged in.
    CMD...              - Subcommand and arguments.
    DASHBOARD           - Identifies a cluster dashboard
    FOLDER              - Identifies a neonCLUSTER folder
    LOGIN-PATH          - Path to a cluster login file including the cluster
                          definition and user credentials.
    NODE                - Identifies a cluster node by name.
    VALUE-EXPR          - A cluster value expression.  See the command for
                          more details.
    SERVER1...          - IP addresses or FQDNs of target servers
    SOURCE              - Path to a source file.
    TARGET              - Path to a destination file.
    USER                - Cluster user name.

OPTIONS:

    --no-tool-container                 - See note below.
    --help                              - Display help
    --image-tag=TAG                     - Replaces any [:latest] Docker image
                                          tags when deploying a cluster (usually
                                          for development/testing purposes)
    --log-folder=LOG-FOLDER             - Optional log folder path
    -m=COUNT, --max-parallel=COUNT      - Maximum number of nodes to be 
                                          configured in parallel [default=1]
    --node=NODE                         - Some commands may be directed at
                                          specific node(s)
    --os=ubuntu-16.04                   - Target host OS
    -p=PASSWORD, --password=PASSWORD    - Cluster host node root password
    -q, --quiet                         - Disables operation progress
    -w=SECONDS, --wait=SECONDS          - Seconds to delay for cluster
                                          stablization (defaults to 60s).
    -u=USER, --user=USER                - Cluster host node root username

NOTES:

By default, this tool runs a [neoncluster/neon-cli] image as a Docker
container, passing the command line and any files into the container such
that the command is actually executed there.  In this case, the tool
is just acting as a shim.

For limited circumstances, it may be desirable to have the tool actually
perform the command on the operator's workstation rather than within a
Docker container.  You can accomplish this by using the [--no-tool-container].
Note that the tool requires admin priviledges for direct mode.
";
            // Disable any logging that might be performed by library classes.

            LogManager.Default.LogLevel = LogLevel.None;

            // Configure the encrypted user-specific application data folder and initialize
            // the subfolders.

            ClusterRootFolder  = NeonClusterHelper.GetRootFolder();
            ClusterLoginFolder = NeonClusterHelper.GetLoginFolder();
            ClusterSetupFolder = NeonClusterHelper.GetVmTemplatesFolder();
            CurrentClusterPath = NeonClusterHelper.CurrentPath;

            // We're going to special case the temp folder and locate this within the [/dev/shm] 
            // tmpfs based RAM drive if we're running in the tool container.

            ClusterTempFolder  = NeonClusterHelper.InToolContainer ? "/dev/shm/temp" : Path.Combine(ClusterRootFolder, "temp");

            Directory.CreateDirectory(ClusterLoginFolder);
            Directory.CreateDirectory(ClusterTempFolder);

            // Process the command line.

            try
            {
                ICommand command;

                CommandLine = new CommandLine(args);

                CommandLine.DefineOption("-u", "--user");
                CommandLine.DefineOption("-p", "--password");
                CommandLine.DefineOption("-os").Default = "ubuntu-16.04";
                CommandLine.DefineOption("-q", "--quiet");
                CommandLine.DefineOption("-m", "--max-parallel").Default = "1";
                CommandLine.DefineOption("-w", "--wait").Default = "60";
                CommandLine.DefineOption("--log-folder").Default = string.Empty;

                var validOptions = new HashSet<string>();

                validOptions.Add("-u");
                validOptions.Add("--user");
                validOptions.Add("-p");
                validOptions.Add("--password");
                validOptions.Add("--os");
                validOptions.Add("--log-folder");
                validOptions.Add("-q");
                validOptions.Add("--quiet");
                validOptions.Add("-m");
                validOptions.Add("--max-parallel");
                validOptions.Add("-w");
                validOptions.Add("--wait");
                validOptions.Add("--no-tool-container");
                validOptions.Add("--image-tag");

                if (CommandLine.Arguments.Length == 0)
                {
                    Console.WriteLine(usage);
                    Program.Exit(0);
                }

                var commands = new List<ICommand>()
                {
                    new AnsibleCommand(),
                    new ClusterCommand(),
                    new ClusterExampleCommand(),
                    new ClusterGetCommand(),
                    new ClusterPrepareCommand(),
                    new ClusterSetupCommand(),
                    new ClusterVerifyCommand(),
                    new CouchbaseCommand(),
                    new CertCommand(),
                    new ConsulCommand(),
                    new CreateCommand(),
                    new CreateCypherCommand(),
                    new CreatePasswordCommand(),
                    new CreateUuidCommand(),
                    new DashboardCommand(),
                    new DockerCommand(),
                    new DownloadCommand(),
                    new ExecCommand(),
                    new FileCommand(),
                    new FolderCommand(),
                    new LoginCommand(),
                    new LoginExportCommand(),
                    new LoginImportCommand(),
                    new LoginListCommand(),
                    new LoginRemoveCommand(),
                    new LoginStatusCommand(),
                    new LogoutCommand(),
                    new ProxyCommand(),
                    new RebootCommand(),
                    new RunCommand(),
                    new ScpCommand(),
                    new SshCommand(),
                    new UploadCommand(),
                    new VaultCommand(),
                    new VersionCommand(),
                    new VpnCommand(),
                    new ZipCommand()
                };

                // Determine whether we're running in direct mode or shimming to a Docker container.

                NoToolContainer = NeonClusterHelper.InToolContainer || CommandLine.GetOption("--no-tool-container") != null;

                // Short-circuit the help command.

                if (!NoToolContainer && CommandLine.Arguments[0] == "help")
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
                        Console.Error.WriteLine(usage);
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
                    Console.Error.WriteLine(usage);
                    Program.Exit(1);
                }

                // Handle the logging options.

                LogPath = CommandLine.GetOption("--log-folder");
                Quiet   = CommandLine.GetFlag("--quiet");

                if (!string.IsNullOrEmpty(LogPath))
                {
                    if (NeonClusterHelper.InToolContainer)
                    {
                        // We hardcode logging to [/log] inside [neon-cli] containers.

                        LogPath = "/log";
                    }

                    LogPath = Path.GetFullPath(LogPath);

                    Directory.CreateDirectory(LogPath);
                }

                // Locate the command.

                command = GetCommand(CommandLine, commands);

                if (command == null)
                {
                    Console.Error.WriteLine($"*** ERROR: Unexpected [{CommandLine.Arguments[0]}] command.");
                    Program.Exit(1);
                }

                // When not running in direct mode, we're going to act as a shim
                // and run the command in a Docker container.

                if (!NoToolContainer)
                {
                    int exitCode;

                    using (var shim = new DockerShim(CommandLine))
                    {
                        var secretsRoot = NeonClusterHelper.GetRootFolder(ignoreNeonToolContainerVar: true);

                        ClusterLogin = GetClusterLogin();

                        // Give the command a chance to modify the shimmed command line and also
                        // verify that the command can be run within Docker.

                        var shimInfo = command.Shim(shim);

                        if (shimInfo.EnsureConnection)
                        {
                            if (ClusterLogin == null)
                            {
                                Console.Error.WriteLine(Program.MustLoginMessage);
                                Program.Exit(1);
                            }

                            if (ClusterLogin.ViaVpn)
                            {
                                NeonClusterHelper.VpnOpen(ClusterLogin,
                                    onStatus: message => Console.Error.WriteLine(message),
                                    onError: message => Console.Error.WriteLine($"*** ERROR: {message}"));
                            }
                        }

                        if (!shimInfo.IsShimmed)
                        {
                            // Run the command locally.

                            goto notShimmed;
                        }

                        // We need administrator privileges to map the local drive
                        // into a Docker container.

                        Program.VerifyAdminPrivileges();

                        // Map the container's [/log] directory as required.

                        var logMount = string.Empty;

                        if (!string.IsNullOrEmpty(LogPath))
                        {
                            var fullLogPath = Path.GetFullPath(LogPath);

                            Directory.CreateDirectory(fullLogPath);

                            logMount = $"-v {fullLogPath}:/log";
                        }

                        shim.WriteScript();

                        // Run the [neoncluster/neon-cli] Docker image, passing the modified command line 
                        // arguments and mounting the following read/write volumes:
                        //
                        //      /neoncluster    - the root folder for this workstation's cluster logins
                        //      /shim           - the generated shim files
                        //      /log            - the logging folder (if logging is enabled)

                        var secretsMount = $"-v \"{secretsRoot}:/neoncluster\"";
                        var shimMount    = $"-v \"{shim.ShimExternalFolder}:/shim\"";
                        var options      = shim.Terminal ? "-it" : "-i";

                        // If the NEON_RUN_ENV=PATH environment variable exists and references an 
                        // existing file, then this instance of [neon] is running within the context 
                        // of a [neon run ...] command.  In this case, we need to forward the run
                        // environment variables into the container we're launching.
                        //
                        // The NEON_RUN_ENV file defines these variables and is compatible with the
                        // [docker run --env-file=PATH] option so we'll use that.

                        var runEnvPath = Environment.GetEnvironmentVariable("NEON_RUN_ENV");

                        if (!string.IsNullOrWhiteSpace(runEnvPath) && File.Exists(runEnvPath))
                        {
                            if (options.Length > 0)
                            {
                                options += " ";
                            }

                            options += $"--env-file \"{runEnvPath}\"";
                        }

                        // Mount any mapped client folders.

                        var sbMappedMount = new StringBuilder();

                        foreach (var mappedFolder in shim.MappedFolders)
                        {
                            var mode = mappedFolder.IsReadOnly ? "ro" : "rw";

                            sbMappedMount.AppendWithSeparator($"-v \"{mappedFolder.ClientFolderPath}:{mappedFolder.ContainerFolderPath}:{mode}\"");
                        }

                        // If the tool was built from the Git production branch then the Docker image
                        // tag will simply be the tool version.  For non-production branches we'll
                        // use [BRANCH-<version>] as the tag.

                        var imageTag = Program.Version;

                        if (ThisAssembly.Git.Branch != NeonClusterConst.GitProdBranch)
                        {
                            imageTag = $"{ThisAssembly.Git.Branch}-{Program.Version}";
                        }

                        // Generate any [--env] options to be passed to the container.

                        var sbEnvOptions = new StringBuilder();

                        foreach (var envOption in shim.EnvironmentVariables)
                        {
                            sbEnvOptions.AppendWithSeparator(NeonHelper.NormalizeExecArgs($"--env={envOption}"));
                        }

                        Process process;

                        try
                        {
                            process = Process.Start("docker", $"run {options} --rm {secretsMount} {shimMount} {logMount} {sbMappedMount} {sbEnvOptions} --network host neoncluster/neon-cli:{imageTag}");
                        }
                        catch (Win32Exception)
                        {
                            Console.Error.WriteLine("*** ERROR: Cannot run Docker.  Make sure that it is installed and is on the PATH.");
                            Program.Exit(1);
                            return;
                        }

                        process.WaitForExit();
                        exitCode = process.ExitCode;

                        if (shim.PostAction != null)
                        {
                            shim.PostAction(exitCode);
                        }
                    }

                    Program.Exit(exitCode);
                }

                // For direct mode, we're going to run the command here.

            notShimmed:

                // Process the standard command line options.

                var leftCommandLine = CommandLine.Split(command.SplitItem).Left;
                var os              = leftCommandLine.GetOption("--os", "ubuntu-16.04").ToLowerInvariant();

                switch (os)
                {
                    // Choose reasonable operating system specific defaults here.

                    case "ubuntu-16.04":

                        OSProperties = new DockerOSProperties()
                        {
                            TargetOS      = TargetOS.Ubuntu_16_04,
                            StorageDriver = DockerStorageDrivers.Overlay2
                        };
                        break;

                    default:

                        Console.Error.WriteLine($"*** ERROR: [--os={os}] is not a supported target operating system.");
                        Program.Exit(1);
                        break;
                }

                // Load the user name and password from the command line options, if present.

                Username = leftCommandLine.GetOption("--user");
                Password = leftCommandLine.GetOption("--password");

                // Handle the other options.

                var maxParallelOption = leftCommandLine.GetOption("--max-parallel");
                int maxParallel;

                if (!int.TryParse(maxParallelOption, out maxParallel) || maxParallel < 1)
                {
                    Console.Error.WriteLine($"*** ERROR: [--max-parallel={maxParallelOption}] option is not valid.");
                    Program.Exit(1);
                }

                Program.MaxParallel = maxParallel;

                var     waitSecondsOption = leftCommandLine.GetOption("--wait");
                double  waitSeconds;

                if (!double.TryParse(waitSecondsOption, NumberStyles.Any, NumberFormatInfo.InvariantInfo, out waitSeconds) || waitSeconds < 0)
                {
                    Console.Error.WriteLine($"*** ERROR: [--wait={waitSecondsOption}] option is not valid.");
                    Program.Exit(1);
                }

                Program.WaitSeconds = waitSeconds;

                // Parse and check any [--image-tag=TAG] option.

                DockerImageTag = leftCommandLine.GetOption("--image-tag");

                if (DockerImageTag != null)
                {
                    if (DockerImageTag.Length == 0)
                    {
                        Console.Error.WriteLine($"*** ERROR: [--image-tag={DockerImageTag}] cannot specify an empty tag.");
                        Program.Exit(1);
                    }

                    if (DockerImageTag[0] == '.' || DockerImageTag[0] == '-')
                    {
                        Console.Error.WriteLine($"*** ERROR: [--image-tag={DockerImageTag}] cannot start with a period (.) or dash (-).");
                        Program.Exit(1);
                    }

                    foreach (var ch in DockerImageTag)
                    {
                        var upper = char.ToUpperInvariant(ch);

                        if ('A' <= upper && upper <= 'Z')
                        {
                            continue;
                        }
                        else if ('0' <= upper && upper <= '9')
                        {
                            continue;
                        }
                        else if (ch == '_' || ch == '.' || ch == '-')
                        {
                            continue;
                        }

                        Console.Error.WriteLine($"*** ERROR: [--image-tag={DockerImageTag}] includes the invalid character [{ch}].  Only [a-zA-Z0-9_.-] are allowed.");
                        Program.Exit(1);
                    }
                }

                if (command.CheckOptions)
                {
                    // Make sure there are no unexpected command line options.

                    validOptions.Add("--help");

                    foreach (var optionName in command.ExtendedOptions)
                    {
                        validOptions.Add(optionName);
                    }

                    foreach (var option in leftCommandLine.Options)
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

                            Console.WriteLine($"*** ERROR: [{commandWords}] command does not support [{option.Key}].");
                            Program.Exit(1);
                        }
                    }
                }

                // Load the current cluster if there is one.

                ClusterLogin = GetClusterLogin();

                // Run the command.

                if (command.NeedsSshCredentials(CommandLine))
                {
                    if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrEmpty(Password))
                    {
                        Console.WriteLine();
                        Console.WriteLine("    Enter cluster SSH credentials:");
                        Console.WriteLine("    ------------------------------");
                    }

                    while (string.IsNullOrWhiteSpace(Username))
                    {
                        Console.Write("    username: ");
                        Username = Console.ReadLine();
                    }

                    while (string.IsNullOrEmpty(Password))
                    {
                        Password = NeonHelper.ReadConsolePassword("    password: ");
                    }
                }

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
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"*** ERROR: {NeonHelper.ExceptionError(e)}");
                Console.Error.WriteLine(string.Empty);
                Program.Exit(1);
            }

            Program.Exit(0);
        }

        /// <summary>
        /// Message written then a user is not logged into a cluster.
        /// </summary>
        public const string MustLoginMessage = "*** ERROR: You must first log into a cluster.";

        /// <summary>
        /// Optionally set to the tag to be used to override any explicit or implicit <b>:latest</b>
        /// image tags specified when deploying a neonCLUSTER.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property is <c>null</c> by default but may be specified using the <b>--image-tag=TAG</b>
        /// command line option.  The main purpose of this is support development and testing by specifying
        /// something like <b>--image-tag=BRANCH-latest</b>, where <b>BRANCH</b> is the current development
        /// branch.
        /// </para>
        /// <para>
        /// This will direct <b>neon-cli</b> to use images built from the branch rather than the default
        /// production images without needing to modify cluster configuration files.  All the developer
        /// needs to do is ensure that all of the required images were built from that branch first and
        /// then published to Docker Hub.
        /// </para>
        /// </remarks>
        public static string DockerImageTag { get; set; } = null;

        /// <summary>
        /// Resolves a Docker Image name/tag into the image specification to be actually deployed, taking
        /// the <see cref="DockerImageTag"/> property into account.
        /// </summary>
        /// <param name="image">The input image specification.</param>
        /// <returns>The output specification.</returns>
        /// <remarks>
        /// <para>
        /// If <see cref="DockerImageTag"/> is empty, then this method simply returns the <paramref name="image"/> 
        /// argument as passed.  Otherwise, if the image argument implicitly or explicitly specifies the
        /// <b>:latest</b> tag, then the value returned will include the <see cref="DockerImageTag"/>.
        /// </para>
        /// <para>
        /// In all cases where <paramref name="image"/> specifies a non-latest tag, then the argument
        /// will be returned unchanged.
        /// </para>
        /// </remarks>
        public static string ResolveDockerImage(string image)
        {
            if (string.IsNullOrEmpty(DockerImageTag) || string.IsNullOrEmpty(image))
            {
                return image;
            }

            var normalized = image;

            if (normalized.IndexOf(':') == -1)
            {
                // The image implicitly specifies [:latest].

                normalized += ":latest";
            }

            if (normalized.EndsWith(":latest"))
            {
                return normalized.Replace(":latest", $":{DockerImageTag}");
            }
            else
            {
                return image;
            }
        }

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
            get { return Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles(x86)"), @"PuTTY\putty.exe"); }
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
                var version = $"{ThisAssembly.Git.Branch}-{ThisAssembly.Git.Commit}";

#pragma warning disable 162 // Unreachable code

                if (ThisAssembly.Git.IsDirty)
                {
                    version += "-DIRTY";
                }

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
            if (NeonClusterHelper.IsConnected)
            {
                NeonClusterHelper.CloseCluster();
            }

            Environment.Exit(exitCode);
        }

        /// <summary>
        /// Returns the orignal program <see cref="CommandLine"/>.
        /// </summary>
        public static CommandLine CommandLine { get; private set; }

        /// <summary>
        /// Returns the command line as a string with sensitive information like a password
        /// obscured.  This is suitable for using as a <see cref="SetupController"/>'s
        /// operation summary.
        /// </summary>
        public static string SafeCommandLine
        {
            get
            {
                // Special case the situation when the tool is running in a Docker container
                // and a special file is present with the original command line presented
                // to the external shim.

                if (NoToolContainer && File.Exists("__shim.org"))
                {
                    return File.ReadAllText("__shim.org").Trim();
                }

                // Obscure the [-p=xxxx] and [--password=xxxx] options.

                var sb = new StringBuilder();

                foreach (var item in CommandLine.Items)
                {
                    if (item.StartsWith("-p="))
                    {
                        sb.AppendWithSeparator("-p=[...]");
                    }
                    else if (item.StartsWith("--password="))
                    {
                        sb.AppendWithSeparator("--password=[...]");
                    }
                    else
                    {
                        sb.AppendWithSeparator(item);
                    }
                }

                return sb.ToString();
            }
        }

        /// <summary>
        /// Returns the node operating system specific information.
        /// </summary>
        public static DockerOSProperties OSProperties { get; private set; }

        /// <summary>
        /// Returns the folder where <b>neon-cli</b> persists local state.  This
        /// folder and all subfolders are encrypted whwn supported by the current
        /// operating system.
        /// </summary>
        public static string ClusterRootFolder { get; private set; }

        /// <summary>
        /// Returns the folder where <b>neon-cli</b> persists cluster login information.
        /// </summary>
        public static string ClusterLoginFolder { get; private set; }

        /// <summary>
        /// Returns the path to the file where the name of the current cluster is saved.
        /// </summary>
        public static string CurrentClusterPath { get; private set; }

        /// <summary>
        /// Returns the path to the (hopefully) encrypted or tmpfs based temporary folder.
        /// </summary>
        public static string ClusterTempFolder { get; private set; }

        /// <summary>
        /// Returns the path to the cluster setup folder.
        /// </summary>
        public static string ClusterSetupFolder { get; private set; }

        /// <summary>
        /// Returns the path to the login information for the named cluster.
        /// </summary>
        /// <param name="username">The operator's user name.</param>
        /// <param name="clusterName">The cluster name.</param>
        /// <returns>The path to the cluster's credentials file.</returns>
        public static string GetClusterLoginPath(string username, string clusterName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(clusterName));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(username));

            return Path.Combine(ClusterLoginFolder, $"{username}@{clusterName}.login.json");
        }

        /// <summary>
        /// Returns the cluster login information for the currently logged in cluster.
        /// </summary>
        /// <param name="isRequired">Optionally ensures that a current login is required (defaults to <c>false</c>).</param>
        /// <returns>The current cluster login or <c>null</c>.</returns>
        public static ClusterLogin GetClusterLogin(bool isRequired = false)
        {
            var clusterLogin = NeonClusterHelper.GetLogin(!isRequired);

            if (isRequired && clusterLogin == null)
            {
                Console.Error.WriteLine(Program.MustLoginMessage);
                Program.Exit(1);
            }

            Program.ClusterLogin = clusterLogin;

            return clusterLogin;
        }

        /// <summary>
        /// Uses <see cref="NeonClusterHelper.OpenRemoteCluster(DebugSecrets, string)"/> to 
        /// ensure that there's a currently logged-in cluster and that the VPN connection
        /// is established if required.
        /// </summary>
        /// <param name="allowPreparedOnly">Optionally allows partially initialized cluster logins (defaults to <c>false</c>).</param>
        /// <returns>The current cluster login or <c>null</c>.</returns>
        /// <remarks>
        /// Nearly all commands required a fully initialized cluster login.  The only exception
        /// at this time it the <b>neon cluster setup</b> command which can accept a login
        /// created by the <b>neon prepare cluster</b> command that generates a login that
        /// has been initialized enough to allow setup to connect to the cluster via a VPN
        /// if necessary, has the host root account credentials, and also includes the
        /// cluster definition.  Partially intializated logins will have <see cref="ClusterLogin.SetupPending"/>
        /// set to <c>true</c>.
        /// </remarks>
        public static ClusterLogin ConnectCluster(bool allowPreparedOnly = false)
        {
            var clusterLogin = Program.GetClusterLogin(isRequired: true);

            if (clusterLogin.SetupPending && !allowPreparedOnly)
            {
                throw new Exception($"Cluster login [{clusterLogin.LoginName}] does not reference a fully configured cluster.  Use the [neon cluster setup...] command to complete cluster configuration.");
            }

            NeonClusterHelper.OpenRemoteCluster(loginPath: NeonClusterHelper.GetLoginPath(NeonClusterConst.RootUser, Program.ClusterLogin.ClusterName));

            // Note that we never try to connect the VPN from within the
            // [neon-cli] container.  Its expected that the VPN is always
            // established on the operator's workstation.

            if (!NeonClusterHelper.InToolContainer && clusterLogin.ViaVpn)
            {
                NeonClusterHelper.VpnOpen(clusterLogin,
                    onStatus: message => Console.Error.WriteLine(message),
                    onError: message => Console.Error.WriteLine($"*** ERROR: {message}"));
            }

            return clusterLogin;
        }

        /// <summary>
        /// Returns the cluster's SSH user name.
        /// </summary>
        public static string Username { get; private set; }

        /// <summary>
        /// Returns the cluster's SSH user password.
        /// </summary>
        public static string Password { get; private set; }

        /// <summary>
        /// Returns the cluster login information for the currently logged in cluster or <c>null</c>.
        /// </summary>
        public static ClusterLogin ClusterLogin { get; set; }

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
        /// The seconds to wait for cluster stablization.
        /// </summary>
        public static double WaitSeconds { get; set; }

        /// <summary>
        /// Indicates whether operation progress output is to be suppressed.
        /// </summary>
        public static bool Quiet { get; set; }

        /// <summary>
        /// Indicates whether the tool executes the command directly (when <c>true</c>)
        /// or acts as a shim to (when <c>false</c>) a tool running in a Docker container.
        /// </summary>
        public static bool NoToolContainer { get; private set; }

        /// <summary>
        /// Creates a <see cref="SshProxy{TMetadata}"/> for the specified host and server name,
        /// configuring logging and the credentials as specified by the global command
        /// line options.
        /// </summary>
        /// <param name="name">The node name.</param>
        /// <param name="publicAddress">The node's public IP address or FQDN.</param>
        /// <param name="privateAddress">The node's private IP address.</param>
        /// <typeparam name="TMetadata">Defines the metadata type the command wishes to associate with the sewrver.</typeparam>
        /// <returns>The <see cref="SshProxy{TMetadata}"/>.</returns>
        public static SshProxy<TMetadata> CreateNodeProxy<TMetadata>(string name, string publicAddress, IPAddress privateAddress)
            where TMetadata : class
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            var logWriter = (TextWriter)null;

            if (!string.IsNullOrEmpty(LogPath))
            {
                logWriter = new StreamWriter(new FileStream(Path.Combine(LogPath, name + ".log"), FileMode.Create, FileAccess.ReadWrite));
            }

            SshCredentials sshCredentials;

            if (!string.IsNullOrEmpty(Program.Username) && !string.IsNullOrEmpty(Program.Password))
            {
                sshCredentials = SshCredentials.FromUserPassword(Program.Username, Program.Password);
            }
            else if (Program.ClusterLogin != null)
            {
                sshCredentials = Program.ClusterLogin.GetSshCredentials();
            }
            else
            {
                Console.Error.WriteLine("*** ERROR: Expected some node credentials.");
                Program.Exit(1);

                return null;
            }

            var proxy = new SshProxy<TMetadata>(name, publicAddress, privateAddress, sshCredentials, logWriter);

            proxy.RemotePath += $":{NodeHostFolders.Setup}";
            proxy.RemotePath += $":{NodeHostFolders.Tools}";

            return proxy;
        }

        /// <summary>
        /// Returns the folder holding the Linux resource files for the target operating system.
        /// </summary>
        public static ResourceFiles.Folder LinuxFolder
        {
            get
            {
                switch (Program.OSProperties.TargetOS)
                {
                    case TargetOS.Ubuntu_16_04:

                        return ResourceFiles.Linux.GetFolder("Ubuntu-16.04");

                    default:

                        throw new NotImplementedException($"Unexpected [{Program.OSProperties.TargetOS}] target operating system.");
                }
            }
        }

        /// <summary>
        /// Identifies the service manager present on the target Linux distribution.
        /// </summary>
        public static ServiceManager ServiceManager
        {
            get
            {
                switch (Program.OSProperties.TargetOS)
                {
                    case TargetOS.Ubuntu_16_04:

                        return ServiceManager.Systemd;

                    default:

                        throw new NotImplementedException($"Unexpected [{Program.OSProperties.TargetOS}] target operating system.");
                }
            }
        }

        /// <summary>
        /// Presents the user with a yes/no question and waits for a response.
        /// </summary>
        /// <param name="prompt">The question prompt.</param>
        /// <returns><c>true</c> if the answer is yes, <b>false</b> for no.</returns>
        public static bool PromptYesNo(string prompt)
        {
            while (true)
            {
                Console.Write($"{prompt} [Y/N]: ");

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

        /// <summary>
        /// Uses WinSCP to convert an OpenSSH PEM formatted key to the PPK format
        /// required by PuTTY/WinSCP.  This works only on Windows.
        /// </summary>
        /// <param name="cluster">The related cluster login information.</param>
        /// <param name="pemKey">The OpenSSH PEM key.</param>
        /// <returns>The converted PPPK key.</returns>
        /// <exception cref="NotImplementedException">Thrown when not running on Windows.</exception>
        /// <exception cref="Win32Exception">Thrown if WinSCP could not be executed.</exception>
        public static string ConvertPUBtoPPK(ClusterLogin cluster, string pemKey)
        {
            if (!NeonHelper.IsWindows)
            {
                throw new NotImplementedException("Not implemented for non-Windows platforms.");
            }

            var programPath = "winscp.com";
            var pemKeyPath  = Path.Combine(Program.ClusterTempFolder, Guid.NewGuid().ToString("D"));
            var ppkKeyPath  = Path.Combine(Program.ClusterTempFolder, Guid.NewGuid().ToString("D"));

            try
            {
                File.WriteAllText(pemKeyPath, pemKey);

                var result = NeonHelper.ExecuteCaptureStreams(programPath, $@"/keygen ""{pemKeyPath}"" /comment=""{cluster.Definition.Name} Key"" /output=""{ppkKeyPath}""");

                if (result.ExitCode != 0)
                {
                    Console.WriteLine(result.OutputText);
                    Console.Error.WriteLine(result.ErrorText);
                    Program.Exit(result.ExitCode);
                }

                return File.ReadAllText(ppkKeyPath);
            }
            catch (Win32Exception)
            {
                Console.WriteLine($"*** ERROR: Cannot launch [{programPath}].");
                throw;
            }
            finally
            {
                if (File.Exists(pemKeyPath))
                {
                    File.Delete(pemKeyPath);
                }

                if (File.Exists(ppkKeyPath))
                {
                    File.Delete(ppkKeyPath);
                }
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
                var result = NeonHelper.ExecuteCaptureStreams(programPath, sbArgs.ToString());

                if (result.ExitCode != 0)
                {
                    Console.Error.Write(result.AllText);
                    Program.Exit(result.ExitCode);
                }
            }
            catch (Win32Exception)
            {
                Console.WriteLine($"*** ERROR: Cannot launch [{programPath}].");
                Program.Exit(1);
            }
        }

        /// <summary>
        /// <para>
        /// Recursively executes a <b>neon-cli</b> command by launching a new
        /// instance of the tool with the arguments passed and capturing the
        /// process output streams.
        /// </para>
        /// <note>
        /// This does not recurse into  a container, it simply launches a new
        /// process instance of the program in the current environment with
        /// the arguments passed.
        /// </note>
        /// </summary>
        /// <param name="args">The arguments.</param>
        /// <returns>The process response.</returns>
        public static ExecuteResult ExecuteRecurseCaptureStreams(params object[] args)
        {
            // We need to prepend the program assembly path to the arguments.

            var argList = new List<object>(args);

            argList.Insert(0, NeonHelper.GetAssemblyPath(Assembly.GetEntryAssembly()));

            return NeonHelper.ExecuteCaptureStreams("dotnet", argList.ToArray());
        }

        /// <summary>
        /// Verify that the current user has administrator privileges, exiting
        /// the application if this is not the case.
        /// </summary>
        public static void VerifyAdminPrivileges()
        {
            if (!NeonClusterHelper.InToolContainer)
            {
                if (NeonHelper.IsWindows)
                {
                    var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());

                    if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
                    {
                        Console.Error.WriteLine("*** ERROR: This command requires elevated administrator privileges.");
                        Program.Exit(1);
                    }
                }
                else if (NeonHelper.IsOSX)
                {
                    // $todo(jeff.lill): Implement this
                }
            }
        }
    }
}
