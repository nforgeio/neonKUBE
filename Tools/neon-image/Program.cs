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

    neon help               COMMAND
    neon version            [-n] [--git] [--minimum=VERSION]

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

            return new ClusterProxy(KubeHelper.CurrentContext, Program.CreateNodeProxy<NodeDefinition>);
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
        /// Uses WinSCP to convert an OpenSSH PEM formatted key to the PPK format
        /// required by PuTTY/WinSCP.  This works only on Windows.
        /// </summary>
        /// <param name="kubeLogin">The cluster login.</param>
        /// <param name="pemKey">The OpenSSH PEM key.</param>
        /// <returns>The converted PPPK key.</returns>
        /// <exception cref="NotImplementedException">Thrown when not running on Windows.</exception>
        /// <exception cref="Win32Exception">Thrown if WinSCP could not be executed.</exception>
        public static string ConvertPUBtoPPK(ClusterLogin kubeLogin, string pemKey)
        {
            if (!NeonHelper.IsWindows)
            {
                throw new NotImplementedException("Not implemented for non-Windows platforms.");
            }

            var programPath = "winscp.com";
            var pemKeyPath  = Path.Combine(KubeHelper.TempFolder, Guid.NewGuid().ToString("d"));
            var ppkKeyPath  = Path.Combine(KubeHelper.TempFolder, Guid.NewGuid().ToString("d"));

            try
            {
                File.WriteAllText(pemKeyPath, pemKey);

                var result = NeonHelper.ExecuteCapture(programPath, $@"/keygen ""{pemKeyPath}"" /comment=""{kubeLogin.ClusterDefinition.Name} Key"" /output=""{ppkKeyPath}""");

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
                Console.Error.WriteLine($"*** ERROR: Cannot launch [{programPath}].");
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
        public static ExecuteResponse ExecuteRecurseCaptureStreams(params object[] args)
        {
            // We need to prepend the program assembly path to the arguments.

            var argList = new List<object>(args);

            argList.Insert(0, NeonHelper.GetEntryAssemblyPath());

            return NeonHelper.ExecuteCapture("dotnet", argList.ToArray());
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
        /// Optionally set to the registry to be used to override any explicit or implicit <b>nkubeio</b>
        /// or <b>nkubedev</b> organizations specified when deploying or updating a neonKUBE.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property is <c>null</c> by default but may be specified using the <b>--image-reg=REGISTRY</b>
        /// command line option.  The main purpose of this is support development and testing scenarios.
        /// </para>
        /// </remarks>
        public static string DockerImageReg { get; private set; } = null;

        /// <summary>
        /// Optionally set to the tag to be used to override any explicit or implicit <b>:latest</b>
        /// image tags specified when deploying or updating a neonKUBE.
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
        public static string DockerImageTag { get; private set; } = null;

        /// <summary>
        /// Resolves a Docker Image name/tag into the image specification to be actually deployed, taking
        /// the <see cref="DockerImageReg"/> and <see cref="DockerImageTag"/> properties into account.
        /// </summary>
        /// <param name="image">The input image specification.</param>
        /// <returns>The output specification.</returns>
        /// <remarks>
        /// <para>
        /// If <see cref="DockerImageReg"/> is empty and <paramref name="image"/> specifies the 
        /// <see cref="KubeConst.NeonProdRegistry"/> and the Git branch used to build <b>neon-cli</b>
        /// is not <b>PROD</b>, then the image registry will be set to <see cref="KubeConst.NeonDevRegistry"/>.
        /// This ensures that non-production <b>neon-cli </b> builds will use the development Docker
        /// images by default.
        /// </para>
        /// <para>
        /// If <see cref="DockerImageReg"/> is not empty  and <paramref name="image"/> specifies the 
        /// <see cref="KubeConst.NeonProdRegistry"/> then <see cref="DockerImageReg"/> will
        /// replace the registry in the image.
        /// </para>
        /// <para>
        /// If <see cref="DockerImageTag"/> is empty, then this method simply returns the <paramref name="image"/>
        /// argument as passed.  Otherwise, if the image argument implicitly or explicitly specifies the 
        /// <b>:latest</b> tag, then the image returned will be tagged with <see cref="DockerImageTag"/>
        /// when that's not empty or <b>:latest</b> for the <b>PROD</b> branch or <b>:BRANCH-latest</b> 
        /// for non-<b>PROD</b> branches.
        /// </para>
        /// <para>
        /// In all cases where <paramref name="image"/> specifies a non-latest tag, then the argument
        /// will be returned unchanged.
        /// </para>
        /// </remarks>
        public static string ResolveDockerImage(string image)
        {
            if (string.IsNullOrEmpty(image))
            {
                return image;
            }

            // Extract the registry from the image.  Note that this will
            // be empty for official images on Docker Hub.

            var registry = (string)null;
            var p        = image.IndexOf('/');

            if (p != -1)
            {
                registry = image.Substring(0, p);
            }

            if (!string.IsNullOrEmpty(registry) && registry == NeonHelper.NeonProdRegistry)
            {
                var imageWithoutRegistry = image.Substring(registry.Length);

                if (!string.IsNullOrEmpty(DockerImageReg))
                {
                    image = DockerImageReg + imageWithoutRegistry;
                }
                else if (!IsRelease)
                {
                    image = NeonHelper.NeonDevRegistry + imageWithoutRegistry;
                }
            }

            if (string.IsNullOrEmpty(image))
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
                if (!string.IsNullOrEmpty(DockerImageTag))
                {
                    return normalized.Replace(":latest", $":{DockerImageTag}");
                }
                else if (IsRelease)
                {
                    return normalized;
                }
                else
                {
#pragma warning disable 0436
                    return normalized.Replace(":latest", $":{ThisAssembly.Git.Branch.ToLowerInvariant()}-latest");
#pragma warning restore 0436
                }
            }
            else
            {
                return image;
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
