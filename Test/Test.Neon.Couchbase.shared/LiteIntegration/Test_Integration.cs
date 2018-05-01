//-----------------------------------------------------------------------------
// FILE:	    Test_Manager.Database.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Couchbase;
using Couchbase.Configuration.Client;
using Couchbase.Lite;
using Couchbase.Lite.Auth;

using Neon.Common;
using Neon.Couchbase.SyncGateway;
using Neon.Xunit;

using Xunit;

using Test.Neon.Models;

#if !MANUAL

namespace TestLiteIntegration
{
    public class Test_Integration
    {
        public Test_Integration()
        {
            // We need to make sure all generated entity 
            // classes have been registered.

            ModelTypes.Register();
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public async Task Basic()
        {
            // Verify that [DatabaseManager] works.

            using (var manager = await DatabaseManager.InitializeAsync())
            {
                var channels = new string[] { "test" };
                var local    = await manager.CreateLocalDatabaseAsync("user0", channels);
                var db       = local.Database;
                var doc      = db.GetEntityDocument<TestEntity>("1");

                doc.SetAttachment("attach", new byte[] { 0, 1, 2, 3, 4 }, "image/jpeg");
                doc.Content.String = "Hello World!";
                doc.Channels = channels;
                doc.Save();

                Assert.Equal(1, local.Replicate());

                doc.Delete();

                Assert.Equal(1, local.Replicate());
            }
        }
    }
}

#endif
