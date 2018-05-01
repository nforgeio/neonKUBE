//-----------------------------------------------------------------------------
// FILE:	    Test_EntityList.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
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
    public class Test_EntityList
    {
        public Test_EntityList()
        {
            // We need to make sure all generated entity 
            // classes have been registered.

            ModelTypes.Register();
        }

        /// <summary>
        /// Hack to verify that an entity list matches a set of values by comparing
        /// the list items' <see cref="TestEntity.String"/> property values with the 
        /// values passed.
        /// </summary>
        /// <param name="values">The expected values..</param>
        /// <param name="list">The list being tested.</param>
        /// <returns><c>true</c> if they match</returns>
        private bool Match(string[] values, IList<TestEntity> list)
        {
            if (list.Count != values.Length)
            {
                return false;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (list[i].String != values[i])
                {
                    return false;
                }
            }

            return true;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void Basic()
        {
            using (var test = new TestDatabase())
            {
                var db = test.Database;
                var doc = db.GetEntityDocument<TestEntity>("1");
                var prop = string.Empty;
                var changed = false;

                doc.Changed +=
                    (s, a) =>
                    {
                        changed = true;
                    };

                doc.Content.PropertyChanged +=
                    (s, a) =>
                    {
                        prop = a.PropertyName;
                    };

                Assert.Null(doc.Content.ChildList);

                changed = false;
                prop = null;

                //---------------------
                // Assign a collection.

                doc.Content.ChildList = new TestEntity[] { new TestEntity() { String = "1" }, new TestEntity() { String = "2" } };

                Assert.True(changed);
                Assert.Equal("ChildList", prop);
                Assert.NotNull(doc.Content.ChildList);
                Assert.True(Match(new string[] { "1", "2" }, doc.Content.ChildList));

                //---------------------
                // Persist and verify

                doc.Save();
                doc = db.GetEntityDocument<TestEntity>("1");

                Assert.NotNull(doc.Content.ChildList);
                Assert.Equal("1", doc.Content.ChildList[0].String);
                Assert.Equal("2", doc.Content.ChildList[1].String);
                Assert.True(Match(new string[] { "1", "2" }, doc.Content.ChildList));

                //---------------------
                // Assign element values then persist and verify.

                doc.Revise();

                doc.Content.ChildList[0].String = "AAA";
                doc.Content.ChildList[1] = null;

                Assert.Equal("AAA", doc.Content.ChildList[0].String);
                Assert.Null(doc.Content.ChildList[1]);

                doc.Save();
                doc = db.GetEntityDocument<TestEntity>("1");

                Assert.Equal("AAA", doc.Content.ChildList[0].String);
                Assert.Null(doc.Content.ChildList[1]);

                //---------------------
                // Assign NULL.

                doc.Changed +=
                    (s, a) =>
                    {
                        changed = true;
                    };

                doc.Content.PropertyChanged +=
                    (s, a) =>
                    {
                        prop = a.PropertyName;
                    };

                doc.Revise();

                doc.Content.ChildList = null;

                Assert.True(changed);
                Assert.Equal("ChildList", prop);
                Assert.Null(doc.Content.ChildList);

                doc.Save();

                doc = db.GetEntityDocument<TestEntity>("1");

                Assert.Null(doc.Content.ChildList);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void Operations()
        {
            // Verify that entity list operations work and also raise the correct 
            // change events.

            using (var test = new TestDatabase())
            {
                var db = test.Database;
                var doc = db.GetEntityDocument<TestEntity>("1");
                var prop = string.Empty;
                var changed = false;
                var collectionChanged = false;

                doc.Changed +=
                    (s, a) =>
                    {
                        changed = true;
                    };

                doc.Content.PropertyChanged +=
                    (s, a) =>
                    {
                        prop = a.PropertyName;
                    };

                Assert.Null(doc.Content.ChildList);

                //---------------------
                // Assignment

                changed = false;
                prop = null;

                var item1 = new TestEntity() { String = "1" };
                var item2 = new TestEntity() { String = "2" };

                doc.Content.ChildList = new TestEntity[] { item1, item2 };

                Assert.True(changed);
                Assert.Equal("ChildList", prop);
                Assert.NotNull(doc.Content.ChildList);
                Assert.True(Match(new string[] { "1", "2" }, doc.Content.ChildList));
                Assert.Equal(2, doc.Content.ChildList.Count);

                //---------------------
                // IndexOf

                changed = false;
                prop = null;

                Assert.Equal(0, doc.Content.ChildList.IndexOf(item1));
                Assert.Equal(1, doc.Content.ChildList.IndexOf(item2));
                Assert.Equal(-1, doc.Content.ChildList.IndexOf(new TestEntity()));

                Assert.False(changed);
                Assert.Null(prop);

                //---------------------
                // Contains

                changed = false;
                prop = null;

                Assert.True(doc.Content.ChildList.Contains(item1));
                Assert.True(doc.Content.ChildList.Contains(item2));
                Assert.False(doc.Content.ChildList.Contains(new TestEntity()));

                Assert.False(changed);
                Assert.Null(prop);

                //---------------------
                // Indexing 

                collectionChanged = false;
                changed = false;
                prop = null;

                ((INotifyCollectionChanged)doc.Content.ChildList).CollectionChanged +=
                    (s, a) =>
                    {
                        collectionChanged = true;
                    };

                Assert.Equal("1", doc.Content.ChildList[0].String);
                Assert.Equal("2", doc.Content.ChildList[1].String);

                doc.Content.ChildList[0] = new TestEntity() { String = "one" };

                Assert.True(changed);
                Assert.True(collectionChanged);
                Assert.Null(prop);
                Assert.Equal("one", doc.Content.ChildList[0].String);
                Assert.Equal("2", doc.Content.ChildList[1].String);

                Assert.True(Match(new string[] { "one", "2" }, doc.Content.ChildList));

                //---------------------
                // Insert 

                collectionChanged = false;
                changed = false;
                prop = null;

                ((INotifyCollectionChanged)doc.Content.ChildList).CollectionChanged +=
                    (s, a) =>
                    {
                        collectionChanged = true;
                    };

                doc.Content.ChildList.Insert(0, new TestEntity() { String = "zero" });
                doc.Content.ChildList.Insert(3, new TestEntity() { String = "three" });

                Assert.True(changed);
                Assert.True(collectionChanged);
                Assert.Null(prop);
                Assert.Equal(4, doc.Content.ChildList.Count);
                Assert.True(Match(new string[] { "zero", "one", "2", "three" }, doc.Content.ChildList));

                //---------------------
                // Remove

                collectionChanged = false;
                changed = false;
                prop = null;

                ((INotifyCollectionChanged)doc.Content.ChildList).CollectionChanged +=
                    (s, a) =>
                    {
                        collectionChanged = true;
                    };

                Assert.True(doc.Content.ChildList.Remove(doc.Content.ChildList[3]));
                Assert.False(doc.Content.ChildList.Remove(new TestEntity() { String = "four" }));

                Assert.True(changed);
                Assert.True(collectionChanged);
                Assert.Null(prop);
                Assert.Equal(3, doc.Content.ChildList.Count);
                Assert.True(Match(new string[] { "zero", "one", "2" }, doc.Content.ChildList));

                //---------------------
                // RemoveAt 

                collectionChanged = false;
                changed = false;
                prop = null;

                ((INotifyCollectionChanged)doc.Content.ChildList).CollectionChanged +=
                    (s, a) =>
                    {
                        collectionChanged = true;
                    };

                doc.Content.ChildList.RemoveAt(2);

                Assert.True(changed);
                Assert.True(collectionChanged);
                Assert.Null(prop);
                Assert.Equal(2, doc.Content.ChildList.Count);
                Assert.True(Match(new string[] { "zero", "one" }, doc.Content.ChildList));

                //---------------------
                // Add 

                collectionChanged = false;
                changed = false;
                prop = null;

                ((INotifyCollectionChanged)doc.Content.ChildList).CollectionChanged +=
                    (s, a) =>
                    {
                        collectionChanged = true;
                    };

                doc.Content.ChildList.Add(new TestEntity() { String = "two" });

                Assert.True(changed);
                Assert.True(collectionChanged);
                Assert.Null(prop);
                Assert.Equal(3, doc.Content.ChildList.Count);
                Assert.True(Match(new string[] { "zero", "one", "two" }, doc.Content.ChildList));

                //---------------------
                // CopyTo 

                collectionChanged = false;
                changed = false;
                prop = null;

                ((INotifyCollectionChanged)doc.Content.ChildList).CollectionChanged +=
                    (s, a) =>
                    {
                        collectionChanged = true;
                    };

                var copy = new TestEntity[3];

                doc.Content.ChildList.CopyTo(copy, 0);
                Assert.True(Match(new string[] { "zero", "one", "two" }, copy));

                Assert.False(changed);
                Assert.False(collectionChanged);
                Assert.Null(prop);
                Assert.Equal(3, doc.Content.ChildList.Count);
                Assert.True(Match(new string[] { "zero", "one", "two" }, doc.Content.ChildList));

                //---------------------
                // Clear 

                collectionChanged = false;
                changed = false;
                prop = null;

                ((INotifyCollectionChanged)doc.Content.ChildList).CollectionChanged +=
                    (s, a) =>
                    {
                        collectionChanged = true;
                    };

                doc.Content.ChildList.Clear();

                Assert.True(changed);
                Assert.True(collectionChanged);
                Assert.Null(prop);
                Assert.Equal(0, doc.Content.ChildList.Count);
                Assert.True(Match(new string[0], doc.Content.ChildList));

                //---------------------
                // Ensure that the array changes are persisted. 

                doc.Content.ChildList.Add(new TestEntity() { String = "a" });
                doc.Content.ChildList.Add(new TestEntity() { String = "b" });
                doc.Content.ChildList.Add(new TestEntity() { String = "c" });

                Assert.True(changed);
                Assert.Null(prop);
                Assert.Equal(3, doc.Content.ChildList.Count);
                Assert.True(Match(new string[] { "a", "b", "c" }, doc.Content.ChildList));

                doc.Save();

                doc = db.GetEntityDocument<TestEntity>("1");

                Assert.NotNull(doc.Content.ChildList);
                Assert.True(Match(new string[] { "a", "b", "c" }, doc.Content.ChildList));

                //---------------------
                // Test enumeration

                doc.Revise();
                doc.Content.ChildList.Clear();
                doc.Content.ChildList.Add(new TestEntity() { String = "a" });
                doc.Content.ChildList.Add(new TestEntity() { String = "b" });
                doc.Content.ChildList.Add(new TestEntity() { String = "c" });
                doc.Content.ChildList.Add(null);
                doc.Save();

                var list = new List<TestEntity>();

                foreach (var element in doc.Content.ChildList)
                {
                    list.Add(element);
                }

                Assert.Equal(4, list.Count);
                Assert.Equal("a", list[0].String);
                Assert.Equal("b", list[1].String);
                Assert.Equal("c", list[2].String);
                Assert.Null(list[3]);

                //---------------------
                // Test NULL list values.

                doc = db.GetEntityDocument<TestEntity>("2");

                Assert.Null(doc.Content.ChildList);

                doc.Content.ChildList = new TestEntity[] { null, new TestEntity() { String = "one" } };

                Assert.Equal(2, doc.Content.ChildList.Count);
                Assert.Null(doc.Content.ChildList[0]);
                Assert.Equal("one", doc.Content.ChildList[1].String);

                doc.Save();

                doc = db.GetEntityDocument<TestEntity>("2");

                Assert.Equal(2, doc.Content.ChildList.Count);
                Assert.Null(doc.Content.ChildList[0]);
                Assert.Equal("one", doc.Content.ChildList[1].String);

                //---------------------
                // Verify that changes to property of a list item bubble up.

                doc.Revise();

                doc.Changed +=
                    (s, a) =>
                    {
                        changed = true;
                    };

                doc.Content.PropertyChanged +=
                    (s, a) =>
                    {
                        prop = a.PropertyName;
                    };

                changed = false;
                doc.Content.ChildList[1].Int = 100;
                Assert.True(changed);

                //---------------------
                // Verify that list items are detached when they
                // replaced so that they will no longer modify the
                // document.

                var item = doc.Content.ChildList[1];

                changed = false;
                doc.Content.ChildList[1].Int = 10;
                Assert.True(changed);

                changed = false;
                doc.Content.ChildList[1] = null;
                Assert.True(changed);

                changed = false;
                item.Int = 20;
                Assert.False(changed);

                doc.Content.ChildList.Add(item);
                changed = false;
                item.Int = 30;
                Assert.True(changed);

                doc.Content.ChildList.Remove(item);
                changed = false;
                item.Int = 40;
                Assert.False(changed);

                doc.Content.ChildList.Add(item);
                changed = false;
                item.Int = 50;
                Assert.True(changed);

                doc.Content.ChildList.RemoveAt(doc.Content.ChildList.Count - 1);
                changed = false;
                item.Int = 60;
                Assert.False(changed);

                //---------------------
                // Verify that list items are detached when the
                // list is cleared so that they will no longer 
                // modify the document.

                item1 = new TestEntity() { String = "one" };
                item2 = new TestEntity() { String = "two" };

                doc.Content.ChildList = new TestEntity[] { item1, item2, null };

                changed = false;
                doc.Content.ChildList[1].Int = 10;
                Assert.True(changed);

                changed = false;
                doc.Content.ChildList.Clear();
                Assert.Equal(0, doc.Content.ChildList.Count);
                Assert.True(changed);

                changed = false;
                item1.Int = 20;
                Assert.False(changed);

                changed = false;
                item2.Int = 30;
                Assert.False(changed);

                doc.Cancel();
            }
        }
    }
}
