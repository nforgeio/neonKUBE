//-----------------------------------------------------------------------------
// FILE:	    Test_NatsFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;

using Neon.Common;
using Neon.Cryptography;
using Neon.Docker;
using Neon.Kube;
using Neon.IO;
using Neon.Web;
using Neon.Xunit;

using NATS.Client;
using STAN.Client;

using Xunit;

using Test.Neon.Models;

namespace TestXunit
{
    /// <summary>
    /// This class tests both the <see cref="NatsFixture"/> as well as the Neon
    /// NATS extensions.
    /// </summary>
    [Trait(TestTrait.Category, TestArea.NeonXunit)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_NatsFixture : IClassFixture<NatsFixture>
    {
        private NatsFixture     fixture;
        private IConnection     connection;

        public Test_NatsFixture(NatsFixture fixture)
        {
            if (fixture.Start() == TestFixtureStatus.AlreadyRunning)
            {
                fixture.Restart();
            }

            this.fixture = fixture;
            this.connection = fixture.Connection;
        }

        [Fact]
        public void Connect()
        {
            Assert.Equal(ConnState.CONNECTED, connection.State);
        }

        [Fact]
        public void Subscribe()
        {
            Assert.Equal(ConnState.CONNECTED, connection.State);

            using (var subscription = connection.SubscribeSync<Person>("subject"))
            {
                Assert.Equal(0, subscription.PendingMessages);

                var jack = new Person()
                {
                    Id = 1,
                    Name = "Jack",
                    Age = 10,
                    Data = new byte[] { 0, 1, 2, 3, 4 }
                };

                connection.Publish("subject", jack);

                var received = subscription.NextMessage(1000);

                Assert.True(received.Data == jack);
            }
        }

        [Fact]
        public void SubscribeQueue()
        {
            Assert.Equal(ConnState.CONNECTED, connection.State);

            using (var subscription = connection.SubscribeSync<Person>("subject", "queue"))
            {
                Assert.Equal(0, subscription.PendingMessages);

                var jack = new Person()
                {
                    Id = 1,
                    Name = "Jack",
                    Age = 10,
                    Data = new byte[] { 0, 1, 2, 3, 4 }
                };

                connection.Publish("subject", jack);

                var received = subscription.NextMessage(1000);

                Assert.True(received.Data == jack);
            }
        }

        [Fact]
        public void SubscribeAsync()
        {
            Assert.Equal(ConnState.CONNECTED, connection.State);

            using (var subscription = connection.SubscribeAsync<Person>("subject"))
            {
                Assert.Equal(0, subscription.PendingMessages);

                var jack = new Person()
                {
                    Id = 1,
                    Name = "Jack",
                    Age = 10,
                    Data = new byte[] { 0, 1, 2, 3, 4 }
                };

                Msg receivedLowLevel = null;
                Msg<Person> received = null;

                subscription.MessageHandler +=
                    (sender, args) =>
                    {
                        receivedLowLevel = args.Message;
                    };

                subscription.RoundtripMessageHandler +=
                    (sender, args) =>
                    {
                        received = args.Message;
                    };

                subscription.Start();
                connection.Publish("subject", jack);
                NeonHelper.WaitFor(() => received != null && receivedLowLevel != null, TimeSpan.FromSeconds(5));

                Assert.True(received.Data == jack);
            }
        }

        [Fact]
        public void SubscribeAsyncHandler()
        {
            Assert.Equal(ConnState.CONNECTED, connection.State);

            Msg<Person> receivedFromHandler = null;

            using (var subscription = connection.SubscribeAsync<Person>("subject", (s, a) => { receivedFromHandler = a.Message; }))
            {
                Assert.Equal(0, subscription.PendingMessages);

                var jack = new Person()
                {
                    Id = 1,
                    Name = "Jack",
                    Age = 10,
                    Data = new byte[] { 0, 1, 2, 3, 4 }
                };

                Msg receivedLowLevel = null;
                Msg<Person> received = null;

                subscription.MessageHandler +=
                    (sender, args) =>
                    {
                        receivedLowLevel = args.Message;
                    };

                subscription.RoundtripMessageHandler +=
                    (sender, args) =>
                    {
                        received = args.Message;
                    };

                subscription.Start();
                connection.Publish("subject", jack);
                NeonHelper.WaitFor(() => received != null && receivedLowLevel != null && receivedFromHandler != null, TimeSpan.FromSeconds(5));

                Assert.True(received.Data == jack);
            }
        }

        [Fact]
        public void SubscribeAsyncQueue()
        {
            Assert.Equal(ConnState.CONNECTED, connection.State);

            using (var subscription = connection.SubscribeAsync<Person>("subject", "queue"))
            {
                Assert.Equal(0, subscription.PendingMessages);

                var jack = new Person()
                {
                    Id = 1,
                    Name = "Jack",
                    Age = 10,
                    Data = new byte[] { 0, 1, 2, 3, 4 }
                };

                Msg receivedLowLevel = null;
                Msg<Person> received = null;

                subscription.MessageHandler +=
                    (sender, args) =>
                    {
                        receivedLowLevel = args.Message;
                    };

                subscription.RoundtripMessageHandler +=
                    (sender, args) =>
                    {
                        received = args.Message;
                    };

                subscription.Start();
                connection.Publish("subject", jack);
                NeonHelper.WaitFor(() => received != null && receivedLowLevel != null, TimeSpan.FromSeconds(5));

                Assert.True(received.Data == jack);
            }
        }

        [Fact]
        public void SubscribeAsyncQueueHandler()
        {
            Assert.Equal(ConnState.CONNECTED, connection.State);

            Msg<Person> receivedFromHandler = null;

            using (var subscription = connection.SubscribeAsync<Person>("subject", "queue", (s, a) => { receivedFromHandler = a.Message; }))
            {
                Assert.Equal(0, subscription.PendingMessages);

                var jack = new Person()
                {
                    Id = 1,
                    Name = "Jack",
                    Age = 10,
                    Data = new byte[] { 0, 1, 2, 3, 4 }
                };

                Msg receivedLowLevel = null;
                Msg<Person> received = null;

                subscription.MessageHandler +=
                    (sender, args) =>
                    {
                        receivedLowLevel = args.Message;
                    };

                subscription.RoundtripMessageHandler +=
                    (sender, args) =>
                    {
                        received = args.Message;
                    };

                subscription.Start();
                connection.Publish("subject", jack);
                NeonHelper.WaitFor(() => received != null && receivedLowLevel != null && receivedFromHandler != null, TimeSpan.FromSeconds(5));

                Assert.True(received.Data == jack);
            }
        }

        [Fact]
        public void Publish()
        {
            Assert.Equal(ConnState.CONNECTED, connection.State);

            using (var subscription = connection.SubscribeSync<Person>("subject"))
            {
                Assert.Equal(0, subscription.PendingMessages);

                var jack = new Person()
                {
                    Id = 1,
                    Name = "Jack",
                    Age = 10,
                    Data = new byte[] { 0, 1, 2, 3, 4 }
                };

                connection.Publish("subject", jack);

                var received = subscription.NextMessage(1000);

                Assert.True(received.Data == jack);
            }
        }

        [Fact]
        public void PublishReply()
        {
            Assert.Equal(ConnState.CONNECTED, connection.State);

            using (var subscription = connection.SubscribeSync<Person>("subject"))
            {
                Assert.Equal(0, subscription.PendingMessages);

                var jack = new Person()
                {
                    Id = 1,
                    Name = "Jack",
                    Age = 10,
                    Data = new byte[] { 0, 1, 2, 3, 4 }
                };

                connection.Publish("subject", "reply", jack);

                var received = subscription.NextMessage(1000);

                Assert.Equal("reply", received.Reply);
                Assert.True(received.Data == jack);
            }
        }

        [Fact]
        public void Request()
        {
            Assert.Equal(ConnState.CONNECTED, connection.State);

            using (var subscription = connection.SubscribeSync<Person>("subject"))
            {
                Assert.Equal(0, subscription.PendingMessages);

                var jack = new Person()
                {
                    Id = 1,
                    Name = "Jack",
                    Age = 10,
                    Data = new byte[] { 0, 1, 2, 3, 4 }
                };

                var jill = new Person()
                {
                    Id = 2,
                    Name = "Jill",
                    Age = 11,
                    Data = new byte[] { 5, 6, 7, 8, 9 }
                };

                Msg<Person> request = null;
                Msg<Person> reply = null;

                var thread = new Thread(
                    new ThreadStart(
                        () =>
                        {
                            request = subscription.NextMessage();

                            connection.Publish(request.Reply, jill);
                        }));

                thread.Start();

                reply = connection.Request<Person, Person>("subject", jack);

                Assert.True(request.Data == jack);
                Assert.True(reply.Data == jill);
            }
        }

        [Fact]
        public void RequestTimeout()
        {
            Assert.Equal(ConnState.CONNECTED, connection.State);

            using (var subscription = connection.SubscribeSync<Person>("subject"))
            {
                Assert.Equal(0, subscription.PendingMessages);

                var jack = new Person()
                {
                    Id = 1,
                    Name = "Jack",
                    Age = 10,
                    Data = new byte[] { 0, 1, 2, 3, 4 }
                };

                var jill = new Person()
                {
                    Id = 2,
                    Name = "Jill",
                    Age = 11,
                    Data = new byte[] { 5, 6, 7, 8, 9 }
                };

                Msg<Person> request = null;
                Msg<Person> reply = null;

                var thread = new Thread(
                    new ThreadStart(
                        () =>
                        {
                            request = subscription.NextMessage();

                            connection.Publish(request.Reply, jill);
                        }));

                thread.Start();

                reply = connection.Request<Person, Person>("subject", jack, 1000);

                Assert.True(request.Data == jack);
                Assert.True(reply.Data == jill);
            }
        }

        [Fact]
        public async Task RequestAsync()
        {
            Assert.Equal(ConnState.CONNECTED, connection.State);

            using (var subscription = connection.SubscribeSync<Person>("subject"))
            {
                Assert.Equal(0, subscription.PendingMessages);

                var jack = new Person()
                {
                    Id = 1,
                    Name = "Jack",
                    Age = 10,
                    Data = new byte[] { 0, 1, 2, 3, 4 }
                };

                var jill = new Person()
                {
                    Id = 2,
                    Name = "Jill",
                    Age = 11,
                    Data = new byte[] { 5, 6, 7, 8, 9 }
                };

                Msg<Person> request = null;
                Msg<Person> reply = null;

                var thread = new Thread(
                    new ThreadStart(
                        () =>
                        {
                            request = subscription.NextMessage();

                            connection.Publish(request.Reply, jill);
                        }));

                thread.Start();

                reply = await connection.RequestAsync<Person, Person>("subject", jack);

                Assert.True(request.Data == jack);
                Assert.True(reply.Data == jill);
            }
        }
    }
}
