//-----------------------------------------------------------------------------
// FILE:	    VaultCreateCommand.cs
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
    /// Implements the <b>vault create</b> command.
    /// </summary>
    [Command]
    public class VaultCreateCommand : CommandBase
    {
        private const string usage = @"
Creates an encrypted file and opens it in a text editor.

USAGE:

    neon tool vault create PATH [PASSWORD-NAME]

ARGUMENTS:

    PATH            - path to the file being created
    PASSWORD-NAME   - optional password name

REMARKS:

This command creates a new file at PATH using an explicitly named password
or have the command search the current and ancestor directories for a
[.password-name] file with the default password name.

The command decrypts the file to a temporary folder and launches a text
editor enabling you to edit the file.  Once the editor exits, the temporary
file will be encrypted to back PATH and then be deleted.

The default platform editor will be launched (NotePad.exe for Windows or 
Vim for OS/x and Linux).  You can customize the editor by setting the EDITOR 
environment variable to the path to the editor executable file.
";
        /// <inheritdoc/>
        public override string[] Words => new string[] { "tool", "vault", "create" }; 

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

            var path         = commandLine.Arguments.ElementAtOrDefault(0);
            var passwordName = commandLine.Arguments.ElementAtOrDefault(1);

            if (string.IsNullOrEmpty(path))
            {
                Console.Error.WriteLine("*** ERROR: The PATH argument is required.");
                Program.Exit(1);
            }

            if (string.IsNullOrEmpty(passwordName))
            {
                passwordName = Program.GetDefaultPasswordName(path);
            }

            if (string.IsNullOrEmpty(passwordName))
            {
                Console.Error.WriteLine("*** ERROR: A PASSWORD-NAME argument or [.password-name] file is required.");
                Program.Exit(1);
            }

            var vault    = new NeonVault(Program.LookupPassword);
            var fileName = Path.GetFileName(path);

            using (var tempFolder = new TempFolder())
            {
                var tempPath = Path.Combine(tempFolder.Path, fileName);

                // Create an empty temporary file, encrypt it to the target
                // file, and then launch the editor on the temporary file.

                File.WriteAllBytes(tempPath, Array.Empty<byte>());
                vault.Encrypt(tempPath, path, passwordName);
                NeonHelper.OpenEditor(tempPath);

                // Re-encrypt the just edited temporary file to the target.

                vault.Encrypt(tempPath, path, passwordName);
            }

            Program.Exit(0);
            await Task.CompletedTask;
        }
    }
}
