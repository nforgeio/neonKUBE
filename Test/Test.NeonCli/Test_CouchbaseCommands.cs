//-----------------------------------------------------------------------------
// FILE:	    Test_CouchbaseCommands.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Text;
using System.Threading.Tasks;

using Couchbase;
using Couchbase.Core;
using Couchbase.Linq;
using Couchbase.Linq.Extensions;
using Couchbase.N1QL;

using Neon;
using Neon.Common;
using Neon.Kube;
using Neon.IO;
using Neon.Xunit;
using Neon.Xunit.Couchbase;
using Neon.Xunit.Kube;

using Test.Neon.Models;

using Xunit;

using NeonCli;

namespace Test.NeonCli
{
    public class Test_CouchbaseCommands : IClassFixture<CouchbaseFixture>
    {
        private const string username = "Administrator";
        private const string password = "password";

        private CouchbaseFixture    couchbase;
        private NeonBucket          bucket;
        private BucketContext       context;

        public Test_CouchbaseCommands(CouchbaseFixture couchbase)
        {
            this.couchbase = couchbase;

            if (couchbase.Start() == TestFixtureStatus.AlreadyRunning)
            {
                couchbase.Clear();
            }

            bucket  = couchbase.Bucket;
            context = new BucketContext(bucket);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Base()
        {
            // Verify that base command returns some help.

            using (var runner = new ProgramRunner())
            {
                var result = runner.Execute(Program.Main, "couchbase");

                Assert.Equal(0, result.ExitCode);
                Assert.Contains("Performs Couchbase related operations.", result.OutputText);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public async Task Query()
        {
            // Verify that the query commands work.

            var endpoint   = bucket.Configuration.GetEndPoint();
            var bucketName = bucket.Configuration.BucketName;

            // Insert a couple documents into the database.

            var jack = new Person()
            {
                Id   = 0,
                Name = "Jack",
                Age  = 10,
                Data = new byte[] { 0, 1, 2, 3, 4 }
            };

            var jill = new Person()
            {
                Id   = 1,
                Name = "Jill",
                Age  = 11,
                Data = new byte[] { 5, 6, 7, 8, 9 }
            };

            await bucket.InsertSafeAsync(jack, persistTo: PersistTo.One);
            await bucket.InsertSafeAsync(jill, persistTo: PersistTo.One);
            await bucket.WaitForIndexerAsync();

            bucket.Query<dynamic>($"select count(*) from `{bucket.Name}`;");

            using (var runner = new ProgramRunner())
            {
                // Query using the [http://...] target format and passing the query on the command line.

                var result = runner.Execute(Program.Main,
                    "couchbase",
                    "query",
                    $"http://{endpoint.Address}:{endpoint.Port}@{username}:{password}:{bucketName}",
                    $"select * from `{bucket.Name}`;");

                Assert.Equal(0, result.ExitCode);
                Assert.Contains("Jack", result.OutputText);
                Assert.Contains("Jill", result.OutputText);

                // Query again, but using the using the [couchbase://...] target format.

                result = runner.Execute(Program.Main,
                    "couchbase",
                    "query",
                    $"couchbase://{endpoint.Address}:{endpoint.Port}@{username}:{password}:{bucketName}",
                    $"select * from `{bucket.Name}`;");

                Assert.Equal(0, result.ExitCode);
                Assert.Contains("Jack", result.OutputText);
                Assert.Contains("Jill", result.OutputText);

                // Pass the query as a file.

                using (var tempFile = new TempFile())
                {
                    File.WriteAllText(tempFile.Path, $"select * from `{bucket.Name}`;");

                    result = runner.Execute(Program.Main,
                        "couchbase",
                        "query",
                        $"couchbase://{endpoint.Address}:{endpoint.Port}@{username}:{password}:{bucketName}",
                        $"@{tempFile.Path}");

                    Assert.Equal(0, result.ExitCode);
                    Assert.Contains("Jack", result.OutputText);
                    Assert.Contains("Jill", result.OutputText);
                }

                // Pass the query as STDIN.

                result = runner.ExecuteWithInput(Program.Main, $"select * from `{bucket.Name}`;",
                    "couchbase",
                    "query",
                    $"couchbase://{endpoint.Address}:{endpoint.Port}@{username}:{password}:{bucketName}",
                    "-");

                Assert.Equal(0, result.ExitCode);
                Assert.Contains("Jack", result.OutputText);
                Assert.Contains("Jill", result.OutputText);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public async Task Upsert()
        {
            ExecuteResponse result;

            // $todo(jefflill):
            //
            // I'm not testing the document key generation features yet.

            // Verify that the query commands work.

            var endpoint   = bucket.Configuration.GetEndPoint();
            var bucketName = bucket.Configuration.BucketName;

            // Generate a string with a couple of JSON documents (one per line)
            // that we'll use for our tests.

            var jack = new Person()
            {
                Id   = 0,
                Name = "Jack",
                Age  = 10,
                Data = new byte[] { 0, 1, 2, 3, 4 }
            };

            var jill = new Person()
            {
                Id   = 1,
                Name = "Jill",
                Age  = 11,
                Data = new byte[] { 5, 6, 7, 8, 9 }
            };

            var sb = new StringBuilder();

            sb.AppendLine(jack.ToString(indented: false));
            sb.AppendLine(jill.ToString(indented: false));

            using (var runner = new ProgramRunner())
            {
                // Upsert using the [http://...] target format and passing the documents as a file.

                using (var tempFile = new TempFile())
                {
                    File.WriteAllText(tempFile.Path, sb.ToString());

                    result = runner.Execute(Program.Main,
                        "couchbase",
                        "upsert",
                        $"http://{endpoint.Address}:{endpoint.Port}@{username}:{password}:{bucketName}",
                        tempFile.Path);

                    Assert.Equal(0, result.ExitCode);

                    await bucket.WaitForIndexerAsync();

                    // Verify that the documents were written.

                    result = runner.ExecuteWithInput(Program.Main, $"select * from `{bucket.Name}`;",
                        "couchbase",
                        "query",
                        $"couchbase://{endpoint.Address}:{endpoint.Port}@{username}:{password}:{bucketName}",
                        "-");

                    Assert.Equal(0, result.ExitCode);
                    Assert.Contains("Jack", result.OutputText);
                    Assert.Contains("Jill", result.OutputText);
                }

                // Generate a couple more documents and test this again but this time passing the
                // JSON documents as STDIN.

                var howard = new Person()
                {
                    Id   = 2,
                    Name = "Howard",
                    Age  = 10,
                    Data = new byte[] { 0, 1, 2, 3, 4 }
                };

                var john = new Person()
                {
                    Id   = 3,
                    Name = "John",
                    Age  = 11,
                    Data = new byte[] { 5, 6, 7, 8, 9 }
                };

                sb.Clear();
                sb.AppendLine(howard.ToString(indented: false));
                sb.AppendLine(john.ToString(indented: false));

                result = runner.ExecuteWithInput(Program.Main, sb.ToString(),
                    "couchbase",
                    "upsert",
                    $"couchbase://{endpoint.Address}:{endpoint.Port}@{username}:{password}:{bucketName}",
                    "-");

                await bucket.WaitForIndexerAsync();

                Assert.Equal(0, result.ExitCode);

                // Verify that the documents were written.

                result = runner.ExecuteWithInput(Program.Main, $"select * from `{bucket.Name}`;",
                    "couchbase",
                    "query",
                    $"couchbase://{endpoint.Address}:{endpoint.Port}@{username}:{password}:{bucketName}",
                    "-");

                Assert.Equal(0, result.ExitCode);
                Assert.Contains("Howard", result.OutputText);
                Assert.Contains("John", result.OutputText);
            }
        }
    }
}
