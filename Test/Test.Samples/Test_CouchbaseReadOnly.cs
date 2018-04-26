//-----------------------------------------------------------------------------
// FILE:	    Test_CouchbaseReadOnly.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Couchbase;
using Couchbase.Core;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Neon;

using Neon.Common;

namespace TestSamples
{
    // This example the <see cref="CouchbaseFixture"/> directly to run some read-only
    // tests against the database.  This test class's constructor initializes
    // the database once and so that the tests will share the database state.
    //
    // This assumes that the tests are read-only or are carefully designed such
    // that any changes made by individual tests no matter what order the tests
    // were run or whether tests passed or failed will not impact subsequent tests.
    //
    // This pattern will run much faster than tests where the database is reset
    // every time at the cost of needing to be more carefully designed.

    public class Test_CouchbaseReadOnly : IClassFixture<CouchbaseFixture>
    {
        private CouchbaseFixture    couchbase;
        private NeonBucket          bucket;

        public Test_CouchbaseReadOnly(CouchbaseFixture couchbase)
        {
            this.couchbase = couchbase;

            couchbase.Initialize(
                () =>
                {
                    // This call starts the Couchbase container.  You can specify
                    // optional paameters to customize the Couchbase settings, Docker
                    // image, container name, or primary index.

                    couchbase.Start();

                    // We're going to initialize read-only Couchbase data here
                    // and since this initialization action is called only once
                    // by the test runner for this test class, the database
                    // state will be shared across all of the test method calls.

                    var bucket = couchbase.Bucket;

                    bucket.UpsertSafeAsync("one", "1").Wait();
                    bucket.UpsertSafeAsync("two", "2").Wait();
                });

            // This needs to be assigned outside of the initialization action
            // so that the bucket will be available for every test.

            bucket = couchbase.Bucket;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.Sample)]
        public async Task One()
        {
            Assert.Equal("1", await bucket.GetSafeAsync<string>("one"));
            await Assert.ThrowsAsync<CouchbaseKeyValueResponseException>(async () => await bucket.GetSafeAsync<string>("not-present"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.Sample)]
        public async Task Two()
        {
            Assert.Equal("2", await bucket.GetSafeAsync<string>("two"));
            await Assert.ThrowsAsync<CouchbaseKeyValueResponseException>(async () => await bucket.GetSafeAsync<string>("not-present"));
        }
    }
}
