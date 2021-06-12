//-----------------------------------------------------------------------------
// FILE:	    GitHubReleasesApi.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

using Neon.Common;
using Neon.Net;

using Octokit;

namespace Neon.Deployment
{
    /// <summary>
    /// Used to publish and manage GitHub releases.
    /// </summary>
    public class GitHubReleasesApi
    {
        /// <summary>
        /// Internal constructor.
        /// </summary>
        internal GitHubReleasesApi()
        {
        }

#if SKIP
        /// <summary>
        /// Creates a GitHub release.
        /// </summary>
        /// <param name="repo">Identifies the target repo.</param>
        /// <param name="tagName">Specifies the name of the tag the release will reference.</param>
        /// <param name="releaseName">Optionally specifies the release name (defaults to <paramref name="tagName"/>).</param>
        /// <param name="body">Optionally specifies the release notes (markdown formatted).</param>
        /// <param name="isDraft">Optionally indicates that this is a draft release.</param>
        /// <param name="isPrerelease">Optionally indicates that the release is not production ready.</param>
        /// <param name="branch">Optionally identifies the branch to be tagged.  This defaults to <b>master</b> or <b>main</b> when either of those branches are already present.</param>
        /// <returns>The created <see cref="Release"/>.</returns>
        /// <remarks>
        /// <para>
        /// If the <paramref name="tagName"/> doesn't already exist in the repo, this method will
        /// tag the latest commit on the specified (or default) <paramref name="branch"/> in the 
        /// target repo and before creating the release.
        /// </para>
        /// </remarks>
        public Release Create(string repo, string tagName = null, string releaseName = null, string body = null, bool isDraft = false, bool isPrerelease = false, string branch = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(tagName), nameof(tagName));

            releaseName = releaseName ?? tagName;

            var repoPath = GitHubRepoPath.Parse(repo);
            var client   = GitHub.CreateGitHubClient();
            var tags     = client.Repository.GetAllTags(repoPath.Owner, repoPath.Repo).Result;
            var tag      = tags.SingleOrDefault(tag => tag.Name == tagName);

            // Tag the specified or default branch when the tag doesn't already exist.
            // Note that we may need to 

            if (tag == null)
            {
                if (string.IsNullOrEmpty(branch))
                {
                    // Identify the default branch.

                    var branches = client.Repository.Branch.GetAll(repoPath.Owner, repoPath.Repo).Result;

                    foreach (var branchDetails in branches)
                    {
                        if (branchDetails.Name == "master")
                        {
                            branch = "master";
                            break;
                        }
                        else if (branchDetails.Name == "main")
                        {
                            branch = "main";
                            break;
                        }
                    }

                    var newTag = new NewTag()
                    {
                        Message = $"release-tag: {tagName}",
                        Tag     = tagName,
                        Object  = "",
                    };

                    client.Git.Tag.Create(repoPath.Owner, repoPath.Repo, newTag);
                }
            }

            // Create the release.

            var release = new NewRelease(tagName)
            {
                Name       = releaseName,
                Draft      = isDraft,
                Prerelease = isPrerelease,
                Body       = body
            };

            return client.Repository.Release.Create(repoPath.Owner, repoPath.Repo, release).Result;
        }

        /// <summary>
        /// Updates a release.
        /// </summary>
        /// <param name="repo">Identifies the target repository.</param>
        /// <param name="release">The updated release release.</param>
        /// <returns>The updated release.</returns>
        public Release Update(string repo, ReleaseUpdate release)
        {
            Covenant.Requires<ArgumentNullException>(release != null, nameof(release));

            var repoPath = GitHubRepoPath.Parse(repo);
            var client   = GitHub.CreateGitHubClient();

            return client.Repository.Release.Edit(repoPath.Owner, repoPath, 1, release).Result;
        }

        /// <summary>
        /// List the releases for a GitHub repo.
        /// </summary>
        /// <returns>The list of releases.</returns>
        public List<Release> List(string repo)
        {
            var repoPath = GitHubRepoPath.Parse(repo);
            var client   = GitHub.CreateGitHubClient();

            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns information about a specific repo release.
        /// </summary>
        /// <param name="repo">Identifies the target repository.</param>
        /// <param name="tag">The target release tag.</param>
        /// <returns>The release information or <c>null</c> when the requested release doesn't exist.</returns>
        public Release Get(string repo, string tag)
        {
            var repoPath = GitHubRepoPath.Parse(repo);
            var client   = GitHub.CreateGitHubClient();

            throw new NotImplementedException();
        }

        /// <summary>
        /// Deletes a specific repo release.
        /// </summary>
        /// <param name="repo">Identifies the target repository.</param>
        /// <param name="tag">The target release tag.</param>
        /// <remarks>
        /// <note>
        /// This fails silently if the release doesn't exist.
        /// </note>
        /// </remarks>
        public void Remove(string repo, string tag)
        {
            var repoPath = GitHubRepoPath.Parse(repo);
            var client   = GitHub.CreateGitHubClient();

            throw new NotImplementedException();
        }

        /// <summary>
        /// Uploads an artifact to a GitHub release.  Any existing artifiact with same name will be replaced.
        /// </summary>
        /// <param name="repo">Identifies the target repository.</param>
        /// <param name="tag">The target release tag.</param>
        /// <param name="path">Path to the source artifact file.</param>
        /// <param name="name">Optionally specifies the file name to assign to the artifact.  This defaults to the file name in <paramref name="path"/>.</param>
        /// <param name="displayLabel">Optionally specifies the display label.</param>
        public void Upload(string repo, string tag, string path, string name = null, string displayLabel = null)
        {
            var repoPath = GitHubRepoPath.Parse(repo);
            var client   = GitHub.CreateGitHubClient();

            throw new NotImplementedException();
        }
#endif
    }
}
