//-----------------------------------------------------------------------------
// FILE:	    ActionRunDeleteCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Deployment;
using Neon.Retry;

namespace GHTool
{
    /// <summary>
    /// Implements the <b>action run delete</b> command.
    /// </summary>
    [Command]
    public class ActionRunDeleteCommand : CommandBase
    {
        private const string usage = @"
Deletes 

USAGE:

    neon action run delete REPO [WORKFLOW-NAME] [--max-age-days=AGE]

ARGUMENTS:

    REPO            - target repository, like: 

                        [SERVER]/OWNER/REPO

    WORKFLOW-NAME   - optional target workflow name

    AGE             - optionally specifies the maximum age for runs
                      to be  retained in days.  This defaults to 0 
                      which deletes all runs.
";

        /// <inheritdoc/>
        public override string[] Words => new string[] { "action", "run", "delete" };

        /// <inheritdoc/>
        public override string[] ExtendedOptions => new string[] { "--max-age-days" };

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
                Help();
                return;
            }

            var repoArg = commandLine.Arguments.ElementAtOrDefault(0);
            var nameArg = commandLine.Arguments.ElementAtOrDefault(1);
            var ageArg  = commandLine.GetOption("--max-age-days", "0");

            if (string.IsNullOrEmpty(repoArg))
            {
                Console.Error.WriteLine("*** ERROR: [REPO] argument is required.");
                Program.Exit(1);
            }

            var repoPath     = GitHubRepoPath.Parse(repoArg);
            var workflowName = nameArg;
            var maxAge       = TimeSpan.FromDays(Math.Max(int.Parse(ageArg), 0));
            var deleted      = await GitHub.Actions.DeleteRunsAsync(repoPath.ToString(), workflowName, maxAge);

            Console.WriteLine();
            Console.WriteLine($"[{deleted}] workflow runs deleted");
            Console.WriteLine();
        }
    }
}
