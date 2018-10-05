//-----------------------------------------------------------------------------
// FILE:	    Test_MessageBus.Basic.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.HiveMQ;
using Neon.Xunit;
using Neon.Xunit.RabbitMQ;

using Xunit;

namespace TestCommon
{
    public partial class Test_MessageBus : IClassFixture<RabbitMQFixture>
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHiveMQ)]
        public void BasicDuplicateSubscription()
        {
            // Verify that the basic channel doesn't allow multiple
            // subscriptions to the same message type.

            using (var bus = fixture.Settings.ConnectHiveBus())
            {
                var channel  = bus.CreateBasicChannel("test");

                channel.Consume<TestMessage>(message => { });

                Assert.Throws<InvalidOperationException>(() => channel.Consume<TestMessage>(message => { }));
                Assert.Throws<InvalidOperationException>(() => channel.Consume<TestMessage>((message, context) => { }));
                Assert.Throws<InvalidOperationException>(() => channel.Consume<TestMessage>(message => Task.CompletedTask));
                Assert.Throws<InvalidOperationException>(() => channel.Consume<TestMessage>((message, context) => Task.CompletedTask));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHiveMQ)]
        public void Basic()
        {
            // Verify that we can synchronously publish and consume from
            // a basic channel.

            using (var bus = fixture.Settings.ConnectHiveBus())
            {
                var channel  = bus.CreateBasicChannel("test");
                var received = (TestMessage)null;

                channel.Consume<TestMessage>(message => received = message.Body);
                channel.Publish(new TestMessage() { Text = "Hello World!" });

                NeonHelper.WaitFor(() => received != null && received.Text == "Hello World!", timeout: timeout);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHiveMQ)]
        public void BasicContext()
        {
            // Verify that we can synchronously publish and consume from
            // a basic channel while receiving additional context info.

            using (var bus = fixture.Settings.ConnectHiveBus())
            {
                var channel   = bus.CreateBasicChannel("test");
                var received  = (TestMessage)null;
                var contextOK = false;

                channel.Consume<TestMessage>(
                    (message, context) =>
                    {
                        received  = message.Body;
                        contextOK = context.Queue == channel.Name;
                    });


                channel.Publish(new TestMessage() { Text = "Hello World!" });

                NeonHelper.WaitFor(() => received != null && received.Text == "Hello World!" && contextOK, timeout: timeout);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHiveMQ)]
        public async Task BasicAsync()
        {
            // Verify that we can asynchronously publish and consume from
            // a basic channel.

            using (var bus = fixture.Settings.ConnectHiveBus())
            {
                var channel  = bus.CreateBasicChannel("test");
                var received = (TestMessage)null;

                channel.Consume<TestMessage>(
                    async message =>
                    {
                        received = message.Body;

                        await Task.CompletedTask;
                    });


                await channel.PublishAsync(new TestMessage() { Text = "Hello World!" });

                NeonHelper.WaitFor(() => received != null && received.Text == "Hello World!", timeout: timeout);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHiveMQ)]
        public async Task BasicContextAsync()
        {
            // Verify that we can asynchronously publish and consume from
            // a basic channel while receiving additional context info.

            using (var bus = fixture.Settings.ConnectHiveBus())
            {
                var channel   = bus.CreateBasicChannel("test");
                var received  = (TestMessage)null;
                var contextOK = false;

                channel.Consume<TestMessage>(
                    async (message, context) =>
                    {
                        received  = message.Body;
                        contextOK = context.Queue == channel.Name;

                        await Task.CompletedTask;
                    });


                await channel.PublishAsync(new TestMessage() { Text = "Hello World!" });

                NeonHelper.WaitFor(() => received != null && received.Text == "Hello World!" && contextOK, timeout: timeout);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHiveMQ)]
        public async Task BasicChannels ()
        {
            // Verify that messages published from one channel can be
            // received on another.

            using (var bus = fixture.Settings.ConnectHiveBus())
            {
                var receiveChannel = bus.CreateBasicChannel("test");
                var received       = (TestMessage)null;
                var contextOK      = false;

                receiveChannel.Consume<TestMessage>(
                    async (message, context) =>
                    {
                        received = message.Body;
                        contextOK = context.Queue == receiveChannel.Name;

                        await Task.CompletedTask;
                    });

                var publishChannel = bus.CreateBasicChannel("test");

                await publishChannel.PublishAsync(new TestMessage() { Text = "Hello World!" });

                NeonHelper.WaitFor(() => received != null && received.Text == "Hello World!" && contextOK, timeout: timeout);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHiveMQ)]
        public async Task BasicLoadbalance()
        {
            // Verify that messages are load balanced across multiple consumer
            // channels by sending a bunch of messages and ensuring that all of 
            // the consumers saw at least one message.

            const int channelCount = 10;
            const int messageCount = channelCount * 100;

            var consumerMessages = new List<TestMessage>[channelCount];

            for (int i = 0; i < consumerMessages.Length; i++)
            {
                consumerMessages[i] = new List<TestMessage>();
            }

            using (var bus = fixture.Settings.ConnectHiveBus())
            {
                for (int channelID = 0; channelID < channelCount; channelID++)
                {
                    var consumeChannel = bus.CreateBasicChannel("test");
                    var id             = channelID;

                    consumeChannel.Consume<TestMessage>(
                        async message =>
                        {
                            lock (consumerMessages)
                            {
                                consumerMessages[id].Add(message.Body);
                            }

                            await Task.CompletedTask;
                        });
                }

                var publishChannel = bus.CreateBasicChannel("test");

                for (int i = 0; i < messageCount; i++)
                {
                    await publishChannel.PublishAsync(new TestMessage() { Text = "{i}" });
                }

                NeonHelper.WaitFor(() => consumerMessages.Where(cm => cm.Count == 0).IsEmpty(), timeout: timeout);
            }
        }
    }
}
