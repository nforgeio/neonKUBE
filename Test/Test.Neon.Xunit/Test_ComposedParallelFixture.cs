//-----------------------------------------------------------------------------
// FILE:	    Test_ComposedParallelFixtures.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

using Neon.Common;
using Neon.Kube;
using Neon.Service;
using Neon.Xunit;
using Neon.Xunit.Couchbase;
using Neon.Xunit.Cadence;

using Xunit;

using Couchbase;
using NATS.Client;

namespace TestXunit
{
    /// <summary>
    /// Verify that we can start fixtures in parallel by group.
    /// </summary>
    public class Test_ComposedParallelFixtures : IClassFixture<ComposedFixture>
    {
        //---------------------------------------------------------------------
        // Test services.

        public class MyService1 : NeonService
        {
            public MyService1() : base("my-service-1")
            {
            }

            protected async override Task<int> OnRunAsync()
            {
                await SetRunningAsync();

                while (!Terminator.CancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }

                return 0;
            }
        }

        public class MyService2 : NeonService
        {
            public MyService2() : base("my-service-2")
            {
            }

            protected async override Task<int> OnRunAsync()
            {
                await SetRunningAsync();

                while (!Terminator.CancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }

                return 0;
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private ComposedFixture fixture;

        public Test_ComposedParallelFixtures(ComposedFixture composedFixture)
        {
            composedFixture.Start(
                () =>
                {
                    // Start Couchbase and Cadence together as [group 0].

                    composedFixture.AddFixture("couchbase", new CouchbaseFixture(),
                        couchbaseFixture =>
                        {
                            couchbaseFixture.StartAsComposed();
                        },
                        group: 0);

                    composedFixture.AddFixture("cadence", new CadenceFixture(),
                        cadenceFixture =>
                        {
                            cadenceFixture.StartAsComposed();
                        },
                        group: 0);

                    // Start NATS and a container as [group 1].

                    composedFixture.AddFixture("nats", new NatsFixture(),
                        natsFixture =>
                        {
                            natsFixture.StartAsComposed();
                        },
                        group: 1);

                    composedFixture.AddFixture("container", new ContainerFixture(),
                        containerFixture =>
                        {
                            containerFixture.StartAsComposed("my-container", $"{NeonHelper.NeonBranchRegistry}/test:latest");
                        },
                        group: 1);

                    // Start two Neon services as [group 2].

                    composedFixture.AddServiceFixture("service1", new NeonServiceFixture<MyService1>(), () => new MyService1(), group: 2);
                    composedFixture.AddServiceFixture("service2", new NeonServiceFixture<MyService2>(), () => new MyService2(), group: 2);
                });

            this.fixture = composedFixture;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task Verify()
        {
            var couchbaseFixture = (CouchbaseFixture)fixture["couchbase"];
            var cadenceFixture   = (CadenceFixture)fixture["cadence"];
            var natsFixture      = (NatsFixture)fixture["nats"];
            var containerFixture = (ContainerFixture)fixture["container"];
            var service1Fixture  = (NeonServiceFixture<MyService1>)fixture["service1"];
            var service2Fixture  = (NeonServiceFixture<MyService2>)fixture["service2"];

            // Verify that Couchbase and Cadence from [group 0] are running.

            couchbaseFixture.Bucket.Insert("my-key", "my-value");
            await cadenceFixture.Client.DescribeDomainAsync(cadenceFixture.Client.Settings.DefaultDomain);

            // Verify that NATS and the container from [group 1] are running.

            natsFixture.Connection.Publish("foo", new byte[] { 0, 1, 2, 3, 4, });
            Assert.True(containerFixture.IsRunning);

            // Verify that the Neon services from [group 2] are running.

            NeonHelper.WaitFor(() => service1Fixture.Service.Status == NeonServiceStatus.Running, timeout: TimeSpan.FromSeconds(10));
            NeonHelper.WaitFor(() => service2Fixture.Service.Status == NeonServiceStatus.Running, timeout: TimeSpan.FromSeconds(10));
        }
    }
}
