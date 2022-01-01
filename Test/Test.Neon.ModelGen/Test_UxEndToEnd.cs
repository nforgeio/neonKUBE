//-----------------------------------------------------------------------------
// FILE:	    Test_UxEndToEnd.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Collections.ObjectModel;
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

using Test.Neon.UxModels;
using Newtonsoft.Json.Linq;

using Xunit;
using Xunit.Abstractions;

namespace TestModelGen.UxAspNet
{
    [Route("/TestUxAspNetFixture")]
    public class TestUxAspNetFixtureController : NeonControllerBase
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
        [Route("person/{id}/{name}/{age}")]
        public Person CreatePerson(int id, string name, int age, Gender gender)
        {
            return new Person()
            {
                Id = id,
                Name = name,
                Age = age,
                Gender = gender
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

        [HttpGet]
        public int DefaultInt(int value = 10)
        {
            return value;
        }

        [HttpGet]
        public bool DefaultBool(bool value = true)
        {
            return value;
        }

        [HttpGet]
        public double DefaultDouble(double value = 1.234)
        {
            return value;
        }

        [HttpGet]
        public string DefaultString(string value = "test")
        {
            return value;
        }

        [HttpGet]
        public MyEnum DefaultEnum(MyEnum value = MyEnum.Three)
        {
            return value;
        }

        [HttpGet]
        [Route("GetOptionalStringViaHeader_Null")]
        public string GetOptionalStringViaHeader_Null([FromHeader(Name = "X-Test")] string value = null)
        {
            return value;
        }

        [HttpGet]
        [Route("GetOptionalStringViaHeader_Value")]
        public string GetOptionalStringViaHeader_Value([FromHeader(Name = "X-Test")] string value = "Hello World!")
        {
            return value;
        }

        [HttpGet]
        [Route("GetOptionalStringViaQuery_Null")]
        public string GetOptionalStringViaQuery_Null([FromQuery] string value = null)
        {
            return value;
        }

        [HttpGet]
        [Route("GetOptionalStringViaQuery_Value")]
        public string GetOptionalStringViaQuery_Value([FromQuery] string value = "Hello World!")
        {
            return value;
        }

        [HttpGet]
        [Route("GetOptionalEnumViaHeader")]
        public MyEnum GetOptionalEnumViaHeader([FromHeader(Name = "X-Test")] MyEnum value = MyEnum.Three)
        {
            return value;
        }

        [HttpGet]
        [Route("GetOptionalEnumViaQuery")]
        public MyEnum GetOptionalEnumViaQuery([FromQuery] MyEnum value = MyEnum.Three)
        {
            return value;
        }

        [HttpGet]
        [Route("GetOptionalDoubleViaHeader")]
        public double GetOptionalDoubleViaHeader([FromHeader(Name = "X-Test")] double value = 1.234)
        {
            return value;
        }

        [HttpGet]
        [Route("GetOptionalDoubleViaQuery")]
        public double GetOptionalDoubleViaQuery([FromQuery] double value = 1.234)
        {
            return value;
        }

        [HttpPut]
        [Route("GetOptionalDoubleViaBody")]
        public double GetOptionalDoubleViaBody([FromBody] double value = 1.234)
        {
            return value;
        }

        [HttpPut]
        [Route("GetOptionalStringViaBody")]
        public string GetOptionalStringViaBody([FromBody] string value = "Hello World!")
        {
            return value;
        }

        [HttpPut]
        [Route("GetStringList")]
        public List<string> GetStringList([FromBody] List<string> value)
        {
            return value;
        }

        [HttpPut]
        [Route("GetPersonList")]
        public List<Person> GetPersonList([FromBody] List<Person> value)
        {
            return value;
        }

        [HttpPut]
        [Route("GetPersonArray")]
        public Person[] GetPersonArray([FromBody] Person[] value)
        {
            return value;
        }

        //[HttpGet]
        //[Route("EchoDateTime")]
        //DateTime EchoDateTime([FromQuery] DateTime date)
        //{
        //    return date;
        //}
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
                .AddMvc(options =>
                {
                    options.EnableEndpointRouting = false;
                })
                .AddNeon();
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
    [Trait(TestTrait.Category, TestArea.NeonModelGen)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_EndToEnd : IClassFixture<AspNetFixture>
    {
        private AspNetFixture               fixture;
        private TestUxAspNetFixtureClient   client;
        private TestOutputWriter            testWriter;

        public Test_EndToEnd(AspNetFixture fixture, ITestOutputHelper outputHelper)
        {
            var testPort = 0;
            var logLevel = Neon.Diagnostics.LogLevel.None;

            this.fixture    = fixture;
            this.testWriter = new TestOutputWriter(outputHelper);

            fixture.Start<Startup>(port: testPort, logWriter: testWriter, logLevel: logLevel);

            client = new TestUxAspNetFixtureClient()
            {
                BaseAddress = fixture.BaseAddress
            };
        }

        [Fact]
        public void ValidateController()
        {
            client.ValidateController<TestUxAspNetFixtureController>();
        }

        [Fact]
        public async Task GetString()
        {
            Assert.Equal("Hello World!", await client.GetStringAsync("Hello World!"));
            Assert.Equal("Goodbye World!", await client.GetStringAsync("Goodbye World!"));
            Assert.Null(await client.GetStringAsync(null));

            // $todo(jefflill):
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
        public async Task GetBool()
        {
            Assert.True(await client.GetBoolAsync(true));
            Assert.False(await client.GetBoolAsync(false));
        }

        [Fact]
        public async Task GetInt()
        {
            Assert.Equal(0, await client.GetIntAsync(0));
            Assert.Equal(100, await client.GetIntAsync(100));
            Assert.Equal(-100, await client.GetIntAsync(-100));
        }

        [Fact]
        public async Task GetDouble()
        {
            Assert.Equal(0, await client.GetDoubleAsync(0));
            Assert.Equal(1.234, await client.GetDoubleAsync(1.234));
            Assert.Equal(-1.234, await client.GetDoubleAsync(-1.234));
        }

        [Fact]
        public async Task GetTimeSpan()
        {
            Assert.Equal(TimeSpan.Zero, await client.GetTimeSpanAsync(TimeSpan.Zero));
            Assert.Equal(TimeSpan.FromDays(2.3456), await client.GetTimeSpanAsync(TimeSpan.FromDays(2.3456)));
            Assert.Equal(TimeSpan.FromDays(-2.3456), await client.GetTimeSpanAsync(TimeSpan.FromDays(-2.3456)));
        }

        [Fact]
        public async Task GetVersion()
        {
            var version = new Version(1, 2, 3);

            Assert.Equal(version, await client.GetVersionAsync(version));
        }

        [Fact]
        public async Task CreatePerson()
        {
            var person = await client.CreatePersonAsync(10, "Jeff", 58, Gender.Male);

            Assert.Equal(10, person.Id);
            Assert.Equal("Jeff", person.Name);
            Assert.Equal(58, person.Age);
            Assert.Equal(Gender.Male, person.Gender);
        }

        [Fact]
        public async Task PutPerson()
        {
            var person = new Person()
            {
                Id = 10,
                Name = "Jeff",
                Age = 58,
                Gender = Gender.Male
            };

            var modified = await client.IncrementAgeAsync(person);

            Assert.Equal(10, modified.Id);
            Assert.Equal("Jeff", modified.Name);
            Assert.Equal(59, modified.Age);
            Assert.Equal(Gender.Male, modified.Gender);
        }

        [Fact]
        public async Task OptionalParams()
        {
            Assert.Null(await client.GetOptionalStringViaHeader_NullAsync());
            Assert.Equal("Goodbye World!", await client.GetOptionalStringViaHeader_ValueAsync("Goodbye World!"));
            Assert.Equal("Hello World!", await client.GetOptionalStringViaHeader_ValueAsync());
            Assert.Equal("Goodbye World!", await client.GetOptionalStringViaHeader_ValueAsync("Goodbye World!"));

            Assert.Null((await client.UnsafeGetOptionalStringViaHeader_NullAsync()).As<string>());
            Assert.Equal("Goodbye World!", (await client.UnsafeGetOptionalStringViaHeader_NullAsync("Goodbye World!")).As<string>());
            Assert.Equal("Hello World!", (await client.UnsafeGetOptionalStringViaHeader_ValueAsync()).As<string>());
            Assert.Equal("Goodbye World!", (await client.UnsafeGetOptionalStringViaHeader_ValueAsync("Goodbye World!")).As<string>());

            Assert.Equal(MyEnum.Three, await client.GetOptionalEnumViaHeaderAsync());
            Assert.Equal(MyEnum.Two, await client.GetOptionalEnumViaHeaderAsync(MyEnum.Two));
            Assert.Equal(MyEnum.Three, (await client.UnsafeGetOptionalEnumViaHeaderAsync()).As<MyEnum>());
            Assert.Equal(MyEnum.Two, (await client.UnsafeGetOptionalEnumViaHeaderAsync(MyEnum.Two)).As<MyEnum>());

            Assert.Equal(1.234, await client.GetOptionalDoubleViaHeaderAsync());
            Assert.Equal(2.345, await client.GetOptionalDoubleViaHeaderAsync(2.345));
            Assert.Equal(1.234, (await client.UnsafeGetOptionalDoubleViaHeaderAsync()).As<double>());
            Assert.Equal(2.345, (await client.UnsafeGetOptionalDoubleViaHeaderAsync(2.345)).As<double>());

            Assert.Equal(1.234, await client.GetOptionalDoubleViaBodyAsync());
            Assert.Equal(2.345, await client.GetOptionalDoubleViaBodyAsync(2.345));
            Assert.Equal(1.234, (await client.UnsafeGetOptionalDoubleViaBodyAsync()).As<double>());
            Assert.Equal(2.345, (await client.UnsafeGetOptionalDoubleViaBodyAsync(2.345)).As<double>());

            Assert.Equal("Hello World!", await client.GetOptionalStringViaBodyAsync());
            Assert.Equal("Goodbye World!", await client.GetOptionalStringViaBodyAsync("Goodbye World!"));
            Assert.Equal("Hello World!", (await client.UnsafeGetOptionalStringViaBodyAsync()).As<string>());
            Assert.Equal("Goodbye World!", (await client.UnsafeGetOptionalStringViaBodyAsync("Goodbye World!")).As<string>());
        }

        [Fact]
        public async Task GetStringList()
        {
            Assert.Null(await client.GetStringListAsync(null));
            Assert.Empty(await client.GetStringListAsync(new ObservableCollection<string>()));

            var list = new ObservableCollection<string>();

            list.Add("zero");
            list.Add("one");
            list.Add("two");

            Assert.Equal(list, await client.GetStringListAsync(list));
        }

        [Fact]
        public async Task GetPersonList()
        {
            Assert.Null(await client.GetPersonListAsync(null));
            Assert.Empty(await client.GetPersonListAsync(new ObservableCollection<Person>()));

            var list = new ObservableCollection<Person>();

            list.Add(new Person()
            {
                Id = 1,
                Name = "Jack",
                Age = 10,
                Gender = Gender.Male,
                Data = new byte[] { 0, 1, 2, 3, 4 }
            });

            list.Add(new Person()
            {
                Id = 2,
                Name = "Jill",
                Age = 11,
                Gender = Gender.Female,
                Data = new byte[] { 5, 6, 7, 8, 9 }
            });

            Assert.Equal(list, await client.GetPersonListAsync(list));
        }

        [Fact]
        public async Task GetPersonArray()
        {
            Assert.Null(await client.GetPersonArrayAsync(null));
            Assert.Empty(await client.GetPersonArrayAsync(new Person[0]));

            var list = new Person[]
            {
                new Person()
                {
                    Id = 1,
                    Name = "Jack",
                    Age = 10,
                    Gender = Gender.Male,
                    Data = new byte[] { 0, 1, 2, 3, 4 }
                },
                new Person()
                {
                    Id = 2,
                    Name = "Jill",
                    Age = 11,
                    Gender = Gender.Female,
                    Data = new byte[] { 5, 6, 7, 8, 9 }
                }
            };

            Assert.Equal(list, await client.GetPersonArrayAsync(list));
        }

        [Fact]
        public void RoundTripUnknown()
        {
            // Verify that persisted properties that were unknown
            // at compile time are still round-tripped successfuly.
            // We're going to test this by accessing the backing
            // [JObject].  We're also going to verify that these
            // unknown properties are included in the equality
            // tests.
            //
            // This requirement was the inspiration for this entire code
            // generation thing and it's funny that it took me this
            // much time to actually test it.

            var jObject = new JObject();

            // Verify that we can round trip the "Unknown" property.

            jObject.Add("Unknown", "very tricky!");
            jObject.Add("T$$", typeof(Person).FullName);

            var person = Person.CreateFrom(jObject.ToString());

            person.Name = "Jack";
            person.Age = 10;
            person.Gender = Gender.Male;

            jObject = person.ToJObject();

            Assert.Equal("Jack", (string)jObject["Name"]);
            Assert.Equal(10, (int)jObject["Age"]);
            Assert.Equal(Gender.Male, NeonHelper.ParseEnum<Gender>((string)jObject["Gender"]));
            Assert.Equal("very tricky!", (string)jObject["Unknown"]);
        }

        [Fact]
        public void Derived()
        {
            //-------------------------------------------------------------
            // Verify that [BaseModel] works.

            var baseModel = new BaseModel();

            Assert.Null(baseModel.ParentProperty);

            baseModel.ParentProperty = "Hello World!";
            Assert.Equal("Hello World!", baseModel.ParentProperty);

            baseModel = BaseModel.CreateFrom(baseModel.ToString());
            Assert.Equal("Hello World!", baseModel.ParentProperty);

            //-------------------------------------------------------------
            // Verify that [DerivedModel] works too.

            var derivedModel = new DerivedModel();

            Assert.Null(derivedModel.ParentProperty);
            Assert.Null(derivedModel.ChildProperty);

            derivedModel.ParentProperty = "parent";
            derivedModel.ChildProperty = "child";

            derivedModel = DerivedModel.CreateFrom(derivedModel.ToString());

            Assert.Equal("parent", derivedModel.ParentProperty);
            Assert.Equal("child", derivedModel.ChildProperty);

            //-------------------------------------------------------------
            // Verify Equals():

            var value1 = new DerivedModel() { ParentProperty = "parent", ChildProperty = "child" };
            var value2 = new DerivedModel() { ParentProperty = "parent", ChildProperty = "child" };

            Assert.True(value1.Equals(value1));
            Assert.True(value1.Equals(value2));
            Assert.True(value2.Equals(value1));

            Assert.False(value1.Equals(null));
            Assert.False(value1.Equals("Hello World!"));

            // Verify that a change to the parent class property is detected.

            value1.ParentProperty = "DIFFERENT";

            Assert.True(value1.Equals(value1));

            Assert.False(value1.Equals(value2));
            Assert.False(value2.Equals(value1));
            Assert.False(value1.Equals(null));
            Assert.False(value1.Equals("Hello World!"));

            // Verify that a change to the derived class property is detected.

            value1.ParentProperty = "parent";
            value1.ChildProperty = "DIFFERENT";

            Assert.True(value1.Equals(value1));

            Assert.False(value1.Equals(value2));
            Assert.False(value2.Equals(value1));
            Assert.False(value1.Equals(null));
            Assert.False(value1.Equals("Hello World!"));

            //-------------------------------------------------------------
            // Verify that we can use [ToDerived<TResult>()] to create a derived instance
            // from the base type.  This also exercises [RoundtripDataFactory] a bit.

            derivedModel = new DerivedModel() { ParentProperty = "parent", ChildProperty = "child" };

            baseModel = BaseModel.CreateFrom(derivedModel.ToString());

            Assert.Equal("parent", baseModel.ParentProperty);

            derivedModel = baseModel.ToDerived<DerivedModel>();

            Assert.Equal("parent", derivedModel.ParentProperty);
            Assert.Equal("child", derivedModel.ChildProperty);
        }
    }
}
