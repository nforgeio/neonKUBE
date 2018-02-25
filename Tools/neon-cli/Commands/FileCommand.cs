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

    neon file create         PATH PASSWORD-NAME
    neon file decrypt        PATH PASSWORD-NAME
    neon file edit [OPTIONS] PATH PASSWORD-NAME
    neon file encrypt        PATH PASSWORD-NAME
    neon file view           PATH PASSWORD-NAME

ARGUMENTS:

    PATH            - Path to the target file
    PASSWORD-NAME   - Identifies the Ansible password

OPTIONS:

    --editor=nano|vim|vi    - Specifies the editor to use for modifying
                              encrypted files.  This defaults to [nano].
";
        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "file" }; }
        }

        /// <inheritdoc/>
        public override string[] ExtendedOptions
        {
            get { return new string[] { "--editor" }; }
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

            string  command      = commandLine.Arguments.ElementAtOrDefault(0);
            string  path         = commandLine.Arguments.ElementAtOrDefault(1);
            string  passwordName = commandLine.Arguments.ElementAtOrDefault(2);

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

            if (command != "create" && !File.Exists(path))
            {
                Console.Error.WriteLine($"*** ERROR: File [{path}] does not exist.");
                Program.Exit(1);
            }

            var passwordPath = Path.Combine(NeonClusterHelper.GetAnsiblePasswordsFolder(), passwordName);

            if (!File.Exists(passwordPath))
            {
                Console.Error.WriteLine($"*** ERROR: Password [{passwordName}] does not exist.");
                Program.Exit(1);
            }

            var editor = commandLine.GetOption("--editor", "nano");

            switch (editor.ToLowerInvariant())
            {
                case "nano":

                    Environment.SetEnvironmentVariable("EDITOR", "/bin/nano");
                    break;

                case "vim":

                    Environment.SetEnvironmentVariable("EDITOR", "/usr/bin/vim");
                    break;

                case "vi":

                    Environment.SetEnvironmentVariable("EDITOR", "/usr/bin/vi");
                    break;

                default:

                    Console.Error.WriteLine($"*** ERROR: [--editor={editor}] does not specify a known editor.  Specify one of: NANO, VIM, or VI.");
                    Program.Exit(1);
                    break;
            }

            // $note(jeff.lill):
            //
            // I tried to call [Program.ExecuteRecurse()] here to recurse into
            // the [neon vault -- COMMAND --vault-password-file=NAME PATH] commands
            // but it didn't work for [edit].  It looks like the command did run but then
            // gets stuck.  I could have sworn that I had this working at one
            // point but I can't get it working again.  I think the standard 
            // I/O streams being redirect might be confusing Docker and Ansible,
            // since Ansible needs to access the Docker TTY.
            //
            // The [view] command was also a bit wonky.  For example, two blank
            // lines in the encrypted file would be returned as only a single
            // blank line.
            //
            // The (not so bad) workaround is to simply recurse into 
            // [Program.Main()].  It's a little sloppy but should be OK
            // (and will be faster to boot).  I'm going to do this for
            // all of the commands.

            switch (command)
            {
                case "create":

                    Program.Main(
                        new string[]
                        {
                            "ansible",
                            "vault",
                            "--",
                            "create",
                            $"--vault-password-file={passwordName}",
                            path
                        });

                    break;

                case "decrypt":

                    Program.Main(
                        new string[]
                        {
                            "ansible",
                            "vault",
                            "--",
                            "decrypt",
                            $"--vault-password-file={passwordName}",
                            path
                        });

                    break;

                case "edit":

                    Program.Main(
                        new string[]
                        {
                            "ansible",
                            "vault",
                            $"--editor={editor}",
                            "--",
                            "edit",
                            $"--vault-password-file={passwordName}",
                            path
                        });

                    break;

                case "encrypt":

                    Program.Main(
                        new string[]
                        {
                            "ansible",
                            "vault",
                            "--",
                            "encrypt",
                            $"--vault-password-file={passwordName}",
                            path
                        });

                    break;

                case "view":

                    Program.Main(
                        new string[]
                        {
                            "ansible",
                            "vault",
                            "--",
                            "view",
                            $"--vault-password-file={passwordName}",
                            path
                        });

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
