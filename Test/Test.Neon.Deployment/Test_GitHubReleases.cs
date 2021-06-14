//-----------------------------------------------------------------------------
// FILE:	    Test_GitHubReleases.cs
// CONTRIBUTOR: Marcus Bowyer
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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

using Neon.Common;
using Neon.Deployment;
using Neon.IO;
using Neon.Xunit;

using Octokit;
using System.Net.Http;

namespace TestDeployment
{
    [Trait(TestTrait.Category, TestArea.NeonDeployment)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public partial class Test_GitHubReleases
    {
        [Fact]
        public void EndToEnd_WithFileAsset()
        {
            const string repo = "neon-test/github-automation";

            var tagName = Guid.NewGuid().ToString("d");

            using (var httpClient = new HttpClient())
            {
                // This test exercises the GitHub Release API (with the default tag name):
                //
                //      1. Create a draft release
                //      2. Add an asset
                //      3. Publish the release (by setting draft=false)
                //      4. List all releases to ensure that new release is included
                //      5. Fetch the new release and verify the asset
                //      6. Delete the release
                //      7. List all releases to ensure that the new release is no longer present
                //      8. Fetch the release to verify that it's no longer present

                // Create a draft release:

                var release = GitHub.Release.Create(repo, tagName, body: "Hello World!", draft: true, prerelease: true);

                Assert.Equal("Hello World!", release.Body);
                Assert.True(release.Draft);
                Assert.True(release.Prerelease);
                Assert.Empty(release.Assets);
                Assert.Null(release.PublishedAt);

                // Add an asset via a file:

                ReleaseAsset asset;

                using (var tempFile = new TempFile())
                {
                    File.WriteAllText(tempFile.Path, "test asset contents");

                    asset = GitHub.Release.UploadAsset(repo, release, tempFile.Path, "test-asset.dat");

                    Assert.Equal("test-asset.dat", asset.Name);
                    Assert.Equal("application/octet-stream", asset.ContentType);
                }

                // Publish the release (by setting draft=false):

                var releaseUpdate = release.ToUpdate();

                releaseUpdate.Draft = false;

                release = GitHub.Release.Update(repo, release, releaseUpdate);

                Assert.False(release.Draft);
                Assert.NotNull(release.PublishedAt);

                // List all releases to ensure that new release is included:

                var releaseList = GitHub.Release.List(repo);

                Assert.NotNull(releaseList.SingleOrDefault(r => r.Id == release.Id));

                // Fetch the new release:

                var fetchedRelease = GitHub.Release.Get(repo, release.TagName);

                Assert.NotNull(fetchedRelease);
                Assert.False(fetchedRelease.Draft);

                var assertUri = GitHub.Release.GetAssetUri(release, asset);
                var assetText = httpClient.GetAsync(assertUri).Result.Content.ReadAsStringAsync().Result;

                Assert.Equal("test asset contents", assetText);

                // Delete the release:

                GitHub.Release.Remove(repo, release);

                // List all releases to ensure that the new release is no longer present:

                releaseList = GitHub.Release.List(repo);

                Assert.Null(releaseList.SingleOrDefault(r => r.Id == release.Id));

                // Fetch the release to verify that it's no longer present:

                fetchedRelease = GitHub.Release.Get(repo, release.TagName);

                Assert.Null(fetchedRelease);
            }
        }

        [Fact]
        public void EndToEnd_WithStreamAsset()
        {
            const string repo = "neon-test/github-automation";

            var tagName = Guid.NewGuid().ToString("d");

            using (var httpClient = new HttpClient())
            {
                // This test exercises the GitHub Release API (with the default tag name):
                //
                //      1. Create a draft release
                //      2. Add an asset
                //      3. Publish the release (by setting draft=false)
                //      4. List all releases to ensure that new release is included
                //      5. Fetch the new release and verify the asset
                //      6. Delete the release
                //      7. List all releases to ensure that the new release is no longer present
                //      8. Fetch the release to verify that it's no longer present

                // Create a draft release:

                var release = GitHub.Release.Create(repo, tagName, body: "Hello World!", draft: true, prerelease: true);

                Assert.Equal("Hello World!", release.Body);
                Assert.True(release.Draft);
                Assert.True(release.Prerelease);
                Assert.Empty(release.Assets);
                Assert.Null(release.PublishedAt);

                // Add an asset via a stream:

                ReleaseAsset asset;

                using (var ms = new MemoryStream())
                {
                    ms.Write(Encoding.UTF8.GetBytes("test asset contents"));
                    ms.Position = 0;

                    asset = GitHub.Release.UploadAsset(repo, release, ms, "test-asset.dat");

                    Assert.Equal("test-asset.dat", asset.Name);
                    Assert.Equal("application/octet-stream", asset.ContentType);
                }

                // Publish the release (by setting draft=false):

                var releaseUpdate = release.ToUpdate();

                releaseUpdate.Draft = false;

                release = GitHub.Release.Update(repo, release, releaseUpdate);

                Assert.False(release.Draft);
                Assert.NotNull(release.PublishedAt);

                // List all releases to ensure that new release is included:

                var releaseList = GitHub.Release.List(repo);

                Assert.NotNull(releaseList.SingleOrDefault(r => r.Id == release.Id));

                // Fetch the new release:

                var fetchedRelease = GitHub.Release.Get(repo, release.TagName);

                Assert.NotNull(fetchedRelease);
                Assert.False(fetchedRelease.Draft);

                var assertUri = GitHub.Release.GetAssetUri(release, asset);
                var assetText = httpClient.GetAsync(assertUri).Result.Content.ReadAsStringAsync().Result;

                Assert.Equal("test asset contents", assetText);

                // Delete the release:

                GitHub.Release.Remove(repo, release);

                // List all releases to ensure that the new release is no longer present:

                releaseList = GitHub.Release.List(repo);

                Assert.Null(releaseList.SingleOrDefault(r => r.Id == release.Id));

                // Fetch the release to verify that it's no longer present:

                fetchedRelease = GitHub.Release.Get(repo, release.TagName);

                Assert.Null(fetchedRelease);
            }
        }

        [Fact]
        public void EndToEnd_WithDefaults()
        {
            const string repo = "neon-test/github-automation";

            var tagName = Guid.NewGuid().ToString("d");

            using (var httpClient = new HttpClient())
            {
                // This test exercises the GitHub Release API like we did above
                // but using default properties and arguments.

                // Create a draft release:

                var release = GitHub.Release.Create(repo, tagName);

                Assert.Null(release.Body);
                Assert.False(release.Draft);
                Assert.False(release.Prerelease);
                Assert.Empty(release.Assets);
                Assert.NotNull(release.PublishedAt);

                // List all releases to ensure that new release is included:

                var releaseList = GitHub.Release.List(repo);

                Assert.NotNull(releaseList.SingleOrDefault(r => r.Id == release.Id));

                // Adding an asset for published releases should fail:

                Assert.Throws<NotSupportedException>(
                    () =>
                    {
                        ReleaseAsset asset;

                        using (var tempFile = new TempFile())
                        {
                            File.WriteAllText(tempFile.Path, "test asset contents");

                            asset = GitHub.Release.UploadAsset(repo, release, tempFile.Path, "test-asset.dat");

                            Assert.Equal("test-asset.dat", asset.Name);
                            Assert.Equal("application/octet-stream", asset.ContentType);
                        }
                    });

                // Fetch the new release:

                var fetchedRelease = GitHub.Release.Get(repo, release.TagName);

                Assert.NotNull(fetchedRelease);
                Assert.False(fetchedRelease.Draft);
                Assert.False(fetchedRelease.Prerelease);

                // Delete the release:

                GitHub.Release.Remove(repo, release);

                // List all releases to ensure that the new release is no longer present:

                releaseList = GitHub.Release.List(repo);

                Assert.Null(releaseList.SingleOrDefault(r => r.Id == release.Id));

                // Fetch the release to verify that it's no longer present:

                fetchedRelease = GitHub.Release.Get(repo, release.TagName);

                Assert.Null(fetchedRelease);
            }
        }

        [Fact]
        public void Delete_Draft()
        {
            // Verify that we can list and delete draft releases.

            const string repo = "neon-test/github-automation";

            var tagName = Guid.NewGuid().ToString("d");

            using (var httpClient = new HttpClient())
            {
                // Create a draft release:

                var release = GitHub.Release.Create(repo, tagName);

                Assert.Null(release.Body);
                Assert.False(release.Draft);
                Assert.False(release.Prerelease);
                Assert.Empty(release.Assets);
                Assert.NotNull(release.PublishedAt);

                // List all releases to ensure that new release is included:

                var releaseList = GitHub.Release.List(repo);

                Assert.NotNull(releaseList.SingleOrDefault(r => r.Id == release.Id));

                // Also confirm that we can fetch the draft release.

                release = GitHub.Release.Get(repo, release.TagName);

                Assert.NotNull(release);

                // Delete the draft release.

                GitHub.Release.Remove(repo, release);

                // Confirm that the release is gone.

                releaseList = GitHub.Release.List(repo);

                Assert.Null(releaseList.SingleOrDefault(r => r.Id == release.Id));
                Assert.Null(GitHub.Release.Get(repo, release.TagName));
            }
        }
    }
}
