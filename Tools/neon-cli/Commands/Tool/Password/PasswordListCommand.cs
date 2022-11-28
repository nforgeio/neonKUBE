//-----------------------------------------------------------------------------
// FILE:	    PasswordListCommand.cs
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
using Neon.Kube;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>password list</b> command.
    /// </summary>
    [Command]
    public class PasswordListCommand : CommandBase
    {
        private const string usage = @"
Lists passwords.

USAGE:

    neon tool password list|ls
";

        /// <inheritdoc/>
        public override string[] Words => new string[] { "tool", "password", "list" }; 

        /// <inheritdoc/>
        public override string[] AltWords => new string[] { "tool", "password", "ls" }; 

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

            foreach (var path in Directory.GetFiles(KubeHelper.PasswordsFolder).OrderBy(path => path.ToLowerInvariant()))
            {
                Console.WriteLine(Path.GetFileName(path));
            }

            Program.Exit(0);
            await Task.CompletedTask;
        }
    }
}
