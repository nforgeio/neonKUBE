//-----------------------------------------------------------------------------
// FILE:	    Test_Binder.cs
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
    public class Test_Binder
    {
        public Test_Binder()
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
                var doc = db.GetBinderDocument<TestBinder>("1");

                doc.Content.String = "FOO";
                doc.Save();

                doc = db.GetBinderDocument<TestBinder>("1");

                var changed = false;
                var prop = string.Empty;
                var propList = new List<string>();

                doc.Changed +=
                    (s, a) =>
                    {
                        changed = true;
                    };

                doc.PropertyChanged +=
                    (s, a) =>
                    {
                        prop = a.PropertyName;
                        propList.Add(a.PropertyName);
                    };

                // Verify that we start out with no attachments.

                Assert.Equal("FOO", doc.Content.String);
                Assert.Null(doc.TestImage1);
                Assert.Null(doc.GetAttachment("TestImage1"));
                Assert.Null(doc.GetTestImage1());
                Assert.Null(doc.TestImage2);
                Assert.Null(doc.GetAttachment("test_image2"));
                Assert.Null(doc.GetTestImage2());

                doc.Revise();

                Assert.Equal("FOO", doc.Content.String);
                Assert.Null(doc.TestImage1);
                Assert.Null(doc.GetAttachment("TestImage1"));
                Assert.Null(doc.GetTestImage1());
                Assert.Null(doc.TestImage2);
                Assert.Null(doc.GetAttachment("test_image2"));
                Assert.Null(doc.GetTestImage2());

                // Verify that we see property change notifications when we set
                // attachments via byte arrays and streams and also that the
                // attachment properties return a path to the attachment file.

                prop = null;
                changed = false;
                doc.SetTestImage1(new byte[] { 0, 1, 2, 3, 4 }, "test-content");
                Assert.Equal("TestImage1", prop);
                Assert.True(changed);
                Assert.True(File.Exists(doc.TestImage1));
                Assert.NotNull(doc.GetAttachment("TestImage1"));
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, File.ReadAllBytes(doc.TestImage1));

                prop = null;
                changed = false;
                doc.SetTestImage2(new MemoryStream(new byte[] { 5, 6, 7, 8, 9 }), "test-content");
                Assert.Equal("TestImage2", prop);
                Assert.True(changed);
                Assert.True(File.Exists(doc.TestImage2));
                Assert.NotNull(doc.GetAttachment("test_image2"));
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, File.ReadAllBytes(doc.TestImage2));

                // Verify that we see attachment property change notifications when we
                // persist the document, confirm that the attachment file paths changed
                // and the temporary files no longer exist.

                var tempImage1 = doc.TestImage1;
                var tempImage2 = doc.TestImage2;

                propList.Clear();
                doc.Save();

                Assert.True(propList.Count(p => p == "TestImage1") > 0);
                Assert.True(propList.Count(p => p == "TestImage2") > 0);
                Assert.NotEqual(tempImage1, doc.TestImage1);
                Assert.NotEqual(tempImage2, doc.TestImage2);
                Assert.False(File.Exists(tempImage1));
                Assert.False(File.Exists(tempImage2));
                Assert.True(File.Exists(doc.TestImage1));
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, File.ReadAllBytes(doc.TestImage1));
                Assert.True(File.Exists(doc.TestImage2));
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, File.ReadAllBytes(doc.TestImage2));

                // Reload the document and verify.

                doc = db.GetBinderDocument<TestBinder>("1");

                doc.Changed +=
                    (s, a) =>
                    {
                        changed = true;
                    };

                doc.PropertyChanged +=
                    (s, a) =>
                    {
                        prop = a.PropertyName;
                        propList.Add(a.PropertyName);
                    };

                Assert.NotNull(doc.TestImage1);
                Assert.True(File.Exists(doc.TestImage1));
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, File.ReadAllBytes(doc.TestImage1));

                Assert.NotNull(doc.TestImage2);
                Assert.True(File.Exists(doc.TestImage2));
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, File.ReadAllBytes(doc.TestImage2));

                // Verify that attachment removal works as expected.

                doc.Revise();

                prop = null;
                propList.Clear();
                changed = false;

                Assert.True(File.Exists(doc.TestImage1));
                Assert.True(File.Exists(doc.TestImage2));

                doc.RemoveTestImage1();

                Assert.True(changed);
                Assert.Equal("TestImage1", prop);

                doc.Save();
                Assert.Null(doc.TestImage1);
                Assert.Null(doc.GetTestImage1());
                Assert.True(File.Exists(doc.TestImage2));
                Assert.NotNull(doc.GetTestImage2());

                // Now remove the second attachment that has a custom name
                // and verify.

                doc.Revise();

                prop = null;
                propList.Clear();
                changed = false;

                Assert.True(File.Exists(doc.TestImage2));

                doc.RemoveTestImage2();

                Assert.True(changed);
                Assert.Equal("TestImage2", prop);

                doc.Save();
                Assert.Null(doc.TestImage1);
                Assert.Null(doc.GetTestImage1());
                Assert.Null(doc.TestImage2);
                Assert.Null(doc.GetTestImage2());
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void DocumentChange()
        {
            // Verify that we see the attachment property change notifications
            // when we see document change notifications.

            using (var test = new TestDatabase())
            {
                var db = test.Database;
                var doc = db.GetBinderDocument<TestBinder>("1");

                var propList = new List<string>();

                doc.PropertyChanged +=
                    (s, a) =>
                    {
                        propList.Add(a.PropertyName);
                    };

                doc.Save();

                var docCopy = db.GetBinderDocument<TestBinder>("1");

                Assert.Empty(propList);

                // Add a new attachment.

                docCopy.Revise();
                docCopy.SetTestImage1(new byte[] { 0, 1, 2, 3, 4 });

                Assert.Empty(propList);    // Haven't saved anything yet
                docCopy.Save();

                Assert.Single(propList);
                Assert.Equal("TestImage1", propList[0]);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, File.ReadAllBytes(doc.TestImage1));

                // Update an existing attachment

                propList.Clear();
                docCopy.Revise();
                docCopy.SetTestImage1(new byte[] { 5, 6, 7, 8, 9 });

                Assert.Empty(propList);    // Haven't saved anything yet
                docCopy.Save();

                Assert.Single(propList);
                Assert.Equal("TestImage1", propList[0]);
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, File.ReadAllBytes(doc.TestImage1));

                // Add a second attachment

                propList.Clear();
                docCopy.Revise();
                docCopy.SetTestImage2(new byte[] { 9, 8, 7, 6, 5 });

                Assert.Empty(propList);    // Haven't saved anything yet
                docCopy.Save();

                Assert.Single(propList);
                Assert.Equal("TestImage2", propList[0]);
                Assert.Equal(new byte[] { 9, 8, 7, 6, 5 }, File.ReadAllBytes(doc.TestImage2));

                // Delete both attachments

                propList.Clear();
                docCopy.Revise();
                docCopy.RemoveTestImage1();
                docCopy.RemoveTestImage2();

                Assert.Empty(propList);    // Haven't saved anything yet
                docCopy.Save();

                Assert.Equal(2, propList.Count);
                Assert.Contains("TestImage1", propList);
                Assert.Contains("TestImage2", propList);
                Assert.Null(doc.TestImage1);
                Assert.Null(doc.TestImage2);
            }
        }
    }
}
