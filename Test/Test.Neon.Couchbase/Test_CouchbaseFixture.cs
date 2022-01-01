//-----------------------------------------------------------------------------
// FILE:	    Test_CouchbaseFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Net.Http.Headers;

using Couchbase;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Net;
using Neon.Xunit;
using Neon.Xunit.Couchbase;

using Xunit;

using Newtonsoft.Json;

namespace TestCouchbase
{
    /// <summary>
    /// Verifies basic <see cref="CouchbaseFixture"/> capabilities.
    /// </summary>
    [Trait(TestTrait.Category, TestArea.NeonCouchbase)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_CouchbaseFixture : IClassFixture<CouchbaseFixture>
    {
        private CouchbaseFixture    couchbase;
        private NeonBucket          bucket;
        private JsonClient          jsonClient;

        public Test_CouchbaseFixture(CouchbaseFixture couchbase)
        {
            TestHelper.ResetDocker(this.GetType());

            this.couchbase = couchbase;

            couchbase.Start();

            bucket = couchbase.Bucket;

            jsonClient = new JsonClient();
            jsonClient.BaseAddress = new Uri("http://localhost:8094");
            jsonClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"Administrator:password")));
        }

        /// <summary>
        /// Verify that we can access the Couchbase bucket and perform
        /// a very simple operation.
        /// </summary>
        [Fact]
        public async Task Basic()
        {
            couchbase.Clear();

            await bucket.UpsertSafeAsync("hello", "world!");
            Assert.Equal("world!", await bucket.GetSafeAsync<string>("hello"));
        }

        [Fact]
        public async Task Clear()
        {
            var indexQuery = $"select * from system:indexes where keyspace_id={CouchbaseHelper.Literal(bucket.Name)}";

            // Flush and verify that the primary index was created by default.

            couchbase.Clear();

            var indexes = await bucket.QuerySafeAsync<JObject>(indexQuery);

            Assert.Single(indexes);

            var index = (JObject)indexes.First().GetValue("indexes");

            Assert.True((bool)index.GetValue("is_primary"));
            Assert.Equal("#primary", (string)index.GetValue("name"));

            // Write some data, verify that it was written then flush
            // the bucket and verify that the data is gone.

            await bucket.UpsertSafeAsync("hello", "world!");
            Assert.Equal("world!", await bucket.GetSafeAsync<string>("hello"));

            couchbase.Clear();
            Assert.Null(await bucket.FindSafeAsync<string>("hello"));

            // Create a secondary index and verify.

            await bucket.QuerySafeAsync<dynamic>($"create index idx_foo on {CouchbaseHelper.LiteralName(bucket.Name)} ( {CouchbaseHelper.LiteralName("Test")} )");

            indexes = await bucket.QuerySafeAsync<JObject>(indexQuery);

            Assert.Equal(2, indexes.Count);     // Expecting the primary and new secondary index

            // Clear the database and then verify that only the
            // recreated primary index exists.

            couchbase.Clear();

            indexes = await bucket.QuerySafeAsync<JObject>(indexQuery);

            Assert.Single(indexes);

            index = (JObject)indexes.First().GetValue("indexes");

            Assert.True((bool)index.GetValue("is_primary"));
            Assert.Equal("#primary", (string)index.GetValue("name"));

            // Create a fts index and verify.

            var ftsIndex = new Dictionary<string, object>();

            ftsIndex.Add("type", "fulltext-index");
            ftsIndex.Add("name", "test");
            ftsIndex.Add("sourceType", "couchbase");
            ftsIndex.Add("sourceName", "test");

            await jsonClient.PutAsync("/api/index/test", JsonConvert.SerializeObject(ftsIndex));

            ftsIndex = new Dictionary<string, object>();

            ftsIndex.Add("type", "fulltext-index");
            ftsIndex.Add("name", "test123");
            ftsIndex.Add("sourceType", "couchbase");
            ftsIndex.Add("sourceName", "test");

            await jsonClient.PutAsync("/api/index/test123", JsonConvert.SerializeObject(ftsIndex));

            var ftsIndexes = jsonClient.GetAsync<dynamic>("/api/index").Result.indexDefs;

            Assert.True(((JObject)ftsIndexes.indexDefs).Count == 2);

            // Clear the database and then verify that only the
            // recreated primary index exists.

            couchbase.Clear();

            ftsIndexes = jsonClient.GetAsync<dynamic>("/api/index").Result.indexDefs;

            Assert.True(((JObject)ftsIndexes.indexDefs).Count == 0);
        }
    }
}