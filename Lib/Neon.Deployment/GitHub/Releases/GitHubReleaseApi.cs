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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Kube.Models.Headend;
using Neon.Net;
using Neon.Retry;

using Octokit;

namespace Neon.Deployment
{
    /// <summary>
    /// Used to publish and manage GitHub releases.
    /// </summary>
    /// <remarks>
    /// <note>
    /// This API doesn't currently support modifying assets of
    /// of published releases although GitHub does support this.
    /// We may add this functionality in the future.
    /// </note>
    /// </remarks>
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
        /// <param name="tagName">Specifies the tag to be referenced by the release.</param>
        /// <param name="releaseName">Optionally specifies the release name (defaults to <paramref name="tagName"/>).</param>
        /// <param name="body">Optionally specifies the markdown formatted release notes.</param>
        /// <param name="draft">Optionally indicates that the release won't be published immediately.</param>
        /// <param name="prerelease">Optionally indicates that the release is not production ready.</param>
        /// <param name="branch">Optionally identifies the branch to be tagged.  This defaults to <b>master</b> or <b>main</b> when either of those branches are already present.</param>
        /// <returns>The newly created <see cref="Release"/>.</returns>
        /// <remarks>
        /// <para>
        /// If the <paramref name="tagName"/> doesn't already exist in the repo, this method will
        /// tag the latest commit on the specified <paramref name="branch"/> or else the defailt branch
        /// in the target repo and before creating the release.
        /// </para>
        /// </remarks>
        public Release Create(string repo, string tagName, string releaseName = null, string body = null, bool draft = false, bool prerelease = false, string branch = null)
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
                Draft      = draft,
                Prerelease = prerelease,
                Body       = body
            };

            return client.Repository.Release.Create(repoPath.Owner, repoPath.Repo, release).Result;
        }

        /// <summary>
        /// Updates a GitHub release.
        /// </summary>
        /// <param name="repo">Identifies the target repository.</param>
        /// <param name="release">Specifies the release being updated.</param>
        /// <param name="releaseUpdate">Specifies the revisions.</param>
        /// <returns>The updated release.</returns>
        /// <remarks>
        /// <para>
        /// To update a release, you'll first need to:
        /// </para>
        /// <list type="number">
        /// <item>
        /// Obtain a <see cref="Release"/> referencing the target release returned from 
        /// <see cref="Create(string, string, string, string, bool, bool, string)"/>
        /// or by listing or getting releases.
        /// </item>
        /// <item>
        /// Obtain a <see cref="ReleaseUpdate"/> by calling <see cref="Release.ToUpdate"/>.
        /// </item>
        /// <item>
        /// Make your changes to the release update.
        /// </item>
        /// <item>
        /// Call <see cref="Update(string, Release, ReleaseUpdate)"/>, passing the 
        /// original release along with the update.
        /// </item>
        /// </list>
        /// </remarks>
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
        /// Retrieves a specific GitHub release.
        /// </summary>
        /// <param name="repo">Identifies the target repository.</param>
        /// <param name="tagName">Specifies the tag for the target release.</param>
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
        /// Returns the releases that satisfies a predicate.
        /// </summary>
        /// <param name="repo">Identifies the target repository.</param>
        /// <param name="predicate">The predicate.</param>
        /// <returns>The list of matching releases.</returns>
        public List<Release> Find(string repo, Func<Release, bool> predicate)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(repo), nameof(repo));
            Covenant.Requires<ArgumentNullException>(predicate != null, nameof(predicate));

            var repoPath = GitHubRepoPath.Parse(repo);
            var client   = GitHub.CreateGitHubClient(repo);

            return List(repo).Where(predicate).ToList();
        }

        /// <summary>
        /// Uploads an asset file to a GitHub release.  Any existing asset with same name will be replaced.
        /// </summary>
        /// <param name="repo">Identifies the target repository.</param>
        /// <param name="release">The target release.</param>
        /// <param name="assetPath">Path to the source asset file.</param>
        /// <param name="assetName">Optionally specifies the file name to assign to the asset.  This defaults to the file name in <paramref name="assetPath"/>.</param>
        /// <param name="contentType">Optionally specifies the asset's <b>Content-Type</b>.  This defaults to: <b> application/octet-stream</b></param>
        /// <returns>The new <see cref="ReleaseAsset"/>.</returns>
        /// <exception cref="NotSupportedException">Thrown when the releas has already been published.</exception>
        /// <remarks>
        /// <note>
        /// The current implementation only works for unpublished releases where <c>Draft=true</c>.
        /// </note>
        /// </remarks>
        public ReleaseAsset UploadAsset(string repo, Release release, string assetPath, string assetName = null, string contentType = "application/octet-stream")
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(repo), nameof(repo));
            Covenant.Requires<ArgumentNullException>(release != null, nameof(release));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(assetPath), nameof(assetPath));

            if (!release.Draft)
            {
                throw new NotSupportedException("Cannot upload asset to already published release.");
            }

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
        /// Uploads an asset stream to a GitHub release.  Any existing asset with same name will be replaced.
        /// </summary>
        /// <param name="repo">Identifies the target repository.</param>
        /// <param name="release">The target release.</param>
        /// <param name="assetStream">The asset source stream.</param>
        /// <param name="assetName">Specifies the file name to assign to the asset.</param>
        /// <param name="contentType">Optionally specifies the asset's <b>Content-Type</b>.  This defaults to: <b> application/octet-stream</b></param>
        /// <returns>The new <see cref="ReleaseAsset"/>.</returns>
        public ReleaseAsset UploadAsset(string repo, Release release, Stream assetStream, string assetName, string contentType = "application/octet-stream")
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
        /// <para>
        /// Returns the URI that can be used to download a GitHub release asset.
        /// </para>
        /// <note>
        /// This works only for published releases.
        /// </note>
        /// </summary>
        /// <param name="release">The target release.</param>
        /// <param name="asset">The target asset.</param>
        /// <returns>The asset URI.</returns>
        public string GetAssetUri(Release release, ReleaseAsset asset)
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
        /// Deletes a GitHub release.
        /// </summary>
        /// <param name="repo">Identifies the target repository.</param>
        /// <param name="release">The target release.</param>
        /// <remarks>
        /// <note>
        /// This fails silently if the release doesn't exist.
        /// </note>
        /// </remarks>
        public void Remove(string repo, Release release)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(repo), nameof(repo));
            Covenant.Requires<ArgumentNullException>(release != null, nameof(release));

            var repoPath = GitHubRepoPath.Parse(repo);
            var client   = GitHub.CreateGitHubClient(repo);

            client.Repository.Release.Delete(repoPath.Owner, repoPath.Repo, release.Id).Wait();
        }

        /// <summary>
        /// Uploads a multi-part download to a release and then publishes the release.
        /// </summary>
        /// <param name="repo">Identifies the target repository.</param>
        /// <param name="release">The target release.</param>
        /// <param name="path">Path to the file being uploaded.</param>
        /// <param name="version">The download version.</param>
        /// <param name="name">Optionally overrides the download file name specified by <paramref name="path"/> to initialize <see cref="Download.Name"/>.</param>
        /// <param name="filename">Optionally overrides the download file name specified by <paramref name="path"/> to initialize <see cref="Download.Filename"/>.</param>
        /// <param name="maxPartSize">Optionally overrides the maximum part size (defailts to 100 MiB).</param>
        /// <returns>The <see cref="Download"/> information.</returns>
        /// <remarks>
        /// <para>
        /// The release passed must be unpublished and you may upload other assets before calling this.
        /// </para>
        /// <note>
        /// Take care that any assets already published have names that won't conflict with the asset
        /// part names, which will be formatted like: <b>part-##</b>
        /// </note>
        /// </remarks>
        public Download UploadMultipartAsset(string repo, Release release, string path, string version, string name = null, string filename = null, long maxPartSize = (long)(100 * ByteUnits.MebiBytes))
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(repo), nameof(repo));
            Covenant.Requires<ArgumentNullException>(release != null, nameof(release));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path), nameof(path));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(version), nameof(version));

            name     = name ?? Path.GetFileName(path);
            filename = filename ?? Path.GetFileName(path);

            using (var input = File.OpenRead(path))
            {
                if (input.Length == 0)
                {
                    throw new IOException($"Asset at [{path}] cannot be empty.");
                }

                var assetPartMap = new List<Tuple<ReleaseAsset, DownloadPart>>();
                var download     = new Download() { Name = name, Version = version, Filename = filename };
                var partCount    = NeonHelper.PartitionCount(input.Length, maxPartSize);
                var partNumber   = 0;
                var partStart    = 0L;
                var cbRemaining  = input.Length;

                download.Md5   = CryptoHelper.ComputeMD5String(input);
                input.Position = 0;

                while (cbRemaining > 0)
                {
                    var partSize = Math.Min(cbRemaining, maxPartSize);
                    var part     = new DownloadPart()
                    {
                        Number = partNumber,
                        Size   = partSize,
                    };

                    // We're going to use a substream to compute the MD5 hash for the part
                    // as well as to actually upload the part to the GitHub release.

                    using (var partStream = new SubStream(input, partStart, partSize))
                    {
                        part.Md5 = CryptoHelper.ComputeMD5String(partStream);
                        partStream.Position = 0;

                        var asset = GitHub.Release.UploadAsset(repo, release, partStream, $"part-{partNumber:0#}");

                        assetPartMap.Add(new Tuple<ReleaseAsset, DownloadPart>(asset, part));
                    }

                    download.Parts.Add(part);

                    // Loop to handle the next part (if any).

                    partNumber++;
                    partStart += partSize;
                    cbRemaining -= partSize;
                }

                download.Size = download.Parts.Sum(part => part.Size);

                // Publish the release.

                var releaseUpdate = release.ToUpdate();

                releaseUpdate.Draft = false;

                release = GitHub.Release.Update(repo, release, releaseUpdate);

                // Now that the release has been published, we can go back and fill in
                // the asset URIs for each of the download parts.

                foreach (var item in assetPartMap)
                {
                    item.Item2.Uri = GitHub.Release.GetAssetUri(release, item.Item1);
                }

                return download;
            }
        }

        /// <summary>
        /// Synchronously downloads and assembles a multi-part file from assets in a GitHub release.
        /// </summary>
        /// <param name="download">The download information.</param>
        /// <param name="targetPath">The target file path.</param>
        /// <param name="progressAction">Optionally specifies an action to be called with the the percentage downloaded.</param>
        /// <param name="retry">Optionally specifies the retry policy.  This defaults to a reasonable policy.</param>
        /// <param name="partTimeout">Optionally specifies the HTTP download timeout for each part (defaults to 10 minutes).</param>
        public void Download(
          Download                          download, 
          string                            targetPath, 
          GitHubDownloadProgressDelegate    progressAction = null, 
          IRetryPolicy                      retry          = null,
          TimeSpan                          partTimeout    = default)
        {
            DownloadAsync(download, targetPath, progressAction, partTimeout, retry).Wait();
        }

        /// <summary>
        /// Asynchronously downloads and assembles a multi-part file from assets in a GitHub release.
        /// </summary>
        /// <param name="download">The download information.</param>
        /// <param name="targetPath">The target file path.</param>
        /// <param name="progressAction">Optionally specifies an action to be called with the the percentage downloaded.</param>
        /// <param name="partTimeout">Optionally specifies the HTTP download timeout for each part (defaults to 10 minutes).</param>
        /// <param name="retry">Optionally specifies the retry policy.  This defaults to a reasonable policy.</param>
        /// <param name="cancellationToken">Optionally specifies the operation cancellation token.</param>
        /// <returns>The path to the downloaded file.</returns>
        /// <exception cref="IOException">Thrown when the download is corrupt.</exception>
        /// <remarks>
        /// <para>
        /// This method downloads the file specified by <paramref name="download"/> to the folder specified, creating 
        /// the folder first when required.  The file will be downloaded in parts, where each part will be validated
        /// by comparing the part's MD5 hash (when present) with the computed value.  The output file will be named 
        /// <see cref="Download.Name"/> and the overall MD5 hash will also be saved using the same file name but
        /// <b>adding</b> the <b>.md5</b> extension.
        /// </para>
        /// <para>
        /// This method will continue downloading a partially downloaded file.  This works by validating the already
        /// downloaded parts against their MD5 hashes and then continuing part downloads after the last valid part.
        /// Nothing will be downloaded when the existing file is fully formed.
        /// </para>
        /// <note>
        /// The target files (output and MD5) will be deleted when download appears to be corrupt.
        /// </note>
        /// </remarks>
        public async Task<string> DownloadAsync(
            Download                        download, 
            string                          targetPath, 
            GitHubDownloadProgressDelegate  progressAction    = null, 
            TimeSpan                        partTimeout       = default, 
            IRetryPolicy                    retry             = null,
            CancellationToken               cancellationToken = default)
        {
            Covenant.Requires<ArgumentNullException>(download != null, nameof(download));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(targetPath), nameof(targetPath));

            retry = retry ?? new ExponentialRetryPolicy(TransientDetector.NetworkOrHttp, maxAttempts: 5);

            if (partTimeout <= TimeSpan.Zero)
            {
                partTimeout = TimeSpan.FromMinutes(10);
            }

            var targetFolder = Path.GetDirectoryName(targetPath);

            Directory.CreateDirectory(targetFolder);

            var targetMd5Path  = Path.Combine(Path.GetDirectoryName(targetPath), Path.GetFileName(targetPath) + ".md5");
            var nextPartNumber = 0;

            // If the target file already exists along with its MD5 hash file, then compare the
            // existing MD5 against the download's MD5 as well as the computed MD5 for the current
            // file and skip the download when the match.

            if (File.Exists(targetPath) && File.Exists(targetMd5Path) && File.ReadAllText(targetMd5Path).Trim() == download.Md5)
            {
                using (var input = File.OpenRead(targetPath))
                {
                    if (CryptoHelper.ComputeMD5String(input) == download.Md5)
                    {
                        return targetPath;
                    }
                }
            }

            NeonHelper.DeleteFile(targetMd5Path);   // We'll recompute this below

            // Validate the parts of any existing target file to determine where
            // to start downloading missing parts.

            if (File.Exists(targetPath))
            {
                using (var output = new FileStream(targetPath, System.IO.FileMode.Open, FileAccess.ReadWrite))
                {
                    var pos = 0L;

                    foreach (var part in download.Parts.OrderBy(part => part.Number))
                    {
                        // Handle a partially downloaded part.  We're going to truncate the file to
                        // remove the partial part and then break to start re-downloading the part.

                        if (output.Length < pos + part.Size)
                        {
                            output.SetLength(pos);

                            nextPartNumber = part.Number;
                            break;
                        }

                        // Validate the part MD5.  We're going to truncate the file to remove the
                        // partial part and then break to start re-downloading the part.

                        using (var partStream = new SubStream(output, pos, part.Size))
                        {
                            if (CryptoHelper.ComputeMD5String(partStream) != part.Md5)
                            {
                                output.SetLength(pos);

                                nextPartNumber = part.Number;
                                break;
                            }
                        }

                        pos           += part.Size;
                        nextPartNumber = part.Number + 1;
                    }
                }
            }

            // Download any remaining parts.

            if (progressAction != null && !progressAction.Invoke(0))
            {
                return targetPath;
            }

            if (nextPartNumber > download.Parts.Count)
            {
                progressAction?.Invoke(100);
                return targetPath;
            }

            try
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = partTimeout;

                    using (var output = new FileStream(targetPath, System.IO.FileMode.OpenOrCreate, FileAccess.ReadWrite))
                    {
                        // Determine the starting position of the next part.

                        var pos = download.Parts
                            .Where(part => part.Number < nextPartNumber)
                            .Sum(part => part.Size);

                        // Download the remaining parts.

                        foreach (var part in download.Parts
                            .Where(part => part.Number >= nextPartNumber)
                            .OrderBy(part => part.Number))
                        {
                            await retry.InvokeAsync(
                                async () =>
                                {
                                    output.Position = pos;

                                    var response = await httpClient.GetAsync(part.Uri, cancellationToken);

                                    response.EnsureSuccessStatusCode();

                                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                                    {
                                        await contentStream.CopyToAsync(output, cancellationToken);
                                    }
                                });

                            // Ensure that the downloaded part size matches the specification.

                            if (output.Position - pos != part.Size)
                            {
                                throw new IOException($"[{download.Name}]: Part [{part.Number}] actual size [{output.Position - pos}] does not match the expected size [{part.Size}].");
                            }

                            // Ensure that the downloaded part MD5 matches the specification.

                            using (var subStream = new SubStream(output, pos, part.Size))
                            {
                                var actualMd5 = CryptoHelper.ComputeMD5String(subStream);

                                if (actualMd5 != part.Md5)
                                {
                                    throw new IOException($"[{download.Name}]: Part [{part.Number}] actual MD5 [{actualMd5}] does not match the expected MD5 [{part.Md5}].");
                                }
                            }

                            pos += part.Size;

                            if (progressAction != null && !progressAction.Invoke((int)(100.0 * ((double)part.Number / (double)download.Parts.Count))))
                            {
                                return targetPath;
                            }
                        }

                        if (output.Length != download.Size)
                        {
                            throw new IOException($"[{download.Name}]: Expected size [{download.Size}] got [{output.Length}].");
                        }
                    }

                    progressAction?.Invoke(100);
                    File.WriteAllText(targetMd5Path, download.Md5, Encoding.ASCII);

                    return targetPath;
                }
            }
            catch (IOException)
            {
                NeonHelper.DeleteFile(targetPath);
                NeonHelper.DeleteFile(targetMd5Path);

                throw;
            }
        }
    }
}
