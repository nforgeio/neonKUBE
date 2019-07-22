//-----------------------------------------------------------------------------
// FILE:	    Test_Web.cs
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
using Neon.Web;
using Neon.Xunit;

using Newtonsoft.Json.Linq;
using Test.Neon.Models;

using Xunit;
using Xunit.Abstractions;

namespace Test.Neon.Web
{
    [Route("/Test")]
    public class TestController : NeonControllerBase
    {
        [HttpGet]
        [Route("/LogTest")]
        public void LogTest()
        {
            LogInfo("This is a test.");
        }
    }

    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddMvc()
                .AddNeon();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseRouting(routes =>
            {
                routes.MapControllers();
            });
        }
    }

    public class TestControllerBase : IClassFixture<AspNetFixture>
    {
        private AspNetFixture       fixture;
        private TestOutputWriter    testWriter;
        private HttpClient          httpClient;

        public TestControllerBase(AspNetFixture fixture, ITestOutputHelper outputHelper)
        {
            var testPort = 0;
            var logLevel = global::Neon.Diagnostics.LogLevel.None;

            this.fixture    = fixture;
            this.testWriter = new TestOutputWriter(outputHelper);

            fixture.Start<Startup>(port: testPort, logWriter: testWriter, logLevel: logLevel);

            this.fixture    = fixture;
            this.httpClient = fixture.HttpClient;
            this.testWriter = new TestOutputWriter(outputHelper);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonWeb)]
        public async Task Log()
        {
            var response = await httpClient.GetAsync("/LogTest");
        }
    }
}
