//-----------------------------------------------------------------------------
// FILE:	    Test_CouchbaseFlushInConstructor.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Couchbase;
using Couchbase.Core;
using Couchbase.Linq;
using Couchbase.Linq.Extensions;
using Couchbase.N1QL;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Xunit;
using Neon.Xunit.Couchbase;
using Neon.Xunit.Hive;

using Xunit;

namespace TestSamples
{
    // This example uses the [CouchbaseFixture] directly to run general
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

            if (!couchbase.Start())
            {
                // Clear the database if we didn't just start it.

                couchbase.Clear();
            }

            bucket = couchbase.Bucket;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.Sample)]
        public async Task WriteReadOne()
        {
            // Ensure that the database starts out empty (because we flushed it in the constructor).

            await Assert.ThrowsAsync<CouchbaseKeyValueResponseException>(async () => await bucket.GetSafeAsync<string>("one"));
            await Assert.ThrowsAsync<CouchbaseKeyValueResponseException>(async () => await bucket.GetSafeAsync<string>("two"));

            // Do a simple write/read test.

            await bucket.UpsertSafeAsync("one", "1");
            Assert.Equal("1", await bucket.GetSafeAsync<string>("one"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.Sample)]
        public async Task WriteReadTwo()
        {
            // Ensure that the database starts out empty (because we flushed it in the constructor).

            await Assert.ThrowsAsync<CouchbaseKeyValueResponseException>(async () => await bucket.GetSafeAsync<string>("one"));
            await Assert.ThrowsAsync<CouchbaseKeyValueResponseException>(async () => await bucket.GetSafeAsync<string>("two"));

            // Do a simple write/read test.

            await bucket.UpsertSafeAsync("two", "2");
            Assert.Equal("2", await bucket.GetSafeAsync<string>("two"));
        }
    }
}
