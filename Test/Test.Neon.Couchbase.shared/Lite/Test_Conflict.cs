//-----------------------------------------------------------------------------
// FILE:	    Test_Conflict.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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
    public class Test_Conflict
    {
        public Test_Conflict()
        {
            // We need to make sure all generated entity 
            // classes have been registered.

            ModelTypes.Register();
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void Default()
        {
            Assert.Equal(ConflictPolicyType.Ignore, ConflictPolicy.Default.Type);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void Ignore()
        {
            // Create conflicting revisions and then verify that one
            // is quietly choosen by Couchbase Lite by [IgnoreConflictPolicy].

            using (var test = new TestDatabase())
            {
                var db = test.Database;
                var doc1 = db.GetEntityDocument<TestEntity>("1");

                doc1.Content.String = "Hello";
                doc1.Save();

                var doc2 = db.GetEntityDocument<TestEntity>("1");

                doc1.Revise();
                doc1.Content.String = "Foo";

                doc2.Revise();
                doc2.Content.String = "Bar";

                doc1.Save(ConflictPolicy.Ignore);
                doc2.Save(ConflictPolicy.Ignore);

                var doc = db.GetEntityDocument<TestEntity>("1");

                Assert.True(doc.Content.String == "Foo" || doc.Content.String == "Bar");
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void Fail()
        {
            // Create conflicting revisions and then verify that 
            // [FailConflictPolicy] throws a [ConflictException].

            using (var test = new TestDatabase())
            {
                var db = test.Database;
                var doc1 = db.GetEntityDocument<TestEntity>("1");

                doc1.Content.String = "Hello";
                doc1.Save();

                var doc2 = db.GetEntityDocument<TestEntity>("1");

                doc1.Revise();
                doc1.Content.String = "Foo";

                doc2.Revise();
                doc2.Content.String = "Bar";

                doc1.Save(ConflictPolicy.Fail);
                Assert.Throws<ConflictException>(() => doc2.Save(ConflictPolicy.Fail));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void KeepThis()
        {
            // Create conflicting revisions and then verify that 
            // [KeepThisConflictPolicy] overwrites an earlier 
            // revision.

            using (var test = new TestDatabase())
            {
                var db = test.Database;
                var doc1 = db.GetEntityDocument<TestEntity>("1");

                doc1.Content.String = "Hello";
                doc1.Save();

                var doc2 = db.GetEntityDocument<TestEntity>("1");

                doc1.Revise();
                doc1.Content.String = "Foo";

                doc2.Revise();
                doc2.Content.String = "Bar";

                doc1.Save(ConflictPolicy.KeepThis);
                doc2.Save(ConflictPolicy.KeepThis);

                Assert.Equal(doc1.Content.String, doc2.Content.String);
                Assert.Equal("Bar", doc1.Content.String);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void KeepOther()
        {
            // Create conflicting revisions and then verify that 
            // [KeepOtherConflictPolicy] retains the earlier 
            // revision.

            using (var test = new TestDatabase())
            {
                var db = test.Database;
                var doc1 = db.GetEntityDocument<TestEntity>("1");

                doc1.Content.String = "Hello";
                doc1.Save();

                var doc2 = db.GetEntityDocument<TestEntity>("1");

                doc1.Revise();
                doc1.Content.String = "Foo";

                doc2.Revise();
                doc2.Content.String = "Bar";

                doc1.Save(ConflictPolicy.KeepOther);
                doc2.Save(ConflictPolicy.KeepOther);

                Assert.Equal(doc1.Content.String, doc2.Content.String);
                Assert.Equal("Foo", doc1.Content.String);
            }
        }

        private class MergePolicy : CustomConflictPolicy
        {
            public override void Resolve(ConflictDetails details)
            {
                // Merge the [String] and [Int] properties for the current and conflicting revisions.

                Assert.Equal(typeof(TestEntity), details.EntityDocument.EntityType);

                var currentRevision = details.Document.CurrentRevision;
                var entityCurrent = (EntityDocument<TestEntity>)details.EntityDocument;

                var history = details.Document.CurrentRevision.RevisionHistory.ToArray();

                foreach (var conflict in details.ConflictingRevisions)
                {
                    var entityConflict = conflict.ToEntityDocument<TestEntity>();

                    if (entityConflict.Content.String != null && entityCurrent.Content.String == null)
                    {
                        entityCurrent.Content.String = entityConflict.Content.String;
                    }

                    if (entityConflict.Content.Int != 0 && entityCurrent.Content.Int == 0)
                    {
                        entityCurrent.Content.Int = entityConflict.Content.Int;
                    }

                    if (conflict != currentRevision)
                    {
                        var unsavedConflict = conflict.CreateRevision();

                        unsavedConflict.IsDeletion = true;
                        unsavedConflict.Save();
                    }
                }

                var unsavedRevision = details.Document.CreateRevision();

                unsavedRevision.SetProperties(entityCurrent.Properties);

                details.SavedRevision = unsavedRevision.Save();
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void Merge()
        {
            // Create conflicting revisions and then verify that 
            // a custom policty can merge a couple of the properties.

            using (var test = new TestDatabase())
            {
                var db = test.Database;
                var doc1 = db.GetEntityDocument<TestEntity>("1");

                doc1.Save();

                var doc2 = db.GetEntityDocument<TestEntity>("1");

                doc1.Revise();
                doc1.Content.String = "Foo";

                doc2.Revise();
                doc2.Content.Int = 10;

                var mergePolicy = new MergePolicy();

                doc1.Save(mergePolicy);
                doc2.Save(mergePolicy);

                Assert.Equal("Foo", doc1.Content.String);
                Assert.Equal(10, doc1.Content.Int);
            }
        }
    }
}
