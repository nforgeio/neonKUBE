//-----------------------------------------------------------------------------
// FILE:	    GitHubTagApi.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;
using Neon.Retry;

using Octokit;

namespace Neon.Deployment
{
    /// <summary>
    /// Used to manage GitHub repo tags.
    /// </summary>
    public class GitHubTagApi
    {
        /// <summary>
        /// Internal constructor.
        /// </summary>
        internal GitHubTagApi()
        {
        }

        /// <summary>
        /// Lists the current tags for a GitHub repo.
        /// </summary>
        /// <param name="repo">Identifies the target repo.</param>
        /// <returns>The list of <see cref="RepositoryTag"/> instances.</returns>
        public IReadOnlyList<RepositoryTag> List(string repo)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(repo), nameof(repo));

            var repoPath = GitHubRepoPath.Parse(repo);
            var client   = GitHub.CreateGitHubClient(repo);
            
            return client.Repository.GetAllTags(repoPath.Owner, repoPath.Repo).Result;
        }
    }
}
