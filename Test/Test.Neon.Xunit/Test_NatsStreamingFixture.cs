//-----------------------------------------------------------------------------
// FILE:	    Test_NatsStreamingFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using Neon.Xunit.Kube;

using NATS.Client;

using Xunit;

using Test.Neon.Models;

namespace TestXunit
{
    /// <summary>
    /// This class tests both the <see cref="NatsStreamingFixture"/> as well as the Neon
    /// STAN extensions.
    public class Test_NatsStreamingFixture : IClassFixture<NatsFixture>
    {
        private NatsFixture fixture;
        private IConnection connection;

        public Test_NatsStreamingFixture(NatsFixture fixture)
        {
            if (fixture.Start(image: "nkubedev/nats-streaming:latest") == TestFixtureStatus.AlreadyRunning)
            {
                fixture.Restart();
            }

            this.fixture = fixture;
            this.connection = fixture.Connection;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonXunit)]
        public void Connect()
        {
            Assert.Equal(ConnState.CONNECTED, connection.State);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonXunit)]
        public void StanExtensions_Subscribe()
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
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonXunit)]
        public void StanExtensions_SubscribeQueue()
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
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonXunit)]
        public void StanExtensions_SubscribeAsync()
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
    }
}
