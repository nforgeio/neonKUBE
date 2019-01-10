//-----------------------------------------------------------------------------
// FILE:	    Test_Manager.User.cs
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
    /// Tests the Sync Gateway manager REST API.
    /// </summary>
    public partial class Test_Manager
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public async Task UserCreateRemoveList()
        {
            await TestCluster.ClearAsync();

            try
            {
                using (var gateway = TestCluster.CreateGateway())
                {
                    var manager = gateway.CreateManager();

                    await TestCluster.CreateDatabaseAsync("db");

                    // Database should start out with no users.

                    Assert.Empty(await manager.UserListAsync("db"));

                    // Create a couple users and verify.

                    for (int i=0;i<2;i++)
                    {
                        await manager.UserCreateAsync("db", 
                            new UserProperties()
                            {
                                Name       = $"user{i}",
                                Password   = $"password{i}",
                                Email      = $"user{i}@gmail.com",
                                IsDisabled = false,
                                AdminChannels   = new List<string>() { $"channel-{i}" },
                                Roles      = new List<string>() { $"role-{i}" }
                            });
                    }

                    var users = await manager.UserListAsync("db");

                    Assert.Equal(2, users.Count);
                    Assert.NotNull(users.SingleOrDefault(u => u == "user0"));
                    Assert.NotNull(users.SingleOrDefault(u => u == "user1"));

                    // Delete users and verify.

                    // $todo(jeff.lill): 
                    //
                    // Looks like user delete isn't working on the Windows test
                    // Sync -Gateway?  I can submit the requests in code as well
                    // as manually, then return OK, but the user is still there.       
#if TODO
                    await manager.UserRemoveAsync("db", "user0");
                    users = await manager.UserListAsync("db");
                    Assert.Equal(1, users.Count);
                    Assert.Null(users.SingleOrDefault(u => u == "user0"));
                    Assert.NotNull(users.SingleOrDefault(u => u == "user1"));

                    await manager.UserRemoveAsync("db", "user1");
                    users = await manager.UserListAsync("db");
                    Assert.Equal(0, users.Count);
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
        public async Task UserGetUpdate()
        {
            await TestCluster.ClearAsync();

            try
            {
                using (var gateway = TestCluster.CreateGateway())
                {
                    var manager = gateway.CreateManager();

                    await TestCluster.CreateDatabaseAsync("db");

                    // Database should start out with no users.

                    Assert.Empty(await manager.UserListAsync("db"));

                    // Create a user and verify.

                    await manager.UserCreateAsync("db", 
                        new UserProperties()
                        {
                            Name       = "user0",
                            Password   = "password0",
                            Email      = "user0@gmail.com",
                            IsDisabled = false,
                            AdminChannels   = new List<string>() { "channel-0" },
                            Roles      = new List<string>() { "role-0" }
                        });

                    var properties = await manager.UserGetAsync("db", "user0");

                    Assert.Equal("user0", properties.Name);
                    Assert.Null(properties.Password);       // Sync Gateway doesn't return the password (must be hashed or otherwise encrypted)
                    Assert.Equal("user0@gmail.com", properties.Email);
                    Assert.False(properties.IsDisabled);
                    Assert.Equal(new string[] { "channel-0" }, properties.AdminChannels);
                    Assert.Equal(new string[] { "role-0" }, properties.Roles);

                    // Update the user properties and verify.

                    await manager.UserUpdateAsync("db", "user0",
                        new UserProperties()
                        {
                            Password   = "password1",
                            Email      = "user1@gmail.com",
                            IsDisabled = true,
                            AdminChannels   = new List<string>() { "channel-1" },
                            Roles      = new List<string>() { "role-1" }
                        });

                    properties = await manager.UserGetAsync("db", "user0");

                    Assert.Equal("user0", properties.Name);
                    Assert.Null(properties.Password);       // Sync Gateway doesn't return the password (must be hashed or otherwise encrypted)
                    Assert.Equal("user1@gmail.com", properties.Email);
                    Assert.True(properties.IsDisabled);
                    Assert.Equal(new string[] { "channel-1" }, properties.AdminChannels);
                    Assert.Equal(new string[] { "role-1" }, properties.Roles);
                }
            }
            finally
            {
                await TestCluster.ClearAsync();
            }
        }
    }
}
