//-----------------------------------------------------------------------------
// FILE:	    Test_HiveBus.VHost.cs
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
using Neon.HiveMQ;
using Neon.Xunit;
using Neon.Xunit.RabbitMQ;

using Xunit;

namespace TestCommon
{
    public partial class Test_HiveBus
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHiveMQ)]
        public async Task VHost()
        {
            // Verify that we can perform queuing operations within a non-root
            // virtual host with non-root user.

            using (var manager = fixture.Settings.ConnectManager())
            {
                var vhost = await manager.CreateVirtualHostAsync("vhost");
                var user  = await manager.CreateUserAsync(new UserInfo("user", "password"));

                await manager.CreatePermissionAsync(new PermissionInfo(user, vhost));
            }

            // Verify that basic channels work in the new vhost.

            using (var bus = fixture.Settings.ConnectHiveBus("user", "password", "vhost"))
            {
                var channel  = bus.GetBasicChannel("test");
                var received = (TestMessage1)null;

                channel.Consume<TestMessage1>(message => received = message);
                channel.Open();

                channel.Publish(new TestMessage1() { Text = "Hello World!" });

                NeonHelper.WaitFor(() => received != null && received.Text == "Hello World!", timeout: timeout);
            }

            // Verify that broadcast channels work in the new vhost.

            using (var bus = fixture.Settings.ConnectHiveBus("user", "password", "vhost"))
            {
                var channel1  = bus.GetBroadcastChannel("test");
                var channel2  = bus.GetBroadcastChannel("test");
                var received1 = (TestMessage1)null;
                var received2 = (TestMessage1)null;

                channel1.Consume<TestMessage1>(message => received1 = message);
                channel1.Open();

                channel2.Consume<TestMessage1>(message => received2 = message);
                channel2.Open();

                channel1.Publish(new TestMessage1() { Text = "Hello World!" });

                NeonHelper.WaitFor(() => received1 != null && received2 != null, timeout: timeout);

                Assert.Equal("Hello World!", received1.Text);
                Assert.Equal("Hello World!", received2.Text);
            }
        }
    }
}
