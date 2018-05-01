//-----------------------------------------------------------------------------
// FILE:	    Test_Manager.Session.cs
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
using Neon.Net;
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
        public async Task SessionCreateDetails()
        {
            await TestCluster.ClearAsync();

            try
            {
                using (var gateway = TestCluster.CreateGateway())
                {
                    var manager = gateway.CreateManager();

                    await TestCluster.CreateDatabaseAsync("db");

                    // Create the test user.

                    await manager.UserCreateAsync("db",
                        new UserProperties()
                        {
                            Name       = "user0",
                            Password   = "password0}",
                            Email      = "user0@gmail.com",
                            IsDisabled = false,
                            AdminChannels   = new List<string>() { "channel-0" },
                            Roles      = new List<string>() { "role-0" }
                        });

                    // Create a session and verify.

                    var session = await manager.SessionCreateAsync("db", "user0", TimeSpan.FromHours(1));

                    Assert.True(!string.IsNullOrWhiteSpace(session.Cookie));
                    Assert.True(!string.IsNullOrWhiteSpace(session.Id));
                    Assert.True(DateTime.Now + TimeSpan.FromHours(0.90) < session.Expires);
                    Assert.True(session.Expires < DateTime.Now + TimeSpan.FromHours(1.10));

                    // Verify the session details.

                    var details = await manager.SessionGetAsync("db", session.Id);

                    Assert.True(details.IsSuccess);
                    Assert.Equal("user0", details.User);
                    Assert.Equal(new string[] { "!", "channel-0" }, details.Channels);
                    Assert.Equal(new string[] { "default", "cookie" }, details.Authenticators);
                }
            }
            finally
            {
                await TestCluster.ClearAsync();
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public async Task SessionRemoveByID()
        {
            await TestCluster.ClearAsync();

            try
            {
                using (var gateway = TestCluster.CreateGateway())
                {
                    var manager = gateway.CreateManager();

                    await TestCluster.CreateDatabaseAsync("db");

                    // Create the test user.

                    await manager.UserCreateAsync("db",
                        new UserProperties()
                        {
                            Name       = "user0",
                            Password   = "password0}",
                            Email      = "user0@gmail.com",
                            IsDisabled = false,
                            AdminChannels   = new List<string>() { "channel-0" },
                            Roles      = new List<string>() { "role-0" }
                        });

                    // Create a session and verify we can delete it by ID. 

                    var session = await manager.SessionCreateAsync("db", "user0", TimeSpan.FromHours(1));
                    var details = await manager.SessionGetAsync("db", session.Id);

                    Assert.True(details.IsSuccess);

                    await manager.SessionRemoveAsync("db", session.Id);
                    await Assert.ThrowsAsync<HttpException>(async () => await manager.SessionGetAsync("db", session.Id));
                }
            }
            finally
            {
                await TestCluster.ClearAsync();
            }
        }

#if TODO
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public async Task SessionRemoveByUserID()
        {
            // $todo(jeff.lill):
            //
            // This test isn't super reliable.  It runs when I run it by
            // itself but not when run along side a larger batch of tests.
            // This means that by REST API wrapper is working and the fault
            // is in the Sync Gateway behavior.

            await TestCluster.ClearAsync();

            try
            {
                using (var gateway = TestCluster.CreateGateway())
                {
                    var manager = gateway.CreateManager();

                    await TestCluster.CreateDatabaseAsync("db1");

                    // Create the test user.

                    await manager.UserCreateAsync("db",
                        new UserProperties()
                        {
                            Name       = "user0",
                            Password   = "password0}",
                            Email      = "user0@gmail.com",
                            IsDisabled = false,
                            Channels   = new List<string>() { "channel-0" },
                            Roles      = new List<string>() { "role-0" }
                        });

                    // Create a session and verify we can delete it by user and ID. 

                    var session = await manager.SessionCreateAsync("db", "user0", TimeSpan.FromHours(1));
                    var details = await manager.SessionGetAsync("db", session.Id);

                    Assert.True(details.IsSuccess);

                    await manager.SessionUserRemoveAsync("db", "user0", session.Id);
                    await Assert.ThrowsAsync<HttpException>(async () => await manager.SessionGetAsync("db", session.Id));
                }
            }
            finally
            {
                await TestCluster.ClearAsync();
            }
        }
#endif

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public async Task SessionRemoveUserSessions()
        {
            await TestCluster.ClearAsync();

            try
            {
                using (var gateway = TestCluster.CreateGateway())
                {
                    var manager = gateway.CreateManager();

                    await TestCluster.CreateDatabaseAsync("db");

                    await manager.UserCreateAsync("db",
                        new UserProperties()
                        {
                            Name       = "user0",
                            Password   = "password0}",
                            Email      = "user0@gmail.com",
                            IsDisabled = false,
                            AdminChannels   = new List<string>() { "channel-0" },
                            Roles      = new List<string>() { "role-0" }
                        });

                    // Create two session and verify we can delete them both by user.

                    var session1 = await manager.SessionCreateAsync("db", "user0", TimeSpan.FromHours(1));
                    var session2 = await manager.SessionCreateAsync("db", "user0", TimeSpan.FromHours(1));

                    var details1 = await manager.SessionGetAsync("db", session1.Id);
                    var details2 = await manager.SessionGetAsync("db", session2.Id);

                    Assert.True(details1.IsSuccess);
                    Assert.True(details2.IsSuccess);
                    await manager.SessionUserRemoveAsync("db", "user0");
                    await Assert.ThrowsAsync<HttpException>(async () => await manager.SessionGetAsync("db", session1.Id));
                    await Assert.ThrowsAsync<HttpException>(async () => await manager.SessionGetAsync("db", session2.Id));
                }
            }
            finally
            {
                await TestCluster.ClearAsync();
            }
        }
    }
}
