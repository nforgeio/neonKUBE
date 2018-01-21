//-----------------------------------------------------------------------------
// FILE:	    ShellCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;

using Neon.Cluster;
using Neon.Common;
using Neon.IO;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>shell</b> command.
    /// </summary>
    public class ShellCommand : CommandBase
    {
        private const string usage = @"
Runs a shell command on the local workstation, in the context of environment
variables and secrets located in a specified folder.

USAGE:

    neon shell SECRETS-PATH -- CMD ...

ARGUMENTS:

    SECRETS-PATH            - Path to the secrets folder.
    --                      - Separates the Neon and Shell commands.
    CMD                     - Command and arguments to be executed. 

NOTES:

Managing secrets for development, test, and production environments can
be difficult.  Docker and neonCLUSTER provide mechanisms for persisting
and using secrets in a cluster, but it's still necessary to provision
the secrets securely in the first place.

This command provides a way to securly manage collections of secrets 
related to specific deployments and then execute local shell commands 
to use the secrets to provision or manage the cluster.

Secrets are simply files in a specified folder.  Secret environment
variables are specified in a special [__env.txt] with VAR=VALUE lines.
Blank and comment lines beginning with ""#"" will be ignored.

Other secret files such as TLS certificates may also be present.

The [shell] command works by loading environment variables into
memory, extracting any other files to a temporary folder and then
executing the command in the local shell the environment variables
and with the current directory set to the data folder.

The shell will be CMD.EXE for Windows and Bash for OSX and Linux.
";

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "shell" }; }
        }

        /// <inheritdoc/>
        public override bool CheckOptions
        {
            get { return false; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            if (commandLine.HasHelpOption)
            {
                Console.WriteLine(usage);
                Program.Exit(0);
            }

            if (commandLine.Arguments.Length < 2)
            {
                Console.WriteLine(usage);
                Program.Exit(1);
            }

            var shellCommandLine = Program.CommandLine.Split().Right;

            if (shellCommandLine == null || shellCommandLine.Arguments.Length == 0)
            {
                Console.Error.WriteLine("*** ERROR: Expecting a [--] argument followed be a shell command.");
                Program.Exit(1);
            }

            var secretsFolder = commandLine.Arguments[0];

            if (!Directory.Exists(secretsFolder))
            {
                Console.Error.WriteLine($"*** ERROR: Secrets folder [{secretsFolder}] does not exist.");
                Program.Exit(1);
            }

            var orgDirectory = Directory.GetCurrentDirectory();
            var shellFolder  = Path.Combine(NeonClusterHelper.GetShellFolder(), Guid.NewGuid().ToString("D"));
            var exitCode     = 1;

            try
            {
                // Create the temporary shell folder and copy the secret files there.

                NeonHelper.CopyFolder(secretsFolder, shellFolder);

                // Make the temporary shell folder the current directory.

                Directory.SetCurrentDirectory(shellFolder);

                // Load environment variables from the the special [__env.txt] file (if present).

                var envFilePath = Path.Combine(shellFolder, "__env.txt");

                if (File.Exists(envFilePath))
                {
                    // Read environment variables of the form VAR=VALUE, skipping over
                    // whitespace, comments and invalid statements.

                    var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    using (var reader = new StreamReader(new FileStream(envFilePath, FileMode.Open, FileAccess.Read)))
                    {
                        foreach (var raw in reader.Lines())
                        {
                            var line = raw.Trim();

                            if (line.Length == 0 || line.StartsWith("#"))
                            {
                                continue;
                            }

                            var parts = line.Split(new char[] { '=' }, 2);

                            if (parts.Length != 2)
                            {
                                continue;
                            }

                            var name  = parts[0].Trim();
                            var value = parts[1].Trim();

                            if (name.Length > 0)
                            {
                                variables[name] = value;
                            }
                        }
                    }

                    // Set the local environment variables.

                    // $todo(jeff.lill):
                    //
                    // Note that we don't attempt to expand environment variables on the value side
                    // of the assignments.  Probably an overkill anyway.

                    foreach (var item in variables)
                    {
                        Environment.SetEnvironmentVariable(item.Key, item.Value);
                    }

                    // Execute the command in the proper shell.

                    var sbCommand = new StringBuilder();

                    foreach (var arg in shellCommandLine.Items)
                    {
                        if (sbCommand.Length > 0)
                        {
                            sbCommand.Append(' ');
                        }

                        if (arg.Contains(' '))
                        {
                            sbCommand.Append("\"" + arg + "\"");
                        }
                        else
                        {
                            sbCommand.Append(arg);
                        }
                    }

                    exitCode = NeonHelper.ExecuteShell(sbCommand.ToString());
                }
            }
            finally
            {
                // Restore the current directory.

                Directory.SetCurrentDirectory(orgDirectory);

                // Cleanup

                if (Directory.Exists(shellFolder))
                {
                    Directory.Delete(shellFolder, true);
                }
            }

            Program.Exit(exitCode);
        }

        /// <inheritdoc/>
        public override ShimInfo Shim(DockerShim shim)
        {
            return new ShimInfo(isShimmed: false, ensureConnection: false);
        }
    }
}
