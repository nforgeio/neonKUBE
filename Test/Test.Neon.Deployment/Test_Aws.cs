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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

using Neon.Common;
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
        public void S3UploadDownload_WithS3Ref()
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

                    // Ensure that downloaded file maches the upload

                    Assert.Equal(File.ReadAllText(tempUploadFile.Path), File.ReadAllText(tempDownloadFile.Path));
                }
            }
        }

        [Fact]
        public void S3UploadDownload_WithHttpsRef()
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

                    // Ensure that downloaded file maches the upload

                    Assert.Equal(File.ReadAllText(tempUploadFile.Path), File.ReadAllText(tempDownloadFile.Path));
                }
            }
        }
    }
}
