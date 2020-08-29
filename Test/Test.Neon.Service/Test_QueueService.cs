//-----------------------------------------------------------------------------
// FILE:	    Test_QueueService.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Service;
using Neon.Xunit;

using Xunit;

namespace TestNeonService
{
    /// <summary>
    /// Demonstrates how to test the <see cref="QueueService"/> that has a single
    /// HTTP endpoint and that also exercises environment variable and file based 
    /// configuration.
    /// </summary>
    public class Test_QueueService : IClassFixture<ComposedFixture>
    {
        private ComposedFixture                     composedFixture;
        private NatsFixture                         natsFixture;
        private NeonServiceFixture<QueueService>    queueServiceFixture;

        public Test_QueueService(ComposedFixture fixture)
        {
            this.composedFixture = fixture;

            composedFixture.Start(
                () =>
                {
                    composedFixture.AddFixture("nats", new NatsFixture(),
                        natsFixture =>
                        {
                            natsFixture.StartAsComposed();
                        });

                    composedFixture.AddServiceFixture("queue-service", new NeonServiceFixture<QueueService>(), () => CreateQueueService());
                });

            this.natsFixture         = (NatsFixture)composedFixture["nats"];
            this.queueServiceFixture = (NeonServiceFixture<QueueService>)composedFixture["queue-service"];
        }

        /// <summary>
        /// Returns the service map.
        /// </summary>
        private ServiceMap CreateServiceMap()
        {
            var description = new ServiceDescription()
            {
                Name = "queue-service",
            };

            var serviceMap = new ServiceMap();

            serviceMap.Add(description);

            return serviceMap;
        }

        /// <summary>
        /// Creates a <see cref="QueueService"/> instance.
        /// </summary>
        /// <returns>The service instance.</returns>
        private QueueService CreateQueueService()
        {
            var service = new QueueService(CreateServiceMap(), "queue-service");

            service.SetEnvironmentVariable("NATS_URI", NatsFixture.ConnectionUri);
            service.SetEnvironmentVariable("NATS_QUEUE", "test");

            return service;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonKube)]
        public async Task Success()
        {
            // Restart the service with with valid environment variables,
            // let it run for a vew seconds and verify that it actually
            // sent and received some queue messages.

            var service = CreateQueueService();

            queueServiceFixture.Restart(() => service);
            Assert.True(queueServiceFixture.IsRunning);

            // Give the service some time to process some messages.

            await Task.Delay(TimeSpan.FromSeconds(5));

            Assert.True(service.SentCount > 0);
            Assert.True(service.ReceiveCount > 0);

            // Signal the service to stop and verify that it returned [exitcode=0].

            service.Stop();
            Assert.Equal(0, service.ExitCode);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonKube)]
        public void BadConfig()
        {
            // Restart the service with with a missing configuration
            // environment variable and verify that the service failed
            // immediately by ensuring that no messages were sent or
            // received and also that it returned a non-zero
            // exit code.

            var service = CreateQueueService();

            service.SetEnvironmentVariable("NATS_QUEUE", null);     // Delete this variable

            queueServiceFixture.Restart(() => service);
            Assert.False(queueServiceFixture.IsRunning);

            // Signal the service to stop and verify that it returned a
            // non-zero exit code indicating an error.

            service.Stop();
            Assert.NotEqual(0, service.ExitCode);
        }
    }
}
