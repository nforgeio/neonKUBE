//-----------------------------------------------------------------------------
// FILE:	    Test_Manager.cs
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
    public class Test_Manager
    {
        public Test_Manager()
        {
            // We need to make sure all generated entity 
            // classes have been registered.

            ModelTypes.Register();
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void EntityDatabase()
        {
            // Verify that manager's entity database extensions work.

            Assert.Null(Manager.SharedInstance.GetExistingEntityDatabase("doesnt-exist"));

            EntityDatabase  dbTest1   = null;
            EntityDatabase  dbTest2   = null;
            Guid            testGuid  = Guid.NewGuid();
            string          test1Name = $"test-1-{testGuid}";
            string          test2Name = $"test-2-{testGuid}";

            try
            {
                dbTest1 = Manager.SharedInstance.GetEntityDatabase(test1Name);

                Assert.NotNull(dbTest1);
                Assert.Same(dbTest1, Manager.SharedInstance.GetEntityDatabase(test1Name));
                Assert.Same(dbTest1, Manager.SharedInstance.GetExistingEntityDatabase(test1Name));

                dbTest2 = Manager.SharedInstance.GetEntityDatabase(test2Name);

                Assert.NotNull(dbTest2);
                Assert.Same(dbTest2, Manager.SharedInstance.GetEntityDatabase(test2Name));
                Assert.Same(dbTest2, Manager.SharedInstance.GetExistingEntityDatabase(test2Name));

                Assert.NotSame(dbTest1, dbTest2);

                var doc1 = dbTest1.GetEntityDocument<TestEntity>("1");
                doc1.Content.String = "FOO";
                doc1.Save();

                var doc2 = dbTest2.GetEntityDocument<TestEntity>("1");
                doc2.Content.String = "BAR";
                doc2.Save();

                dbTest1.Dispose();
                dbTest2.Dispose();

                // We should get new instances after Dispose().

                Assert.NotSame(dbTest1, Manager.SharedInstance.GetEntityDatabase(test1Name));
                Assert.NotSame(dbTest2, Manager.SharedInstance.GetEntityDatabase(test2Name));

                // Make sure we really had two different physical databases.

                dbTest1 = Manager.SharedInstance.GetEntityDatabase(test1Name);
                dbTest2 = Manager.SharedInstance.GetEntityDatabase(test2Name);

                doc1 = dbTest1.GetExistingEntityDocument<TestEntity>("1");
                Assert.Equal("FOO", doc1.Content.String);

                doc2 = dbTest2.GetExistingEntityDocument<TestEntity>("1");
                Assert.Equal("BAR", doc2.Content.String);
            }
            finally
            {
                if (dbTest1 != null)
                {
                    dbTest1.Delete();
                    dbTest1.Dispose();
                }

                if (dbTest2 != null)
                {
                    dbTest2.Delete();
                    dbTest2.Dispose();
                }
            }
        }
    }
}
