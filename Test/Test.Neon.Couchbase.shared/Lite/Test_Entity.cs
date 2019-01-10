//-----------------------------------------------------------------------------
// FILE:	    Test_Entity.cs
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
    public class Test_Entity
    {
        public Test_Entity()
        {
            // We need to make sure all generated entity 
            // classes have been registered.

            ModelTypes.Register();
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void Create()
        {
            using (var test = new TestDatabase())
            {
                var db = test.Database;
                var doc1 = db.CreateEntityDocument<TestEntity>();

                Assert.Equal("test.entity", doc1.Type);
                Assert.False(doc1.IsReadOnly);

                doc1.Content.String = "Hello World!";
                doc1.Content.Int = 10;
                Assert.Equal("Hello World!", doc1.Content.String);
                Assert.Equal(10, doc1.Content.Int);

                doc1.Save();

                var doc2 = db.GetEntityDocument<TestEntity>(doc1.Id);

                Assert.True(doc2.IsReadOnly);
                Assert.Equal("Hello World!", doc2.Content.String);
                Assert.Equal(10, doc2.Content.Int);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void Get()
        {
            using (var test = new TestDatabase())
            {
                var db = test.Database;
                var doc1 = db.GetEntityDocument<TestEntity>("1");

                Assert.Equal("1", doc1.Id);
                Assert.False(doc1.IsReadOnly);

                doc1.Content.String = "Hello World!";
                doc1.Content.Int = 22;
                Assert.Equal("Hello World!", doc1.Content.String);
                Assert.Equal(22, doc1.Content.Int);

                doc1.Save();

                var doc2 = db.GetEntityDocument<TestEntity>("1");

                Assert.True(doc2.IsReadOnly);
                Assert.Equal("Hello World!", doc1.Content.String);
                Assert.Equal(22, doc2.Content.Int);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void GetExisting()
        {
            using (var test = new TestDatabase())
            {
                var db = test.Database;

                Assert.Null(db.GetExistingEntityDocument<TestEntity>("1"));

                var doc1 = db.GetEntityDocument<TestEntity>("1");

                Assert.False(doc1.IsReadOnly);

                doc1.Content.String = "Hello World!";
                doc1.Content.Int = 22;
                doc1.Save();

                var doc2 = db.GetExistingEntityDocument<TestEntity>("1");

                Assert.True(doc2.IsReadOnly);
                Assert.Equal("Hello World!", doc1.Content.String);
                Assert.Equal(22, doc2.Content.Int);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void Defaults()
        {
            // Verify that entity properties are initialized with default values.

            using (var test = new TestDatabase())
            {
                var db = test.Database;
                var doc = db.GetEntityDocument<TestEntity>("1");

                Assert.Null(doc.Content.String);
                Assert.Equal(0, doc.Content.Int);
                Assert.Equal(Guid.Empty, doc.Content.Guid);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void PropertyChanged()
        {
            using (var test = new TestDatabase())
            {
                var db = test.Database;
                var doc = db.GetEntityDocument<TestEntity>("1");
                var prop = string.Empty;

                doc.Content.PropertyChanged +=
                    (s, a) =>
                    {
                        prop = a.PropertyName;
                    };

                prop = null;
                doc.Content.String = null;
                Assert.Null(prop);

                prop = null;
                doc.Content.String = "hello";
                Assert.Equal("String", prop);

                prop = null;
                doc.Content.String = "world";
                Assert.Equal("String", prop);

                prop = null;
                doc.Content.String = "world";
                Assert.Null(prop);

                prop = null;
                doc.Content.String = null;
                Assert.Equal("String", prop);
                Assert.Null(doc.Content.String);

                prop = null;
                doc.Content.Int = 10;
                Assert.Equal("Int", prop);

                prop = null;
                doc.Content.Int = 10;
                Assert.Null(prop);

                prop = null;
                doc.Content.Guid = Guid.Empty;
                Assert.Null(prop);

                prop = null;
                doc.Content.Guid = Guid.NewGuid();
                Assert.Equal("Guid", prop);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void Nested()
        {
            using (var test = new TestDatabase())
            {
                var db = test.Database;
                var doc = db.GetEntityDocument<TestEntity>("1");

                Assert.Null(doc.Content.Child);

                doc.Content.String = "FOOBAR";
                doc.Content.Child = new TestEntity()
                {
                    String = "Hello",
                    Int = 55
                };

                Assert.NotNull(doc.Content.Child);
                Assert.Equal("FOOBAR", doc.Content.String);
                Assert.Equal("Hello", doc.Content.Child.String);
                Assert.Equal(55, doc.Content.Child.Int);

                doc.Save();

                doc = db.GetEntityDocument<TestEntity>("1");

                Assert.NotNull(doc.Content.Child);
                Assert.Equal("FOOBAR", doc.Content.String);
                Assert.Equal("Hello", doc.Content.Child.String);
                Assert.Equal(55, doc.Content.Child.Int);

                doc.Revise();

                var guid = Guid.NewGuid();

                doc.Content.Child.String = "HELLO WORLD!";
                doc.Content.Child.Guid = guid;

                Assert.Equal("HELLO WORLD!", doc.Content.Child.String);
                Assert.Equal(guid, doc.Content.Child.Guid);

                doc.Save();
                doc = db.GetEntityDocument<TestEntity>("1");

                Assert.Equal("HELLO WORLD!", doc.Content.Child.String);
                Assert.Equal(guid, doc.Content.Child.Guid);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void Nested_Detached()
        {
            using (var test = new TestDatabase())
            {
                var db = test.Database;
                var doc = db.GetEntityDocument<TestEntity>("1");

                Assert.Null(doc.Content.Child);

                var child = new TestEntity()
                {
                    String = "Hello",
                    Int = 55
                };

                doc.Content.Child = child;

                Assert.NotNull(doc.Content.Child);
                Assert.Equal("Hello", doc.Content.Child.String);
                Assert.Equal(55, doc.Content.Child.Int);

                doc.Save();

                doc = db.GetEntityDocument<TestEntity>("1");

                Assert.NotNull(doc.Content.Child);
                Assert.Equal("Hello", doc.Content.Child.String);
                Assert.Equal(55, doc.Content.Child.Int);

                doc.Revise();

                var guid = Guid.NewGuid();

                doc.Content.Child.String = "HELLO WORLD!";
                doc.Content.Child.Guid = guid;
                doc.Save();

                doc = db.GetEntityDocument<TestEntity>("1");

                Assert.Equal("HELLO WORLD!", doc.Content.Child.String);
                Assert.Equal(guid, doc.Content.Child.Guid);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void Nested_PropertChanged()
        {
            using (var test = new TestDatabase())
            {
                var db = test.Database;
                var doc = db.GetEntityDocument<TestEntity>("1");
                var changed = false;
                var prop0 = string.Empty;
                var prop1 = string.Empty;

                doc.Changed +=
                    (s, a) =>
                    {
                        changed = true;
                    };

                doc.Content.PropertyChanged +=
                    (s, a) =>
                    {
                        prop0 = a.PropertyName;
                    };

                changed = false;
                prop0 = null;
                doc.Content.Child = new TestEntity()
                {
                    String = "Hello",
                    Int = 55
                };
                Assert.True(changed);
                Assert.Equal("Child", prop0);

                doc.Content.Child.PropertyChanged +=
                    (s, a) =>
                    {
                        prop1 = a.PropertyName;
                    };

                changed = false;
                prop0 = null;
                prop1 = null;
                doc.Content.Child.String = "TEST";
                Assert.True(changed);
                Assert.Null(prop0);
                Assert.Equal("String", prop1);

                // Detach the child property and make sure that changes
                // to it no longer bubble up to the document.

                var child = doc.Content.Child;

                doc.Content.Child = null;

                changed = false;
                child.String = "TEST #2";
                Assert.False(changed);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void Delete()
        {
            using (var test = new TestDatabase())
            {
                var db = test.Database;

                var doc1 = db.GetEntityDocument<TestEntity>("1");

                Assert.False(doc1.IsReadOnly);

                doc1.Content.String = "Hello World!";
                doc1.Content.Int = 22;
                doc1.Save();

                var doc2 = db.GetExistingEntityDocument<TestEntity>("1");

                Assert.True(doc2.IsReadOnly);
                Assert.Equal("Hello World!", doc1.Content.String);
                Assert.Equal(22, doc2.Content.Int);

                Assert.False(doc2.IsDeleted);
                doc2.Delete();
                Assert.True(doc2.IsDeleted);
                Assert.Null(db.GetExistingEntityDocument<TestEntity>("1"));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void Metadata()
        {
            using (var test = new TestDatabase())
            {
                var db = test.Database;
                var doc = db.GetEntityDocument<TestEntity>("1");
                var changed = false;

                doc.Changed +=
                    (s, a) =>
                    {
                        changed = true;
                    };

                Assert.Equal("test.entity", doc.Type);
                Assert.Null(doc.Channels);

                // Verify that Type and Channels are persisted.

                changed = false;

                doc.Type = "t1";
                doc.Channels = new string[] { "c1", "c2" };

                Assert.True(changed);
                Assert.Equal("t1", doc.Type);
                Assert.Equal(new string[] { "c1", "c2" }, doc.Channels);

                Assert.True(doc.IsModified);
                doc.Save();
                doc = db.GetExistingEntityDocument<TestEntity>("1");

                Assert.Equal("t1", doc.Type);
                Assert.Equal(new string[] { "c1", "c2" }, doc.Channels);

                // Verify that setting an empty channel array removes the channels.

                doc.Revise();
                doc.Channels = new string[0];
                doc.Save();
                doc = db.GetExistingEntityDocument<TestEntity>("1");
                Assert.Null(doc.Channels);
                Assert.True(!doc.Properties.ContainsKey(NeonPropertyNames.Channels));
                Assert.Equal("t1", doc.Type);

                // Verify that setting a null Type removes the property. 

                doc.Revise();
                doc.Type = null;
                doc.Save();
                doc = db.GetExistingEntityDocument<TestEntity>("1");
                Assert.Null(doc.Type);
                Assert.True(!doc.Properties.ContainsKey(NeonPropertyNames.Type));

                // Verify that we can set a custom property.

                doc.Changed +=
                    (s, a) =>
                    {
                        changed = true;
                    };

                doc.Revise();

                changed = false;
                doc["FOO"] = "BAR";
                Assert.True(changed);

                doc.Save();
                doc = db.GetExistingEntityDocument<TestEntity>("1");
                Assert.Equal("BAR", doc["FOO"]);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void ListString_Basic()
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

                Assert.Null(doc.Content.StringList);

                changed = false;
                prop = null;

                //---------------------
                // Assign a collection.

                doc.Content.StringList = new string[] { "1", "2" };

                Assert.True(changed);
                Assert.Equal("StringList", prop);
                Assert.NotNull(doc.Content.StringList);
                Assert.Equal(new string[] { "1", "2" }, doc.Content.StringList, new CollectionComparer<string>());

                //---------------------
                // Persist and verify

                doc.Save();

                doc = db.GetEntityDocument<TestEntity>("1");

                Assert.NotNull(doc.Content.StringList);
                Assert.Equal(new string[] { "1", "2" }, doc.Content.StringList, new CollectionComparer<string>());

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

                doc.Content.StringList = null;

                Assert.True(changed);
                Assert.Equal("StringList", prop);
                Assert.Null(doc.Content.StringList);

                doc.Save();

                doc = db.GetEntityDocument<TestEntity>("1");

                Assert.Null(doc.Content.StringList);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void ListString_Operations()
        {
            // Verify that list operations work and also raise the correct 
            // change events.

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

                Assert.Null(doc.Content.StringList);

                //---------------------
                // Assignment

                changed = false;
                prop = null;

                doc.Content.StringList = new string[] { "1", "2" };

                Assert.True(changed);
                Assert.Equal("StringList", prop);
                Assert.NotNull(doc.Content.StringList);
                Assert.Equal(new string[] { "1", "2" }, doc.Content.StringList, new CollectionComparer<string>());
                Assert.Equal(2, doc.Content.StringList.Count);

                //---------------------
                // IndexOf

                changed = false;
                prop = null;

                Assert.Equal(0, doc.Content.StringList.IndexOf("1"));
                Assert.Equal(1, doc.Content.StringList.IndexOf("2"));
                Assert.Equal(-1, doc.Content.StringList.IndexOf("3"));

                Assert.False(changed);
                Assert.Null(prop);

                //---------------------
                // Contains

                changed = false;
                prop = null;

                Assert.True(doc.Content.StringList.Contains("1"));
                Assert.True(doc.Content.StringList.Contains("2"));
                Assert.False(doc.Content.StringList.Contains("3"));

                Assert.False(changed);
                Assert.Null(prop);

                //---------------------
                // Indexing 

                changed = false;
                prop = null;

                Assert.Equal("1", doc.Content.StringList[0]);
                Assert.Equal("2", doc.Content.StringList[1]);

                doc.Content.StringList[0] = "one";

                Assert.True(changed);
                Assert.Null(prop);
                Assert.Equal("one", doc.Content.StringList[0]);
                Assert.Equal("2", doc.Content.StringList[1]);

                Assert.Equal(new string[] { "one", "2" }, doc.Content.StringList, new CollectionComparer<string>());

                //---------------------
                // Insert 

                changed = false;
                prop = null;

                doc.Content.StringList.Insert(0, "zero");
                doc.Content.StringList.Insert(3, "three");

                Assert.True(changed);
                Assert.Null(prop);
                Assert.Equal(4, doc.Content.StringList.Count);
                Assert.Equal(new string[] { "zero", "one", "2", "three" }, doc.Content.StringList, new CollectionComparer<string>());

                //---------------------
                // Remove

                changed = false;
                prop = null;

                Assert.True(doc.Content.StringList.Remove("three"));
                Assert.False(doc.Content.StringList.Remove("four"));

                Assert.True(changed);
                Assert.Null(prop);
                Assert.Equal(3, doc.Content.StringList.Count);
                Assert.Equal(new string[] { "zero", "one", "2" }, doc.Content.StringList, new CollectionComparer<string>());

                //---------------------
                // RemoveAt 

                changed = false;
                prop = null;

                doc.Content.StringList.RemoveAt(2);

                Assert.True(changed);
                Assert.Null(prop);
                Assert.Equal(2, doc.Content.StringList.Count);
                Assert.Equal(new string[] { "zero", "one" }, doc.Content.StringList, new CollectionComparer<string>());

                //---------------------
                // Add 

                changed = false;
                prop = null;

                doc.Content.StringList.Add("two");

                Assert.True(changed);
                Assert.Null(prop);
                Assert.Equal(3, doc.Content.StringList.Count);
                Assert.Equal(new string[] { "zero", "one", "two" }, doc.Content.StringList, new CollectionComparer<string>());

                //---------------------
                // CopyTo 

                changed = false;
                prop = null;

                var copy = new string[3];

                doc.Content.StringList.CopyTo(copy, 0);
                Assert.Equal(new string[] { "zero", "one", "two" }, copy, new CollectionComparer<string>());

                Assert.False(changed);
                Assert.Null(prop);
                Assert.Equal(3, doc.Content.StringList.Count);
                Assert.Equal(new string[] { "zero", "one", "two" }, doc.Content.StringList, new CollectionComparer<string>());

                //---------------------
                // Clear 

                changed = false;
                prop = null;

                doc.Content.StringList.Clear();

                Assert.True(changed);
                Assert.Null(prop);
                Assert.Equal(0, doc.Content.StringList.Count);
                Assert.Equal(new string[0], doc.Content.StringList, new CollectionComparer<string>());

                //---------------------
                // Ensure that the array changes are persisted. 

                changed = false;
                prop = null;

                doc.Content.StringList.Add("a");
                doc.Content.StringList.Add("b");
                doc.Content.StringList.Add("c");

                Assert.True(changed);
                Assert.Null(prop);
                Assert.Equal(3, doc.Content.StringList.Count);
                Assert.Equal(new string[] { "a", "b", "c" }, doc.Content.StringList, new CollectionComparer<string>());

                doc.Save();

                doc = db.GetEntityDocument<TestEntity>("1");

                Assert.NotNull(doc.Content.StringList);
                Assert.Equal(new string[] { "a", "b", "c" }, doc.Content.StringList, new CollectionComparer<string>());

                //---------------------
                // Enumeration.

                doc.Revise();
                doc.Content.StringList.Clear();
                doc.Content.StringList.Add("a");
                doc.Content.StringList.Add("b");
                doc.Content.StringList.Add("c");
                doc.Content.StringList.Add(null);
                doc.Save();

                var list = new List<string>();

                foreach (var element in doc.Content.StringList)
                {
                    list.Add(element);
                }

                Assert.Equal(4, list.Count);
                Assert.Equal("a", list[0]);
                Assert.Equal("b", list[1]);
                Assert.Equal("c", list[2]);
                Assert.Null(list[3]);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void EventsAfterRead()
        {
            using (var test = new TestDatabase())
            {
                var db = test.Database;

                // Verify that nested change events still work after documents
                // are saved.

                var doc = db.GetEntityDocument<TestEntity>("foo");
                var changed = false;

                doc.Changed +=
                    (s, a) =>
                    {
                        changed = true;
                    };

                changed = false;
                doc.Content.String = "one";
                doc.Content.Child = new TestEntity() { String = "two" };
                Assert.True(changed);

                doc.Save();
                doc.Revise();

                changed = false;
                doc.Content.String = "xxx";
                Assert.True(changed);

                changed = false;
                doc.Content.Child.String = "yyy";
                Assert.True(changed);

                doc.Save();
                doc.Revise();

                changed = false;
                doc.Content.Child.Int = 10;
                Assert.True(changed);

                changed = false;
                doc.Content.StringList = new string[] { "1" };
                Assert.True(changed);

                changed = false;
                doc.Content.ChildList = new TestEntity[] { new TestEntity() { String = "a" } };
                Assert.True(changed);

                changed = false;
                doc.Content.ChildList[0].String = "b";
                Assert.True(changed);

                // Save and reload the document and try again.

                doc.Save();
                doc = db.GetEntityDocument<TestEntity>("foo");

                doc.Changed +=
                    (s, a) =>
                    {
                        changed = true;
                    };

                doc.Revise();

                changed = false;
                doc.Content.String = "HELLO";
                Assert.True(changed);

                changed = false;
                doc.Content.Int = 20;
                Assert.True(changed);

                changed = false;
                doc.Content.Child.String = "HELLO";
                Assert.True(changed);

                changed = false;
                doc.Content.Child.Int = 20;
                Assert.True(changed);

                changed = false;
                doc.Content.StringList = new string[] { "2" };
                Assert.True(changed);

                changed = false;
                doc.Content.ChildList[0].String = "c";
                Assert.True(changed);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void IsModified()
        {
            using (var test = new TestDatabase())
            {
                var db = test.Database;

                // Verify that new documents are implicitly considered as modified.

                var doc = db.CreateEntityDocument<TestEntity>();

                Assert.True(doc.IsModified);
                Assert.False(doc.IsReadOnly);

                doc = db.GetEntityDocument<TestEntity>("Foo");

                Assert.True(doc.IsModified);
                Assert.False(doc.IsReadOnly);

                // Verify changing a property marks a document as modified.

                doc.Content.String = "Hello";
                Assert.True(doc.IsModified);

                // Verify the persisting a document clears IsModified

                doc.Save();
                Assert.False(doc.IsModified);

                // Verify that newly loaded documents are unmodified.

                doc = db.GetEntityDocument<TestEntity>("Foo");
                Assert.False(doc.IsModified);

                // Verify that adding a sub-entity marks a document as modified.

                doc.Revise();
                doc.Content.Child = new TestEntity();
                Assert.True(doc.IsModified);
                doc.Save();
                Assert.False(doc.IsModified);

                // Verify that setting a property on a sub-entity marks a document as modified.

                doc.Revise();
                doc.Content.Child.Int = 10;
                Assert.True(doc.IsModified);
                doc.Save();
                Assert.False(doc.IsModified);

                // Verify that modifying an element of a simple array marks a document as modified.

                doc.Revise();
                doc.Content.StringList = new string[] { "one", "two", "three" };
                doc.Save();

                Assert.False(doc.IsModified);
                doc.Revise();
                doc.Content.StringList[1] = "2";
                Assert.True(doc.IsModified);
                doc.Save();

                Assert.False(doc.IsModified);
                doc.Revise();
                doc.Content.StringList.Add("four");
                Assert.Equal("four", doc.Content.StringList[3]);
                Assert.True(doc.IsModified);
                doc.Save();

                // Verify that modifying an element of an entity array marks a document as modified.

                doc.Revise();
                doc.Content.ChildList = new TestEntity[] { new TestEntity() { String = "zero" } };
                doc.Save();

                Assert.False(doc.IsModified);
                doc.Revise();
                doc.Content.ChildList[0].String = "0";
                Assert.True(doc.IsModified);
                doc.Save();

                Assert.False(doc.IsModified);
                doc.Revise();
                doc.Content.ChildList.Add(new TestEntity() { String = "one" });
                Assert.Equal("one", doc.Content.ChildList[1].String);
                Assert.True(doc.IsModified);
                doc.Save();
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void ReadOnly()
        {
            using (var test = new TestDatabase())
            {
                var db = test.Database;

                // Verify that we'll see exceptions when we try to modify a read-only document.

                var doc = db.GetEntityDocument<TestEntity>("foo");

                doc.Content.Int = 10;
                doc.Content.StringList = new string[] { "zero", "one", "two" };
                doc.Content.Child = new TestEntity() { String = "foo" };
                doc.Content.ChildList = new TestEntity[] { new TestEntity() { String = "bar" } };

                doc.Save();
                doc = db.GetEntityDocument<TestEntity>("foo");

                Assert.Throws<InvalidOperationException>(() => doc.Content.Int = 20);
                Assert.Throws<InvalidOperationException>(() => doc.Content.StringList[1] = "1");
                Assert.Throws<InvalidOperationException>(() => doc.Content.StringList.RemoveAt(1));
                Assert.Throws<InvalidOperationException>(() => doc.Content.Child.String = null);
                Assert.Throws<InvalidOperationException>(() => doc.Content.ChildList[0] = null);
                Assert.Throws<InvalidOperationException>(() => doc.Type = "hello");
                Assert.Throws<InvalidOperationException>(() => doc.Channels = new string[] { "a", "b" });
                Assert.Throws<InvalidOperationException>(() => doc["custom"] = "value");
                Assert.Throws<InvalidOperationException>(() => doc.Save());
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void EntityEquals_Explicit()
        {
            // Verify that entities equality comparisions take into
            // account every entity property and array.

            var entity1 = new TestEntity();
            var entity2 = new TestEntity();

            Assert.False(entity1.Equals(null));
            Assert.True(entity1.Equals(entity2));

            entity1.String = "hello";
            entity2.String = "hello";
            Assert.True(entity1.Equals(entity2));

            entity1.String = "hello";
            entity2.String = "world";
            Assert.False(entity1.Equals(entity2));
            entity2.String = "hello";

            entity1.String = "hello";
            entity2.String = null;
            Assert.False(entity1.Equals(entity2));
            entity2.String = "hello";

            entity1.Int = 10;
            entity2.Int = 10;
            Assert.True(entity1.Equals(entity2));

            entity1.Int = 10;
            entity2.Int = 20;
            Assert.False(entity1.Equals(entity2));
            entity2.Int = 10;

            var guid = Guid.NewGuid();

            entity1.Guid = guid;
            entity2.Guid = guid;
            Assert.True(entity1.Equals(entity2));

            entity1.Guid = guid;
            entity2.Guid = Guid.Empty;
            Assert.False(entity1.Equals(entity2));
            entity2.Guid = guid;

            entity1.StringList = new string[] { "0", "1", "2" };
            entity2.StringList = new string[] { "0", "1", "2" };
            Assert.True(entity1.Equals(entity2));

            entity1.StringList = new string[] { "zero", "1", "2" };
            entity2.StringList = new string[] { "0", "1", "2" };
            Assert.False(entity1.Equals(entity2));
            entity1.StringList = new string[] { "0", "1", "2" };

            entity1.StringList = new string[] { "0", "1" };
            entity2.StringList = new string[] { "0", "1", "2" };
            Assert.False(entity1.Equals(entity2));
            entity1.StringList = new string[] { "0", "1", "2" };

            entity1.StringList = new string[] { "0", "1" };
            entity2.StringList = null;
            Assert.False(entity1.Equals(entity2));
            entity2.StringList = new string[] { "0", "1", "2" };

            entity1.StringList = new string[] { "0", "1", null };
            entity2.StringList = new string[] { "0", "1", null };
            Assert.True(entity1.Equals(entity2));
            entity2.StringList = new string[] { "0", "1", "2" };

            entity1.StringList = new string[] { "0", "1", null };
            entity2.StringList = new string[] { "0", "1", "2" };
            Assert.False(entity1.Equals(entity2));
            entity1.StringList = new string[] { "0", "1", "2" };

            entity1.Child = new TestEntity();
            entity2.Child = new TestEntity();
            Assert.True(entity1.Equals(entity2));

            entity1.Child = new TestEntity();
            entity2.Child = null;
            Assert.False(entity1.Equals(entity2));
            entity2.Child = new TestEntity();

            entity1.Child.String = "Hello";
            Assert.False(entity1.Equals(entity2));
            entity2.Child.String = "Hello";
            Assert.True(entity1.Equals(entity2));

            entity1.ChildList = new TestEntity[] { new TestEntity() { Int = 10 } };
            entity2.ChildList = new TestEntity[] { new TestEntity() { Int = 10 } };
            Assert.True(entity1.Equals(entity2));

            entity1.ChildList[0].Int = 20;
            entity2.ChildList = new TestEntity[] { new TestEntity() { Int = 10 } };
            Assert.False(entity1.Equals(entity2));
            entity1.ChildList[0].Int = 10;

            entity1.ChildList.Add(new TestEntity() { Int = 20 });
            Assert.False(entity1.Equals(entity2));

            entity2.ChildList.Add(new TestEntity() { Int = 20 });
            Assert.True(entity1.Equals(entity2));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void EntityEquals_Operator()
        {
            // Verify that entity equality and inequality
            // operators work.

            var entity1 = new TestEntity();
            var entity2 = new TestEntity();

            Assert.False(entity1 == null);
            Assert.True(entity1 != null);
            Assert.True(entity1 == entity2);
            Assert.False(entity1 != entity2);

            entity1.String = "hello";
            entity2.String = "hello";
            Assert.True(entity1 == entity2);
            Assert.False(entity1 != entity2);

            entity1.String = "hello";
            entity2.String = "world";
            Assert.False(entity1 == entity2);
            Assert.True(entity1 != entity2);
            entity2.String = "hello";

            entity1.Int = 10;
            entity2.Int = 10;
            Assert.True(entity1 == entity2);
            Assert.False(entity1 != entity2);

            entity1.StringList = new string[] { "0", "1", "2" };
            entity2.StringList = new string[] { "0", "1", "2" };
            Assert.True(entity1 == entity2);
            Assert.False(entity1 != entity2);

            entity1.StringList = new string[] { "zero", "1", "2" };
            entity2.StringList = new string[] { "0", "1", "2" };
            Assert.False(entity1 == entity2);
            Assert.True(entity1 != entity2);
            entity1.StringList = new string[] { "0", "1", "2" };

            entity1.Child = new TestEntity();
            entity2.Child = new TestEntity();
            Assert.True(entity1 == entity2);
            Assert.False(entity1 != entity2);

            entity1.Child = new TestEntity();
            entity2.Child = null;
            Assert.False(entity1 == entity2);
            Assert.True(entity1 != entity2);
            entity2.Child = new TestEntity();

            entity1.Child.String = "Hello";
            Assert.False(entity1 == entity2);
            Assert.True(entity1 != entity2);
            entity2.Child.String = "Hello";
            Assert.True(entity1 == entity2);
            Assert.False(entity1 != entity2);

            entity1.ChildList = new TestEntity[] { new TestEntity() { Int = 10 } };
            entity2.ChildList = new TestEntity[] { new TestEntity() { Int = 10 } };
            Assert.True(entity1 == entity2);
            Assert.False(entity1 != entity2);

            entity1.ChildList[0].Int = 20;
            entity2.ChildList = new TestEntity[] { new TestEntity() { Int = 10 } };
            Assert.False(entity1 == entity2);
            Assert.True(entity1 != entity2);
            entity1.ChildList[0].Int = 10;

            entity1.ChildList.Add(new TestEntity() { Int = 20 });
            Assert.False(entity1 == entity2);
            Assert.True(entity1 != entity2);

            entity2.ChildList.Add(new TestEntity() { Int = 20 });
            Assert.True(entity1 == entity2);
            Assert.False(entity1 != entity2);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void Cancel()
        {
            using (var test = new TestDatabase())
            {
                var db = test.Database;
                var doc = db.GetEntityDocument<TestEntity>("1");

                // Verify that cancelling the revision of a new document doesn't barf.

                doc.Content.String = "Hello World!";
                doc.Cancel();

                // Test canceling the revision of an existing document.

                doc = db.GetEntityDocument<TestEntity>("2");

                doc.Revise();   // Verify that we can call this when we're already R/W
                doc.Content.String = "Hello";
                doc.Save();

                Assert.Equal("Hello", doc.Content.String);

                doc.Revise();
                doc.Content.String = "Goodbye";
                doc.Cancel();

                Assert.Equal("Hello", doc.Content.String);

                // Test cancelling with changed metadata

                doc = db.GetEntityDocument<TestEntity>("2");

                Assert.Equal("Hello", doc.Content.String);

                doc.Revise();
                doc.Channels = new string[] { "one", "two" };
                doc["FOO"] = "BAR";
                doc.Content.String = "BYE";
                doc.Content.Int = 100;
                doc.Cancel();

                Assert.Null(doc.Channels);
                Assert.Null(doc["FOO"]);
                Assert.Equal("Hello", doc.Content.String);
                Assert.Equal(0, doc.Content.Int);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void Timestamp()
        {
            using (var test = new TestDatabase())
            {
                var db = test.Database;
                var doc = db.GetEntityDocument<TestEntity>("1");
                var now = DateTime.UtcNow;

                doc.SaveWithTimestamp();

                Assert.True(now <= doc.Timestamp && doc.Timestamp <= now + TimeSpan.FromSeconds(5));

                doc = db.GetEntityDocument<TestEntity>("1");

                Assert.True(now <= doc.Timestamp && doc.Timestamp <= now + TimeSpan.FromSeconds(5));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void Change()
        {
            // Verify that Change and PropertyChange events are raised when 
            // documents are indirectly modified.

            using (var test = new TestDatabase())
            {
                var db = test.Database;
                var doc1 = db.GetEntityDocument<TestEntity>("1");

                doc1.Save();

                var doc2 = db.GetExistingEntityDocument<TestEntity>("1");
                var changed = false;
                var docProps = new List<string>();
                var contentProps = new List<string>();

                doc2.Changed +=
                    (s, a) =>
                    {
                        changed = true;
                    };

                doc2.PropertyChanged +=
                    (s, a) =>
                    {
                        docProps.Add(a.PropertyName);
                    };

                doc2.Content.PropertyChanged +=
                    (s, a) =>
                    {
                        contentProps.Add(a.PropertyName);
                    };

                //-------------------------------
                // Verify a content property.

                changed = false;
                docProps.Clear();
                contentProps.Clear();

                doc1.Revise();
                doc1.Content.Int = 10;
                doc1.Save();

                Assert.True(changed);
                Assert.Contains("Int", contentProps);
                Assert.Equal(10, doc2.Content.Int);

                //-------------------------------
                // Verify a document property.

                changed = false;
                docProps.Clear();
                contentProps.Clear();

                var utcNow = DateTime.UtcNow;

                doc1.Revise();
                doc1.Content.String = "Hello!";
                doc1.SaveWithTimestamp();

                Assert.True(changed);
                Assert.Equal(10, doc2.Content.Int);
                Assert.Equal("Hello!", doc2.Content.String);
                Assert.True(doc2.Timestamp >= utcNow);

                //-------------------------------
                // Verify document deletion.

                changed = false;

                doc1.Delete();

                Assert.True(changed);
                Assert.True(doc2.IsDeleted);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void EnumEntityType()
        {
            // Verify that the entity generator handles enumeration type values
            // correctly.

            // Verify that the generator used the [EnumMember] attribute value.
            Assert.Equal("test.entity1", new EnumTypedEntity1()._GetEntityType());

            // Verify that the generator fell-back to the enum string value.
            Assert.Equal("TestEntity2", new EnumTypedEntity2()._GetEntityType());

            // Verify that we handle [IsPropertyType=true] generation correctly.

            var entity3 = new EnumTypedEntity3();

            Assert.Equal("TestEntity3", entity3._GetEntityType());
            Assert.Equal(TestEntityTypes.TestEntity3, entity3.Type);

            // Verify that we can persist these types to a database.

            using (var test = new TestDatabase())
            {
                var db = test.Database;
                var doc1 = db.GetEntityDocument<EnumTypedEntity1>("1");

                Assert.Equal("test.entity1", doc1.Type);

                doc1.Content.Age = 55;
                doc1.Save();

                doc1 = db.GetEntityDocument<EnumTypedEntity1>("1");
                Assert.Equal("test.entity1", doc1.Type);
                Assert.Equal(55, doc1.Content.Age);

                //-------------------------------

                var doc2 = db.GetEntityDocument<EnumTypedEntity2>("2");

                Assert.Equal("TestEntity2", doc2.Type);

                doc2.Content.Age = 66;
                doc2.Save();

                doc2 = db.GetEntityDocument<EnumTypedEntity2>("2");
                Assert.Equal("TestEntity2", doc2.Type);
                Assert.Equal(66, doc2.Content.Age);

                //-------------------------------

                var doc3 = db.GetEntityDocument<EnumTypedEntity3>("3");

                Assert.Equal("TestEntity3", doc3.Type);
                Assert.Equal(TestEntityTypes.TestEntity3, doc3.Content.Type);

                doc3.Content.Age = 77;
                doc3.Save();

                doc3 = db.GetEntityDocument<EnumTypedEntity3>("3");
                Assert.Equal("TestEntity3", doc3.Type);
                Assert.Equal(TestEntityTypes.TestEntity3, doc3.Content.Type);
                Assert.Equal(77, doc3.Content.Age);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void StringEntityType()
        {
            // Verify that the entity generator handles string type values
            // correctly.

            // Verify that the generator used the [EnumMember] attribute value.
            Assert.Equal("string.entity1", new StringTypedEntity1()._GetEntityType());

            // Verify that the generator fell-back to the enum string value.
            Assert.Equal("string.entity2", new StringTypedEntity2()._GetEntityType());

            // Verify that we handle [IsPropertyType=true] generation correctly.

            var entity3 = new StringTypedEntity3();

            Assert.Equal("string.entity3", entity3._GetEntityType());
            Assert.Equal("string.entity3", entity3.Type);

            // Verify that we can persist these types to a database.

            using (var test = new TestDatabase())
            {
                var db = test.Database;
                var doc1 = db.GetEntityDocument<StringTypedEntity1>("1");

                Assert.Equal("string.entity1", doc1.Type);

                doc1.Content.Age = 55;
                doc1.Save();

                doc1 = db.GetEntityDocument<StringTypedEntity1>("1");
                Assert.Equal("string.entity1", doc1.Type);
                Assert.Equal(55, doc1.Content.Age);

                //-------------------------------

                var doc2 = db.GetEntityDocument<StringTypedEntity2>("2");

                Assert.Equal("string.entity2", doc2.Type);

                doc2.Content.Age = 66;
                doc2.Save();

                doc2 = db.GetEntityDocument<StringTypedEntity2>("2");
                Assert.Equal("string.entity2", doc2.Type);
                Assert.Equal(66, doc2.Content.Age);

                //-------------------------------

                var doc3 = db.GetEntityDocument<StringTypedEntity3>("3");

                Assert.Equal("string.entity3", doc3.Type);
                Assert.Equal("string.entity3", doc3.Content.Type);

                doc3.Content.Age = 77;
                doc3.Save();

                doc3 = db.GetEntityDocument<StringTypedEntity3>("3");
                Assert.Equal("string.entity3", doc3.Type);
                Assert.Equal("string.entity3", doc3.Content.Type);
                Assert.Equal(77, doc3.Content.Age);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void EnumProperties()
        {
            // Verify that enum and enum array property types work.

            using (var test = new TestDatabase())
            {
                var db = test.Database;
                var doc = db.GetEntityDocument<EnumTypedEntity3>("1");

                doc.Content.Enum = TestEntityTypes.TestEntity2;
                doc.Content.EnumArray = new TestEntityTypes[] { TestEntityTypes.TestEntity1, TestEntityTypes.TestEntity2, TestEntityTypes.TestEntity3 };

                Assert.Equal(TestEntityTypes.TestEntity2, doc.Content.Enum);
                Assert.Equal(new TestEntityTypes[] { TestEntityTypes.TestEntity1, TestEntityTypes.TestEntity2, TestEntityTypes.TestEntity3 }, doc.Content.EnumArray.ToArray());

                doc.Save();
                doc = db.GetEntityDocument<EnumTypedEntity3>("1");

                Assert.Equal(TestEntityTypes.TestEntity2, doc.Content.Enum);
                Assert.Equal(new TestEntityTypes[] { TestEntityTypes.TestEntity1, TestEntityTypes.TestEntity2, TestEntityTypes.TestEntity3 }, doc.Content.EnumArray.ToArray());
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void LinkedEntity()
        {
            // Verify that simple linked entities work.

            using (var test = new TestDatabase())
            {
                var db = test.Database;
                var doc1 = db.GetEntityDocument<TestEntity>("1");

                // Verify that link related things are initialized.

                Assert.Null(doc1.Content.ChildLink);
                Assert.Equal(doc1.Id, doc1.Content._GetLink());

                doc1.Content.String = "FOO";
                doc1.Save();

                // Link a document and verify.

                var doc2 = db.GetEntityDocument<TestEntity>("2");

                doc2.Content.String = "BAR";
                doc2.Content.ChildLink = doc1.Content;

                Assert.NotNull(doc2.Content.ChildLink);
                Assert.Equal("FOO", doc2.Content.ChildLink.String);
                Assert.Equal("BAR", doc2.Content.String);

                // Save, reload and re-verify.

                doc2.Save();
                doc2 = db.GetEntityDocument<TestEntity>("2");

                Assert.NotNull(doc2.Content.ChildLink);
                Assert.Equal("FOO", doc2.Content.ChildLink.String);
                Assert.Equal("BAR", doc2.Content.String);

                // Link to an unsaved document and verify that the link
                // returns NULL until the document is saved.  This is the
                // expected behavior because the IEntityContext can't load
                // the entity until after it's been persisted.

                var doc3 = db.GetEntityDocument<TestEntity>("3");

                doc3.Content.String = "FOOBAR";

                doc2.Revise();
                doc2.Content.ChildLink = doc3.Content;
                doc2.Save();
                Assert.Null(doc2.Content.ChildLink);

                doc3.Save();
                Assert.NotNull(doc2.Content.ChildLink);
                Assert.Equal("FOOBAR", doc2.Content.ChildLink.String);

                // Verify that a link returns NULL after the target document
                // has been deleted.

                doc3.Delete();
                Assert.Null(doc2.Content.ChildLink);

                // Verify that a change to a linked document DOES NOT 
                // cause the parent to think it's been modified.

                var doc4 = db.GetEntityDocument<TestEntity>("4");
                var doc5 = db.GetEntityDocument<TestEntity>("5");

                doc4.Content.ChildLink = doc5.Content;
                doc5.Content.String = "DOC-5";
                doc4.Save();
                doc5.Save();

                Assert.False(doc4.IsModified);
                Assert.False(doc5.IsModified);

                doc5.Revise();
                doc5.Content.String = "HELLO WORLD!";
                doc5.Save();

                Assert.False(doc4.IsModified);
                Assert.Equal("HELLO WORLD!", doc4.Content.ChildLink.String);

                Assert.False(doc5.IsModified);
                Assert.Equal("HELLO WORLD!", doc5.Content.String);

                // Verify that we're not allowed to link to an entity that's
                // not hosted by a document.

                Assert.Throws<ArgumentException>(() => doc4.Content.ChildLink = new TestEntity());
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void LinkedDocument()
        {
            // Verify that simple linked documents work.

            using (var test = new TestDatabase())
            {
                var db = test.Database;
                var doc1 = db.GetBinderDocument<TestBinder>("1");

                // Verify that document link related things are initialized.

                Assert.Null(doc1.Content.DocLink);
                Assert.Equal(doc1.Id, doc1._GetLink());

                doc1.Content.String = "FOO";
                doc1.Save();

                // Link a document and verify.

                var doc2 = db.GetBinderDocument<TestBinder>("2");

                doc2.Content.String = "BAR";
                doc2.Content.DocLink = doc1;

                Assert.NotNull(doc2.Content.DocLink);
                Assert.Equal("FOO", doc2.Content.DocLink.Content.String);
                Assert.Equal("BAR", doc2.Content.String);

                // Save, reload and re-verify.

                doc2.Save();
                doc2 = db.GetBinderDocument<TestBinder>("2");

                Assert.NotNull(doc2.Content.DocLink);
                Assert.Equal("FOO", doc2.Content.DocLink.Content.String);
                Assert.Equal("BAR", doc2.Content.String);

                // Link to an unsaved document and verify that the link
                // returns NULL until the document is saved.  This is the
                // expected behavior because the IEntityContext can't load
                // the entity until after it's been persisted.

                var doc3 = db.GetBinderDocument<TestBinder>("3");

                doc3.Content.String = "FOOBAR";

                doc2.Revise();
                doc2.Content.DocLink = doc3;
                doc2.Save();
                Assert.Null(doc2.Content.DocLink);

                doc3.Save();
                Assert.NotNull(doc2.Content.DocLink);
                Assert.Equal("FOOBAR", doc2.Content.DocLink.Content.String);

                // Verify that a link returns NULL after the target document
                // has been deleted.

                doc3.Delete();
                Assert.Null(doc2.Content.DocLink);

                // Verify that a change to a linked document DOES NOT 
                // cause the parent to think it's been modified.

                var doc4 = db.GetBinderDocument<TestBinder>("4");
                var doc5 = db.GetBinderDocument<TestBinder>("5");

                doc4.Content.DocLink = doc5;
                doc5.Content.String = "DOC-5";
                doc4.Save();
                doc5.Save();

                Assert.False(doc4.IsModified);
                Assert.False(doc5.IsModified);

                doc5.Revise();
                doc5.Content.String = "HELLO WORLD!";
                doc5.Save();

                Assert.False(doc4.IsModified);
                Assert.Equal("HELLO WORLD!", doc4.Content.DocLink.Content.String);

                Assert.False(doc5.IsModified);
                Assert.Equal("HELLO WORLD!", doc5.Content.String);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void NoChange()
        {
            using (var test = new TestDatabase())
            {
                var db = test.Database;
                var doc = db.GetEntityDocument<TestEntity>("1");
                var changed = false;

                doc.Changed +=
                    (s, a) =>
                    {
                        changed = true;
                    };

                doc.Save();
                Assert.False(changed);

                doc.Revise();
                Assert.False(changed);

                doc.Save();
                Assert.False(changed);
            }
        }
    }
}