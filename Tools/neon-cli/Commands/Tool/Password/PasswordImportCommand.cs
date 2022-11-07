//-----------------------------------------------------------------------------
// FILE:	    PasswordImportCommand.cs
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

using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Cryptography;
using Neon.Kube;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>password import</b> command.
    /// </summary>
    [Command]
    public class PasswordImportCommand : CommandBase
    {
        private const string usage = @"
Imports passwords from an encrypted ZIP file.

USAGE:

    neon password tool import [--stdin] PATH

ARGUMENTS:

    PATH        - Path to the input ZIP archive.

OPTIONS:

    --stdin     - Read the ZIP archive password from STDIN
                  rather then prompting for it.

REMARKS:

This command reads and saves passwords from an encrypted
ZIP archive.
";

        /// <inheritdoc/>
        public override string[] Words => new string[] { "tool", "password", "import" }; 

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

            var zipPath       = commandLine.Arguments.ElementAtOrDefault(0);
            var fromStdin     = commandLine.HasOption("--stdin");
            var zipPassword   = (string)null;
            var passwordCount = 0;

            if (zipPath == null)
            {
                Console.Error.WriteLine("*** ERROR: PATH argument is required.");
                Program.Exit(1);
            }

            if (!File.Exists(zipPath))
            {
                Console.Error.WriteLine($"*** ERROR: File [{zipPath}] does not exist.");
                Program.Exit(1);
            }

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
            else
            {
                zipPassword = NeonHelper.ReadConsolePassword("ZIP Password: ");
            }

            using (var input = new FileStream(zipPath, FileMode.Open, FileAccess.ReadWrite))
            {
                using (var zip = new ZipFile(input))
                {
                    if (!string.IsNullOrWhiteSpace(zipPassword))
                    {
                        zip.Password = zipPassword;
                    }

                    foreach (ZipEntry zipEntry in zip)
                    {
                        if (!zipEntry.IsFile)
                        {
                            continue;
                        }

                        passwordCount++;

                        using (var zipStream = zip.GetInputStream(zipEntry))
                        {
                            using (var passwordStream = new FileStream(Path.Combine(KubeHelper.PasswordsFolder, zipEntry.Name), FileMode.Create, FileAccess.ReadWrite))
                            {
                                zipStream.CopyTo(passwordStream);
                            }
                        }
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine($"[{passwordCount}] passwords imported.");
            Program.Exit(0);
            await Task.CompletedTask;
        }
    }
}
