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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

using Neon.Common;
using Neon.Cryptography;
using Neon.Deployment;
using Neon.IO;
using Neon.Kube.Models.Headend;
using Neon.Xunit;

using Octokit;

namespace TestDeployment
{
    [Trait(TestTrait.Category, TestArea.NeonDeployment)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public partial class Test_GitHubReleases
    {
        const string repo = "neon-test/github-automation";

        [Fact]
        public void EndToEnd_WithFileAsset()
        {
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

        [Fact]
        public void Find()
        {
            // Create a couple releases one draft and the other published and then
            // verify that Find() works with different predicates.

            // Verify that we can list and delete draft releases.

            var tagName1 = Guid.NewGuid().ToString("d");
            var tagName2 = Guid.NewGuid().ToString("d");

            using (var httpClient = new HttpClient())
            {
                // Create the draft release:

                var release1 = GitHub.Release.Create(repo, tagName1, draft: true);

                Assert.Null(release1.Body);
                Assert.True(release1.Draft);
                Assert.False(release1.Prerelease);
                Assert.Empty(release1.Assets);
                Assert.Null(release1.PublishedAt);

                // Create the published release:

                var release2 = GitHub.Release.Create(repo, tagName2, draft: false);

                Assert.Null(release2.Body);
                Assert.False(release2.Draft);
                Assert.False(release2.Prerelease);
                Assert.Empty(release2.Assets);
                Assert.NotNull(release2.PublishedAt);

                // Exercise Find()

                Assert.Empty(GitHub.Release.Find(repo, release => release.Name == null));

                var match = GitHub.Release.Find(repo, release => release.Draft).SingleOrDefault(release => release.TagName == tagName1);

                Assert.NotNull(match);
                Assert.Equal(tagName1, match.TagName);
                Assert.True(match.Draft);

                match = GitHub.Release.Find(repo, release => !release.Draft).SingleOrDefault(release => release.TagName == tagName2);

                Assert.NotNull(match);
                Assert.Equal(tagName2, match.TagName);
                Assert.False(match.Draft);
            }
        }

        [Fact]
        public async Task Download()
        {
            // Upload file as a multi-part release and verify that we can download it.

            var tagName = Guid.NewGuid().ToString("d");
            var release = GitHub.Release.Create(repo, tagName);

            try
            {
                var partCount = 10;
                var partSize  = 1024;
                var download  = PublishMultipartAsset(release, "test.dat", "v1.0", partCount, partSize);

                Assert.Equal("test.dat", download.Name);
                Assert.Equal("v1.0", download.Version);
                Assert.NotNull(download.Md5);
                Assert.Equal(10, download.Parts.Count);
                Assert.Equal(partCount * partSize, download.Parts.Sum(part => part.Size));
                Assert.Equal(partCount * partSize, download.Size);

                // Verify basic downloading.

                using (var tempFolder = new TempFolder())
                {
                    var targetPath = Path.Combine(tempFolder.Path, download.Filename);
                    var path       = await GitHub.Release.DownloadAsync(download, targetPath);

                    Assert.Equal(targetPath, path);

                    using (var stream = File.OpenRead(targetPath))
                    {
                        Assert.Equal(download.Md5, CryptoHelper.ComputeMD5String(stream));
                    }

                    // Verify that the MD5 file was written and that it's correct.

                    var md5Path = targetPath + ".md5";

                    Assert.True(File.Exists(md5Path));
                    Assert.Equal(download.Md5, File.ReadAllText(md5Path, Encoding.ASCII).Trim());
                }

                // Verify that the progress callback is invoked.
                //
                // We're expecting progress = 0 to start and progress = 100 at the end
                // and then a progress call for each downloaded part.

                var progressValues = new List<int>();

                using (var tempFolder = new TempFolder())
                {
                    var targetPath = Path.Combine(tempFolder.Path, download.Filename);

                    await GitHub.Release.DownloadAsync(download, targetPath, progress => progressValues.Add(progress));
                }

                Assert.Equal(partCount + 2, progressValues.Count);
                Assert.Equal(0, progressValues.First());
                Assert.Equal(100, progressValues.Last());

                for (int i = 1; i < progressValues.Count; i++)
                {
                    Assert.True(progressValues[i - 1] <= progressValues[i]);
                }
            }
            finally
            {
                GitHub.Release.Remove(repo, release);
            }
        }

        [Fact]
        public async Task Download_Restart()
        {
            // Upload file as a multi-part release, simulate a partial download and then verify
            // that downloading it again completes the download.

            var tagName = Guid.NewGuid().ToString("d");
            var release = GitHub.Release.Create(repo, tagName);

            try
            {
                var partCount      = 10;
                var partSize       = 1024;
                var download       = PublishMultipartAsset(release, "test.dat", "v1.0", partCount, partSize);
                var progressValues = new List<int>();

                Assert.Equal("test.dat", download.Name);
                Assert.Equal("v1.0", download.Version);
                Assert.NotNull(download.Md5);
                Assert.Equal(10, download.Parts.Count);
                Assert.Equal(partCount * partSize, download.Parts.Sum(part => part.Size));
                Assert.Equal(partCount * partSize, download.Size);

                using (var tempFolder = new TempFolder())
                {
                    var targetPath = Path.Combine(tempFolder.Path, download.Name);

                    await GitHub.Release.DownloadAsync(download, targetPath);

                    using (var stream = File.OpenRead(targetPath))
                    {
                        Assert.Equal(download.Md5, CryptoHelper.ComputeMD5String(stream));
                    }

                    // Set the file size to zero and verify that downloading it again downloads
                    // all of the parts.
                    //
                    // Note that we're using the progress action to count how many parts were downloaded.

                    using (var stream = new FileStream(targetPath, System.IO.FileMode.Open, FileAccess.ReadWrite))
                    {
                        stream.SetLength(0);
                    }

                    progressValues.Clear();

                    await GitHub.Release.DownloadAsync(download, targetPath, progress => progressValues.Add(progress));

                    using (var stream = File.OpenRead(targetPath))
                    {
                        Assert.Equal(download.Md5, CryptoHelper.ComputeMD5String(stream));
                    }

                    Assert.Equal(partCount + 2, progressValues.Count);

                    // Set the file size to just the first part and 1/2 of the second part and then download
                    // again to fetch the missing parts.

                    using (var stream = new FileStream(targetPath, System.IO.FileMode.Open, FileAccess.ReadWrite))
                    {
                        stream.SetLength(partSize + partSize/2);
                    }

                    progressValues.Clear();

                    await GitHub.Release.DownloadAsync(download, targetPath, progress => progressValues.Add(progress));

                    using (var stream = File.OpenRead(targetPath))
                    {
                        Assert.Equal(download.Md5, CryptoHelper.ComputeMD5String(stream));
                    }

                    Assert.Equal((partCount - 1) + 2, progressValues.Count);

                    // Set the file size to cut the last part in half and then download again to fetch the missing part.

                    using (var stream = new FileStream(targetPath, System.IO.FileMode.Open, FileAccess.ReadWrite))
                    {
                        stream.SetLength(download.Size - partSize / 2);
                    }

                    progressValues.Clear();

                    await GitHub.Release.DownloadAsync(download, targetPath, progress => progressValues.Add(progress));

                    using (var stream = File.OpenRead(targetPath))
                    {
                        Assert.Equal(download.Md5, CryptoHelper.ComputeMD5String(stream));
                    }

                    Assert.Equal(3, progressValues.Count);
                }
            }
            finally
            {
                GitHub.Release.Remove(repo, release);
            }
        }

        [Fact]
        public async Task DownloadError_Md5()
        {
            // Upload file as a multi-part release, mess with a part's MD5 and
            // verify that we detect the problem when downloading.

            var tagName = Guid.NewGuid().ToString("d");
            var release = GitHub.Release.Create(repo, tagName);

            try
            {
                var partCount = 10;
                var partSize  = 1024;
                var download  = PublishMultipartAsset(release, "test.dat", "v1.0", partCount, partSize);

                Assert.Equal("test.dat", download.Name);
                Assert.Equal("v1.0", download.Version);
                Assert.NotNull(download.Md5);
                Assert.Equal(10, download.Parts.Count);
                Assert.Equal(partCount * partSize, download.Parts.Sum(part => part.Size));
                Assert.Equal(partCount * partSize, download.Size);

                download.Parts[0].Md5 += "222";

                using (var tempFolder = new TempFolder())
                {
                    var targetPath = Path.Combine(tempFolder.Path, download.Filename);

                    await Assert.ThrowsAsync<IOException>(async () => await GitHub.Release.DownloadAsync(download, targetPath));
                }
            }
            finally
            {
                GitHub.Release.Remove(repo, release);
            }
        }

        [Fact]
        public async Task DownloadError_TooLong()
        {
            // Upload file as a multi-part release and then remove a part
            // from the download and verify that we detect that the downloaded
            // data is longer than we expected.

            var tagName = Guid.NewGuid().ToString("d");
            var release = GitHub.Release.Create(repo, tagName);

            try
            {
                var partCount = 10;
                var partSize  = 1024;
                var download  = PublishMultipartAsset(release, "test.dat", "v1.0", partCount, partSize);

                Assert.Equal("test.dat", download.Name);
                Assert.Equal("v1.0", download.Version);
                Assert.NotNull(download.Md5);
                Assert.Equal(10, download.Parts.Count);
                Assert.Equal(partCount * partSize, download.Parts.Sum(part => part.Size));
                Assert.Equal(partCount * partSize, download.Size);

                download.Parts.Remove(download.Parts.Last());

                using (var tempFolder = new TempFolder())
                {
                    var targetPath = Path.Combine(tempFolder.Path, download.Filename);

                    await Assert.ThrowsAsync<IOException>(async () => await GitHub.Release.DownloadAsync(download, targetPath));
                }
            }
            finally
            {
                GitHub.Release.Remove(repo, release);
            }
        }

        [Fact]
        public async Task DownloadError_TooShort()
        {
            // Upload file as a multi-part release and then add a fake part
            // to the download and verify that we detect that the downloaded
            // data is shorter than we expected.

            var tagName = Guid.NewGuid().ToString("d");
            var release = GitHub.Release.Create(repo, tagName);

            try
            {
                var partCount = 10;
                var partSize  = 1024;
                var download  = PublishMultipartAsset(release, "test.dat", "v1.0", partCount, partSize);

                Assert.Equal("test.dat", download.Name);
                Assert.Equal("v1.0", download.Version);
                Assert.NotNull(download.Md5);
                Assert.Equal(10, download.Parts.Count);
                Assert.Equal(partCount * partSize, download.Parts.Sum(part => part.Size));
                Assert.Equal(partCount * partSize, download.Size);

                download.Parts.Add(download.Parts.First());

                using (var tempFolder = new TempFolder())
                {
                    var targetPath = Path.Combine(tempFolder.Path, download.Filename);

                    await Assert.ThrowsAsync<IOException>(async () => await GitHub.Release.DownloadAsync(download, targetPath));
                }
            }
            finally
            {
                GitHub.Release.Remove(repo, release);
            }
        }

        /// <summary>
        /// Uploads a file as multilpe assets to a release, publishes the release and then 
        /// return the <see cref="Download"/> details.
        /// </summary>
        /// <param name="release">The target brelease.</param>
        /// <param name="name">The download name.</param>
        /// <param name="version">The download version.</param>
        /// <param name="partCount">The number of parts to be uploaded.</param>
        /// <param name="partSize">The size of each part.</param>
        /// <returns>The <see cref="Download"/> describing how to download the parts.</returns>
        /// <remarks>
        /// Each part will be filled with bytes where the byte of each part will start
        /// with the part number and the following bytes will increment the previous byte
        /// value.
        /// </remarks>
        private Download PublishMultipartAsset(Release release, string name, string version, int partCount, long partSize)
        {
            Covenant.Requires<ArgumentNullException>(release != null, nameof(release));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(version), nameof(version));
            Covenant.Requires<ArgumentException>(partCount > 0, nameof(release));
            Covenant.Requires<ArgumentException>(partSize > 0, nameof(release));

            using (var tempFile = new TempFile())
            {
                using (var output = new FileStream(tempFile.Path, System.IO.FileMode.Create, FileAccess.ReadWrite))
                {
                    for (int partNumber = 0; partNumber < partCount; partNumber++)
                    {
                        for (long i = 0; i < partSize; i++)
                        {
                            output.WriteByte((byte)i);
                        }
                    }
                }

                return GitHub.Release.UploadMultipartAsset(repo, release, tempFile.Path, version: version, name: name, maxPartSize: partSize);
            }
        }
    }
}
