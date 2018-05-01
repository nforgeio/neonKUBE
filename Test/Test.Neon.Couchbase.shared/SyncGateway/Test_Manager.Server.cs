//-----------------------------------------------------------------------------
// FILE:	    Test_Manager.Server.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Couchbase;

using Neon.Common;
using Neon.Couchbase.SyncGateway;
using Neon.Xunit;

using Xunit;

namespace TestSyncGateway
{
    /// <summary>
    /// Tests the Sync Gateway manager REST API.
    /// </summary>
    public partial class Test_Manager
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public async Task ServerInformation()
        {
            try
            {
                using (var gateway = TestCluster.CreateGateway())
                {
                    var manager = gateway.CreateManager();
                    var info    = await manager.GetServerInformationAsync();

                    Assert.True(info.IsAdmin);
                    Assert.True(!string.IsNullOrEmpty(info.ProductVersion));
                    Assert.Contains("Couchbase Sync Gateway", info.ProductName);
                    Assert.Contains("Couchbase Sync Gateway", info.Version);
                }
            }
            finally
            {
                await TestCluster.ClearAsync();
            }
        }
    }
}
