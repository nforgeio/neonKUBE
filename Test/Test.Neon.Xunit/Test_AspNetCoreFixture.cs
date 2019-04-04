//-----------------------------------------------------------------------------
// FILE:	    Test_AspNetCoreFixture.cs
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
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

using Xunit;

using Test.Neon.Models;

namespace TestXunit.AspNetFixture
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.EnvironmentName.Equals("Development"))
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting(routes =>
            {
                routes.MapControllers();
            });
        }
    }

    [Route("/TestAspCoreFixture")]
    public class TestAspCoreFixtureController : NeonController
    {
        [HttpGet]
        public string Hello()
        {
            return "World!";
        }

        [HttpGet]
        [Route("person/{id}/{name}/{age}")]
        public Person Create(int id, string name, int age)
        {
            return new Person()
            {
                 Id   = id,
                 Name = name,
                 Age  = age
            };
        }

        [HttpPut]
        public Person IncrementAge([FromBody] Person person)
        {
            if (person == null)
            {
                return person;
            }

            person.Age++;
            return person;
        }
    }

    public class Test_AspNetCoreFixture : AspNetCoreFixture
    {
        private TestAspCoreFixtureClient client;

        public Test_AspNetCoreFixture(AspNetCoreFixture fixture)
        {
            fixture.Start<Startup>();

            client = new TestAspCoreFixtureClient()
            {
                BaseAddress = fixture.BaseAddress
            };
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonXunit)]
        public async Task Client()
        {
            // Verify that a simple GET request works.

            Assert.Equal("World!", await client.HelloAsync());
        }
    }
}
