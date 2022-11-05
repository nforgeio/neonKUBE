//-----------------------------------------------------------------------------
// FILE:	    VaultEncryptCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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

using Newtonsoft;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>vault encrypt</b> command.
    /// </summary>
    [Command]
    public class VaultEncryptCommand : CommandBase
    {
        private const string usage = @"
Encrypts a file using a named password.

USAGE:

    neon tool vault encrypt PATH [--password-name=PASSWORD-NAME]
    neon tool vault encrypt SOURCE TARGET [PASSWORD-NAME]

ARGUMENTS:

    PATH            - Path to the file to encrypted in-place
    SOURCE          - Unencrypted source file path
    TARGET          - Unencrypted target file path
    PASSWORD-NAME   - optional password name

OPTIONS:

    --password-name=NAME    - Optionally specifies the password name
    -p=NAME                   when encrypting a file in-place.

REMARKS:

This command encrypts an existing file in-place or by encrypting the
file to another location.  You may explicitly specify a password or
or have the command search the current and ancestor directories
for a [.password-name] file with the default password name.

NOTE: The search for the [.password-name] file will start from the
      directory where the encrypted file will be written.
";
        /// <inheritdoc/>
        public override string[] Words => new string[] { "tool", "vault", "encrypt" }; 

        /// <inheritdoc/>
        public override string[] ExtendedOptions => new string[] { "--password-name", "-p" }; 

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override async Task RunAsync(CommandLine commandLine)
        {
            if (commandLine.HasHelpOption)
            {
                Console.WriteLine(usage);
                Program.Exit(0);
            }

            var sourcePath   = commandLine.Arguments.ElementAtOrDefault(0);
            var targetPath   = commandLine.Arguments.ElementAtOrDefault(1);
            var passwordName = commandLine.Arguments.ElementAtOrDefault(2);
            var inPlace      = false;

            if (string.IsNullOrEmpty(sourcePath))
            {
                Console.Error.WriteLine("*** ERROR: The PATH argument is required.");
                Program.Exit(1);
            }

            if (string.IsNullOrEmpty(targetPath))
            {
                targetPath = sourcePath;
                inPlace    = true;
            }

            if (string.IsNullOrEmpty(targetPath) || string.IsNullOrEmpty(passwordName))
            {
                passwordName = commandLine.GetOption("--password-name");

                if (passwordName != null && string.IsNullOrWhiteSpace(passwordName))
                {
                    Console.Error.WriteLine("*** ERROR: [--password-name] specifies a blank password.");
                    Program.Exit(1);
                }

                if (passwordName == null)
                {
                    passwordName = commandLine.GetOption("-p");

                    if (passwordName != null && string.IsNullOrWhiteSpace(passwordName))
                    {
                        Console.Error.WriteLine("*** ERROR: [-p] specifies a blank password.");
                        Program.Exit(1);
                    }
                }
            }

            if (NeonVault.IsEncrypted(sourcePath))
            {
                Console.Error.WriteLine($"*** ERROR: The [{sourcePath}] file is already encrypted.");
                Program.Exit(1);
            }

            if (string.IsNullOrEmpty(passwordName))
            {
                passwordName = Program.GetDefaultPasswordName(targetPath);
            }

            if (string.IsNullOrEmpty(passwordName))
            {
                Console.Error.WriteLine("*** ERROR: A PASSWORD-NAME argument or [.password-name] file is required.");
                Program.Exit(1);
            }

            var vault = new NeonVault(Program.LookupPassword);

            if (inPlace)
            {
                // For in-place encryption, we'll first copy the file to
                // a secure folder and then encrypt from there to overwrite
                // the original file.

                using (var tempFile = new TempFile())
                {
                    File.Copy(sourcePath, tempFile.Path);
                    vault.Encrypt(tempFile.Path, sourcePath, passwordName);
                }
            }
            else
            {
                // Otherwise, we can simply encrypt from the source to the target.

                vault.Encrypt(sourcePath, targetPath, passwordName);
            }

            Program.Exit(0);
            await Task.CompletedTask;
        }
    }
}
