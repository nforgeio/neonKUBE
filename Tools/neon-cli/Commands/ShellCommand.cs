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
variables loaded from one or more Ansible compatible YAML variable files,
optionally decryting the secrets file first.

USAGE:

    neon shell [--password-file=PATH] [--ask-vault-pass] VARS1 [VARS2...] -- CMD...

ARGUMENTS:

    VARS#                   - Path to a YAML variables file
    --                      - Indicates the start of the command/args
                              to be invoked
    CMD...                  - Command and arguments

OPTIONS:

    --password-file=PATH    - Optionally specifies the path to the password
                              file to be used to decrypt the variable files.
                              See the notes below discussing where password
                              files are located.

    --ask-vault-pass        - Optionally specifies that the user should
                              be prompted for the decryption password.

NOTES:

This command works by reading variables from one or more files, setting
these as environment variables and then executing a command in the 
context of these environment variables.  The variable files are formatted
as Ansible compatible YAML, like:

    username: jeff
    password: super.dude
    mysql:
        username: dbuser
        password: dbpass

This defines two simple passwords and two passwords in a dictionary.
This will generate these environment variables:

    username=jeff
    password=super.dude
    mysql_username=dbuser
    mysql_password=dbpass

Variable files can be encrypted using the [neon ansible vault encrypt]
command and then can be used by [neon shell] and other [neon ansible]
commands.  Encryption passwords can be specified manually using a 
prompt by passing [--ask-vault-pass] or by passing the PATH to a
password file via [--password-file=PATH].

Password files simply hold a password as a single line text file.
[neon-cli] expects password files to be located in a user-specific
directory on your workstation:

    %LOCALAPPDATA%\neonFORGE\neoncluster\ansible\passwords  - for Windows
    ~/.neonforge/neoncluster/ansible/passwords              - for OSX

These folders are encrypted at rest for security.  You can use the 
[neon ansible password ...] commands to manage your passwords.
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
