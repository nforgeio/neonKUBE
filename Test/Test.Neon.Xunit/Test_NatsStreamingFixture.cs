//-----------------------------------------------------------------------------
// FILE:	    Test_NatsStreamingFixture.cs
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
    /// This class tests both the <see cref="NatsStreamingFixture"/> as well as the Neon
    /// STAN extensions.
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_NatsStreamingFixture : IClassFixture<NatsStreamingFixture>
    {
        private NatsStreamingFixture    fixture;
        private IStanConnection         connection;

        public Test_NatsStreamingFixture(NatsStreamingFixture fixture)
        {
            if (fixture.Start() == TestFixtureStatus.AlreadyRunning)
            {
                fixture.Restart();
            }

            this.fixture = fixture;
            this.connection = fixture.Connection;
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonXunit)]
        public void Connect()
        {
            Assert.Equal(ConnState.CONNECTED, connection.NATSConnection.State);
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonXunit)]
        public void Subscribe()
        {
            Assert.Equal(ConnState.CONNECTED, connection.NATSConnection.State);

            StanMsg<Person> received = null;

            using (var subscription = connection.Subscribe<Person>("subject",
                (sender, args) =>
                {
                    received = args.Msg;
                    received.Ack();
                }))
            {
                var jack = new Person()
                {
                    Id = 1,
                    Name = "Jack",
                    Age = 10,
                    Data = new byte[] { 0, 1, 2, 3, 4 }
                };

                connection.Publish("subject", jack);

                NeonHelper.WaitFor(() => received != null, TimeSpan.FromSeconds(5));
                Assert.True(received.Data == jack);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonXunit)]
        public void SubscribeOptions()
        {
            Assert.Equal(ConnState.CONNECTED, connection.NATSConnection.State);

            StanMsg<Person> received = null;

            var options = StanSubscriptionOptions.GetDefaultOptions();

            using (var subscription = connection.Subscribe<Person>("subject", options,
                (sender, args) =>
                {
                    received = args.Msg;
                    received.Ack();
                }))
            {
                var jack = new Person()
                {
                    Id = 1,
                    Name = "Jack",
                    Age = 10,
                    Data = new byte[] { 0, 1, 2, 3, 4 }
                };

                connection.Publish("subject", jack);

                NeonHelper.WaitFor(() => received != null, TimeSpan.FromSeconds(5));
                Assert.True(received.Data == jack);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonXunit)]
        public void SubscribeQGroup()
        {
            Assert.Equal(ConnState.CONNECTED, connection.NATSConnection.State);

            StanMsg<Person> received = null;

            using (var subscription = connection.Subscribe<Person>("subject", "qgroup",
                (sender, args) =>
                {
                    received = args.Msg;
                    received.Ack();
                }))
            {
                var jack = new Person()
                {
                    Id = 1,
                    Name = "Jack",
                    Age = 10,
                    Data = new byte[] { 0, 1, 2, 3, 4 }
                };

                connection.Publish("subject", jack);

                NeonHelper.WaitFor(() => received != null, TimeSpan.FromSeconds(5));
                Assert.True(received.Data == jack);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonXunit)]
        public void SubscribeQGroupOptions()
        {
            Assert.Equal(ConnState.CONNECTED, connection.NATSConnection.State);

            StanMsg<Person> received = null;

            var options = StanSubscriptionOptions.GetDefaultOptions();

            using (var subscription = connection.Subscribe<Person>("subject", "qgroup", options,
                (sender, args) =>
                {
                    received = args.Msg;
                    received.Ack();
                }))
            {
                var jack = new Person()
                {
                    Id = 1,
                    Name = "Jack",
                    Age = 10,
                    Data = new byte[] { 0, 1, 2, 3, 4 }
                };

                connection.Publish("subject", jack);

                NeonHelper.WaitFor(() => received != null, TimeSpan.FromSeconds(5));
                Assert.True(received.Data == jack);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonXunit)]
        public void Publish()
        {
            Assert.Equal(ConnState.CONNECTED, connection.NATSConnection.State);

            StanMsg<Person> received = null;

            using (var subscription = connection.Subscribe<Person>("subject",
                (sender, args) =>
                {
                    received = args.Msg;
                    received.Ack();
                }))
            {
                var jack = new Person()
                {
                    Id = 1,
                    Name = "Jack",
                    Age = 10,
                    Data = new byte[] { 0, 1, 2, 3, 4 }
                };

                connection.Publish("subject", jack);

                NeonHelper.WaitFor(() => received != null, TimeSpan.FromSeconds(5));
                Assert.True(received.Data == jack);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonXunit)]
        public void PublishHandler()
        {
            Assert.Equal(ConnState.CONNECTED, connection.NATSConnection.State);

            StanMsg<Person> received = null;
            bool            ackReceived = false;

            using (var subscription = connection.Subscribe<Person>("subject",
                (sender, args) =>
                {
                    received = args.Msg;
                    received.Ack();
                }))
            {
                var jack = new Person()
                {
                    Id = 1,
                    Name = "Jack",
                    Age = 10,
                    Data = new byte[] { 0, 1, 2, 3, 4 }
                };

                connection.Publish("subject", jack,
                    (sender, args) =>
                    {
                        ackReceived = true;
                    });

                NeonHelper.WaitFor(() => received != null && ackReceived, TimeSpan.FromSeconds(5));
                Assert.True(received.Data == jack);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonXunit)]
        public async Task PublishAsync()
        {
            Assert.Equal(ConnState.CONNECTED, connection.NATSConnection.State);

            StanMsg<Person> received = null;

            using (var subscription = connection.Subscribe<Person>("subject",
                (sender, args) =>
                {
                    received = args.Msg;
                    received.Ack();
                }))
            {
                var jack = new Person()
                {
                    Id = 1,
                    Name = "Jack",
                    Age = 10,
                    Data = new byte[] { 0, 1, 2, 3, 4 }
                };

                await connection.PublishAsync("subject", jack);

                NeonHelper.WaitFor(() => received != null, TimeSpan.FromSeconds(5));
                Assert.True(received.Data == jack);
            }
        }
    }
}
