//-----------------------------------------------------------------------------
// FILE:	    Test_Aws.cs
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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

using Neon.Common;
using Neon.Cryptography;
using Neon.Deployment;
using Neon.IO;
using Neon.Xunit;

namespace TestDeployment
{
    // NOTE: These tests need to be run manually.

    [Trait(TestTrait.Category, TestArea.NeonDeployment)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_Aws : IClassFixture<EnvironmentFixture>
    {
        private const string TestBucketS3Ref    = "s3://neon-unit-test";
        private const string TestBucketHttpsRef = "https://neon-unit-test.s3.us-west-2.amazonaws.com";

        private EnvironmentFixture  fixture;

        public Test_Aws(EnvironmentFixture fixture)
        {
            // $todo(jefflill):
            //
            // We should be clearing the S3 bucket here so it'll be in a known
            // state before running the tests.  We should probably do this via
            // the AWS REST SDK.

            this.fixture = fixture;

            if (fixture.Start() == TestFixtureStatus.AlreadyRunning)
            {
                fixture.Restore();
            }

            AwsCli.SetCredentials();
        }

        /// <summary>
        /// Checks the AWS credentials for the non-CI environments.  This works because
        /// neonKUBE developers generally have their AWS credentials persisted as environment
        /// variables.  For CI environments, this is a NOP and we'll rely on fetching credentials
        /// from 1Password.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown for non-CI environments when the AWS credentials are not present as
        /// environment variables.
        /// </exception>
        private void CheckCredentials()
        {
            if (!NeonHelper.IsCI)
            {
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID")))
                {
                    throw new ArgumentException($"Missing AWS credential environment variable: AWS_ACCESS_KEY_ID");
                }

                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY")))
                {
                    throw new ArgumentException($"Missing AWS credential environment variable: AWS_SECRET_ACCESS_KEY");
                }
            }
        }

        [Fact]
        public void S3_File_UploadDownload_WithS3Ref()
        {
            CheckCredentials();

            using (var tempUploadFile = new TempFile())
            {
                using (var tempDownloadFile = new TempFile())
                {
                    using (var writer = new StreamWriter(tempUploadFile.Path))
                    {
                        for (int i = 0; i < 10000; i++)
                        {
                            writer.WriteLine($"LINE #{i}");
                        }
                    }

                    // Upload the file

                    AwsCli.S3Upload(tempUploadFile.Path, $"{TestBucketS3Ref}/upload.txt");

                    // Download the file

                    AwsCli.S3Download($"{TestBucketS3Ref}/upload.txt", tempDownloadFile.Path);

                    // Ensure that downloaded file matches the upload

                    Assert.Equal(File.ReadAllText(tempUploadFile.Path), File.ReadAllText(tempDownloadFile.Path));
                }
            }
        }

        [Fact]
        public void S3_File_UploadDownload_WithHttpsRef()
        {
            CheckCredentials();

            using (var tempUploadFile = new TempFile())
            {
                using (var tempDownloadFile = new TempFile())
                {
                    using (var writer = new StreamWriter(tempUploadFile.Path))
                    {
                        for (int i = 0; i < 10000; i++)
                        {
                            writer.WriteLine($"LINE #{i}");
                        }
                    }

                    // Upload the file

                    AwsCli.S3Upload(tempUploadFile.Path, $"{TestBucketHttpsRef}/upload.txt");

                    // Download the file

                    AwsCli.S3Download($"{TestBucketS3Ref}/upload.txt", tempDownloadFile.Path);

                    // Ensure that downloaded file matches the upload

                    Assert.Equal(File.ReadAllText(tempUploadFile.Path), File.ReadAllText(tempDownloadFile.Path));
                }
            }
        }

        [Fact]
        public void S3_Text_UploadDownload_WithS3Ref()
        {
            CheckCredentials();

            var sb = new StringBuilder();

            for (int i = 0; i < 10000; i++)
            {
                sb.AppendLine($"LINE #{i}");
            }

            // Upload the file

            AwsCli.S3UploadText(sb.ToString(), $"{TestBucketS3Ref}/upload.txt");

            // Download the file

            var download = AwsCli.S3DownloadText($"{TestBucketS3Ref}/upload.txt");

            // Ensure that downloaded file matches the upload

            Assert.Equal(sb.ToString(), download);
        }

        [Fact]
        public void S3_Text_UploadDownload_WithHttpsRef()
        {
            CheckCredentials();

            var sb = new StringBuilder();

            for (int i = 0; i < 10000; i++)
            {
                sb.AppendLine($"LINE #{i}");
            }

            // Upload the file

            AwsCli.S3UploadText(sb.ToString(), $"{TestBucketHttpsRef}/upload.txt");

            // Download the file

            var download = AwsCli.S3DownloadText($"{TestBucketHttpsRef}/upload.txt");

            // Ensure that downloaded file matches the upload

            Assert.Equal(sb.ToString(), download);
        }

        [Fact]
        public void S3_Bytes_UploadDownload_WithS3Ref()
        {
            CheckCredentials();

            var bytes = new byte[10000];

            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)i;
            }

            // Upload the file

            AwsCli.S3UploadBytes(bytes, $"{TestBucketS3Ref}/upload.txt");

            // Download the file

            var download = AwsCli.S3DownloadBytes($"{TestBucketS3Ref}/upload.txt");

            // Ensure that downloaded file matches the upload

            Assert.Equal(bytes, download);
        }

        [Fact]
        public void S3_Bytes_UploadDownload_WithHttpsRef()
        {
            CheckCredentials();

            var bytes = new byte[10000];

            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)i;
            }

            // Upload the file

            AwsCli.S3UploadBytes(bytes, $"{TestBucketHttpsRef}/upload.txt");

            // Download the file

            var download = AwsCli.S3DownloadBytes($"{TestBucketHttpsRef}/upload.txt");

            // Ensure that downloaded file matches the upload

            Assert.Equal(bytes, download);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task S3_MultiPart(bool publicReadAccess)
        {
            CheckCredentials();

            // Verify that uploading a multi-part file to S3 works.

            using (var tempFolder = new TempFolder())
            {
                // We're going to upload a 9900 byte file with maximum
                // part size of 1000 bytes.  This should result in nine
                // 1000 byte parts and one 900 byte part being uploaded
                // (the last part).

                var tempPath    = Path.Combine(tempFolder.Path, "multi-part.test");
                var tempName    = Path.GetFileName(tempPath);
                var uploadBytes = NeonHelper.GetCryptoRandomBytes(9900);

                File.WriteAllBytes(tempPath, uploadBytes);

                var upload = AwsCli.S3UploadMultiPart(tempPath, TestBucketHttpsRef, "1.0", maxPartSize: 1000, publicReadAccess: publicReadAccess);

                // Validate the Download information.

                var manifest = upload.manifest;

                Assert.Equal(tempName, manifest.Name);
                Assert.Equal("1.0", manifest.Version);
                Assert.Equal(tempName, manifest.Filename);
                Assert.Equal(9900, manifest.Size);
                Assert.Equal(10, manifest.Parts.Count);
                Assert.Equal(CryptoHelper.ComputeMD5String(uploadBytes), manifest.Md5);

                // Verify that the download information matches our expections.

                using (var uploadStream = new MemoryStream(uploadBytes))
                {
                    var partOffset = 0L;

                    for (int partNumber = 0; partNumber < manifest.Parts.Count; partNumber++)
                    {
                        var part = manifest.Parts[partNumber];

                        Assert.Equal(partNumber, part.Number);
                        Assert.Equal($"{TestBucketHttpsRef}/{tempName}.parts/part-{partNumber:000#}", part.Uri);

                        if (partNumber < 9)
                        {
                            Assert.Equal(1000, part.Size);
                        }
                        else
                        {
                            Assert.Equal(900, part.Size);
                        }

                        using (var substream = new SubStream(uploadStream, partOffset, part.Size))
                        {
                            Assert.Equal(part.Md5, CryptoHelper.ComputeMD5String(substream));
                        }

                        partOffset += part.Size;
                    }
                }

                using (var uploadStream = new MemoryStream(uploadBytes))
                {
                    using (var httpClient = new HttpClient())
                    {
                        // Verify that the actual download file on S3 matches the download information returned.

                        var response = await httpClient.GetSafeAsync(upload.manifestUri);

                        Assert.Equal(DeploymentHelper.DownloadManifestContentType, response.Content.Headers.ContentType.MediaType);

                        var remoteDownload = NeonHelper.JsonDeserialize<DownloadManifest>(await response.Content.ReadAsStringAsync());

                        Assert.Equal(NeonHelper.JsonSerialize(upload.manifest, Formatting.Indented), NeonHelper.JsonSerialize(remoteDownload, Formatting.Indented));

                        // Verify that the uploaded parts match what we sent.

                        var partOffset = 0L;

                        for (int partNumber = 0; partNumber < manifest.Parts.Count; partNumber++)
                        {
                            var part = manifest.Parts[partNumber];

                            response = await httpClient.GetSafeAsync(part.Uri);

                            Assert.Equal(part.Md5, CryptoHelper.ComputeMD5String(await response.Content.ReadAsByteArrayAsync()));

                            partOffset += part.Size;
                        }
                    }
                }

                using (var tempFile = new TempFile())
                {
                    // Verify that [DownloadMultiPartAsync()] checks the [Content-Type] header.

                    await Assert.ThrowsAsync<FormatException>(async () => await DeploymentHelper.DownloadMultiPartAsync("https://www.google.com", Path.Combine(tempFolder.Path, "test1.dat")));

                    // Verify that [DownloadMultiPartAsync()] actually works.

                    var targetPath = Path.Combine(tempFolder.Path, "test2.dat");

                    await DeploymentHelper.DownloadMultiPartAsync($"{TestBucketHttpsRef}/{tempName}.manifest", targetPath);

                    Assert.True(File.Exists(targetPath));
                    Assert.Equal(9900L, new FileInfo(targetPath).Length);
                }
            }
        }
    }
}
