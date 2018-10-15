//-----------------------------------------------------------------------------
// FILE:	    Test_HiveBus.Basic.cs
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
    public partial class Test_HiveBus : IClassFixture<RabbitMQFixture>
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHiveMQ)]
        public void BasicDuplicateSubscription()
        {
            // Verify that the basic channel doesn't allow multiple
            // subscriptions to the same message type.

            using (var bus = fixture.Settings.ConnectHiveBus())
            {
                var channel  = bus.GetBasicChannel("test");

                channel.Consume<TestMessage1>(message => { });

                Assert.Throws<InvalidOperationException>(() => channel.Consume<TestMessage1>(message => { }));
                Assert.Throws<InvalidOperationException>(() => channel.Consume<TestMessage1>((message, envelope, context) => { }));
                Assert.Throws<InvalidOperationException>(() => channel.ConsumeAsync<TestMessage1>(message => Task.CompletedTask));
                Assert.Throws<InvalidOperationException>(() => channel.ConsumeAsync<TestMessage1>((message, envelope, context) => Task.CompletedTask));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHiveMQ)]
        public void Basic()
        {
            // Verify that we can synchronously publish and consume two 
            // different message types via a basic channel.

            using (var bus = fixture.Settings.ConnectHiveBus())
            {
                var channel   = bus.GetBasicChannel("test");
                var received1 = (TestMessage1)null;
                var received2 = (TestMessage2)null;

                channel.Consume<TestMessage1>(message => received1 = message);
                channel.Consume<TestMessage2>(message => received2 = message);
                channel.Open();

                channel.Publish(new TestMessage1() { Text = "Hello World!" });
                NeonHelper.WaitFor(() => received1 != null && received1.Text == "Hello World!", timeout: timeout);

                channel.Publish(new TestMessage2() { Text = "Hello World!" });
                NeonHelper.WaitFor(() => received2 != null && received2.Text == "Hello World!", timeout: timeout);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHiveMQ)]
        public void BasicSubscribeAction()
        {
            // Verify that we can register consumers within a subscribe action.

            using (var bus = fixture.Settings.ConnectHiveBus())
            {
                var received1 = (TestMessage1)null;
                var received2 = (TestMessage2)null;

                var channel = bus.GetBasicChannel("test");

                channel.Consume<TestMessage1>(message => received1 = message);
                channel.Consume<TestMessage2>(message => received2 = message);
                channel.Open();

                channel.Publish(new TestMessage1() { Text = "Hello World!" });
                NeonHelper.WaitFor(() => received1 != null && received1.Text == "Hello World!", timeout: timeout);

                channel.Publish(new TestMessage2() { Text = "Hello World!" });
                NeonHelper.WaitFor(() => received2 != null && received2.Text == "Hello World!", timeout: timeout);
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
                var channel   = bus.GetBasicChannel("test");
                var received  = (TestMessage1)null;
                var contextOK = false;

                channel.Consume<TestMessage1>(
                    (message, envelope, context) =>
                    {
                        received  = message;
                        contextOK = context.Queue == channel.Name;
                    });

                channel.Open();

                channel.Publish(new TestMessage1() { Text = "Hello World!" });

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
                var channel  = bus.GetBasicChannel("test");
                var received = (TestMessage1)null;

                channel.ConsumeAsync<TestMessage1>(
                    async message =>
                    {
                        received = message;

                        await Task.CompletedTask;
                    });

                channel.Open();

                await channel.PublishAsync(new TestMessage1() { Text = "Hello World!" });

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
                var channel   = bus.GetBasicChannel("test");
                var received  = (TestMessage1)null;
                var contextOK = false;

                channel.ConsumeAsync<TestMessage1>(
                    async (message, envelope, context) =>
                    {
                        received  = message;
                        contextOK = context.Queue == channel.Name;

                        await Task.CompletedTask;
                    });

                channel.Open();

                await channel.PublishAsync(new TestMessage1() { Text = "Hello World!" });

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
                var receiveChannel = bus.GetBasicChannel("test");
                var received       = (TestMessage1)null;
                var contextOK      = false;

                receiveChannel.ConsumeAsync<TestMessage1>(
                    async (message, envelope, context) =>
                    {
                        received = message;
                        contextOK = context.Queue == receiveChannel.Name;

                        await Task.CompletedTask;
                    });

                receiveChannel.Open();

                var publishChannel = bus.GetBasicChannel("test").Open();

                await publishChannel.PublishAsync(new TestMessage1() { Text = "Hello World!" });

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

            var consumerMessages = new List<TestMessage1>[channelCount];

            for (int i = 0; i < consumerMessages.Length; i++)
            {
                consumerMessages[i] = new List<TestMessage1>();
            }

            using (var bus = fixture.Settings.ConnectHiveBus())
            {
                for (int channelID = 0; channelID < channelCount; channelID++)
                {
                    var consumeChannel = bus.GetBasicChannel("test");
                    var id             = channelID;

                    consumeChannel.ConsumeAsync<TestMessage1>(
                        async message =>
                        {
                            lock (consumerMessages)
                            {
                                consumerMessages[id].Add(message);
                            }

                            await Task.CompletedTask;
                        });

                    consumeChannel.Open();
                }

                var publishChannel = bus.GetBasicChannel("test").Open();

                for (int i = 0; i < messageCount; i++)
                {
                    await publishChannel.PublishAsync(new TestMessage1() { Text = "{i}" });
                }

                NeonHelper.WaitFor(() => consumerMessages.Where(cm => cm.Count == 0).IsEmpty(), timeout: timeout);
            }
        }
    }
}
