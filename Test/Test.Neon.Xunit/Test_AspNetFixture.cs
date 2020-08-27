//-----------------------------------------------------------------------------
// FILE:	    Test_AspNetFixture.cs
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

using Xunit;

namespace TestXunit
{
    public class Test_AspNetFixture : IClassFixture<AspNetFixture>
    {
        //---------------------------------------------------------------------
        // Private types

        public class Startup
        {
            public static string Answer { get; set; }

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
                        await context.Response.WriteAsync(Answer);
                    });
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private AspNetFixture   fixture;
        private HttpClient      client;

        public Test_AspNetFixture(AspNetFixture fixture)
        {
            this.fixture   = fixture;
            Startup.Answer = "World!";

            fixture.Start<Startup>();

            client = new HttpClient()
            {
                BaseAddress = fixture.BaseAddress
            };
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonXunit)]
        public async Task Test()
        {
            // Verify that we can communicate with the service.

            Assert.Equal("World!", await client.GetStringAsync("Hello"));

            // Restart the service and verify that we actually see
            // a new service instance by changing the answer.

            Startup.Answer = "FooBar!";
            fixture.Restart<Startup>();

            Assert.Equal("FooBar!", await client.GetStringAsync("Hello"));
        }
    }
}
