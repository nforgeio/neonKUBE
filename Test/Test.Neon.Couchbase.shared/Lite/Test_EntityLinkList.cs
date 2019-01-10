//-----------------------------------------------------------------------------
// FILE:	    Test_EntityLinkList.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

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
    public class Test_EntityLinkList
    {
        public Test_EntityLinkList()
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
                var doc = db.GetEntityDocument<TestEntity>("0");
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

                Assert.Null(doc.Content.LinkList);

                changed = false;
                prop = null;

                //---------------------
                // Assign a collection of linkable entities.

                var doc1 = db.GetEntityDocument<TestEntity>("1");
                var doc2 = db.GetEntityDocument<TestEntity>("2");

                doc1.Content.String = "1";
                doc2.Content.String = "2";

                doc1.Save();
                doc2.Save();

                doc.Content.LinkList = new TestEntity[] { doc1.Content, doc2.Content };

                Assert.True(changed);
                Assert.Equal("LinkList", prop);
                Assert.NotNull(doc.Content.LinkList);
                Assert.True(Match(new string[] { "1", "2" }, doc.Content.LinkList));

                //---------------------
                // Persist and verify

                doc.Save();
                doc = db.GetEntityDocument<TestEntity>("0");

                Assert.NotNull(doc.Content.LinkList);
                Assert.Equal("1", doc.Content.LinkList[0].String);
                Assert.Equal("2", doc.Content.LinkList[1].String);
                Assert.True(Match(new string[] { "1", "2" }, doc.Content.LinkList));

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

                doc.Content.LinkList = null;

                Assert.True(changed);
                Assert.Equal("LinkList", prop);
                Assert.Null(doc.Content.LinkList);

                doc.Save();

                doc = db.GetEntityDocument<TestEntity>("1");

                Assert.Null(doc.Content.LinkList);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void Operations()
        {
            // Verify that entity link list operations work and also raise the correct 
            // change events.

            using (var test = new TestDatabase())
            {
                var db = test.Database;
                var doc = db.GetEntityDocument<TestEntity>("0");
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

                Assert.Null(doc.Content.LinkList);

                //---------------------
                // Assignment

                var doc1 = db.GetEntityDocument<TestEntity>("1");
                var doc2 = db.GetEntityDocument<TestEntity>("2");
                var doc3 = db.GetEntityDocument<TestEntity>("3");

                doc1.Content.String = "1";
                doc2.Content.String = "2";
                doc3.Content.String = "3";

                doc1.Save();
                doc2.Save();
                doc3.Save();

                changed = false;
                prop = null;

                var item1 = doc1.Content;
                var item2 = doc2.Content;
                var item3 = doc3.Content;

                doc.Content.LinkList = new TestEntity[] { item1, item2 };

                Assert.True(changed);
                Assert.Equal("LinkList", prop);
                Assert.NotNull(doc.Content.LinkList);
                Assert.True(Match(new string[] { "1", "2" }, doc.Content.LinkList));
                Assert.Equal(2, doc.Content.LinkList.Count);

                //---------------------
                // IndexOf

                collectionChanged = false;
                changed = false;
                prop = null;

                ((INotifyCollectionChanged)doc.Content.LinkList).CollectionChanged +=
                    (s, a) =>
                    {
                        collectionChanged = true;
                    };

                Assert.Equal(0, doc.Content.LinkList.IndexOf(item1));
                Assert.Equal(1, doc.Content.LinkList.IndexOf(item2));
                Assert.Equal(-1, doc.Content.LinkList.IndexOf(item3));
                Assert.Equal(-1, doc.Content.LinkList.IndexOf(null));
                Assert.Throws<ArgumentException>(() => doc.Content.LinkList.IndexOf(new TestEntity()));

                Assert.False(changed);
                Assert.False(collectionChanged);
                Assert.Null(prop);

                //---------------------
                // Contains

                ((INotifyCollectionChanged)doc.Content.LinkList).CollectionChanged +=
                    (s, a) =>
                    {
                        collectionChanged = true;
                    };

                Assert.True(doc.Content.LinkList.Contains(item1));
                Assert.True(doc.Content.LinkList.Contains(item2));
                Assert.False(doc.Content.LinkList.Contains(item3));
                Assert.Throws<ArgumentException>(() => doc.Content.LinkList.Contains(new TestEntity()));

                Assert.False(changed);
                Assert.False(collectionChanged);
                Assert.Null(prop);

                //---------------------
                // Indexing 

                ((INotifyCollectionChanged)doc.Content.LinkList).CollectionChanged +=
                    (s, a) =>
                    {
                        collectionChanged = true;
                    };

                Assert.Equal("1", doc.Content.LinkList[0].String);
                Assert.Equal("2", doc.Content.LinkList[1].String);

                doc.Content.LinkList[0] = item3;

                Assert.True(changed);
                Assert.True(collectionChanged);
                Assert.Null(prop);
                Assert.Equal("3", doc.Content.LinkList[0].String);
                Assert.Equal("2", doc.Content.LinkList[1].String);

                Assert.True(Match(new string[] { "3", "2" }, doc.Content.LinkList));

                doc.Content.LinkList[0] = null;

                Assert.Null(doc.Content.LinkList[0]);

                Assert.Throws<ArgumentException>(() => doc.Content.LinkList[0] = new TestEntity());

                //---------------------
                // Insert 

                doc.Content.LinkList = new TestEntity[] { item1, item2 };

                ((INotifyCollectionChanged)doc.Content.LinkList).CollectionChanged +=
                    (s, a) =>
                    {
                        collectionChanged = true;
                    };

                prop = null;
                changed = false;
                collectionChanged = false;

                doc.Content.LinkList.Insert(0, item3);
                doc.Content.LinkList.Insert(3, null);

                Assert.True(changed);
                Assert.True(collectionChanged);
                Assert.Null(prop);
                Assert.Equal(4, doc.Content.LinkList.Count);
                Assert.Equal("3", doc.Content.LinkList[0].String);
                Assert.Equal("1", doc.Content.LinkList[1].String);
                Assert.Equal("2", doc.Content.LinkList[2].String);
                Assert.Null(doc.Content.LinkList[3]);

                Assert.Throws<ArgumentException>(() => doc.Content.LinkList.Insert(4, new TestEntity()));

                //---------------------
                // Remove

                doc.Content.LinkList = new TestEntity[] { item1, item2 };

                ((INotifyCollectionChanged)doc.Content.LinkList).CollectionChanged +=
                    (s, a) =>
                    {
                        collectionChanged = true;
                    };

                prop = null;
                changed = false;
                collectionChanged = false;

                Assert.True(doc.Content.LinkList.Remove(item1));
                Assert.False(doc.Content.LinkList.Remove(item3));
                Assert.False(doc.Content.LinkList.Remove(null));

                Assert.True(changed);
                Assert.True(collectionChanged);
                Assert.Null(prop);
                Assert.Equal(1, doc.Content.LinkList.Count);
                Assert.True(Match(new string[] { "2" }, doc.Content.LinkList));

                Assert.Throws<ArgumentException>(() => doc.Content.LinkList.Remove(new TestEntity()));

                //---------------------
                // RemoveAt 

                doc.Content.LinkList = new TestEntity[] { item1, item2 };

                ((INotifyCollectionChanged)doc.Content.LinkList).CollectionChanged +=
                    (s, a) =>
                    {
                        collectionChanged = true;
                    };

                prop = null;
                changed = false;
                collectionChanged = false;

                doc.Content.LinkList.RemoveAt(1);

                Assert.True(changed);
                Assert.True(collectionChanged);
                Assert.Null(prop);
                Assert.Equal(1, doc.Content.LinkList.Count);
                Assert.True(Match(new string[] { "1" }, doc.Content.LinkList));

                //---------------------
                // Add 

                doc.Content.LinkList = new TestEntity[] { item1, item2 };

                ((INotifyCollectionChanged)doc.Content.LinkList).CollectionChanged +=
                    (s, a) =>
                    {
                        collectionChanged = true;
                    };

                prop = null;
                changed = false;
                collectionChanged = false;

                doc.Content.LinkList.Add(item3);

                Assert.True(changed);
                Assert.True(collectionChanged);
                Assert.Null(prop);
                Assert.Equal(3, doc.Content.LinkList.Count);
                Assert.True(Match(new string[] { "1", "2", "3" }, doc.Content.LinkList));

                doc.Content.LinkList.Add(null);

                Assert.Equal(4, doc.Content.LinkList.Count);
                Assert.Null(doc.Content.LinkList[3]);

                Assert.Throws<ArgumentException>(() => doc.Content.LinkList.Add(new TestEntity()));

                //---------------------
                // CopyTo 

                doc.Content.LinkList = new TestEntity[] { item1, item2, null };

                ((INotifyCollectionChanged)doc.Content.LinkList).CollectionChanged +=
                    (s, a) =>
                    {
                        collectionChanged = true;
                    };

                prop = null;
                changed = false;
                collectionChanged = false;

                var copy = new TestEntity[3];

                doc.Content.LinkList.CopyTo(copy, 0);
                Assert.Equal("1", copy[0].String);
                Assert.Equal("2", copy[1].String);
                Assert.Null(copy[2]);

                Assert.False(changed);
                Assert.False(collectionChanged);
                Assert.Null(prop);
                Assert.Equal(3, doc.Content.LinkList.Count);
                Assert.Equal("1", doc.Content.LinkList[0].String);
                Assert.Equal("2", doc.Content.LinkList[1].String);
                Assert.Null(doc.Content.LinkList[2]);

                //---------------------
                // Clear 

                doc.Content.LinkList = new TestEntity[] { item1, item2 };

                ((INotifyCollectionChanged)doc.Content.LinkList).CollectionChanged +=
                    (s, a) =>
                    {
                        collectionChanged = true;
                    };

                Assert.Equal(2, doc.Content.LinkList.Count);

                prop = null;
                changed = false;
                collectionChanged = false;

                doc.Content.LinkList.Clear();

                Assert.True(changed);
                Assert.True(collectionChanged);
                Assert.Null(prop);
                Assert.Equal(0, doc.Content.LinkList.Count);
                Assert.True(Match(new string[0], doc.Content.LinkList));

                //---------------------
                // Ensure that the array changes are persisted. 

                prop = null;
                changed = false;
                collectionChanged = false;

                doc.Content.LinkList.Clear();
                doc.Content.LinkList.Add(item1);
                doc.Content.LinkList.Add(null);
                doc.Content.LinkList.Add(item3);

                Assert.True(changed);
                Assert.True(collectionChanged);
                Assert.Null(prop);
                Assert.Equal(3, doc.Content.LinkList.Count);
                Assert.Equal("1", doc.Content.LinkList[0].String);
                Assert.Null(doc.Content.LinkList[1]);
                Assert.Equal("3", doc.Content.LinkList[2].String);

                doc.Save();

                doc = db.GetEntityDocument<TestEntity>("0");

                Assert.NotNull(doc.Content.LinkList);
                Assert.Equal(3, doc.Content.LinkList.Count);
                Assert.Equal("1", doc.Content.LinkList[0].String);
                Assert.Null(doc.Content.LinkList[1]);
                Assert.Equal("3", doc.Content.LinkList[2].String);

                //---------------------
                // Enumeration

                var list = new List<TestEntity>();

                foreach (var element in doc.Content.LinkList)
                {
                    list.Add(element);
                }

                Assert.Equal(3, list.Count);
                Assert.Equal("1", list[0].String);
                Assert.Null(list[1]);
                Assert.Equal("3", list[2].String);
            }
        }
    }
}
