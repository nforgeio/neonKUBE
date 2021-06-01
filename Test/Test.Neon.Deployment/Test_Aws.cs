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
        private const string TestBucket = "s3://neon-unit-test";

        private EnvironmentFixture  fixture;

        public Test_Aws(EnvironmentFixture fixture)
        {
            this.fixture = fixture;

            if (fixture.Start() == TestFixtureStatus.AlreadyRunning)
            {
                fixture.Restore();
            }
            else
            {
                AwsCli.SetCredentials();
            }
        }

        [Fact]
        public void S3UploadDownload()
        {
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

                    var keyID = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
                    var key   = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");

                    AwsCli.S3Upload(tempUploadFile.Path, $"{TestBucket}/upload.txt");

                    // Download the file

                    AwsCli.S3Download($"{TestBucket}/upload.txt", tempDownloadFile.Path);

                    // Ensure that downloaded file maches the upload

                    Assert.Equal(File.ReadAllText(tempUploadFile.Path), File.ReadAllText(tempDownloadFile.Path));
                }
            }
        }
    }
}
