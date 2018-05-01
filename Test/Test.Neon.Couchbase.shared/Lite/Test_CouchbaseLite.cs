//-----------------------------------------------------------------------------
// FILE:	    Test_CouchbaseLite.cs
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

using TestEntity = Test.Neon.Models.TestEntity;

namespace TestLiteExtensions
{
    /// <summary>
    /// Couchbase Lite low-level behavior tests.
    /// </summary>
    public class Test_CouchbaseLite
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void RevisionProperties()
        {
            using (var test = new TestDatabase())
            {
                var db = test.Database;

                var doc = db.CreateDocument();

                Assert.Null(doc.CurrentRevision);

                var unsaved1 = doc.CreateRevision();

                unsaved1.Properties[NeonPropertyNames.Content] = new JObject();

                var saved1 = unsaved1.Save();

                Assert.NotNull(doc.CurrentRevision);

                var unsaved2 = doc.CreateRevision();

                Assert.NotSame(unsaved1, doc.CurrentRevision);
                Assert.NotSame(unsaved2, doc.CurrentRevision);

                Assert.NotSame(unsaved2.Properties, unsaved1.Properties);
                Assert.NotSame(unsaved2.Properties, unsaved1.Properties);
                Assert.NotSame(unsaved2.Properties, doc.CurrentRevision.Properties);
                Assert.NotSame(saved1.Properties, unsaved1.Properties);
                Assert.NotSame(saved1.Properties, unsaved2.Properties);

                Assert.NotSame(unsaved2.Properties[NeonPropertyNames.Content], unsaved1.Properties[NeonPropertyNames.Content]);
                Assert.NotSame(unsaved2.Properties[NeonPropertyNames.Content], unsaved1.Properties[NeonPropertyNames.Content]);
                Assert.NotSame(unsaved1.Properties[NeonPropertyNames.Content], unsaved2.Properties[NeonPropertyNames.Content]);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void ArrayClone()
        {
            // Verifying that ToArray() of an array actually makes a copy.

            var array = new string[] { "1", "2", "3" };

            Assert.NotSame(array, array.ToArray());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void Attachments_Bytes()
        {
            using (var test = new TestDatabase())
            {
                var db  = test.Database;
                var doc = db.CreateDocument();

                var unsaved = doc.CreateRevision();

                unsaved.SetAttachment("test", "application/data", new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

                var saved = unsaved.Save();

                var attachment = saved.GetAttachment("test");
                var bytes      = new byte[10];

                using (var stream = attachment.ContentStream)
                {
                    Assert.Equal(10, stream.Read(bytes, 0, 10));
                    Assert.Equal(0, stream.Read(bytes, 0, 10));

                    var file = (FileStream)stream;
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void Attachments_Stream()
        {
            var filePath = Path.GetTempFileName();

            try
            {
                using (var test = new TestDatabase())
                {
                    var db = test.Database;
                    var doc = db.CreateDocument();

                    var unsaved = doc.CreateRevision();
                    var output = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite);

                    output.Write(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
                    output.Position = 0;

                    unsaved.SetAttachment("test", "application/data", output);

                    var saved = unsaved.Save();

                    output.Close();

                    var attachment = saved.GetAttachment("test");

                    using (var input = attachment.ContentStream)
                    {
                        var bytes = new byte[10];

                        Assert.Equal(10, input.Read(bytes, 0, 10));
                        Assert.Equal(0, input.Read(bytes, 0, 10));

                        var file = (FileStream)input;
                    }
                }
            }
            finally
            {
                File.Delete(filePath);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void RemoveAttachment()
        {
            using (var db = Manager.SharedInstance.GetDatabase("wonky"))
            {
                var doc     = db.CreateDocument();
                var unsaved = doc.CreateRevision();

                unsaved.SetAttachment("attach", "type", new byte[] { 0, 1, 2, 3, 4 });

                var saved = unsaved.Save();

                Assert.NotNull(doc.CurrentRevision.GetAttachment("attach"));

                unsaved = saved.CreateRevision();

                unsaved.RemoveAttachment("attach");

                saved = unsaved.Save();

                Assert.Equal(doc.CurrentRevision, saved);

                Assert.Empty(doc.CurrentRevision.AttachmentNames);
                Assert.Null(doc.CurrentRevision.GetAttachment("attach"));

                Assert.Empty(saved.AttachmentNames);
                Assert.Null(saved.GetAttachment("attach"));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void REPORT_RemoveAttachment()
        {
            // Used to report a Couchbase Lite issue:
            //
            //      https://github.com/couchbase/couchbase-lite-net/issues/661

            using (var db = Manager.SharedInstance.GetDatabase("wonky"))
            {
                var doc = db.CreateDocument();
                var unsaved = doc.CreateRevision();

                unsaved.SetAttachment("attach", "type", new byte[] { 0, 1, 2, 3, 4 });

                // NOTE: These asserts pass.

                Assert.Single(unsaved.AttachmentNames);
                Assert.NotNull(unsaved.GetAttachment("attach"));

                var saved = unsaved.Save();

                Assert.Single(saved.AttachmentNames);
                Assert.NotNull(saved.GetAttachment("attach"));

                unsaved = saved.CreateRevision();
                unsaved.RemoveAttachment("attach");

                // NOTE: The following two asserts will both fail.

                //Assert.Equal(0, unsaved.AttachmentNames.Count());
                //Assert.Null(unsaved.GetAttachment("attach"));

                // NOTE: These asserts will fail when Couchbase addresses issue #661

                Assert.Single(unsaved.AttachmentNames);
                Assert.NotNull(unsaved.GetAttachment("attach"));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void REPORT_UnsavedAttachmentContentType()
        {
            // Used to report a Couchbase Lite issue:
            //
            //      https://github.com/couchbase/couchbase-lite-net/issues/666

            using (var db = Manager.SharedInstance.GetDatabase("wonky"))
            {
                var doc = db.CreateDocument();
                var unsaved = doc.CreateRevision();

                unsaved.SetAttachment("attach", "type", new byte[] { 0, 1, 2, 3, 4 });

                var attachment = unsaved.GetAttachment("attach");

                //Assert.Equal("type", attachment.ContentType);  // <<--- Throws a NullReferenceException

                // NOTE: This assert will fail when Couchbase addresses issue #666

                Assert.Throws<NullReferenceException>(() => attachment.ContentType);
            }
        }
    }
}
