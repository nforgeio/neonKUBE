//-----------------------------------------------------------------------------
// FILE:	    Test_RabbitMQFixture_Compiled.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using EasyNetQ.Management.Client;
using EasyNetQ.Management.Client.Model;
using Xunit;

using Neon.Common;
using Neon.Xunit;
using Neon.Xunit.RabbitMQ;

namespace TestCommon
{
    public class Test_RabbitMQFixture_Compiled : IClassFixture<RabbitMQFixture>
    {
        //---------------------------------------------------------------------
        // Local types

        public class HelloMessage
        {
            public string Message { get; set; }
        }

        //---------------------------------------------------------------------
        // Implementation

        private RabbitMQFixture fixture;

        public Test_RabbitMQFixture_Compiled(RabbitMQFixture fixture)
        {
            this.fixture = fixture;

            fixture.Start(precompile: true);    // Enable RabbitMQ precompiling.
            fixture.Clear();
        }

        /// <summary>
        /// Verifies that the target RabbitMQ system is in a pristine state.
        /// </summary>
        /// <param name="manager">The management client.</param>
        private void VerifyPristine(ManagementClient manager)
        {
            Assert.Equal(new string[] { "/" }, manager.GetVHostsAsync().Result.Select(vh => vh.Name));
            Assert.Equal(new string[] { fixture.Settings.Username }, manager.GetUsersAsync().Result.Select(u => u.Name));
            Assert.Empty(manager.GetQueuesAsync().Result);
            Assert.Empty(manager.GetBindingsAsync().Result);
            Assert.Empty(manager.GetPoliciesAsync().Result);

            // Confirm that only the built-in RabbitMQ exchanges exist.  These
            // have either an empty name or a name that starts with "amq.".

            Assert.Empty(manager.GetExchangesAsync().Result.Where(exchange => exchange.Name != string.Empty && !exchange.Name.StartsWith("amq.")));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHiveMQ)]
        public void Clear()
        {
            // Verify that RabbitMQ starts out with a cleared initial state and
            // that <see cref="RabbitMQFixture.Reset()"/> clears the state.
            
            using (var manager = fixture.Settings.ConnectManager())
            {
                //-------------------------------------------------------------
                // Ensure that RabbitMQ starts out pristine.

                VerifyPristine(manager);

                //-------------------------------------------------------------
                // Add some stuff to the root virtual host and then verify
                // the we can clear it.

                // $todo(jeff.lill):
                //
                // Verify that policies are deleted too (I'm not sure what these are yet).

                var rootVHost    = manager.GetVhostAsync("/").Result;
                var rootQueue    = manager.CreateQueueAsync(new QueueInfo("root-queue"), rootVHost).Result;
                var rootExchange = manager.CreateExchangeAsync(new ExchangeInfo("root-exchange", "direct"), rootVHost).Result;

                manager.CreateBinding(rootExchange, rootQueue, new BindingInfo("test")).Wait();

                fixture.Clear();
                VerifyPristine(manager);

                //-------------------------------------------------------------
                // Add another virtual host, user, and queue and verify that they
                // are cleared.

                var testVHost    = manager.CreateVirtualHostAsync("test").Result;
                var testUser     = manager.CreateUserAsync(new UserInfo("test", "password")).Result;
                var testQueue    = manager.CreateQueueAsync(new QueueInfo("test-queue"), testVHost).Result;
                var testExchange = manager.CreateExchangeAsync(new ExchangeInfo("test-exchange", "direct"), testVHost).Result;

                manager.CreateBinding(testExchange, testQueue, new BindingInfo("test")).Wait();

                fixture.Clear();
                VerifyPristine(manager);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHiveMQ)]
        public void EasyNetQ()
        {
            // Verify that we can perform basic queue operations via 
            // the EasyNetQ [IBus] API.

            using (var bus = fixture.Settings.ConnectEasyNetQ())
            {
                Assert.True(bus.IsConnected);

                // Verify that we can send/receive a simple message synchronously.

                var message = (string)null;

                using (bus.Subscribe<HelloMessage>("0", received => message = received.Message))
                {
                    bus.Publish(new HelloMessage() { Message = "Hello World!" });

                    NeonHelper.WaitFor(() => message == "Hello World!", TimeSpan.FromSeconds(10));
                }

                // Verify async send/receive.

                message = null;

                using (bus.SubscribeAsync<HelloMessage>("0",
                    async received =>
                    {
                        message = received.Message;
                        await Task.CompletedTask;
                    }))
                {
                    bus.PublishAsync(new HelloMessage() { Message = "Hello World!" }).Wait();

                    NeonHelper.WaitFor(() => message == "Hello World!", TimeSpan.FromSeconds(10));
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHiveMQ)]
        public void RabbitMQ()
        {
            // Verify that we can establish a low-level RabbitMQ connection.

            using (var connection = fixture.Settings.ConnectRabbitMQ())
            {
                Assert.True(connection.IsOpen);
            }
        }
    }
}
