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
    /// Implements the <b>workflow</b> command.
    /// </summary>
    [Command]
    public class WorkflowRunDeleteCommand : CommandBase
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Holds important state from a workflow run.
        /// </summary>
        private class RunInfo
        {
            /// <summary>
            /// The run ID.
            /// </summary>
            public long Id { get; set; }

            /// <summary>
            /// The workflow name.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// The status.
            /// </summary>
            public string Status { get; set; }

            /// <summary>
            /// The time (UTC) when the run was last updated.
            /// </summary>
            public DateTime UpdatedAtUtc { get; set; }
        }

        //---------------------------------------------------------------------
        // Implementation

        private const string usage = @"
Deletes 

USAGE:

    neon workflow run delete REPO WORKFLOW-NAME [--age=AGE-IN-DAYS]

ARGUMENTS:

    REPO            - target repository, like: 

                        [SERVER]/OWNER/REPO

    WORKFLOW-NAME   - optional target workflow name

    AGE-IN-DAYS     - optional the minimum age for runs to be 
                      deleted, in days.  This defaults to 0 
                      which deletes all runs.
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

            var repoArg = commandLine.Arguments.ElementAtOrDefault(0);
            var nameArg = commandLine.Arguments.ElementAtOrDefault(1);
            var ageArg  = commandLine.GetOption("--age", "0");

            if (string.IsNullOrEmpty(repoArg))
            {
                Console.Error.WriteLine("*** ERROR: [REPO] argument is required.");
                Program.Exit(1);
            }

            var repoPath      = GitHubRepoPath.Parse(repoArg);
            var workflowName = nameArg;
            var maxAge          = TimeSpan.FromDays(Math.Max(int.Parse(ageArg), 0));

            using (var client = new HttpClient())
            {
                var retry = new ExponentialRetryPolicy(TransientDetector.NetworkOrHttp, 5);

                client.BaseAddress                         = new Uri("https://api.github.com");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Program.GitHubPAT);

                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("neonforge.com", "0"));
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

                // List all of the workflow runs for the repo, paging to get all of them.
                //
                //      https://docs.github.com/en/rest/reference/actions#list-workflow-runs-for-a-repository

                var runs = new List<RunInfo>();
                var page = 1;

                while (true)
                {
                    var response = await retry.InvokeAsync(
                        async () =>
                        {
                            var request = new HttpRequestMessage(HttpMethod.Get, $"/repos/{repoPath.Owner}/{repoPath.Repo}/actions/runs?page={page}");

                            return await client.SendAsync(request);
                        });

                    response.EnsureSuccessStatusCode();

                    var json   = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<dynamic>(json);

                    var workflowRuns = result.workflow_runs;

                    if (workflowRuns.Count == 0)
                    {
                        // We've seen all of the runs.

                        break;
                    }

                    foreach (var run in workflowRuns)
                    {
                        runs.Add(
                            new RunInfo()
                            {
                                Id           = run.id,
                                Name         = run.name,
                                Status       = run.status,
                                UpdatedAtUtc = run.updated_at
                            });
                    }

                    Console.WriteLine($"page: {page}");
                    page++;
                }

                // $todo(jefflill):
                //
                // I'm just going to delete all of the runs older than the minimum for now.
                // We'll come back later and filter by workflow name.
                //
                //      https://docs.github.com/en/rest/reference/actions#delete-a-workflow-run

                var minDate     = DateTime.UtcNow - maxAge;
                var deleteCount = 0;

                foreach (var run in runs.Where(run => run.UpdatedAtUtc <= minDate && run.Status == "completed"))
                {
                    var response = await retry.InvokeAsync(
                        async () =>
                        {
                            var request = new HttpRequestMessage(HttpMethod.Delete, $"/repos/{repoPath.Owner}/{repoPath.Repo}/actions/runs/{run.Id}");

                            return await client.SendAsync(request);
                        });

                    // We're also seeing some 500s but I'm not sure why.  We'll ignore these
                    // for now.

                    if (response.StatusCode == HttpStatusCode.InternalServerError)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2));  // Pause in case this is a rate-limit thing
                        continue;
                    }

                    // We're seeing 403s for some runs, so we'll ignore those.

                    if (response.StatusCode != HttpStatusCode.Forbidden)
                    {
                        response.EnsureSuccessStatusCode();
                    }

                    deleteCount++;
                    if (deleteCount % 30 == 0)
                    {
                        Console.WriteLine($"deleted: {deleteCount}");
                    }
                }
            }
        }
    }
}
