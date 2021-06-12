//-----------------------------------------------------------------------------
// FILE:	    GitHubReleaseApi.cs
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
using System.Net;
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
    public class GitHubReleaseApi
    {
        /// <summary>
        /// Internal constructor.
        /// </summary>
        internal GitHubReleaseApi()
        {
        }

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
        /// <returns>The newly created <see cref="Release"/>.</returns>
        /// <remarks>
        /// <para>
        /// If the <paramref name="tagName"/> doesn't already exist in the repo, this method will
        /// tag the latest commit on the specified (or default) <paramref name="branch"/> in the 
        /// target repo and before creating the release.
        /// </para>
        /// </remarks>
        public Release Create(string repo, string tagName, string releaseName = null, string body = null, bool isDraft = false, bool isPrerelease = false, string branch = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(repo), nameof(repo));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(tagName), nameof(tagName));

            releaseName = releaseName ?? tagName;

            var repoPath = GitHubRepoPath.Parse(repo);
            var client   = GitHub.CreateGitHubClient(repo);
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
        /// <param name="release">The target release.</param>
        /// <param name="releaseUpdate">The updated release release.</param>
        /// <returns>The updated release.</returns>
        public Release Update(string repo, Release release, ReleaseUpdate releaseUpdate)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(repo), nameof(repo));
            Covenant.Requires<ArgumentNullException>(release != null, nameof(release));
            Covenant.Requires<ArgumentNullException>(releaseUpdate != null, nameof(releaseUpdate));

            var repoPath = GitHubRepoPath.Parse(repo);
            var client   = GitHub.CreateGitHubClient(repo);

            return client.Repository.Release.Edit(repoPath.Owner, repoPath.Repo, release.Id, releaseUpdate).Result;
        }

        /// <summary>
        /// List the releases for a GitHub repo.
        /// </summary>
        /// <returns>The list of releases.</returns>
        public IReadOnlyList<Release> List(string repo)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(repo), nameof(repo));

            var repoPath = GitHubRepoPath.Parse(repo);
            var client   = GitHub.CreateGitHubClient(repo);

            return client.Repository.Release.GetAll(repoPath.Owner, repoPath.Repo).Result;
        }

        /// <summary>
        /// Returns information about a specific repo release.
        /// </summary>
        /// <param name="repo">Identifies the target repository.</param>
        /// <param name="tagName">The tag name for the release.</param>
        /// <returns>The release information or <c>null</c> when the requested release doesn't exist.</returns>
        public Release Get(string repo, string tagName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(repo), nameof(repo));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(tagName), nameof(tagName));

            var repoPath = GitHubRepoPath.Parse(repo);
            var client   = GitHub.CreateGitHubClient(repo);

            try
            {
                return client.Repository.Release.Get(repoPath.Owner, repoPath.Repo, tagName).Result;
            }
            catch (Exception e)
            {
                if (e.Find<NotFoundException>() != null)
                {
                    return null;
                }

                throw;
            }
        }

        /// <summary>
        /// Uploads an asset file to a GitHub release.  Any existing artifiact with same name will be replaced.
        /// </summary>
        /// <param name="repo">Identifies the target repository.</param>
        /// <param name="release">The target release.</param>
        /// <param name="assetPath">Path to the source asset file.</param>
        /// <param name="assetName">Optionally specifies the file name to assign to the asset.  This defaults to the file name in <paramref name="assetPath"/>.</param>
        /// <param name="displayLabel">Optionally specifies the display label.</param>
        /// <param name="contentType">Optionally specifies the asset's <b>Content-Type</b>.  This defaults to: <b> application/octet-stream</b></param>
        /// <returns>The new <see cref="ReleaseAsset"/>.</returns>
        public ReleaseAsset UploadAsset(string repo, Release release, string assetPath, string assetName = null, string displayLabel = null, string contentType = "application/octet-stream")
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(repo), nameof(repo));
            Covenant.Requires<ArgumentNullException>(release != null, nameof(release));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(assetPath), nameof(assetPath));

            var repoPath = GitHubRepoPath.Parse(repo);
            var client   = GitHub.CreateGitHubClient(repo);

            using (var assetStream = File.OpenRead(assetPath))
            {
                if (string.IsNullOrEmpty(assetName))
                {
                    assetName = Path.GetFileName(assetPath);
                }

                var upload = new ReleaseAssetUpload()
                {
                    FileName    = assetName,
                    ContentType = contentType,
                    RawData     = assetStream
                };

                return client.Repository.Release.UploadAsset(release, upload).Result;
            }
        }

        /// <summary>
        /// Uploads an asset stream to a GitHub release.  Any existing artifiact with same name will be replaced.
        /// </summary>
        /// <param name="repo">Identifies the target repository.</param>
        /// <param name="release">The target release.</param>
        /// <param name="assetStream">The asset source stream.</param>
        /// <param name="assetName">Specifies the file name to assign to the asset.</param>
        /// <param name="displayLabel">Optionally specifies the display label.</param>
        /// <param name="contentType">Optionally specifies the asset's <b>Content-Type</b>.  This defaults to: <b> application/octet-stream</b></param>
        /// <returns>The new <see cref="ReleaseAsset"/>.</returns>
        public ReleaseAsset UploadAsset(string repo, Release release, Stream assetStream, string assetName, string displayLabel = null, string contentType = "application/octet-stream")
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(repo), nameof(repo));
            Covenant.Requires<ArgumentNullException>(release != null, nameof(release));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(assetName), nameof(assetName));
            Covenant.Requires<ArgumentNullException>(assetStream != null, nameof(assetStream));

            var repoPath = GitHubRepoPath.Parse(repo);
            var client   = GitHub.CreateGitHubClient(repo);

            var upload = new ReleaseAssetUpload()
            {
                FileName    = assetName,
                ContentType = contentType,
                RawData     = assetStream
            };

            return client.Repository.Release.UploadAsset(release, upload).Result;
        }

        /// <summary>
        /// Returns the URI that can be used to download a release asset.
        /// </summary>
        /// <param name="release">The target release.</param>
        /// <param name="asset">The target asset.</param>
        /// <returns>The asset URI.</returns>
        public string GetAssetDownloadUri(Release release, ReleaseAsset asset)
        {
            Covenant.Requires<ArgumentNullException>(release != null, nameof(release));
            Covenant.Requires<ArgumentNullException>(asset != null, nameof(asset));

            var releasedAsset = release.Assets.SingleOrDefault(a => a.Id == asset.Id);

            if (releasedAsset == null)
            {
                throw new DeploymentException($"Asset [id={asset.Id}] is not present in release [id={release.Id}].");
            }

            return releasedAsset.BrowserDownloadUrl;
        }

        /// <summary>
        /// Deletes a specific repo release.
        /// </summary>
        /// <param name="repo">Identifies the target repository.</param>
        /// <param name="tagName">The target release tag name.</param>
        /// <remarks>
        /// <note>
        /// This fails silently if the release doesn't exist.
        /// </note>
        /// </remarks>
        public void Remove(string repo, string tagName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(repo), nameof(repo));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(tagName), nameof(tagName));

            var repoPath = GitHubRepoPath.Parse(repo);
            var client   = GitHub.CreateGitHubClient(repo);
            var release  = Get(repo, tagName);

            if (release != null)
            {
                client.Repository.Release.Delete(repoPath.Owner, repoPath.Repo, release.Id).Wait();
            }
        }
    }
}
