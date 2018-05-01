//-----------------------------------------------------------------------------
// FILE:	    Test_Manager.Role.cs
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
        public async Task RoleCreateRemoveList()
        {
            await TestCluster.ClearAsync();

            try
            {
                using (var gateway = TestCluster.CreateGateway())
                {
                    var manager = gateway.CreateManager();

                    await TestCluster.CreateDatabaseAsync("db");

                    // Database should start out with no roles.

                    Assert.Empty(await manager.RoleListAsync("db"));

                    // Create a couple roles and verify.

                    for (int i=0;i<2;i++)
                    {
                        await manager.RoleCreateAsync("db", 
                            new RoleProperties()
                            {
                                Name     = $"role{i}",
                                AdminChannels = new List<string>() { $"channel-{i}" }
                            });
                    }

                    var roles = await manager.RoleListAsync("db");

                    Assert.Equal(2, roles.Count);
                    Assert.NotNull(roles.SingleOrDefault(u => u == "role0"));
                    Assert.NotNull(roles.SingleOrDefault(u => u == "role1"));

                    // Delete roles and verify.

                    // $todo(jeff.lill): 
                    //
                    // Looks like role delete isn't working on the Windows test
                    // Sync -Gateway?  I can submit the requests in code as well
                    // as manually, then return OK, but the role is still there.       
#if TODO
                    await manager.RoleRemoveAsync("db", "role0");
                    roles = await manager.RoleListAsync("db");
                    Assert.Equal(1, roles.Count);
                    Assert.Null(roles.SingleOrDefault(u => u == "role0"));
                    Assert.NotNull(roles.SingleOrDefault(u => u == "role1"));

                    await manager.RoleRemoveAsync("db", "role1");
                    roles = await manager.RoleListAsync("db");
                    Assert.Equal(0, roles.Count);
#endif
                }
            }
            finally
            {
                await TestCluster.ClearAsync();
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public async Task RoleGetUpdate()
        {
            await TestCluster.ClearAsync();

            try
            {
                using (var gateway = TestCluster.CreateGateway())
                {
                    var manager = gateway.CreateManager();

                    await TestCluster.CreateDatabaseAsync("db");

                    // Database should start out with no roles.

                    Assert.Empty(await manager.RoleListAsync("db"));

                    // Create a role and verify.

                    await manager.RoleCreateAsync("db", 
                        new RoleProperties()
                        {
                            Name     = "role0",
                            AdminChannels = new List<string>() { "channel-0" }
                        });

                    var properties = await manager.RoleGetAsync("db", "role0");

                    Assert.Equal("role0", properties.Name);
                    Assert.Equal(new string[] { "channel-0" }, properties.AdminChannels);

                    // Update the role properties and verify.

                    await manager.RoleUpdateAsync("db", "role0",
                        new RoleProperties()
                        {
                            AdminChannels = new List<string>() { "channel-1" },
                        });

                    properties = await manager.RoleGetAsync("db", "role0");

                    Assert.Equal("role0", properties.Name);
                    Assert.Equal(new string[] { "channel-1" }, properties.AdminChannels);
                }
            }
            finally
            {
                await TestCluster.ClearAsync();
            }
        }
    }
}
