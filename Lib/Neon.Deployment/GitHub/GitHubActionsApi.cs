//-----------------------------------------------------------------------------
// FILE:	    GitHubActionsApi.cs
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

namespace Neon.Deployment
{
    /// <summary>
    /// Implements GitHub Actions operations.
    /// </summary>
    public class GitHubActionsApi
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

        /// <summary>
        /// Internal constructor.
        /// </summary>
        internal GitHubActionsApi()
        {
            GitHub.GetCredentials();
            GitHub.EnsureCredentials();
        }

        /// <summary>
        /// <para>
        /// Deletes workflow runs from a GitHub repo.
        /// </para>
        /// <note>
        /// Only completed runs will be deleted.
        /// </note>
        /// </summary>
        /// <param name="repo">Identifies the target repository.</param>
        /// <param name="workflowName">
        /// Optionally specifies the workflow whose runs are to be deleted otherwise
        /// runs from all workflows in the repo will be deleted.
        /// </param>
        /// <param name="maxAge">
        /// Optionally specifies the age at which workflow runs are to be deleted.  
        /// This defaults to deleting all runs.
        /// </param>
        /// <returns>The number of runs deleted.</returns>
        public int DeleteRuns(string repo, string workflowName = null, TimeSpan maxAge = default)
        {
            return DeleteRunsAsync(repo, workflowName, maxAge).Result;
        }

        /// <summary>
        /// <para>
        /// Deletes workflow runs from a GitHub repo.
        /// </para>
        /// <note>
        /// Only completed runs will be deleted.
        /// </note>
        /// </summary>
        /// <param name="repo">Identifies the target repository.</param>
        /// <param name="workflowName">
        /// Optionally specifies the workflow whose runs are to be deleted otherwise
        /// runs from all workflows in the repo will be deleted.
        /// </param>
        /// <param name="maxAge">
        /// Optionally specifies the age at which workflow runs are to be deleted.  
        /// This defaults to deleting all runs.
        /// </param>
        /// <returns>The number of runs deleted.</returns>
        public async Task<int> DeleteRunsAsync(string repo, string workflowName = null, TimeSpan maxAge = default)
        {
            var repoPath    = GitHubRepoPath.Parse(repo);
            var deleteCount = 0;

            using (var client = new HttpClient())
            {
                var retry = new ExponentialRetryPolicy(TransientDetector.NetworkOrHttp, 5);

                client.BaseAddress                         = new Uri("https://api.github.com");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GitHub.AccessToken);

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

                    var json   = response.Content.ReadAsStringAsync().Result;
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

                    page++;
                }

                // Here's the reference for deleting runs:
                //
                //      https://docs.github.com/en/rest/reference/actions#delete-a-workflow-run

                var minDate      = DateTime.UtcNow - maxAge;
                var selectedRuns = runs.Where(run => run.UpdatedAtUtc <= minDate && run.Status == "completed");

                if (!string.IsNullOrEmpty(workflowName))
                {
                    selectedRuns = selectedRuns.Where(run => run.Name.Equals(workflowName, StringComparison.InvariantCultureIgnoreCase));
                }

                foreach (var run in selectedRuns)
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
                        Task.Delay(TimeSpan.FromSeconds(2)).Wait();     // Pause in case this is a rate-limit thing
                        continue;
                    }

                    // We're seeing 403s for some runs, so we'll ignore those too.

                    if (response.StatusCode != HttpStatusCode.Forbidden)
                    {
                        response.EnsureSuccessStatusCode();
                    }

                    deleteCount++;
                }
            }

            return deleteCount;
        }
    }
}
