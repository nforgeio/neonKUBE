//-----------------------------------------------------------------------------
// FILE:	    Test_EntityViewQuery.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Couchbase;
using Couchbase.Lite;
using Couchbase.Lite.Auth;

using Neon.Common;
using Neon.DynamicData;
using Neon.DynamicData.Internal;
using Neon.Xunit;

using Xunit;

using Test.Neon.Models;

namespace TestLiteExtensions
{
    public class Test_EntityViewQuery
    {
        public Test_EntityViewQuery()
        {
            // We need to make sure all generated entity 
            // classes have been registered.

            ModelTypes.Register();
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void Map()
        {
            // Verify that a entity view map function (without reduce) works.

            using (var test = new TestDatabase())
            {
                var db = test.Database;
                var view = test.Database.GetView("view");

                view.SetMap<TestEntity>(
                    (doc, emit) =>
                    {
                        emit(doc.Content.String, doc);
                    },
                    "1");

                for (int i = 0; i < 10; i++)
                {
                    var doc = db.CreateEntityDocument<TestEntity>();

                    doc.Content.String = $"Jeff-{i}";
                    doc.Save();
                }

                var query = view.CreateQuery();

                Assert.Equal(10, query.Run().Count());
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void EntityQuery()
        {
            // Test synchronous entity queries.

            using (var test = new TestDatabase())
            {
                var db = test.Database;
                var view = test.Database.GetView("view");

                view.SetMap<TestEntity>(
                    (doc, emit) =>
                    {
                        emit(doc.Content.String, doc);
                    },
                    "1");

                for (int i = 0; i < 10; i++)
                {
                    var doc = db.CreateEntityDocument<TestEntity>();

                    doc.Content.String = $"Jeff-{i}";
                    doc.Content.Int = i;
                    doc.Save();
                }

                // Verify that we can query for all of the view rows.

                var query = view.CreateQuery<TestEntity>();
                var results = query.Run().ToList();

                Assert.Equal(10, results.Count);

                for (var i = 0; i < 10; i++)
                {
                    var row = results[i];

                    Assert.Equal($"Jeff-{i}", row.Key);
                    Assert.Equal($"Jeff-{i}", row.KeyString);
                    Assert.Equal($"Jeff-{i}", row.Document.Content.String);
                    Assert.Equal(i, row.Document.Content.Int);
                }

                // Verify that we can query for a specific key.

                query.Keys = new object[] { "Jeff-5" };

                results = query.Run().ToList();

                Assert.Single(results);
                Assert.Equal($"Jeff-5", results[0].Key);
                Assert.Equal($"Jeff-5", results[0].KeyString);
                Assert.Equal($"Jeff-5", results[0].Document.Content.String);
                Assert.Equal(5, results[0].Document.Content.Int);

                query.Keys = null;

                // Test post filters.

                query.PostFilter =
                    row =>
                    {
                        return row.Document.Content.Int < 5;
                    };

                results = query.Run().ToList();

                Assert.Equal(5, results.Count);

                for (var i = 0; i < 5; i++)
                {
                    var row = results[i];

                    Assert.Equal($"Jeff-{i}", row.Key);
                    Assert.Equal($"Jeff-{i}", row.KeyString);
                    Assert.Equal($"Jeff-{i}", row.Document.Content.String);
                    Assert.Equal(i, row.Document.Content.Int);
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public async Task EntityQueryAsync()
        {
            // Test asynchronous entity queries.

            using (var test = new TestDatabase())
            {
                var db = test.Database;
                var view = test.Database.GetView("view");

                view.SetMap<TestEntity>(
                    (doc, emit) =>
                    {
                        emit(doc.Content.String, doc);
                    },
                    "1");

                for (int i = 0; i < 10; i++)
                {
                    var doc = db.CreateEntityDocument<TestEntity>();

                    doc.Content.String = $"Jeff-{i}";
                    doc.Content.Int = i;
                    doc.Save();
                }

                // Verify that we can query for all of the view rows.

                var query = view.CreateQuery<TestEntity>();
                var results = (await query.RunAsync()).ToList();

                Assert.Equal(10, results.Count);

                for (var i = 0; i < 10; i++)
                {
                    var row = results[i];

                    Assert.Equal($"Jeff-{i}", row.Key);
                    Assert.Equal($"Jeff-{i}", row.KeyString);
                    Assert.Equal($"Jeff-{i}", row.Document.Content.String);
                    Assert.Equal(i, row.Document.Content.Int);
                }

                // Verify that we can query for a specific key.

                query.Keys = new object[] { "Jeff-5" };

                results = (await query.RunAsync()).ToList();

                Assert.Single(results);
                Assert.Equal($"Jeff-5", results[0].Key);
                Assert.Equal($"Jeff-5", results[0].KeyString);
                Assert.Equal($"Jeff-5", results[0].Document.Content.String);
                Assert.Equal(5, results[0].Document.Content.Int);

                query.Keys = null;

                // Test post filters.

                query.PostFilter =
                    row =>
                    {
                        return row.Document.Content.Int < 5;
                    };

                results = (await query.RunAsync()).ToList();

                Assert.Equal(5, results.Count);

                for (var i = 0; i < 5; i++)
                {
                    var row = results[i];

                    Assert.Equal($"Jeff-{i}", row.Key);
                    Assert.Equal($"Jeff-{i}", row.KeyString);
                    Assert.Equal($"Jeff-{i}", row.Document.Content.String);
                    Assert.Equal(i, row.Document.Content.Int);
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public async Task EntityLiveQuery()
        {
            // Test live entity queries.

            using (var test = new TestDatabase())
            {
                var db = test.Database;
                var view = test.Database.GetView("view");

                view.SetMap<TestEntity>(
                    (doc, emit) =>
                    {
                        emit(doc.Content.String, doc);
                    },
                    "1");

                for (int i = 0; i < 5; i++)
                {
                    var doc = db.CreateEntityDocument<TestEntity>();

                    doc.Content.String = $"Jeff-{i}";
                    doc.Content.Int = i;
                    doc.Save();
                }

                // Create a live query.

                var liveQuery = view.CreateQuery<TestEntity>().ToLiveQuery();

                // Wait for the initial query to complete.

                await liveQuery.WaitForRowsAsync();

                Assert.Equal(5, liveQuery.Count);

                for (var i = 0; i < 5; i++)
                {
                    var row = liveQuery[i];

                    Assert.Equal($"Jeff-{i}", row.Key);
                    Assert.Equal($"Jeff-{i}", row.KeyString);
                    Assert.Equal($"Jeff-{i}", row.Document.Content.String);
                    Assert.Equal(i, row.Document.Content.Int);
                }

                // Add a sixth document and verify that we see the change.

                var sixthDoc = db.CreateEntityDocument<TestEntity>();

                sixthDoc.Content.String = $"Jeff-5";
                sixthDoc.Content.Int = 5;
                sixthDoc.Save();

                NeonHelper.WaitFor(() => liveQuery.Count == 6, TimeSpan.FromSeconds(10));

                Assert.Equal(6, liveQuery.Count);

                for (var i = 0; i < 6; i++)
                {
                    var row = liveQuery[i];

                    Assert.Equal($"Jeff-{i}", row.Key);
                    Assert.Equal($"Jeff-{i}", row.KeyString);
                    Assert.Equal($"Jeff-{i}", row.Document.Content.String);
                    Assert.Equal(i, row.Document.Content.Int);
                }

                // Delete the 6th document and verify that the query updates.

                sixthDoc.Delete();

                NeonHelper.WaitFor(() => liveQuery.Count == 5, TimeSpan.FromSeconds(10));

                Assert.Equal(5, liveQuery.Count);

                for (var i = 0; i < 5; i++)
                {
                    var row = liveQuery[i];

                    Assert.Equal($"Jeff-{i}", row.Key);
                    Assert.Equal($"Jeff-{i}", row.KeyString);
                    Assert.Equal($"Jeff-{i}", row.Document.Content.String);
                    Assert.Equal(i, row.Document.Content.Int);
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public async Task EntityLiveQuery_PostFilter()
        {
            // Verify that post filters work for live queries.

            using (var test = new TestDatabase())
            {
                var db = test.Database;
                var view = test.Database.GetView("view");

                view.SetMap<TestEntity>(
                    (doc, emit) =>
                    {
                        emit(doc.Content.String, doc);
                    },
                    "1");

                for (int i = 0; i < 10; i++)
                {
                    var doc = db.CreateEntityDocument<TestEntity>();

                    doc.Content.String = $"Jeff-{i}";
                    doc.Content.Int = i;
                    doc.Save();
                }

                // Create a live query with a post filter.

                var query = view.CreateQuery<TestEntity>();

                query.PostFilter =
                    row =>
                    {
                        return row.Document.Content.Int < 5;
                    };

                var liveQuery = query.ToLiveQuery();

                // Wait for the initial query to complete.

                await liveQuery.WaitForRowsAsync();

                Assert.Equal(5, liveQuery.Count);

                for (var i = 0; i < 5; i++)
                {
                    var row = liveQuery[i];

                    Assert.Equal($"Jeff-{i}", row.Key);
                    Assert.Equal($"Jeff-{i}", row.KeyString);
                    Assert.Equal($"Jeff-{i}", row.Document.Content.String);
                    Assert.Equal(i, row.Document.Content.Int);
                }

                // Remove the 5th document and verify that we see the change.

                var fifthDoc = db.GetExistingEntityDocument<TestEntity>(liveQuery[4].Document.Id);

                fifthDoc.Delete();

                NeonHelper.WaitFor(() => liveQuery.Count == 4, TimeSpan.FromSeconds(10));

                Assert.Equal(4, liveQuery.Count);

                for (var i = 0; i < 4; i++)
                {
                    var row = liveQuery[i];

                    Assert.Equal($"Jeff-{i}", row.Key);
                    Assert.Equal($"Jeff-{i}", row.KeyString);
                    Assert.Equal($"Jeff-{i}", row.Document.Content.String);
                    Assert.Equal(i, row.Document.Content.Int);
                }
            }
        }
    }
}
