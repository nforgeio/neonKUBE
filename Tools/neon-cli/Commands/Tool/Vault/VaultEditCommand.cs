//-----------------------------------------------------------------------------
// FILE:	    VaultEditCommand.cs
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

using Newtonsoft;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>vault edit</b> command.
    /// </summary>
    [Command]
    public class VaultEditCommand : CommandBase
    {
        private const string usage = @"
Edits an encypted file.

USAGE:

    neon tool vault edit PATH

ARGUMENTS:

    PATH    - path to the file being created

REMARKS:

This command edits an existing encrypted file at PATH.

The command decrypts the file to a temporary folder and launches a text
editor enabling you to edit the file.  Once the editor exits, the temporary
file will be encrypted back to PATH and then be deleted.

The default platform editor will be launched (NotePad.exe for Windows or 
Vim for OS/x and Linux).  You can customize the editor by setting the EDITOR 
environment with the command line required to launch your favorite editor.
If $FILE exists in the environment variable, then that will be replaced
by the tareget file path or else the file path will be appended to the
command line if $FILE isn't present.

NOTE: You don't need to specify a password name for this command 
      because the password name is saved within encrypted files.
";
        /// <inheritdoc/>
        public override string[] Words => new string[] { "tool", "vault", "edit" }; 

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

            var path = commandLine.Arguments.ElementAtOrDefault(0);

            if (string.IsNullOrEmpty(path))
            {
                Console.Error.WriteLine("*** ERROR: The PATH argument is required.");
                Program.Exit(1);
            }

            if (!NeonVault.IsEncrypted(path, out var passwordName))
            {
                Console.Error.WriteLine($"*** ERROR: The [{path}] file is not encrypted.");
                Program.Exit(1);
            }

            var vault    = new NeonVault(Program.LookupPassword);
            var fileName = Path.GetFileName(path);

            using (var tempFolder = new TempFolder())
            {
                var tempPath = Path.Combine(tempFolder.Path, fileName);

                // Decrypt the file to a secure temporary folder, launch the
                // editor and re-encrypt the file after the editor returns.

                vault.Decrypt(path, tempPath);
                NeonHelper.OpenEditor(tempPath);
                vault.Encrypt(tempPath, path, passwordName);
            }

            Program.Exit(0);
            await Task.CompletedTask;
        }
    }
}
