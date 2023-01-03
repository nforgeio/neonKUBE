
//-----------------------------------------------------------------------------
// FILE:	    VaultPasswordNameCommand.cs
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
    /// Implements the <b>vault password-name</b> command.
    /// </summary>
    [Command]
    public class VaultPasswordNameCommand : CommandBase
    {
        private const string usage = @"
Returns the name of the password used to encrypt a file.

USAGE:

    neon tool vault password-name [-n] PATH

ARGUMENTS:

    PATH    - Path to the encrypted file

OPTIONS:

    -n      - Don't write a newline after password

REMARKS:

This command determines a file is encrypted.  For encypted files, the command
returns 0 exit code and writes the password name to the output.

The command returns a non-zero exit code for unencrypted files.
";
        /// <inheritdoc/>
        public override string[] Words => new string[] { "tool", "vault", "password-name" }; 

        /// <inheritdoc/>
        public override string[] ExtendedOptions => new string[] { "-n" }; 

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

            if (NeonVault.IsEncrypted(path, out var passwordName))
            {
                Console.Write(passwordName);

                if (!commandLine.HasOption("-n"))
                {
                    Console.WriteLine();
                }
            }
            else
            {
                Program.Exit(1);
            }

            Program.Exit(0);
            await Task.CompletedTask;
        }
    }
}
