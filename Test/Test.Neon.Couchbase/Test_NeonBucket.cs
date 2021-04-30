//-----------------------------------------------------------------------------
// FILE:	    Test_CbHelper.cs
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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Couchbase;
using Couchbase.Core;

using Neon.Common;
using Neon.Xunit;
using Neon.Xunit.Couchbase;

using Xunit;

namespace TestCouchbase
{
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_NeonBucket : IClassFixture<CouchbaseFixture>
    {
        private const string username = "Administrator";
        private const string password = "password";

        private CouchbaseFixture    couchbase;
        private NeonBucket          bucket;

        public Test_NeonBucket(CouchbaseFixture couchbase)
        {
            this.couchbase = couchbase;

            couchbase.Start();

            bucket = couchbase.Bucket;
        }

        [Fact]
        [Trait(TestTrait.Project, TestProject.NeonCouchbase)]
        public async Task Basic()
        {
            // Basic test to verify that we can put/get/remove a document.

            await bucket.UpsertSafeAsync("hello", "world!");
            Assert.Equal("world!", await bucket.GetSafeAsync<string>("hello"));
            await bucket.RemoveSafeAsync("hello");
            Assert.Null(await bucket.FindSafeAsync<string>("hello"));
        }

        [Fact]
        [Trait(TestTrait.Project, TestProject.NeonCouchbase)]
        public async Task CasTransientDetector()
        {
            // Verify that the CAS transient detector function works.

            var result = await bucket.UpsertAsync("test", "zero");
            var orgCas = result.Cas;

            // Update the value.  We shouldn't see a CAS error.

            result = await bucket.UpsertAsync("test", "one");

            var newCas = result.Cas;

            // Now attempt to update value using the old CAS.  This
            // should throw the CAS mismatch exception.

            var caught = (Exception)null;

            try
            {
                await bucket.UpsertSafeAsync("test", "two", cas: orgCas);
            }
            catch (Exception e)
            {
                caught = e;
            }

            Assert.NotNull(caught);
            Assert.True(CouchbaseTransientDetector.IsCasTransient(caught));
        }

        [Fact]
        [Trait(TestTrait.Project, TestProject.NeonCouchbase)]
        public async Task DurabilityOverrides()
        {
            couchbase.Clear();

            // Verify that the NeonBucket durability overrides work.  We're going
            // to be modifying the DEV_WORKSTATION environment variable so we
            // need to take care to restore its original value before the test
            // exits, to ensure that it will be correct for subsequent tests
            // running in the test runner process.

            var orgDevWorkstation = Environment.GetEnvironmentVariable("DEV_WORKSTATION");

            try
            {
                // Remove any existing DEV_WORKSTATION variable and verify that we
                // can explicitly specify the durability override.
                //
                // Note that the code below assumes that the Couchbase test fixture
                // creates a single node cluster.

                Environment.SetEnvironmentVariable("DEV_WORKSTATION", null);

                using (var bucket = couchbase.Settings.OpenBucket(username, password, ignoreDurability: false))
                {
                    // Verify that we can ensure persistence to one node.

                    await bucket.UpsertSafeAsync("test", "zero", ReplicateTo.Zero, PersistTo.One);

                    // Verify that we see failures when we try to ensure
                    // durability to more nodes than we actually have.

                    await Assert.ThrowsAsync<CouchbaseKeyValueResponseException>(async () => await bucket.UpsertSafeAsync("test", "one", ReplicateTo.One, PersistTo.One));
                    await Assert.ThrowsAsync<CouchbaseKeyValueResponseException>(async () => await bucket.UpsertSafeAsync("test", "two", ReplicateTo.One, PersistTo.Two));
                }

                using (var bucket = couchbase.Settings.OpenBucket(username, password, ignoreDurability: true))
                {
                    // Verify that durability constraints are being ignored.

                    await bucket.UpsertSafeAsync("test", "three", ReplicateTo.Zero, PersistTo.One);
                    await bucket.UpsertSafeAsync("test", "four", ReplicateTo.One, PersistTo.One);
                    await bucket.UpsertSafeAsync("test", "five", ReplicateTo.One, PersistTo.Two);
                }

                // Verify the behavior of the DEV_WORKSTATION variable when we
                // don't explicitly override the durability constraints.

                Environment.SetEnvironmentVariable("DEV_WORKSTATION", null);

                using (var bucket = couchbase.Settings.OpenBucket(username, password))
                {
                    // Verify that we can ensure durability to one node.

                    await bucket.UpsertSafeAsync("test", "six", ReplicateTo.Zero, PersistTo.One);

                    // Verify that we see failures when we try to ensure
                    // durability to more nodes than we have.

                    await Assert.ThrowsAsync<CouchbaseKeyValueResponseException>(async () => await bucket.UpsertSafeAsync("test", "seven", ReplicateTo.One, PersistTo.One));
                    await Assert.ThrowsAsync<CouchbaseKeyValueResponseException>(async () => await bucket.UpsertSafeAsync("test", "eight", ReplicateTo.One, PersistTo.Two));
                }

                Environment.SetEnvironmentVariable("DEV_WORKSTATION", "1");

                using (var bucket = couchbase.Settings.OpenBucket(username, password))
                {
                    // Verify that durability constraints are being ignored.

                    await bucket.UpsertSafeAsync("test", "nine", ReplicateTo.Zero, PersistTo.One);
                    await bucket.UpsertSafeAsync("test", "ten", ReplicateTo.One, PersistTo.One);
                    await bucket.UpsertSafeAsync("test", "eleven", ReplicateTo.One, PersistTo.Two);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("DEV_WORKSTATION", orgDevWorkstation);
            }
        }
    }
}
