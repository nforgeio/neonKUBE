//-----------------------------------------------------------------------------
// FILE:	    VaultDecryptCommand.cs
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
    /// Implements the <b>vault decrypt</b> command.
    /// </summary>
    [Command]
    public class VaultDecryptCommand : CommandBase
    {
        private const string usage = @"
Decrypts a file to another location.

USAGE:

    neon tool vault decrypt SOURCE TARGET

ARGUMENTS:

    SOURCE      - Path to the encrypted file
    TARGET      - Path to the new decrypted file

REMARKS:

This command decrypts the SOURCE file to TARGET using the password named
within the SOURCE file.

NOTE: We explicitly don't support decrypting a file in-place to discourage
      temporarily decrypting a sensitive file within a source repository
      and then accidentially commiting the unencrypted file, which is really
      easy to do.
";
        /// <inheritdoc/>
        public override string[] Words => new string[] { "tool", "vault", "decrypt" }; 

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

            var sourcePath = commandLine.Arguments.ElementAtOrDefault(0);
            var targetPath = commandLine.Arguments.ElementAtOrDefault(1);

            if (string.IsNullOrEmpty(sourcePath))
            {
                Console.Error.WriteLine("*** ERROR: The SOURCE argument is required.");
                Program.Exit(1);
            }

            if (string.IsNullOrEmpty(targetPath))
            {
                Console.Error.WriteLine("*** ERROR: The TARGET argument is required.");
                Program.Exit(1);
            }

            if (!NeonVault.IsEncrypted(sourcePath))
            {
                Console.Error.WriteLine($"*** ERROR: The [{sourcePath}] file is not encrypted.");
                Program.Exit(1);
            }

            var vault = new NeonVault(Program.LookupPassword);

            vault.Decrypt(sourcePath, targetPath);

            Program.Exit(0);
            await Task.CompletedTask;
        }
    }
}
