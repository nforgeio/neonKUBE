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

using Neon.Common;
using Neon.Xunit;
using Neon.Xunit.Cluster;
using Neon.Xunit.Couchbase;

using Xunit;

namespace TestSamples
{
    // This example uses the [CouchbaseFixture] directly to run general
    // tests against the database.  This test methods call the Flush() explicitly
    // reset the database state rather than doing this globally in the constructor.

    public class Test_CouchbaseFlushInTest : IClassFixture<CouchbaseFixture>
    {
        private CouchbaseFixture    couchbase;
        private NeonBucket          bucket;

        public Test_CouchbaseFlushInTest(CouchbaseFixture couchbase)
        {
            this.couchbase = couchbase;

            couchbase.Start();

            bucket = couchbase.Bucket;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.Sample)]
        public async Task One()
        {
            // Ensure that the database starts out empty.

            couchbase.Flush();

            await Assert.ThrowsAsync<CouchbaseKeyValueResponseException>(async () => await bucket.GetSafeAsync<string>("one"));
            await Assert.ThrowsAsync<CouchbaseKeyValueResponseException>(async () => await bucket.GetSafeAsync<string>("two"));

            // Do a simple write/read test.

            bucket.UpsertSafeAsync("one", "1").Wait();
            Assert.Equal("1", await bucket.GetSafeAsync<string>("one"));

            // Do another flush just for fun.

            couchbase.Flush();

            await Assert.ThrowsAsync<CouchbaseKeyValueResponseException>(async () => await bucket.GetSafeAsync<string>("one"));
            await Assert.ThrowsAsync<CouchbaseKeyValueResponseException>(async () => await bucket.GetSafeAsync<string>("two"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.Sample)]
        public async Task Two()
        {
            // Ensure that the database starts out empty.

            couchbase.Flush();

            await Assert.ThrowsAsync<CouchbaseKeyValueResponseException>(async () => await bucket.GetSafeAsync<string>("one"));
            await Assert.ThrowsAsync<CouchbaseKeyValueResponseException>(async () => await bucket.GetSafeAsync<string>("two"));

            // Do a simple write/read test.

            bucket.UpsertSafeAsync("two", "2").Wait();
            Assert.Equal("2", await bucket.GetSafeAsync<string>("two"));
        }
    }
}
