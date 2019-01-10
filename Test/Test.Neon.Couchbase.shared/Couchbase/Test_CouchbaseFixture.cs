//-----------------------------------------------------------------------------
// FILE:	    Test_CouchbaseFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Couchbase;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Xunit;
using Neon.Xunit.Couchbase;

using Xunit;

namespace TestCouchbase
{
    /// <summary>
    /// Verifies that we can launch a Docker container fixture during tests.
    /// </summary>
    public class Test_CouchbaseFixture : IClassFixture<CouchbaseFixture>
    {
        private CouchbaseFixture    couchbase;
        private NeonBucket          bucket;

        public Test_CouchbaseFixture(CouchbaseFixture couchbase)
        {
            this.couchbase = couchbase;

            couchbase.Start();

            bucket = couchbase.Bucket;
        }

        /// <summary>
        /// Verify that we can access the Couchbase bucket and perform
        /// a very simple operation.
        /// </summary>
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public async Task Basic()
        {
            couchbase.Clear();

            await bucket.UpsertSafeAsync("hello", "world!");
            Assert.Equal("world!", await bucket.GetSafeAsync<string>("hello"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public async Task Flush()
        {
            var indexQuery = $"select * from system:indexes where keyspace_id={CbHelper.Literal(bucket.Name)}";

            // Flush and verify that the primary index was created by default.

            couchbase.Clear();

            var indexes = await bucket.QuerySafeAsync<JObject>(indexQuery);

            Assert.Single(indexes);

            var index = (JObject)indexes.First().GetValue("indexes");

            Assert.True((bool)index.GetValue("is_primary"));
            Assert.Equal("idx_primary", (string)index.GetValue("name"));

            // Write some data, verify that it was written then flush
            // the bucket and verify that the data is gone.

            await bucket.UpsertSafeAsync("hello", "world!");
            Assert.Equal("world!", await bucket.GetSafeAsync<string>("hello"));

            couchbase.Clear();
            Assert.Null(await bucket.FindSafeAsync<string>("hello"));

            // Create a secondary index and verify.

            await bucket.QuerySafeAsync<dynamic>($"create index idx_foo on {CbHelper.LiteralName(bucket.Name)} ( {CbHelper.LiteralName("Test")} )");

            indexes = await bucket.QuerySafeAsync<JObject>(indexQuery);

            Assert.Equal(2, indexes.Count);     // Expecting the primary and new secondary index

            // Clear the database and then verify that only the
            // recreated primary index exists.

            couchbase.Clear();

            indexes = await bucket.QuerySafeAsync<JObject>(indexQuery);

            Assert.Single(indexes);

            index = (JObject)indexes.First().GetValue("indexes");

            Assert.True((bool)index.GetValue("is_primary"));
            Assert.Equal("idx_primary", (string)index.GetValue("name"));
        }
    }
}
