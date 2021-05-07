//-----------------------------------------------------------------------------
// FILE:	    WorkflowRunDeleteCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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

using Octokit;

namespace GHTool
{
    /// <summary>
    /// Implements the <b>workflow</b> command.
    /// </summary>
    [Command]
    public class WorkflowRunDeleteCommand : CommandBase
    {
        private const string usage = @"
Deletes 

USAGE:

    neon workflow-run delete REPO WORKFLOW-NAME [AGE-IN-DAYS]

ARGUMENTS:

    REPO            - target repository, like: 

                        [SERVER]/OWNER/REPO

    WORKFLOW-NAME   - target workflow name

    AGE-IN-DAYS     - optionally specifies the minimum age for runs
                      to be deleted in days.  This defaults to 0 which
                      deletes all runs.
";

        /// <inheritdoc/>
        public override string[] Words => new string[] { "workflow", "run", "delete" };

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

            var repoArg = commandLine.Arguments.ElementAt(0);
            var nameArg = commandLine.Arguments.ElementAt(1);
            var ageArg  = commandLine.Arguments.ElementAt(2);

            if (string.IsNullOrEmpty(repoArg))
            {
                Console.Error.WriteLine("*** ERROR: [REPO] argument is required.");
                Program.Exit(1);
            }

            if (string.IsNullOrEmpty(nameArg))
            {
                Console.Error.WriteLine("*** ERROR: [WORKFLOW-NAME] argument is required.");
                Program.Exit(1);
            }

            if (string.IsNullOrEmpty(ageArg))
            {
                ageArg = "0";
            }

            var repoPath      = GitHubRepoPath.Parse(repoArg);
            var workflowName = nameArg;
            var age          = Math.Max(int.Parse(ageArg), 0);

            var client = new GitHubClient(Program.ProductHeader)
            {
                Credentials = Program.GitHubCredentials
            };
        }
    }
}
