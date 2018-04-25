//-----------------------------------------------------------------------------
// FILE:	    Test_CouchbaseFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Couchbase;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Neon;

using Neon.Common;

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

            couchbase.Initialize(
                () =>
                {
                    couchbase.Start();
                });

            bucket = couchbase.Bucket;
        }

        /// <summary>
        /// Verify that we can access the Couchbase bucket and perform
        /// a very simple operation.
        /// </summary>
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public async Task BasicAsync()
        {
            couchbase.Flush();

            await bucket.UpsertSafeAsync("hello", "world!");
            Assert.Equal("world!", await bucket.GetSafeAsync<string>("hello"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public async Task FlushAsync()
        {
            var indexQuery = $"select * from system:indexes where keyspace_id={CbHelper.Literal(bucket.Name)}";

            // Flush and verify that the primary index was created by default.

            couchbase.Flush();

            var indexes = await bucket.QuerySafeAsync<JObject>(indexQuery);

            Assert.Single(indexes);

            var index = (JObject)indexes.First().GetValue("indexes");

            Assert.True((bool)index.GetValue("is_primary"));
            Assert.Equal("idx_primary", (string)index.GetValue("name"));

            // Write some data, verify that it was written then flush
            // the bucket and verify that the data is gone.  We're also
            // going to disable creation of the default primary index 
            // and then verify that it was not created.

            await bucket.UpsertSafeAsync("hello", "world!");
            Assert.Equal("world!", await bucket.GetSafeAsync<string>("hello"));

            couchbase.Flush(primaryIndex: null);
            Assert.Null(await bucket.FindSafeAsync<string>("hello"));

            indexes = await bucket.QuerySafeAsync<JObject>(indexQuery);
            Assert.Empty(indexes);

            // Flush the bucket again and verify that we can specify a
            // custom primary key index namee.

            couchbase.Flush(primaryIndex: "idx_custom");

            indexes = await bucket.QuerySafeAsync<JObject>(indexQuery);

            Assert.Single(indexes);

            index = (JObject)indexes.First().GetValue("indexes");

            Assert.True((bool)index.GetValue("is_primary"));
            Assert.Equal("idx_custom", (string)index.GetValue("name"));

            // Create a secondary index and verify that it along with the
            // primary index are deleted during a flush.

            await bucket.QuerySafeAsync<dynamic>($"create index idx_foo on {CbHelper.LiteralName(bucket.Name)} ( {CbHelper.LiteralName("Test")} )");

            indexes = await bucket.QuerySafeAsync<JObject>(indexQuery);

            Assert.Equal(2, indexes.Count);     // Expecting the primary and new secondary index

            couchbase.Flush(primaryIndex: null);

            indexes = await bucket.QuerySafeAsync<JObject>(indexQuery);

            Assert.Empty(indexes);
        }
    }
}
