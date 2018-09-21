//-----------------------------------------------------------------------------
// FILE:	    Test_HiveMQ.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using EasyNetQ.Management.Client.Model;

using Neon.Common;
using Neon.Hive;
using Neon.Xunit;
using Neon.Xunit.Hive;

using Xunit;

namespace TestNeonCluster
{
    public class Test_HiveMQ : IClassFixture<HiveFixture>
    {
        private HiveFixture     hiveFixture;
        private HiveProxy       hive;

        public Test_HiveMQ(HiveFixture fixture)
        {
            if (!fixture.LoginAndInitialize())
            {
                fixture.Reset();
            }

            this.hiveFixture = fixture;
            this.hive        = fixture.Hive;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Reset()
        {
            // Verify that [HiveFixture.Reset()] resets the HiveMQ, including:
            //
            //      * Removing any non-standard users (e.g. not "sysadmin", "neon", or "app")
            //      * Removing any non-standard virtual hosts
            //      * Removing any "app" virtual host queues

            using (var mqManager = hiveFixture.Hive.HiveMQ.ConnectHiveMQManager())
            {
                var testVHost = await mqManager.CreateVirtualHostAsync("test");
                var testUser  = await mqManager.CreateUserAsync(new UserInfo("test", "password"));

                await mqManager.CreatePermissionAsync(new PermissionInfo(testUser, testVHost));
                await mqManager.CreateQueueAsync(new QueueInfo("test"), testVHost);

                var appVHost = await mqManager.GetVhostAsync(hive.Definition.HiveMQ.AppVHost);

                await mqManager.CreatePermissionAsync(new PermissionInfo(testUser, appVHost));
                await mqManager.CreateQueueAsync(new QueueInfo("app-queue"), testVHost);
            }

            hiveFixture.Reset();

            using (var mqManager = hiveFixture.Hive.HiveMQ.ConnectHiveMQManager())
            {
                // Ensure that the "/" and "neon" virtual hosts are still there.

                Assert.Single((await mqManager.GetVHostsAsync()).Where(vh => vh.Name == "/"));
                Assert.Single((await mqManager.GetVHostsAsync()).Where(vh => vh.Name == hive.Definition.HiveMQ.NeonVHost));

                // Ensure that the "sysadmin", "neon", and "app" users are still around and have the
                // correct permissions.

                Assert.Single((await mqManager.GetUsersAsync()).Where(u => u.Name == hive.Definition.HiveMQ.SysadminUser));
                Assert.Single((await mqManager.GetUsersAsync()).Where(u => u.Name == hive.Definition.HiveMQ.NeonUser));
                Assert.Single((await mqManager.GetUsersAsync()).Where(u => u.Name == hive.Definition.HiveMQ.AppUser));

                var permissions = await mqManager.GetPermissionsAsync();
                var allRights   = ".*";

                Assert.Equal(3, permissions.Count());
                Assert.Single(permissions.Where(p => p.User == hive.Definition.HiveMQ.SysadminUser && p.Vhost == "/" && p.Read == allRights && p.Write == allRights && p.Configure == allRights));
                Assert.Single(permissions.Where(p => p.User == hive.Definition.HiveMQ.NeonUser && p.Vhost == hive.Definition.HiveMQ.NeonVHost && p.Read == allRights && p.Write == allRights && p.Configure == allRights));
                Assert.Single(permissions.Where(p => p.User == hive.Definition.HiveMQ.AppUser && p.Vhost == hive.Definition.HiveMQ.AppVHost && p.Read == allRights && p.Write == allRights && p.Configure == allRights));

                // Ensure that the "app" virtual host has no queues.

                Assert.Empty((await mqManager.GetQueuesAsync()).Where(q => q.Vhost == hive.Definition.HiveMQ.AppVHost));
            }
        }
    }
}
