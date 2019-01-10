//-----------------------------------------------------------------------------
// FILE:	    Test_Attachments.cs
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
    public class Test_Attachments
    {
        public Test_Attachments()
        {
            // We need to make sure all generated entity 
            // classes have been registered.

            ModelTypes.Register();
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void Basic()
        {
            using (var test = new TestDatabase())
            {
                var db = test.Database;
                var doc = db.GetEntityDocument<Product>("1");
                var changed = false;

                //-----------------------------------------
                // Verify that we start out with no attachments.

                Assert.Empty(doc.Attachments);
                Assert.Empty(doc.AttachmentNames);
                Assert.Null(doc.GetAttachment("not-there"));

                //-----------------------------------------
                // Add an attachment and partially verify.  Note that
                // the attachment contents or metadata can't be accessed
                // until the document is saved.

                doc.Changed +=
                    (s, a) =>
                    {
                        changed = true;
                    };

                changed = false;

                doc.SetAttachment("attach-1", new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, "type-1");

                Assert.True(changed);
                Assert.Single(doc.Attachments);
                Assert.Single(doc.AttachmentNames);

                Assert.NotNull(doc.GetAttachment("attach-1"));

                //-----------------------------------------
                // Save, reload, and fully verify.

                doc.Save();

                doc = db.GetEntityDocument<Product>("1");

                doc.Changed +=
                    (s, a) =>
                    {
                        changed = true;
                    };

                Assert.Single(doc.Attachments);
                Assert.Single(doc.AttachmentNames);

                using (var attachment = doc.GetAttachment("attach-1"))
                {
                    Assert.NotNull(attachment);
                    Assert.Equal("attach-1", attachment.Name);
                    Assert.Equal("type-1", attachment.ContentType);
                    Assert.Equal(10, attachment.Length);
                    Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, attachment.Content.ToArray());
                }

                //-----------------------------------------
                // Add another attachment using a stream, save and verify.

                doc.Revise();
                changed = false;
                doc.SetAttachment("attach-2", new MemoryStream(new byte[] { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 }), "type-2");
                Assert.True(changed);
                doc.Save();

                Assert.Equal(2, doc.Attachments.Count());
                Assert.Equal(2, doc.AttachmentNames.Count());

                using (var attachment = doc.GetAttachment("attach-2"))
                {
                    Assert.NotNull(attachment);
                    Assert.Equal("attach-2", attachment.Name);
                    Assert.Equal("type-2", attachment.ContentType);
                    Assert.Equal(10, attachment.Length);
                    Assert.Equal(new byte[] { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 }, attachment.Content.ToArray());
                }

                //-----------------------------------------
                // Remove an attachment and verify after saving.

                doc.Revise();
                changed = false;
                doc.RemoveAttachment("attach-1");
                Assert.True(changed);
                doc.Save();

                Assert.Single(doc.Attachments);
                Assert.Single(doc.AttachmentNames);

                Assert.Null(doc.GetAttachment("attach-1"));

                using (var attachment = doc.GetAttachment("attach-2"))
                {
                    Assert.NotNull(attachment);
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void Remove()
        {
            using (var test = new TestDatabase())
            {
                var db = test.Database;
                var doc = db.GetEntityDocument<Product>("1");

                doc.SetAttachment("attach", new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, "type-1");
                doc.Save();

                Assert.Single(doc.Attachments);
                Assert.Single(doc.AttachmentNames);

                using (var attachment = doc.GetAttachment("attach"))
                {
                    Assert.NotNull(attachment);
                }

                doc.Revise();
                doc.RemoveAttachment("attach");

                Assert.Empty(doc.Attachments);
                Assert.Empty(doc.AttachmentNames);
                Assert.Null(doc.GetAttachment("attach"));

                doc.Save();

                Assert.Empty(doc.Attachments);
                Assert.Empty(doc.AttachmentNames);
                Assert.Null(doc.GetAttachment("attach"));
            }
        }
    }
}
