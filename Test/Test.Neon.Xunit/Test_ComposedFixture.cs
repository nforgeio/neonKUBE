//-----------------------------------------------------------------------------
// FILE:	    Test_ComposedFixture.cs
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
using Neon.Xunit;

using Xunit;

using Couchbase;
using NATS.Client;

// NOTE: We're not testing [NatsStreamingFixture] here because we can't run
//       it at the same time as the [NatsFixture] (by default) due to port 
//       conflicts.  We'll test [NatsStreamingFixture] by composing it with
//       the CouchbaseFixture in the Couchbase unit tests.

namespace TestXunit
{
    /// <summary>
    /// Verify that a test fixture composed of the base fixtures works.
    /// </summary>
    [Trait(TestTrait.Category, TestArea.NeonXunit)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_ComposedFixture : IClassFixture<ComposedFixture>
    {
        //---------------------------------------------------------------------
        // Private types

        public class Startup
        {
            public Startup(IConfiguration configuration)
            {
                Configuration = configuration;
            }

            public IConfiguration Configuration { get; }

            public void ConfigureServices(IServiceCollection services)
            {
            }

            public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
            {
                // This is a simple test that replies to all requests with: [Answer].

                app.Run(
                    async context =>
                    {
                        await context.Response.WriteAsync("World!");
                    });
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private ComposedFixture     composedFixture;
        private AspNetFixture       aspNetFixture;
        private ContainerFixture    containerFixture;
        private DockerFixture       dockerFixture;
        private HostsFixture        hostsFixture;
        private NatsFixture         natsFixture;

        public Test_ComposedFixture(ComposedFixture composedFixture)
        {
            this.composedFixture = composedFixture;

            var fixtureStatus = composedFixture.Start(
                () =>
                {
                    // NOTE: Adding this one first because it clears the local Docker
                    //       state when it starts and we want the containers started
                    //       by the other fixtures to be unmolested.

                    composedFixture.AddFixture("docker", new DockerFixture());

                    composedFixture.AddFixture("aspNet", new AspNetFixture(),
                        aspNetFixture =>
                        {
                            aspNetFixture.StartAsComposed<Startup>();
                        });

                    composedFixture.AddFixture("container", new ContainerFixture(),
                        containerFixture =>
                        {
                            containerFixture.StartAsComposed("my-container", $"{NeonHelper.NeonLibraryBranchRegistry}/test:latest");
                        });

                    composedFixture.AddFixture("hosts", new HostsFixture());

                    composedFixture.AddFixture("nats", new NatsFixture(),
                        natsFixture =>
                        {
                            natsFixture.StartAsComposed();
                        });
                });

            this.aspNetFixture    = (AspNetFixture)composedFixture["aspNet"];
            this.dockerFixture    = (DockerFixture)composedFixture["docker"];
            this.containerFixture = (ContainerFixture)composedFixture["container"];
            this.hostsFixture     = (HostsFixture)composedFixture["hosts"];
            this.natsFixture      = (NatsFixture)composedFixture["nats"];

            if (fixtureStatus == TestFixtureStatus.Started)
            {
                hostsFixture.AddHostAddress("foo.bar", "127.1.2.3");
            }
        }

        /// <summary>
        /// Verify that the fixtures look OK.
        /// </summary>
        [Fact]
        public async Task Verify()
        {
            // Verify AspNetFixture.

            using (var client = new HttpClient() { BaseAddress = aspNetFixture.BaseAddress })
            {
                Assert.Equal("World!", await client.GetStringAsync("Hello"));
            }

            // Verify the ContainerFixture.

            var result = NeonHelper.ExecuteCapture(NeonHelper.DockerCli, "ps");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("my-container", result.AllText);

            // Verify HostsFixture.

            var hostEntry = Dns.GetHostEntry("foo.bar");

            Assert.Equal("127.1.2.3", hostEntry.AddressList.Single().ToString());

            // Verify NatsFixture.

            Assert.Equal(ConnState.CONNECTED, natsFixture.Connection.State);

            // Verify DockerFixture.

            Assert.NotEmpty(dockerFixture.ListContainers().Where(container => container.Name == "my-container"));

            var composeYaml =
$@"version: '3'
services:
  my-service:
    image: {NeonHelper.NeonLibraryBranchRegistry}/test:latest
    deploy:
      replicas: 2
";
            dockerFixture.DeployStack("my-stack", composeYaml);

            Assert.NotEmpty(dockerFixture.ListStacks().Where(stack => stack.Name == "my-stack"));
        }
    }
}
