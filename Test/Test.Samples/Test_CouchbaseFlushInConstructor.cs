//-----------------------------------------------------------------------------
// FILE:	    Test_CouchbaseFlushInConstructor.cs
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
    // tests against the database.  This test class's calls the Flush() method 
    // in the constructor to reset the database state before calling each test 
    // method.  This ensures that each method will start an empty database.

    public class Test_CouchbaseFlushInConstructor : IClassFixture<CouchbaseFixture>
    {
        private CouchbaseFixture    couchbase;
        private NeonBucket          bucket;

        public Test_CouchbaseFlushInConstructor(CouchbaseFixture couchbase)
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

            // This call ensures that the database is reset to an empty
            // state before the test runner invokes each test method.

            couchbase.Flush();
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.Sample)]
        public async Task One()
        {
            // Ensure that the database starts out empty (because we flushed it in the constructor).

            await Assert.ThrowsAsync<CouchbaseKeyValueResponseException>(async () => await bucket.GetSafeAsync<string>("one"));
            await Assert.ThrowsAsync<CouchbaseKeyValueResponseException>(async () => await bucket.GetSafeAsync<string>("two"));

            // Do a simple write/read test.

            bucket.UpsertSafeAsync("one", "1").Wait();
            Assert.Equal("1", await bucket.GetSafeAsync<string>("one"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.Sample)]
        public async Task Two()
        {
            // Ensure that the database starts out empty (because we flushed it in the constructor).

            await Assert.ThrowsAsync<CouchbaseKeyValueResponseException>(async () => await bucket.GetSafeAsync<string>("one"));
            await Assert.ThrowsAsync<CouchbaseKeyValueResponseException>(async () => await bucket.GetSafeAsync<string>("two"));

            // Do a simple write/read test.

            bucket.UpsertSafeAsync("two", "2").Wait();
            Assert.Equal("2", await bucket.GetSafeAsync<string>("two"));
        }
    }
}
