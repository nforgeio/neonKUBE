//-----------------------------------------------------------------------------
// FILE:	    HelmCommand.cs
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
using Neon.Kube;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>helm</b> command.
    /// </summary>
    [Command]
    public class HelmCommand : CommandBase
    {
        private const string usage = @"
Extended [neon-cli] related commands.

USAGE:

    neon helm COMMAND [ARGS...]

REMARKS:

    This command executes the standard [helm] utility, passing thru any
    commands and arguments.

HELM COMMANDS (note that you'll need to prefix these with ""neon""):

";

        /// <inheritdoc/>
        public override string[] Words => new string[] { "helm" };

        /// <inheritdoc/>
        public override bool CheckOptions => false;

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
            NeonHelper.Execute(Program.HelmPath, Array.Empty<object>());
        }

        /// <inheritdoc/>
        public override async Task RunAsync(CommandLine commandLine)
        {
            if (commandLine.Items.Length == 0)
            {
                Help();
                return;
            }

            Program.Exit(NeonHelper.Execute(Program.HelmPath, commandLine.Items));
            await Task.CompletedTask;
        }
    }
}
