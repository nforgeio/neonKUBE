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
using Couchbase.Linq;
using Couchbase.Linq.Extensions;
using Couchbase.N1QL;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Data;
using Neon.Xunit;
using Neon.Xunit.Couchbase;
using Neon.Xunit.Hive;

using Xunit;

namespace TestSamples
{
    // This example uses the [CouchbaseFixture] directly to run general
    // tests against the database.  This test methods call the Flush() explicitly
    // reset the database state rather than doing this globally in the constructor.

    public class Test_CouchbaseFlushInTest : IClassFixture<CouchbaseFixture>
    {
        //---------------------------------------------------------------------
        // Internal types

        public class TestDoc
        {
            public string Name { get; set; }
            public int Age { get; set; }
        }

        //---------------------------------------------------------------------
        // Implementation

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
        public async Task WriteReadOne()
        {
            // Ensure that the database starts out empty.

            couchbase.Clear();

            await Assert.ThrowsAsync<CouchbaseKeyValueResponseException>(async () => await bucket.GetSafeAsync<string>("one"));
            await Assert.ThrowsAsync<CouchbaseKeyValueResponseException>(async () => await bucket.GetSafeAsync<string>("two"));

            // Do a simple write/read test.

            await bucket.UpsertSafeAsync("one", "1");
            Assert.Equal("1", await bucket.GetSafeAsync<string>("one"));

            // Do another flush just for fun.

            couchbase.Clear();

            await Assert.ThrowsAsync<CouchbaseKeyValueResponseException>(async () => await bucket.GetSafeAsync<string>("one"));
            await Assert.ThrowsAsync<CouchbaseKeyValueResponseException>(async () => await bucket.GetSafeAsync<string>("two"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.Sample)]
        public async Task WriteReadTwo()
        {
            // Ensure that the database starts out empty.

            couchbase.Clear();

            await Assert.ThrowsAsync<CouchbaseKeyValueResponseException>(async () => await bucket.GetSafeAsync<string>("one"));
            await Assert.ThrowsAsync<CouchbaseKeyValueResponseException>(async () => await bucket.GetSafeAsync<string>("two"));

            // Do a simple write/read test.

            await bucket.UpsertSafeAsync("two", "2");
            Assert.Equal("2", await bucket.GetSafeAsync<string>("two"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.Sample)]
        public async Task Query()
        {
            // Ensure that the database starts out empty.

            couchbase.Clear();

            await Assert.ThrowsAsync<CouchbaseKeyValueResponseException>(async () => await bucket.GetSafeAsync<string>("jack"));
            await Assert.ThrowsAsync<CouchbaseKeyValueResponseException>(async () => await bucket.GetSafeAsync<string>("jill"));

            // Write a couple documents and then query for them.  Note
            // that we're explicitly using [RequestPlus] consistency
            // and we're doing a synchronous query.

            await bucket.UpsertSafeAsync("jack", new TestDoc() { Name = "Jack", Age = 11 });
            await bucket.UpsertSafeAsync("jill", new TestDoc() { Name = "Jill", Age = 12 });

            var context = new BucketContext(bucket);

            // Note that we're explicitly using [RequestPlus] consistency
            // and we're doing a synchronous query.

            var query = 
                from doc in context.Query<TestDoc>()
                    .ScanConsistency(Couchbase.N1QL.ScanConsistency.RequestPlus)
                select doc;

            var results = query.ToList();

            Assert.Equal(2, results.Count);
            Assert.Single(results.Where(doc => doc.Name == "Jack"));
            Assert.Single(results.Where(doc => doc.Name == "Jill"));

            // Use Couchbase [IQueryable<T>.ExecuteAsync()] to execute the query
            // and enable result streaming from the server.

            query = 
                from doc in context.Query<TestDoc>()
                    .ScanConsistency(Couchbase.N1QL.ScanConsistency.RequestPlus)
                    .UseStreaming(true)
                select doc;

            await query.ExecuteAsync();

            results = query.ToList();

            Assert.Equal(2, results.Count);
            Assert.Single(results.Where(doc => doc.Name == "Jack"));
            Assert.Single(results.Where(doc => doc.Name == "Jill"));

            // Do a string based query using the [QueryAsync()] extension
            // method which throws an exception on errors.  Note that this doesn't
            // appear to be compatible with streaming (Rows is NULL).

            var queryRequest = new QueryRequest($"select {bucket.Name}.* from {bucket.Name}")
                .ScanConsistency(ScanConsistency.RequestPlus);

            var queryResult = await bucket.QueryAsync<TestDoc>(queryRequest);

            var rows = queryResult.Rows;

            Assert.Equal(2, rows.Count);
            Assert.Single(rows.Where(doc => doc.Name == "Jack"));
            Assert.Single(rows.Where(doc => doc.Name == "Jill"));

            // Do a string based query using the [QuerySafeAsync()] extension
            // method which throws an exception on errors.  Note that this doesn't
            // appear to be compatible with streaming (the result is NULL).

            queryRequest = new QueryRequest($"select {bucket.Name}.* from {bucket.Name}")
                .ScanConsistency(ScanConsistency.RequestPlus);

            results = await bucket.QuerySafeAsync<TestDoc>(queryRequest);

            Assert.Equal(2, results.Count);
            Assert.Single(results.Where(doc => doc.Name == "Jack"));
            Assert.Single(results.Where(doc => doc.Name == "Jill"));
        }
    }
}
