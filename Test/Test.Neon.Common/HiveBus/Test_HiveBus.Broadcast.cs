//-----------------------------------------------------------------------------
// FILE:	    Test_HiveBus.Broadcast.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
        public void BroadcastDuplicateSubscription()
        {
            // Verify that the broadcast channel doesn't allow multiple
            // subscriptions to the same message type.

            using (var bus = fixture.Settings.ConnectHiveBus())
            {
                var channel = bus.GetBroadcastChannel("test");

                channel.Consume<TestMessage1>(message => { });

                Assert.Throws<InvalidOperationException>(() => channel.Consume<TestMessage1>(message => { }));
                Assert.Throws<InvalidOperationException>(() => channel.Consume<TestMessage1>((message, context) => { }));
                Assert.Throws<InvalidOperationException>(() => channel.Consume<TestMessage1>(message => Task.CompletedTask));
                Assert.Throws<InvalidOperationException>(() => channel.Consume<TestMessage1>((message, context) => Task.CompletedTask));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHiveMQ)]
        public void Broadcast()
        {
            // Verify that we can synchronously publish and consume multiple
            // message types via a broadcast channel.

            using (var bus = fixture.Settings.ConnectHiveBus())
            {
                var channel1   = bus.GetBroadcastChannel("test");
                var channel2   = bus.GetBroadcastChannel("test");
                var receivedA1 = (TestMessage1)null;
                var receivedA2 = (TestMessage1)null;
                var receivedB1 = (TestMessage2)null;
                var receivedB2 = (TestMessage2)null;

                channel1.Consume<TestMessage1>(
                    message =>
                    {
                        receivedA1 = message.Body;
                    });

                channel2.Consume<TestMessage1>(
                    message =>
                    {
                        receivedA2 = message.Body;
                    });

                channel1.Consume<TestMessage2>(
                    message =>
                    {
                        receivedB1 = message.Body;
                    });

                channel2.Consume<TestMessage2>(
                    message =>
                    {
                        receivedB2 = message.Body;
                    });

                channel1.Publish(new TestMessage1() { Text = "Hello World!" });
                NeonHelper.WaitFor(() => receivedA1 != null && receivedA2 != null, timeout: timeout);
                Assert.Equal("Hello World!", receivedA1.Text);
                Assert.Equal("Hello World!", receivedA2.Text);

                channel1.Publish(new TestMessage2() { Text = "Hello World!" });
                NeonHelper.WaitFor(() => receivedB1 != null && receivedB2 != null, timeout: timeout);
                Assert.Equal("Hello World!", receivedB1.Text);
                Assert.Equal("Hello World!", receivedB2.Text);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHiveMQ)]
        public void Broadcast_FilterSelf()
        {
            // Verify that we can synchronously publish and consume from
            // a broadcast channel and the we can filter self-originating
            // messages.

            using (var bus = fixture.Settings.ConnectHiveBus())
            {
                var channel1  = bus.GetBroadcastChannel("test");
                var channel2  = bus.GetBroadcastChannel("test");
                var received1 = (TestMessage1)null;
                var received2 = (TestMessage1)null;

                channel1.Consume<TestMessage1>(
                    message =>
                    {
                        received1 = message.Body;
                    },
                    filterSelf: true);

                channel2.Consume<TestMessage1>(
                    message =>
                    {
                        received2 = message.Body;
                    },
                    filterSelf: true);

                channel1.Publish(new TestMessage1() { Text = "Hello World!" });

                // Wait a few seconds and verify that we didn't receive 
                // the message sent by the same channel and the other 
                // channel did receive the message.

                NeonHelper.WaitFor(() => received2 != null, timeout: timeout);
                Thread.Sleep(TimeSpan.FromSeconds(5));
                Assert.Null(received1);
                Assert.NotNull(received2);
                Assert.Equal("Hello World!", received2.Text);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHiveMQ)]
        public void BroadcastContext()
        {
            // Verify that we can synchronously publish and consume from
            // a broadcast channel while receiving additional context info.

            using (var bus = fixture.Settings.ConnectHiveBus())
            {
                var channel1   = bus.GetBroadcastChannel("test");
                var channel2   = bus.GetBroadcastChannel("test");
                var received1  = (TestMessage1)null;
                var received2  = (TestMessage1)null;
                var contextOK1 = false;
                var contextOK2 = false;

                channel1.Consume<TestMessage1>(
                    (message, context) =>
                    {
                        received1  = message.Body;
                        contextOK1 = context.Queue.StartsWith("test-");
                    });

                channel2.Consume<TestMessage1>(
                    (message, context) =>
                    {
                        received2  = message.Body;
                        contextOK2 = context.Queue.StartsWith("test-");
                    });

                channel1.Publish(new TestMessage1() { Text = "Hello World!" });

                NeonHelper.WaitFor(() => received1 != null && received2 != null, timeout: timeout);

                Assert.Equal("Hello World!", received1.Text);
                Assert.True(contextOK1);
                Assert.Equal("Hello World!", received2.Text);
                Assert.True(contextOK2);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHiveMQ)]
        public void BroadcastContext_FilterSelf()
        {
            // Verify that we can synchronously publish and consume from
            // a broadcast channel and the we can filter self-originating
            // messages.

            using (var bus = fixture.Settings.ConnectHiveBus())
            {
                var channel1   = bus.GetBroadcastChannel("test");
                var channel2   = bus.GetBroadcastChannel("test");
                var received1  = (TestMessage1)null;
                var received2  = (TestMessage1)null;
                var contextOK1 = false;
                var contextOK2 = false;

                channel1.Consume<TestMessage1>(
                    (message, context) =>
                    {
                        received1 = message.Body;
                        contextOK1 = context.Queue.StartsWith("test-");
                    },
                    filterSelf: true);

                channel2.Consume<TestMessage1>(
                    (message, context) =>
                    {
                        received2 = message.Body;
                        contextOK2 = context.Queue.StartsWith("test-");
                    },
                    filterSelf: true);

                channel1.Publish(new TestMessage1() { Text = "Hello World!" });

                // Wait a few seconds and verify that we didn't receive 
                // the message sent by the same channel and the other 
                // channel did receive the message.

                NeonHelper.WaitFor(() => received2 != null, timeout: timeout);
                Thread.Sleep(TimeSpan.FromSeconds(5));
                Assert.Null(received1);
                Assert.NotNull(received2);
                Assert.Equal("Hello World!", received2.Text);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHiveMQ)]
        public async Task BroadcastAsync()
        {
            // Verify that we can asynchronously publish and consume from
            // a broadcast channel.

            using (var bus = fixture.Settings.ConnectHiveBus())
            {
                var channel1  = bus.GetBroadcastChannel("test");
                var channel2  = bus.GetBroadcastChannel("test");
                var received1 = (TestMessage1)null;
                var received2 = (TestMessage1)null;

                channel1.Consume<TestMessage1>(
                    async (message) =>
                    {
                        received1 = message.Body;
                        await Task.CompletedTask;
                    });

                channel2.Consume<TestMessage1>(
                    async (message) =>
                    {
                        received2 = message.Body;
                        await Task.CompletedTask;
                    });

                await channel1.PublishAsync(new TestMessage1() { Text = "Hello World!" });

                NeonHelper.WaitFor(() => received1 != null && received2 != null, timeout: timeout);

                Assert.Equal("Hello World!", received1.Text);
                Assert.Equal("Hello World!", received2.Text);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHiveMQ)]
        public async Task BroadcastAsync_FilterSelf()
        {
            // Verify that we can asynchronously publish and consume from
            // a broadcast channel and the we can filter self-originating
            // messages.

            using (var bus = fixture.Settings.ConnectHiveBus())
            {
                var channel1  = bus.GetBroadcastChannel("test");
                var channel2  = bus.GetBroadcastChannel("test");
                var received1 = (TestMessage1)null;
                var received2 = (TestMessage1)null;

                channel1.Consume<TestMessage1>(
                    async message =>
                    {
                        received1 = message.Body;

                        await Task.CompletedTask;
                    },
                    filterSelf: true);

                channel2.Consume<TestMessage1>(
                    async message =>
                    {
                        received2 = message.Body;

                        await Task.CompletedTask;
                    },
                    filterSelf: true);

                await channel1.PublishAsync(new TestMessage1() { Text = "Hello World!" });

                // Wait a few seconds and verify that we didn't receive 
                // the message sent by the same channel and the other 
                // channel did receive the message.

                NeonHelper.WaitFor(() => received2 != null, timeout: timeout);
                Thread.Sleep(TimeSpan.FromSeconds(5));
                Assert.Null(received1);
                Assert.NotNull(received2);
                Assert.Equal("Hello World!", received2.Text);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHiveMQ)]
        public async Task BroadcastContextAsync()
        {
            // Verify that we can asynchronously publish and consume from
            // a broadcast channel.

            using (var bus = fixture.Settings.ConnectHiveBus())
            {
                var channel1   = bus.GetBroadcastChannel("test");
                var channel2   = bus.GetBroadcastChannel("test");
                var received1  = (TestMessage1)null;
                var received2  = (TestMessage1)null;
                var contextOK1 = false;
                var contextOK2 = false;

                channel1.Consume<TestMessage1>(
                    async (message, context) =>
                    {
                        received1  = message.Body;
                        contextOK1 = context.Queue.StartsWith("test-");
                        await Task.CompletedTask;
                    });

                channel2.Consume<TestMessage1>(
                    async (message, context) =>
                    {
                        received2  = message.Body;
                        contextOK2 = context.Queue.StartsWith("test-");
                        await Task.CompletedTask;
                    });

                await channel1.PublishAsync(new TestMessage1() { Text = "Hello World!" });

                NeonHelper.WaitFor(() => received1 != null && received2 != null, timeout: timeout);

                Assert.Equal("Hello World!", received1.Text);
                Assert.True(contextOK1);
                Assert.Equal("Hello World!", received2.Text);
                Assert.True(contextOK2);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHiveMQ)]
        public async Task BroadcastContextAsync_FilterSelf()
        {
            // Verify that we can asynchronously publish and consume from
            // a broadcast channel while receiving additional context info.

            using (var bus = fixture.Settings.ConnectHiveBus())
            {
                var channel = bus.GetBroadcastChannel("test");
                var received = (TestMessage1)null;
                var contextOK = false;

                channel.Consume<TestMessage1>(
                    async (message, context) =>
                    {
                        received = message.Body;
                        contextOK = context.Queue.StartsWith("test-");

                        await Task.CompletedTask;
                    },
                    filterSelf: true);


                await channel.PublishAsync(new TestMessage1() { Text = "Hello World!" });

                // Wait a few seconds and verify that we didn't receive 
                // the message sent by the same channel.

                Thread.Sleep(TimeSpan.FromSeconds(5));
                Assert.Null(received);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHiveMQ)]
        public async Task BroadcastBlast()
        {
            // Verify that we can broadcast a bunch of messages 
            // to several consumers.

            const int receiverCount = 10;
            const int messageCount  = 1000;

            using (var bus = fixture.Settings.ConnectHiveBus())
            {
                var receiveChannels  = new List<BroadcastChannel>();
                var recieverMessages = new List<List<TestMessage1>>();
                var contextOK        = true;

                for (int i = 0; i < receiverCount; i++)
                {
                    var channelIndex = i;

                    receiveChannels.Add(bus.GetBroadcastChannel("test"));
                    recieverMessages.Add(new List<TestMessage1>());

                    receiveChannels[i].Consume<TestMessage1>(
                        async (message, context) =>
                        {
                            recieverMessages[channelIndex].Add(message.Body);

                            if (!context.Queue.StartsWith("test-"))
                            {
                                contextOK = false;
                            }

                            await Task.CompletedTask;
                        });
                }

                var publishChannel = bus.GetBroadcastChannel("test");

                for (int i = 0; i < messageCount; i++)
                {
                    await publishChannel.PublishAsync(new TestMessage1() { Text = "{i}" });
                }

                NeonHelper.WaitFor(
                    () =>
                    {
                        foreach (var messageList in recieverMessages)
                        {
                            if (messageList.Count < messageCount)
                            {
                                return false;
                            }
                        }

                        return true;
                    },
                    timeout: timeout);

                Assert.True(contextOK);
            }
        }
    }
}
