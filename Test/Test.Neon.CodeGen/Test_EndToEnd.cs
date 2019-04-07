//-----------------------------------------------------------------------------
// FILE:	    Test_EndToEnd.cs
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

using Test.Neon.Models;

using Xunit;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace TestCodeGen.AspNet
{
    [Route("/TestAspNetFixture")]
    public class TestAspNetFixtureController : NeonController
    {
        [HttpGet]
        [Route("Hello")]
        public string Hello()
        {
            return "World!";
        }

        [HttpGet]
        [Route("person/{id}/{name}/{age}")]
        public Person CreatePerson(int id, string name, int age)
        {
            return new Person()
            {
                Id = id,
                Name = name,
                Age = age
            };
        }

        [HttpPut]
        public Person IncrementAge([FromBody] Person person)
        {
            if (person == null)
            {
                return null;
            }

            person.Age++;

            return person;
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
            services.AddMvc(
                options =>
                {
                    options.EnableEndpointRouting = true;
                    options.AddNeonRoundTripJsonFormatters();
                });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseMvc();
        }
    }

    /// <summary>
    /// This tests end-to-end integration of generated data models and service clients as well as
    /// their integration with the an MVC based backend service controller.
    /// </summary>
    public class Test_EndToEnd : IClassFixture<AspNetFixture>
    {
        private AspNetFixture           fixture;
        private TestAspNetFixtureClient client;

        public Test_EndToEnd(AspNetFixture fixture)
        {
            this.fixture = fixture;

            fixture.Start<Startup>();

            client = new TestAspNetFixtureClient()
            {
                BaseAddress = fixture.BaseAddress
            };
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonXunit)]
        public async Task Hello()
        {
            // Verify that we can communicate with the service by calling 
            // a very simply API.

            Assert.Equal("World!", await client.HelloAsync());
        }
    }
}
