//-----------------------------------------------------------------------------
// FILE:	    FileCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;

using Neon.Cluster;
using Neon.Common;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>file</b> commands.
    /// </summary>
    public class FileCommand : CommandBase
    {
        private const string usage = @"
Easily create, edit, decrypt and encrypt files using Ansible encryption.
These commands are shortcuts for the [neon ansible vault] commands.

USAGE:

    neon file create    PATH PASSWORD-NAME
    neon file decrypt   PATH PASSWORD-NAME
    neon file edit      PATH PASSWORD-NAME
    neon file encrypt   PATH PASSWORD-NAME
    neon file view      PATH PASSWORD-NAME

ARGS:

    PATH            - Path to the target file
    PASSWORD-NAME   - Identifies the Ansible password
";
        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "file" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            if (NeonClusterHelper.InToolContainer)
            {
                Console.Error.WriteLine("*** ERROR: [file] commands cannot be run inside a Docker container.");
                Program.Exit(1);
            }

            if (commandLine.Arguments.Length == 0 || commandLine.HasHelpOption)
            {
                Help();
                Program.Exit(0);
            }

            string  command      = commandLine.Arguments.AtIndexOrDefault(0);
            string  path         = commandLine.Arguments.AtIndexOrDefault(1);
            string  passwordName = commandLine.Arguments.AtIndexOrDefault(2);
            int     exitCode;

            if (string.IsNullOrEmpty(path))
            {
                Console.Error.WriteLine("*** ERROR: PATH argument is missing.");
                Program.Exit(1);
            }

            if (string.IsNullOrEmpty(passwordName))
            {
                Console.Error.WriteLine("*** ERROR: PASSWORD-NAME argument is missing.");
                Program.Exit(1);
            }

            switch (command)
            {
                case "create":

                    exitCode = Program.ExecuteRecurse(
                        new object[]
                        {
                            "ansible",
                            "vault",
                            "--",
                            "create",
                            $"--vault-password-file={passwordName}",
                            path
                        });

                    if (exitCode != 0)
                    {
                        Program.Exit(exitCode);
                    }
                    break;

                case "decrypt":

                    exitCode = Program.ExecuteRecurse(
                        new object[]
                        {
                            "ansible",
                            "vault",
                            "--",
                            "decrypt",
                            $"--vault-password-file={passwordName}",
                            path
                        });

                    if (exitCode != 0)
                    {
                        Program.Exit(exitCode);
                    }
                    break;

                case "edit":

                    exitCode = Program.ExecuteRecurse(
                        new object[]
                        {
                            "ansible",
                            "vault",
                            "--",
                            "edit",
                            $"--vault-password-file={passwordName}",
                            path
                        });

                    if (exitCode != 0)
                    {
                        Program.Exit(exitCode);
                    }
                    break;

                case "encrypt":

                    exitCode = Program.ExecuteRecurse(
                        new object[]
                        {
                            "ansible",
                            "vault",
                            "--",
                            "encrypt",
                            $"--vault-password-file={passwordName}",
                            path
                        });

                    if (exitCode != 0)
                    {
                        Program.Exit(exitCode);
                    }
                    break;

                case "view":

                    var result = Program.ExecuteRecurseCaptureStreams(
                        new object[]
                        {
                            "ansible",
                            "vault",
                            "--",
                            "view",
                            $"--vault-password-file={passwordName}",
                            path
                        });

                    if (result.ExitCode != 0)
                    {
                        Console.Error.WriteLine(CleanAnsibleError(result.AllText));
                        Program.Exit(result.ExitCode);
                    }

                    Console.Write(result.OutputText);
                    break;

                default:

                    Console.Error.WriteLine($"*** ERROR: Unexpected {command} command.");
                    Program.Exit(1);
                    break;
            }
        }

        /// <inheritdoc/>
        public override ShimInfo Shim(DockerShim shim)
        {
            return new ShimInfo(isShimmed: false);
        }

        /// <summary>
        /// Strips off a weird prefix and suffix from Ansible error messages 
        /// if present.  I'm  assuming that these are TTY formatting codes.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private string CleanAnsibleError(string message)
        {
            const string prefix = "\u001b[0;31m";
            const string suffix = "\u001b[0m\r\n";

            if (message.StartsWith(prefix))
            {
                message = message.Substring(prefix.Length);
            }

            if (message.EndsWith(suffix))
            {
                message = message.Substring(0, message.Length - suffix.Length);
            }

            return message;
        }
    }
}
