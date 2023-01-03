//-----------------------------------------------------------------------------
// FILE:	    PasswordExportCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Cryptography;
using Neon.Kube;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>password export</b> command.
    /// </summary>
    [Command]
    public class PasswordExportCommand : CommandBase
    {
        private const string usage = @"
Exports selected passwords to an encrypted ZIP file.

USAGE:

    neon tool password export [--stdin] PATH NAME...
    neon tool password export [--stdin] PATH *

ARGUMENTS:

    PATH        - Path to the output ZIP archive.
    NAME...     - Names of one or more passwords to be included.
    *           - Includes all passwords.

OPTIONS:

    --stdin     - Read the ZIP archive password from STDIN
                  rather then prompting for it.

REMARKS:

This command generates an encrypted ZIP archive incuding the
selected passwords.  You'll be prompted for the password to
be used to encrypt the archive by default.  Use the [--stdin]
option to have the command read this passwords from STDIN
instead.
";

        /// <inheritdoc/>
        public override string[] Words => new string[] { "tool", "password", "export" }; 

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override string[] ExtendedOptions
        {
            get { return new string[] { "--stdin" }; }
        }

        /// <inheritdoc/>
        public override async Task RunAsync(CommandLine commandLine)
        {
            if (commandLine.HasHelpOption)
            {
                Console.WriteLine(usage);
                Program.Exit(0);
            }

            var zipPath   = commandLine.Arguments.ElementAtOrDefault(0);
            var firstName = commandLine.Arguments.ElementAtOrDefault(1);
            var fromStdin = commandLine.HasOption("--stdin");
            var names     = new List<string>();

            if (zipPath == null)
            {
                Console.Error.WriteLine("*** ERROR: PATH argument is required.");
                Program.Exit(1);
            }

            if (firstName == null)
            {
                Console.Error.WriteLine("*** ERROR: At least one NAME argument is required.");
                Program.Exit(1);
            }

            if (firstName == "*")
            {
                foreach (var path in Directory.GetFiles(KubeHelper.PasswordsFolder))
                {
                    names.Add(Path.GetFileName(path));
                }
            }
            else
            {
                foreach (var name in commandLine.Arguments.Skip(1))
                {
                    var validatedName = NeonVault.ValidatePasswordName(name);

                    if (!File.Exists(Path.Combine(KubeHelper.PasswordsFolder, validatedName)))
                    {
                        Console.Error.WriteLine($"*** ERROR: Password [{validatedName}] does not exist.");
                        Program.Exit(1);
                    }

                    names.Add(validatedName);
                }
            }

            if (names.Count == 0)
            {
                Console.Error.WriteLine("*** ERROR: No passwords selected for export.");
                Program.Exit(1);
            }

            var zipPassword = (string)null;

            if (fromStdin)
            {
                // Read the password from STDIN and trim.

                using (var stdin = NeonHelper.OpenStandardInput())
                {
                    using (var reader = new StreamReader(stdin))
                    {
                        zipPassword = reader.ReadLine().Trim();
                    }
                }
            }

        retryPassword:

            if (!fromStdin)
            {
                if (string.IsNullOrEmpty(zipPassword))
                {
                    zipPassword = NeonHelper.ReadConsolePassword("Enter Password:   ");
                }

                if (!string.IsNullOrEmpty(zipPassword) && zipPassword != NeonHelper.ReadConsolePassword("Confirm Password: "))
                {
                    Console.WriteLine();
                    Console.WriteLine("The passwords don't match.  Please try again:");
                    Console.WriteLine();

                    goto retryPassword;
                }
            }

            using (var zip = ZipFile.Create(zipPath))
            {
                zip.Password = zipPassword;
                zip.BeginUpdate();

                foreach (var name in names)
                {
                    zip.Add(Path.Combine(KubeHelper.PasswordsFolder, name), name);
                }

                zip.CommitUpdate();
            }

            Console.WriteLine();
            Console.WriteLine($"[{names.Count}] passwords exported.");
            Program.Exit(0);
            await Task.CompletedTask;
        }
    }
}
