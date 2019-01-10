//-----------------------------------------------------------------------------
// FILE:	    Test_DocLinkList.cs
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
    public class Test_DocLinkList
    {
        public Test_DocLinkList()
        {
            // We need to make sure all generated entity 
            // classes have been registered.

            ModelTypes.Register();
        }

        /// <summary>
        /// Hack to verify that a document list matches a set of values by comparing
        /// the list items' <see cref="TestEntity.String"/> property values with the 
        /// values passed.
        /// </summary>
        /// <param name="values">The expected values..</param>
        /// <param name="list">The list being tested.</param>
        /// <returns><c>true</c> if they match</returns>
        private bool Match(string[] values, IList<TestBinder> list)
        {
            if (list.Count != values.Length)
            {
                return false;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (list[i].Content.String != values[i])
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
                var doc = db.GetBinderDocument<TestBinder>("0");
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

                Assert.Null(doc.Content.DocList);

                changed = false;
                prop = null;

                //---------------------
                // Assign a collection of linkable documents.

                var doc1 = db.GetBinderDocument<TestBinder>("1");
                var doc2 = db.GetBinderDocument<TestBinder>("2");

                doc1.Content.String = "1";
                doc2.Content.String = "2";

                doc1.Save();
                doc2.Save();

                doc.Content.DocList = new TestBinder[] { doc1, doc2 };

                Assert.True(changed);
                Assert.Equal("DocList", prop);
                Assert.NotNull(doc.Content.DocList);
                Assert.True(Match(new string[] { "1", "2" }, doc.Content.DocList));

                //---------------------
                // Persist and verify

                doc.Save();
                doc = db.GetBinderDocument<TestBinder>("0");

                Assert.NotNull(doc.Content.DocList);
                Assert.Equal("1", doc.Content.DocList[0].Content.String);
                Assert.Equal("2", doc.Content.DocList[1].Content.String);
                Assert.True(Match(new string[] { "1", "2" }, doc.Content.DocList));

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

                doc.Content.DocList = null;

                Assert.True(changed);
                Assert.Equal("DocList", prop);
                Assert.Null(doc.Content.DocList);

                doc.Save();

                doc = db.GetBinderDocument<TestBinder>("1");

                Assert.Null(doc.Content.DocList);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void Operations()
        {
            // Verify that document link list operations work and also raise the correct 
            // change events.

            using (var test = new TestDatabase())
            {
                var db = test.Database;
                var doc = db.GetBinderDocument<TestBinder>("0");
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

                Assert.Null(doc.Content.DocList);

                //---------------------
                // Assignment

                var doc1 = db.GetBinderDocument<TestBinder>("1");
                var doc2 = db.GetBinderDocument<TestBinder>("2");
                var doc3 = db.GetBinderDocument<TestBinder>("3");

                doc1.Content.String = "1";
                doc2.Content.String = "2";
                doc3.Content.String = "3";

                doc1.Save();
                doc2.Save();
                doc3.Save();

                changed = false;
                prop = null;

                var item1 = doc1;
                var item2 = doc2;
                var item3 = doc3;

                doc.Content.DocList = new TestBinder[] { item1, item2 };

                Assert.True(changed);
                Assert.Equal("DocList", prop);
                Assert.NotNull(doc.Content.DocList);
                Assert.True(Match(new string[] { "1", "2" }, doc.Content.DocList));
                Assert.Equal(2, doc.Content.DocList.Count);

                //---------------------
                // IndexOf

                collectionChanged = false;
                changed = false;
                prop = null;

                ((INotifyCollectionChanged)doc.Content.DocList).CollectionChanged +=
                    (s, a) =>
                    {
                        collectionChanged = true;
                    };

                Assert.Equal(0, doc.Content.DocList.IndexOf(item1));
                Assert.Equal(1, doc.Content.DocList.IndexOf(item2));
                Assert.Equal(-1, doc.Content.DocList.IndexOf(item3));
                Assert.Equal(-1, doc.Content.DocList.IndexOf(null));

                Assert.False(changed);
                Assert.False(collectionChanged);
                Assert.Null(prop);

                //---------------------
                // Contains

                ((INotifyCollectionChanged)doc.Content.DocList).CollectionChanged +=
                    (s, a) =>
                    {
                        collectionChanged = true;
                    };

                Assert.True(doc.Content.DocList.Contains(item1));
                Assert.True(doc.Content.DocList.Contains(item2));
                Assert.False(doc.Content.DocList.Contains(item3));

                Assert.False(changed);
                Assert.False(collectionChanged);
                Assert.Null(prop);

                //---------------------
                // Indexing 

                ((INotifyCollectionChanged)doc.Content.DocList).CollectionChanged +=
                    (s, a) =>
                    {
                        collectionChanged = true;
                    };

                Assert.Equal("1", doc.Content.DocList[0].Content.String);
                Assert.Equal("2", doc.Content.DocList[1].Content.String);

                doc.Content.DocList[0] = item3;

                Assert.True(changed);
                Assert.True(collectionChanged);
                Assert.Null(prop);
                Assert.Equal("3", doc.Content.DocList[0].Content.String);
                Assert.Equal("2", doc.Content.DocList[1].Content.String);

                Assert.True(Match(new string[] { "3", "2" }, doc.Content.DocList));

                doc.Content.DocList[0] = null;

                Assert.Null(doc.Content.DocList[0]);

                //---------------------
                // Insert 

                doc.Content.DocList = new TestBinder[] { item1, item2 };

                ((INotifyCollectionChanged)doc.Content.DocList).CollectionChanged +=
                    (s, a) =>
                    {
                        collectionChanged = true;
                    };

                prop = null;
                changed = false;
                collectionChanged = false;

                doc.Content.DocList.Insert(0, item3);
                doc.Content.DocList.Insert(3, null);

                Assert.True(changed);
                Assert.True(collectionChanged);
                Assert.Null(prop);
                Assert.Equal(4, doc.Content.DocList.Count);
                Assert.Equal("3", doc.Content.DocList[0].Content.String);
                Assert.Equal("1", doc.Content.DocList[1].Content.String);
                Assert.Equal("2", doc.Content.DocList[2].Content.String);
                Assert.Null(doc.Content.DocList[3]);

                //---------------------
                // Remove

                doc.Content.DocList = new TestBinder[] { item1, item2 };

                ((INotifyCollectionChanged)doc.Content.DocList).CollectionChanged +=
                    (s, a) =>
                    {
                        collectionChanged = true;
                    };

                prop = null;
                changed = false;
                collectionChanged = false;

                Assert.True(doc.Content.DocList.Remove(item1));
                Assert.False(doc.Content.DocList.Remove(item3));
                Assert.False(doc.Content.DocList.Remove(null));

                Assert.True(changed);
                Assert.True(collectionChanged);
                Assert.Null(prop);
                Assert.Equal(1, doc.Content.DocList.Count);
                Assert.True(Match(new string[] { "2" }, doc.Content.DocList));

                //---------------------
                // RemoveAt 

                doc.Content.DocList = new TestBinder[] { item1, item2 };

                ((INotifyCollectionChanged)doc.Content.DocList).CollectionChanged +=
                    (s, a) =>
                    {
                        collectionChanged = true;
                    };

                prop = null;
                changed = false;
                collectionChanged = false;

                doc.Content.DocList.RemoveAt(1);

                Assert.True(changed);
                Assert.True(collectionChanged);
                Assert.Null(prop);
                Assert.Equal(1, doc.Content.DocList.Count);
                Assert.True(Match(new string[] { "1" }, doc.Content.DocList));

                //---------------------
                // Add 

                doc.Content.DocList = new TestBinder[] { item1, item2 };

                ((INotifyCollectionChanged)doc.Content.DocList).CollectionChanged +=
                    (s, a) =>
                    {
                        collectionChanged = true;
                    };

                prop = null;
                changed = false;
                collectionChanged = false;

                doc.Content.DocList.Add(item3);

                Assert.True(changed);
                Assert.True(collectionChanged);
                Assert.Null(prop);
                Assert.Equal(3, doc.Content.DocList.Count);
                Assert.True(Match(new string[] { "1", "2", "3" }, doc.Content.DocList));

                doc.Content.DocList.Add(null);

                Assert.Equal(4, doc.Content.DocList.Count);
                Assert.Null(doc.Content.DocList[3]);

                //---------------------
                // CopyTo 

                doc.Content.DocList = new TestBinder[] { item1, item2, null };

                ((INotifyCollectionChanged)doc.Content.DocList).CollectionChanged +=
                    (s, a) =>
                    {
                        collectionChanged = true;
                    };

                prop = null;
                changed = false;
                collectionChanged = false;

                var copy = new TestBinder[3];

                doc.Content.DocList.CopyTo(copy, 0);
                Assert.Equal("1", copy[0].Content.String);
                Assert.Equal("2", copy[1].Content.String);
                Assert.Null(copy[2]);

                Assert.False(changed);
                Assert.False(collectionChanged);
                Assert.Null(prop);
                Assert.Equal(3, doc.Content.DocList.Count);
                Assert.Equal("1", doc.Content.DocList[0].Content.String);
                Assert.Equal("2", doc.Content.DocList[1].Content.String);
                Assert.Null(doc.Content.DocList[2]);

                //---------------------
                // Clear 

                doc.Content.DocList = new TestBinder[] { item1, item2 };

                ((INotifyCollectionChanged)doc.Content.DocList).CollectionChanged +=
                    (s, a) =>
                    {
                        collectionChanged = true;
                    };

                Assert.Equal(2, doc.Content.DocList.Count);

                prop = null;
                changed = false;
                collectionChanged = false;

                doc.Content.DocList.Clear();

                Assert.True(changed);
                Assert.True(collectionChanged);
                Assert.Null(prop);
                Assert.Equal(0, doc.Content.DocList.Count);
                Assert.True(Match(new string[0], doc.Content.DocList));

                //---------------------
                // Ensure that the array changes are persisted. 

                prop = null;
                changed = false;
                collectionChanged = false;

                doc.Content.DocList.Clear();
                doc.Content.DocList.Add(item1);
                doc.Content.DocList.Add(null);
                doc.Content.DocList.Add(item3);

                Assert.True(changed);
                Assert.True(collectionChanged);
                Assert.Null(prop);
                Assert.Equal(3, doc.Content.DocList.Count);
                Assert.Equal("1", doc.Content.DocList[0].Content.String);
                Assert.Null(doc.Content.DocList[1]);
                Assert.Equal("3", doc.Content.DocList[2].Content.String);

                doc.Save();

                doc = db.GetBinderDocument<TestBinder>("0");

                Assert.NotNull(doc.Content.DocList);
                Assert.Equal(3, doc.Content.DocList.Count);
                Assert.Equal("1", doc.Content.DocList[0].Content.String);
                Assert.Null(doc.Content.DocList[1]);
                Assert.Equal("3", doc.Content.DocList[2].Content.String);

                //---------------------
                // Enumeration

                var list = new List<TestBinder>();

                foreach (var element in doc.Content.DocList)
                {
                    list.Add(element);
                }

                Assert.Equal(3, list.Count);
                Assert.Equal("1", list[0].Content.String);
                Assert.Null(list[1]);
                Assert.Equal("3", list[2].Content.String);
            }
        }
    }
}
