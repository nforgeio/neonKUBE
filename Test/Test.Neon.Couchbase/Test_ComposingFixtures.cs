//-----------------------------------------------------------------------------
// FILE:	    Test_ComposingFixtures.cs
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
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

using Neon.Common;
using Neon.Xunit;
using Neon.Xunit.Couchbase;

using Xunit;

using Couchbase;
using NATS.Client;

namespace TestCouchbase
{
    /// <summary>
    /// Verify that a test fixture composed of Couchbase and other fixtures works.
    /// </summary>
    [Trait(TestTrait.Category, TestArea.NeonCommon)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_ComposingFixtures : IClassFixture<ComposedFixture>
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

        private ComposedFixture         composedFixture;
        private AspNetFixture           aspNetFixture;
        private CouchbaseFixture        couchbaseFixture;
        private NatsStreamingFixture    natsStreamingFixture;

        public Test_ComposingFixtures(ComposedFixture composedFixture)
        {
            this.composedFixture = composedFixture;

            composedFixture.Start(
                () =>
                {
                    composedFixture.AddFixture("aspNet", new AspNetFixture(),
                        aspNetFixture =>
                        {
                            aspNetFixture.StartAsComposed<Startup>();
                        });

                    composedFixture.AddFixture("natsStreaming", new NatsStreamingFixture(),
                        natsStreamingFixture =>
                        {
                            natsStreamingFixture.StartAsComposed();
                        });

                    composedFixture.AddFixture("couchbase", new CouchbaseFixture(),
                        couchbaseFixture =>
                        {
                            couchbaseFixture.StartAsComposed();
                        });
                });

            this.aspNetFixture        = (AspNetFixture)composedFixture["aspNet"];
            this.natsStreamingFixture = (NatsStreamingFixture)composedFixture["natsStreaming"];
            this.couchbaseFixture     = (CouchbaseFixture)composedFixture["couchbase"];
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

            // Verify NatsStreamingFixture

            Assert.Equal(ConnState.CONNECTED, natsStreamingFixture.Connection.NATSConnection.State);

            // Verify CouchbaseFixture

            var bucket = couchbaseFixture.Bucket;

            await bucket.UpsertSafeAsync("hello", "world!");
            Assert.Equal("world!", await bucket.GetSafeAsync<string>("hello"));
        }
    }
}
