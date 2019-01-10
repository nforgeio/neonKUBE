//-----------------------------------------------------------------------------
// FILE:	    Test_Public.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

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
    /// Tests the Sync Gateway public REST API.
    /// </summary>
    public sealed class Test_Public
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public async Task ServerInformation()
        {
            await TestCluster.ClearAsync();

            try
            {
                using (var gateway = TestCluster.CreateGateway())
                {
                    var info = await gateway.GetServerInformationAsync();

                    Assert.False(info.IsAdmin);
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
