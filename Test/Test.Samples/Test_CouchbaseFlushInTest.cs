//-----------------------------------------------------------------------------
// FILE:	    Test_CouchbaseFlushInTest.cs
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

namespace TestCouchbase
{
    // This example the <see cref="CouchbaseFixture"/> directly to run general
    // tests against the database.  This test methods call the Flush() explicitly
    // reset the database state rather than doing this globally in the constructor.

    public class Test_CouchbaseFlushInTest : IClassFixture<CouchbaseFixture>
    {
        private CouchbaseFixture    couchbase;
        private NeonBucket          bucket;

        public Test_CouchbaseFlushInTest(CouchbaseFixture couchbase)
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
                });

            // This needs to be assigned outside of the initialization action
            // so that the bucket will be available for every test.

            bucket = couchbase.Bucket;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.Sample)]
        public async Task One()
        {
            // Ensure that the database starts out empty.

            couchbase.Reset();

            await Assert.ThrowsAsync<CouchbaseKeyValueResponseException>(async () => await bucket.GetSafeAsync<string>("one"));
            await Assert.ThrowsAsync<CouchbaseKeyValueResponseException>(async () => await bucket.GetSafeAsync<string>("two"));

            // Do a simple write/read test.

            bucket.UpsertSafeAsync("one", "1").Wait();
            Assert.Equal("1", await bucket.GetSafeAsync<string>("one"));

            // Do another flush just for fun.

            couchbase.Reset();

            await Assert.ThrowsAsync<CouchbaseKeyValueResponseException>(async () => await bucket.GetSafeAsync<string>("one"));
            await Assert.ThrowsAsync<CouchbaseKeyValueResponseException>(async () => await bucket.GetSafeAsync<string>("two"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.Sample)]
        public async Task Two()
        {
            // Ensure that the database starts out empty.

            couchbase.Reset();

            await Assert.ThrowsAsync<CouchbaseKeyValueResponseException>(async () => await bucket.GetSafeAsync<string>("one"));
            await Assert.ThrowsAsync<CouchbaseKeyValueResponseException>(async () => await bucket.GetSafeAsync<string>("two"));

            // Do a simple write/read test.

            bucket.UpsertSafeAsync("two", "2").Wait();
            Assert.Equal("2", await bucket.GetSafeAsync<string>("two"));
        }
    }
}
