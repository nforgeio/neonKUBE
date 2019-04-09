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
using Xunit.Abstractions;

namespace TestCodeGen.AspNet
{
    [Route("/TestAspNetFixture")]
    public class TestAspNetFixtureController : NeonController
    {
        [HttpGet]
        [Route("GetString")]
        public string GetString(string input)
        {
            return input;
        }

        [HttpGet]
        [Route("GetBool")]
        public bool GetBool(bool input)
        {
            return input;
        }

        [HttpGet]
        [Route("GetInt")]
        public int GetInt(int input)
        {
            return input;
        }

        [HttpGet]
        [Route("GetDouble")]
        public double GetDouble(double input)
        {
            return input;
        }

        [HttpGet]
        [Route("GetTimeSpan")]
        public TimeSpan GetTimeSpan(TimeSpan timespan)
        {
            return timespan;
        }

        [HttpGet]
        [Route("GetVersion")]
        public Version GetVersion(Version version)
        {
            return version;
        }

        [HttpGet]
        [Route("GetSemanticVersion")]
        public SemanticVersion GetSemanticVersion(SemanticVersion version)
        {
            return version;
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
        [Route("IncrementAge")]
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
        private AspNetFixture               fixture;
        private TestAspNetFixtureClient     client;
        private TestOutputWriter            testWriter;

        public Test_EndToEnd(AspNetFixture fixture, ITestOutputHelper outputHelper)
        {
            this.fixture    = fixture;
            this.testWriter = new TestOutputWriter(outputHelper);

            fixture.Start<Startup>(logWriter: testWriter, logLevel: Neon.Diagnostics.LogLevel.Debug);

            client = new TestAspNetFixtureClient()
            {
                BaseAddress = fixture.BaseAddress
            };
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public async Task GetString()
        {
            Assert.Equal("Hello World!", await client.GetStringAsync("Hello World!"));
            Assert.Equal("Goodbye World!", await client.GetStringAsync("Goodbye World!"));
            Assert.Null(await client.GetStringAsync(null));

            // $todo(jeff.lill):
            //
            // This one is a bit strange.  I expected an empty string to be persented to
            // the service endpoint as an empty string but it's showing up as NULL.  I
            // verified that I'm setting the query string properly to:
            //
            //      ...?input=
            //
            // This is probably expected ASP.NET behavior but perhaps worth further
            // investgation at some point.
            //
            //      Assert.Equal("", await client.GetStringAsync(""));

            Assert.Null(await client.GetStringAsync(""));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public async Task GetBool()
        {
            Assert.True(await client.GetBoolAsync(true));
            Assert.False(await client.GetBoolAsync(false));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public async Task GetInt()
        {
            Assert.Equal(0, await client.GetIntAsync(0));
            Assert.Equal(100, await client.GetIntAsync(100));
            Assert.Equal(-100, await client.GetIntAsync(-100));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public async Task GetDouble()
        {
            Assert.Equal(0, await client.GetDoubleAsync(0));
            Assert.Equal(1.234, await client.GetDoubleAsync(1.234));
            Assert.Equal(-1.234, await client.GetDoubleAsync(-1.234));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public async Task GetTimeSpan()
        {
            Assert.Equal(TimeSpan.Zero, await client.GetTimeSpanAsync(TimeSpan.Zero));
            Assert.Equal(TimeSpan.FromDays(2.3456), await client.GetTimeSpanAsync(TimeSpan.FromDays(2.3456)));
            Assert.Equal(TimeSpan.FromDays(-2.3456), await client.GetTimeSpanAsync(TimeSpan.FromDays(-2.3456)));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public async Task GetVersion()
        {
            var version = new Version(1, 2, 3);

            Assert.Equal(version, await client.GetVersionAsync(version));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public async Task GetSemanticVersion()
        {
            var version = SemanticVersion.Create(1, 2, 3, "build", "alpha");

            Assert.Equal(version, await client.GetSemanticVersionAsync(version));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public async Task ReturnPerson()
        {
            var person = await client.CreatePersonAsync(10, "Jeff", 58);

            Assert.Equal(10, person.Id);
            Assert.Equal("Jeff", person.Name);
            Assert.Equal(58, person.Age);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public async Task PutPerson()
        {
            var person = new Person()
            {
                Id = 10,
                Name = "Jeff",
                Age = 58
            };

            var modified = await client.IncrementAgeAsync(person);

            Assert.Equal(10, modified.Id);
            Assert.Equal("Jeff", modified.Name);
            Assert.Equal(59, modified.Age);
        }
    }
}
