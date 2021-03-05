//-----------------------------------------------------------------------------
// FILE:	    Test_ServiceModel.cs
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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using Neon.ModelGen;
using Neon.Common;
using Neon.Xunit;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Xunit;

namespace TestModelGen.ServiceModel
{
    internal static class TestSettings
    {
        public const string BaseAddress = "http://127.0.0.1:888/";
    }

    [Target("Default")]
    public enum MyEnum
    {
        One,
        Two,
        Three
    }

    [Target("Default")]
    public interface SimpleData
    {
        string Name { get; set; }
        int Age { get; set; }
        MyEnum Enum { get; set; }
    }

    [Target("Default")]
    public interface ComplexData
    {
        List<string> List { get; set; }
        Dictionary<string, int> Dictionary { get; set; }
        MyEnum Enum { get; set; }
        SimpleData Simple { get; set; }
        int[] SingleArray { get; set; }
        int[][] DoubleArray { get; set; }

        [JsonIgnore]
        int IgnoreThis { get; set; }
    }

    [Target("Default")]
    [ServiceModel]
    [Target("Default")]
    public interface EmptyServiceController
    {
    }

    [Target("Default")]
    [ServiceModel(name: "EmptyOverride")]
    public interface Empty2ServiceController
    {
    }

    [Target("Default")]
    [ServiceModel]
    public interface VoidServiceController
    {
        void VoidResult();
        IActionResult VoidAction();
        Task VoidTask();
    }

    [Target("Default")]
    [ServiceModel]
    public interface MethodsServiceController
    {
        void TestDefault(int p1, string p2, MyEnum p3);

        [HttpGet]
        void TestGet(int p1, string p2, MyEnum p3);

        [HttpDelete]
        void TestDelete([FromQuery]int p1, string p2, MyEnum p3);

        [HttpHead]
        void TestHead(int p1, [FromQuery]string p2, MyEnum p3);

        [HttpOptions]
        void TestOptions(int p1, string p2, [FromQuery]MyEnum p3);

        [HttpPatch]
        void TestPatch([FromQuery]int p1, [FromQuery]string p2, [FromQuery]MyEnum p3);

        [HttpPost]
        void TestPost([FromQuery]int p1, [FromQuery]string p2, [FromQuery]MyEnum p3);

        [HttpPut]
        void TestPut([FromQuery]int p1, [FromQuery]string p2, [FromQuery]MyEnum p3);
    }

    [Target("Default")]
    [ServiceModel]
    public interface ResultsServiceController
    {
        string GetString();
        short GetShort();
        int GetInt();
        bool GetBool();
        MyEnum GetEnum();
        double GetDouble();
        int[] GetIntArray();
        List<int> GetIntList();
        SimpleData GetSimpleData();
    }

    [Target("Default")]
    [ServiceModel]
    public interface SpecialQueryController
    {
        [HttpGet]
        [Route("DateTime")]
        void DateTime([FromQuery] DateTime date);

        [HttpGet]
        [Route("NullableDateTime")]
        void NullableDateTime([FromQuery] DateTime? date = null);

        [HttpGet]
        [Route("DateTimeOffset")]
        void DateTimeOffset([FromQuery] DateTimeOffset date);

        [HttpGet]
        [Route("NullableDateTimeOffset")]
        void NullableDateTimeOffset([FromQuery] DateTimeOffset? date = null);

        [HttpGet]
        [Route("NullableEnum")]
        void NullableEnum([FromQuery] MyEnum? value = null);

        [HttpGet]
        [Route("NullableByte")]
        void NullableByte([FromQuery] byte? value = null);

        [HttpGet]
        [Route("NullableSByte")]
        void NullableSByte([FromQuery] sbyte? value = null);

        [HttpGet]
        [Route("NullableShort")]
        void NullableShort([FromQuery] short? value = null);

        [HttpGet]
        [Route("NullableUShort")]
        void NullableUShort([FromQuery] ushort? value = null);

        [HttpGet]
        [Route("NullableInt")]
        void NullableInt([FromQuery] int? value = null);

        [HttpGet]
        [Route("NullableUInt")]
        void NullableUInt([FromQuery] uint? value = null);

        [HttpGet]
        [Route("NullableLong")]
        void NullableLong([FromQuery] long? value = null);

        [HttpGet]
        [Route("NullableULong")]
        void NullableULong([FromQuery] ulong? value = null);

        [HttpGet]
        [Route("NullableFloat")]
        void NullableFloat([FromQuery] float? value = null);

        [HttpGet]
        [Route("NullableDouble")]
        void NullableDouble([FromQuery] double? value = null);

        [HttpGet]
        [Route("NullableDecimal")]
        void NullableDecimal([FromQuery] decimal? value = null);

        [HttpGet]
        [Route("NullableBool")]
        void NullableBool([FromQuery] bool? value = null);
    }

    [Target("Default")]
    [ServiceModel]
    public interface RouteService1
    {
        /// <summary>
        /// Used to verify default route parameter names.
        /// </summary>
        [Route("Test1/{p1}/{p2}/{p3}")]
        void Test1([FromRoute]int p1, [FromRoute]string p2, [FromRoute]MyEnum p3);

        /// <summary>
        /// Used to verify custom route parameter names.
        /// </summary>
        [Route("Test2/{arg1}/{arg2}/{arg3}")]
        void Test2([FromRoute(Name = "arg1")]int p1, [FromRoute(Name = "arg2")]string p2, [FromRoute(Name = "arg3")]MyEnum p3);
    }

    [Target("Default")]
    [ServiceModel]
    [Route("/api/v1/service2")]
    public interface RouteService2
    {
        /// <summary>
        /// These parameters should be passed as query args.
        /// </summary>
        void Test1(int p1, string p2, MyEnum p3);

        /// <summary>
        /// These parameters should passed in the route, because the parameter
        /// names match the route parameters.
        /// </summary>
        [Route("Test2/{p1}/{p2}/{p3}")]
        void Test2(int p1, string p2, MyEnum p3);
    }

    [Target("Default")]
    [ServiceModel(name: "MyRouteService")]
    [Route("/api/v1/service3")]
    public interface RouteService3
    {
        /// <summary>
        /// Used to ensure that setting route to the empty string is the same as NULL.
        /// </summary>
        [Route("")]
        void Test1(int p1, string p2, MyEnum p3);

        /// <summary>
        /// Used to ensure that we can set having a leading "/" doesn't
        /// override the controller's route by setting an absolute path.
        /// </summary>
        [Route("/{p1}/{p2}/{p3}")]
        void Test2(int p1, string p2, MyEnum p3);
    }

    /// <summary>
    /// Used to impersonate the custom class generated for <see cref="RouteService3"/>
    /// </summary>
    [Target("Default")]
    public interface MyRouteService
    {
    }

    /// <summary>
    /// Used for testing [FromBody] method parameters.
    /// </summary>
    [Target("Default")]
    [ServiceModel]
    [Route("/api/v1/frombody")]
    public interface FromBodyService
    {
        /// <summary>
        /// Verify passing a single data model parameter.
        /// </summary>
        [HttpPost]
        void Test1([FromBody]SimpleData p1);

        /// <summary>
        /// Verify passing other parameters along with a data model parameter.
        /// </summary>
        [HttpPost]
        void Test2(int p1, string p2, MyEnum p3, [FromBody]SimpleData p4);

        /// <summary>
        /// Verify passing a byte array.
        /// </summary>
        [HttpPost]
        void Test3([FromBody]byte[] p1);

        /// <summary>
        /// Verify passing a generic list.
        /// </summary>
        [HttpPost]
        void Test4([FromBody]List<string> p1);
    }

    /// <summary>
    /// Used for testing a service client composed of multiple controllers.
    /// </summary>
    [Target("Default")]
    [ServiceModel(name: "Composed", group: "User")]
    [Route("/api/v1/user")]
    public interface ComposedUserController
    {
        [HttpGet]
        [Route("{id}")]
        string Get(int id);

        [HttpGet]
        string[] List();
    }

    /// <summary>
    /// Used for testing a service client composed of multiple controllers.
    /// </summary>
    [Target("Default")]
    [ServiceModel(name: "Composed", group: "Delivery")]
    [Route("/api/v1/delivery")]
    public interface ComposedDeliveryController
    {
        [HttpGet]
        [Route("{id}")]
        string Get(int id);

        [HttpGet]
        string[] List();
    }

    /// <summary>
    /// Used for testing a service client composed of multiple controllers.
    /// </summary>
    [Target("Default")]
    [ServiceModel(name: "Composed")]
    [Route("/api/v1")]
    public interface ComposedController
    {
        [HttpGet]
        string GetVersion();
    }

    [NoCodeGen]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public partial class Test_ServiceModel
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonModelGen)]
        public void EmptyService()
        {
            // Verify a controller with no methods.

            var settings = new ModelGeneratorSettings("Default")
            {
                SourceNamespace = typeof(Test_ServiceModel).Namespace,
            };

            var generator = new ModelGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = ModelGenerator.Compile(output.SourceCode, "test-assembly", references => ModelGenTestHelper.ReferenceHandler(references));

            Assert.Contains("public partial class Empty", output.SourceCode);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonModelGen)]
        public void RenameService()
        {
            // Verify that we can rename a service controller.

            var settings = new ModelGeneratorSettings("Default")
            {
                SourceNamespace = typeof(Test_ServiceModel).Namespace,
            };

            var generator = new ModelGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = ModelGenerator.Compile(output.SourceCode, "test-assembly", references => ModelGenTestHelper.ReferenceHandler(references));

            Assert.Contains("public partial class EmptyOverride", output.SourceCode);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonModelGen)]
        public async Task VoidService()
        {
            // Verify that we can generate and call a service defined without
            // any special routing (etc) attributes and where all methods
            // return VOID.

            var settings = new ModelGeneratorSettings("Default")
            {
                SourceNamespace = typeof(Test_ServiceModel).Namespace,
            };

            var generator = new ModelGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = ModelGenerator.Compile(output.SourceCode, "test-assembly", references => ModelGenTestHelper.ReferenceHandler(references));

            // Spin up a mock service and a service client and then call the service
            // via the client.  The mock service will record the HTTP method, URI, and
            // JSON text received in the request body and then return so that the
            // caller can verify that these were passed correctly.

            var requestMethod      = string.Empty;
            var requestPath        = string.Empty;
            var requestQueryString = string.Empty;
            var requestContentType = string.Empty;
            var requestBody        = string.Empty;

            using (new MockHttpServer(TestSettings.BaseAddress,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    requestMethod      = request.Method;
                    requestPath        = request.Path;
                    requestQueryString = request.QueryString;
                    requestContentType = request.ContentType;

                    if (request.HasEntityBody)
                    {
                        requestBody = request.GetBodyText();
                    }
                    else
                    {
                        requestBody = null;
                    }

                    response.ContentType = "application/json";

                    await Task.CompletedTask;
                }))
            {
                using (var context = new AssemblyContext("Neon.ModelGen.Output", assemblyStream))
                {
                    using (var client = context.CreateServiceWrapper<VoidServiceController>(TestSettings.BaseAddress))
                    {
                        // Call: VoidResult()

                        await client.CallAsync("VoidResult");
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/VoidService/VoidResult", requestPath);
                        Assert.Equal("", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // Call: VoidAction()

                        await client.CallAsync("VoidAction");
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/VoidService/VoidAction", requestPath);
                        Assert.Equal("", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // Call: VoidTask()

                        await client.CallAsync("VoidTask");
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/VoidService/VoidTask", requestPath);
                        Assert.Equal("", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);
                    }
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonModelGen)]
        public async Task MethodService()
        {
            // Verify that the correct HTTP methods are used.

            var settings = new ModelGeneratorSettings("Default")
            {
                SourceNamespace = typeof(Test_ServiceModel).Namespace,
            };

            var generator = new ModelGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = ModelGenerator.Compile(output.SourceCode, "test-assembly", references => ModelGenTestHelper.ReferenceHandler(references));

            // Spin up a mock service and a service client and then call the service
            // via the client.  The mock service will record the HTTP method, URI, and
            // JSON text received in the request body and then return so that the
            // caller can verify that these were passed correctly.

            var requestMethod      = string.Empty;
            var requestPath        = string.Empty;
            var requestQueryString = string.Empty;
            var requestContentType = string.Empty;
            var requestBody        = string.Empty;

            using (new MockHttpServer(TestSettings.BaseAddress,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    requestMethod      = request.Method;
                    requestPath        = request.Path;
                    requestQueryString = request.QueryString;
                    requestContentType = request.ContentType;

                    if (request.HasEntityBody)
                    {
                        requestBody = request.GetBodyText();
                    }
                    else
                    {
                        requestBody = null;
                    }

                    response.ContentType = "application/json";

                    await Task.CompletedTask;
                }))
            {
                using (var context = new AssemblyContext("Neon.ModelGen.Output", assemblyStream))
                {
                    using (var client = context.CreateServiceWrapper<MethodsServiceController>(TestSettings.BaseAddress))
                    {
                        // Call: TestDefault()

                        await client.CallAsync("TestDefault", 1, "two", MyEnum.Three);
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/MethodsService/TestDefault", requestPath);
                        Assert.Equal("?p1=1&p2=two&p3=Three", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // Call: TestGet()

                        await client.CallAsync("TestGet", 1, "two", MyEnum.Three);
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/MethodsService/TestGet", requestPath);
                        Assert.Equal("?p1=1&p2=two&p3=Three", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // Call: TestDelete()

                        await client.CallAsync("TestDelete", 1, "two", MyEnum.Three);
                        Assert.Equal("DELETE", requestMethod);
                        Assert.Equal("/MethodsService/TestDelete", requestPath);
                        Assert.Equal("?p1=1&p2=two&p3=Three", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // Call: TestHead()

                        await client.CallAsync("TestHead", 1, "two", MyEnum.Three);
                        Assert.Equal("HEAD", requestMethod);
                        Assert.Equal("/MethodsService/TestHead", requestPath);
                        Assert.Equal("?p1=1&p2=two&p3=Three", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // Call: TestOptions()

                        await client.CallAsync("TestOptions", 1, "two", MyEnum.Three);
                        Assert.Equal("OPTIONS", requestMethod);
                        Assert.Equal("/MethodsService/TestOptions", requestPath);
                        Assert.Equal("?p1=1&p2=two&p3=Three", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // Call: TestPatch()

                        await client.CallAsync("TestPatch", 1, "two", MyEnum.Three);
                        Assert.Equal("PATCH", requestMethod);
                        Assert.Equal("/MethodsService/TestPatch", requestPath);
                        Assert.Equal("?p1=1&p2=two&p3=Three", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // Call: TestPost()

                        await client.CallAsync("TestPost", 1, "two", MyEnum.Three);
                        Assert.Equal("POST", requestMethod);
                        Assert.Equal("/MethodsService/TestPost", requestPath);
                        Assert.Equal("?p1=1&p2=two&p3=Three", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // Call: TestPut()

                        await client.CallAsync("TestPut", 1, "two", MyEnum.Three);
                        Assert.Equal("PUT", requestMethod);
                        Assert.Equal("/MethodsService/TestPut", requestPath);
                        Assert.Equal("?p1=1&p2=two&p3=Three", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);
                    }
                }
            }
        }


        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonModelGen)]
        public async Task UnsafeMethodService()
        {
            // Verify that the correct HTTP methods are used when performing unsafe
            // operations.

            var settings = new ModelGeneratorSettings("Default")
            {
                SourceNamespace = typeof(Test_ServiceModel).Namespace,
            };

            var generator = new ModelGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = ModelGenerator.Compile(output.SourceCode, "test-assembly", references => ModelGenTestHelper.ReferenceHandler(references));

            // Spin up a mock service and a service client and then call the service
            // via the client.  The mock service will record the HTTP method, URI, and
            // JSON text received in the request body and then return so that the
            // caller can verify that these were passed correctly.

            var requestMethod      = string.Empty;
            var requestPath        = string.Empty;
            var requestQueryString = string.Empty;
            var requestContentType = string.Empty;
            var requestBody        = string.Empty;

            using (new MockHttpServer(TestSettings.BaseAddress,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    requestMethod      = request.Method;
                    requestPath        = request.Path;
                    requestQueryString = request.QueryString;
                    requestContentType = request.ContentType;

                    if (request.HasEntityBody)
                    {
                        requestBody = request.GetBodyText();
                    }
                    else
                    {
                        requestBody = null;
                    }

                    response.ContentType = "application/json";

                    await Task.CompletedTask;
                }))
            {
                using (var context = new AssemblyContext("Neon.ModelGen.Output", assemblyStream))
                {
                    using (var client = context.CreateServiceWrapper<MethodsServiceController>(TestSettings.BaseAddress))
                    {
                        // Call: TestDefault()

                        await client.CallAsync("UnsafeTestDefault", 1, "two", MyEnum.Three);
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/MethodsService/TestDefault", requestPath);
                        Assert.Equal("?p1=1&p2=two&p3=Three", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // Call: TestGet()

                        await client.CallAsync("UnsafeTestGet", 1, "two", MyEnum.Three);
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/MethodsService/TestGet", requestPath);
                        Assert.Equal("?p1=1&p2=two&p3=Three", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // Call: TestDelete()

                        await client.CallAsync("UnsafeTestDelete", 1, "two", MyEnum.Three);
                        Assert.Equal("DELETE", requestMethod);
                        Assert.Equal("/MethodsService/TestDelete", requestPath);
                        Assert.Equal("?p1=1&p2=two&p3=Three", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // Call: TestHead()

                        await client.CallAsync("UnsafeTestHead", 1, "two", MyEnum.Three);
                        Assert.Equal("HEAD", requestMethod);
                        Assert.Equal("/MethodsService/TestHead", requestPath);
                        Assert.Equal("?p1=1&p2=two&p3=Three", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // Call: TestOptions()

                        await client.CallAsync("UnsafeTestOptions", 1, "two", MyEnum.Three);
                        Assert.Equal("OPTIONS", requestMethod);
                        Assert.Equal("/MethodsService/TestOptions", requestPath);
                        Assert.Equal("?p1=1&p2=two&p3=Three", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // Call: TestPatch()

                        await client.CallAsync("UnsafeTestPatch", 1, "two", MyEnum.Three);
                        Assert.Equal("PATCH", requestMethod);
                        Assert.Equal("/MethodsService/TestPatch", requestPath);
                        Assert.Equal("?p1=1&p2=two&p3=Three", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // Call: TestPost()

                        await client.CallAsync("UnsafeTestPost", 1, "two", MyEnum.Three);
                        Assert.Equal("POST", requestMethod);
                        Assert.Equal("/MethodsService/TestPost", requestPath);
                        Assert.Equal("?p1=1&p2=two&p3=Three", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // Call: TestPut()

                        await client.CallAsync("UnsafeTestPut", 1, "two", MyEnum.Three);
                        Assert.Equal("PUT", requestMethod);
                        Assert.Equal("/MethodsService/TestPut", requestPath);
                        Assert.Equal("?p1=1&p2=two&p3=Three", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);
                    }
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonModelGen)]
        public async Task ResultsService()
        {
            // Verify that we can parse various types of service results.

            var settings = new ModelGeneratorSettings("Default")
            {
                SourceNamespace = typeof(Test_ServiceModel).Namespace,
            };

            var generator = new ModelGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = ModelGenerator.Compile(output.SourceCode, "test-assembly", references => ModelGenTestHelper.ReferenceHandler(references));

            // Spin up a mock service and a service client and then call the service
            // via the client.  The mock service will record the HTTP method, URI, and
            // JSON text received in the request body and then return so that the
            // caller can verify that these were passed correctly.

            var requestMethod      = string.Empty;
            var requestPath        = string.Empty;
            var requestQueryString = string.Empty;
            var requestContentType = string.Empty;
            var requestBody        = string.Empty;

            using (new MockHttpServer(TestSettings.BaseAddress,
                async context =>
                {
                    var request    = context.Request;
                    var response   = context.Response;
                    var method     = request.Path.Replace("/ResultsService/", string.Empty);
                    var result     = (object)null;
                    var jsonResult = (string)null;

                    requestMethod      = request.Method;
                    requestPath        = request.Path;
                    requestQueryString = request.QueryString;
                    requestContentType = request.ContentType;

                    if (request.HasEntityBody)
                    {
                        requestBody = request.GetBodyText();
                    }
                    else
                    {
                        requestBody = null;
                    }

                    switch (method)
                    {
                        case "GetString":

                            result = "Hello World!";
                            break;

                        case "GetShort":

                            result = (short)555;
                            break;

                        case "GetInt":

                            result = (int)555555;
                            break;

                        case "GetBool":

                            result = true;
                            break;

                        case "GetEnum":

                            result = MyEnum.Two;
                            break;

                        case "GetDouble":

                            result = 123.456;
                            break;

                        case "GetIntArray":

                            result = new int[] { 0, 1, 2, 3, 4 };
                            break;

                        case "GetIntList":

                            result = new List<int>() { 5, 6, 7, 8, 9 };
                            break;

                        case "GetSimpleData":

                            result =
@"{
  ""Name"": ""Joe Bloe"",
  ""Age"": 67,
  ""Enum"": ""Three""
}
";
                            break;

                        default:

                            throw new Exception($"Unexpected service method: {method}");
                    }

                    response.ContentType = "application/json";

                    if (result != null)
                    {
                        await response.WriteAsync(NeonHelper.JsonSerialize(result));
                    }
                    else if (!string.IsNullOrEmpty(jsonResult))
                    {
                        await response.WriteAsync(jsonResult);
                    }
                }))
            {
                using (var context = new AssemblyContext("Neon.ModelGen.Output", assemblyStream))
                {
                    using (var client = context.CreateServiceWrapper<ResultsServiceController>(TestSettings.BaseAddress))
                    {
                        // Call: GetString()

                        var getStringResult = await client.CallAsync<string>("GetString");
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/ResultsService/GetString", requestPath);
                        Assert.Equal(string.Empty, requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);
                        Assert.Equal("Hello World!", getStringResult);

                        // Call: GetShort()

                        var getShortResult = await client.CallAsync<short>("GetShort");
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/ResultsService/GetShort", requestPath);
                        Assert.Equal(string.Empty, requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);
                        Assert.Equal((short)555, getShortResult);

                        // Call: GetInt()

                        var getIntResult = await client.CallAsync<int>("GetInt");
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/ResultsService/GetInt", requestPath);
                        Assert.Equal(string.Empty, requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);
                        Assert.Equal((int)555555, getIntResult);

                        // Call: GetBool()

                        var getBoolResult = await client.CallAsync<bool>("GetBool");
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/ResultsService/GetBool", requestPath);
                        Assert.Equal(string.Empty, requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);
                        Assert.True(getBoolResult);

                        // Call: GetDouble()

                        var getDoublelResult = await client.CallAsync<double>("GetDouble");
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/ResultsService/GetDouble", requestPath);
                        Assert.Equal(string.Empty, requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);
                        Assert.Equal(123.456, getDoublelResult);

                        // Call: GetIntArray()

                        var getIntArrayResult = await client.CallAsync<int[]>("GetIntArray");
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/ResultsService/GetIntArray", requestPath);
                        Assert.Equal(string.Empty, requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);
                        Assert.Equal(new int[] { 0, 1, 2, 3, 4 }, getIntArrayResult);

                        // Call: GetIntList()

                        var getIntListResult = await client.CallAsync<List<int>>("GetIntList");
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/ResultsService/GetIntList", requestPath);
                        Assert.Equal(string.Empty, requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);
                        Assert.Equal(new List<int>() { 5, 6, 7, 8, 9 }, getIntListResult);
                    }
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonModelGen)]
        public async Task RouteService1()
        {
            // Verify that we do URI routing properly.

            var settings = new ModelGeneratorSettings("Default")
            {
                SourceNamespace = typeof(Test_ServiceModel).Namespace,
            };

            var generator = new ModelGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = ModelGenerator.Compile(output.SourceCode, "test-assembly", references => ModelGenTestHelper.ReferenceHandler(references));

            // Spin up a mock service and a service client and then call the service
            // via the client.  The mock service will record the HTTP method, URI, and
            // JSON text received in the request body and then return so that the
            // caller can verify that these were passed correctly.

            var requestMethod      = string.Empty;
            var requestPath        = string.Empty;
            var requestQueryString = string.Empty;
            var requestContentType = string.Empty;
            var requestBody        = string.Empty;

            using (new MockHttpServer(TestSettings.BaseAddress,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    requestMethod      = request.Method;
                    requestPath        = request.Path;
                    requestQueryString = request.QueryString;
                    requestContentType = request.ContentType;

                    if (request.HasEntityBody)
                    {
                        requestBody = request.GetBodyText();
                    }
                    else
                    {
                        requestBody = null;
                    }

                    response.ContentType = "application/json";

                    await Task.CompletedTask;
                }))
            {
                using (var context = new AssemblyContext("Neon.ModelGen.Output", assemblyStream))
                {
                    using (var client = context.CreateServiceWrapper<RouteService1>(TestSettings.BaseAddress))
                    {
                        // Call: Test1()

                        await client.CallAsync("Test1", 1, "two", MyEnum.Three);
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/RouteService1/Test1/1/two/Three", requestPath);
                        Assert.Equal(string.Empty, requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // Call: Test2()

                        await client.CallAsync("Test2", 1, "two", MyEnum.Three);
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/RouteService1/Test2/1/two/Three", requestPath);
                        Assert.Equal(string.Empty, requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);
                    }
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonModelGen)]
        public async Task RouteService2()
        {
            // Verify that we do URI routing properly.

            var settings = new ModelGeneratorSettings("Default")
            {
                SourceNamespace = typeof(Test_ServiceModel).Namespace,
            };

            var generator = new ModelGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = ModelGenerator.Compile(output.SourceCode, "test-assembly", references => ModelGenTestHelper.ReferenceHandler(references));

            // Spin up a mock service and a service client and then call the service
            // via the client.  The mock service will record the HTTP method, URI, and
            // JSON text received in the request body and then return so that the
            // caller can verify that these were passed correctly.

            var requestMethod      = string.Empty;
            var requestPath        = string.Empty;
            var requestQueryString = string.Empty;
            var requestContentType = string.Empty;
            var requestBody        = string.Empty;

            using (new MockHttpServer(TestSettings.BaseAddress,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    requestMethod      = request.Method;
                    requestPath        = request.Path;
                    requestQueryString = request.QueryString;
                    requestContentType = request.ContentType;

                    if (request.HasEntityBody)
                    {
                        requestBody = request.GetBodyText();
                    }
                    else
                    {
                        requestBody = null;
                    }

                    response.ContentType = "application/json";

                    await Task.CompletedTask;
                }))
            {
                using (var context = new AssemblyContext("Neon.ModelGen.Output", assemblyStream))
                {
                    using (var client = context.CreateServiceWrapper<RouteService2>(TestSettings.BaseAddress))
                    {
                        // Call: Test1()

                        await client.CallAsync("Test1", 1, "two", MyEnum.Three);
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/api/v1/service2/Test1", requestPath);
                        Assert.Equal("?p1=1&p2=two&p3=Three", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // Call: Test2()

                        await client.CallAsync("Test2", 1, "two", MyEnum.Three);
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/api/v1/service2/Test2/1/two/Three", requestPath);
                        Assert.Equal(string.Empty, requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);
                    }
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonModelGen)]
        public async Task RouteService3()
        {
            // Verify that we do URI routing properly.

            var settings = new ModelGeneratorSettings("Default")
            {
                SourceNamespace = typeof(Test_ServiceModel).Namespace,
            };

            var generator = new ModelGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = ModelGenerator.Compile(output.SourceCode, "test-assembly", references => ModelGenTestHelper.ReferenceHandler(references));

            // Spin up a mock service and a service client and then call the service
            // via the client.  The mock service will record the HTTP method, URI, and
            // JSON text received in the request body and then return so that the
            // caller can verify that these were passed correctly.

            var requestMethod      = string.Empty;
            var requestPath        = string.Empty;
            var requestQueryString = string.Empty;
            var requestContentType = string.Empty;
            var requestBody        = string.Empty;

            using (new MockHttpServer(TestSettings.BaseAddress,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    requestMethod      = request.Method;
                    requestPath        = request.Path;
                    requestQueryString = request.QueryString;
                    requestContentType = request.ContentType;

                    if (request.HasEntityBody)
                    {
                        requestBody = request.GetBodyText();
                    }
                    else
                    {
                        requestBody = null;
                    }

                    response.ContentType = "application/json";

                    await Task.CompletedTask;
                }))
            {
                using (var context = new AssemblyContext("Neon.ModelGen.Output", assemblyStream))
                {
                    using (var client = context.CreateServiceWrapper<MyRouteService>(TestSettings.BaseAddress))
                    {
                        // Call: Test1()

                        await client.CallAsync("Test1", 1, "two", MyEnum.Three);
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/api/v1/service3/Test1", requestPath);
                        Assert.Equal("?p1=1&p2=two&p3=Three", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // Call: Test2()

                        await client.CallAsync("Test2", 1, "two", MyEnum.Three);
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/api/v1/service3/1/two/Three", requestPath);
                        Assert.Equal(string.Empty, requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);
                    }
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonModelGen)]
        public async Task SpecialQuery()
        {
            // Verify that we can pass some complex and nullable types as query parameters.

            var settings = new ModelGeneratorSettings("Default")
            {
                SourceNamespace = typeof(Test_ServiceModel).Namespace,
            };

            var generator = new ModelGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = ModelGenerator.Compile(output.SourceCode, "test-assembly", references => ModelGenTestHelper.ReferenceHandler(references));

            // Spin up a mock service and a service client and then call the service
            // via the client.  The mock service will record the HTTP method, URI, and
            // JSON text received in the request body and then return so that the
            // caller can verify that these were passed correctly.

            var requestMethod      = string.Empty;
            var requestPath        = string.Empty;
            var requestQueryString = string.Empty;
            var requestContentType = string.Empty;
            var requestBody        = string.Empty;

            using (new MockHttpServer(TestSettings.BaseAddress,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    requestMethod      = request.Method;
                    requestPath        = request.Path;
                    requestQueryString = request.QueryString;
                    requestContentType = request.ContentType;

                    if (request.HasEntityBody)
                    {
                        requestBody = request.GetBodyText();
                    }
                    else
                    {
                        requestBody = null;
                    }

                    response.ContentType = "application/json";

                    await Task.CompletedTask;
                }))
            {
                using (var context = new AssemblyContext("Neon.ModelGen.Output", assemblyStream))
                {
                    using (var client = context.CreateServiceWrapper<SpecialQueryController>(TestSettings.BaseAddress))
                    {
                        // DateTime

                        await client.CallAsync("DateTime", new DateTime(2019, 9, 11));
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/SpecialQuery/DateTime", requestPath);
                        Assert.Equal("?date=2019-09-11T00%3A00%3A00.000Z", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // DateTime? with value

                        await client.CallAsync("NullableDateTime", new DateTime(2019, 9, 11));
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/SpecialQuery/NullableDateTime", requestPath);
                        Assert.Equal("?date=2019-09-11T00%3A00%3A00.000Z", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // DateTime? as NULL

                        await client.CallAsync("NullableDateTime", new object[] { null });
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/SpecialQuery/NullableDateTime", requestPath);
                        Assert.Equal("", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // DateTimeOffset

                        await client.CallAsync("DateTimeOffset", new DateTimeOffset(2019, 9, 11, 1, 2, 3, 4, TimeSpan.FromHours(1)));
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/SpecialQuery/DateTimeOffset", requestPath);
                        Assert.Equal("?date=2019-09-11T01%3A02%3A03.004%2B01%3A00", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // DateTimeOffset? with value

                        await client.CallAsync("NullableDateTimeOffset", new DateTimeOffset(2019, 9, 11, 1, 2, 3, 4, TimeSpan.FromHours(1)));
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/SpecialQuery/NullableDateTimeOffset", requestPath);
                        Assert.Equal("?date=2019-09-11T01%3A02%3A03.004%2B01%3A00", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // DateTimeOffset? as NULL

                        await client.CallAsync("NullableDateTimeOffset", new object[] { null });
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/SpecialQuery/NullableDateTimeOffset", requestPath);
                        Assert.Equal("", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // $todo(jefflill):
                        //
                        // This test doesn't work because I can't instantiate the generated enumeration
                        // value.  I don't believe this is a big deal though, because we can test the
                        // NULL case below and we test enums values elsewhere,
#if TODO
                        // enum? with value

                        await client.CallAsync("NullableEnum", MyEnum.Two);
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/SpecialQuery/NullableEnum", requestPath);
                        Assert.Equal("?value=Two", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);
#endif
                        // enum? as NULL

                        await client.CallAsync("NullableEnum", new object[] { null });
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/SpecialQuery/NullableEnum", requestPath);
                        Assert.Equal("", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // byte? with value

                        await client.CallAsync("NullableByte", (byte)10);
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/SpecialQuery/NullableByte", requestPath);
                        Assert.Equal("?value=10", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // byte? as NULL

                        await client.CallAsync("NullableByte", new object[] { null });
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/SpecialQuery/NullableByte", requestPath);
                        Assert.Equal("", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // sbyte? with value

                        await client.CallAsync("NullableSByte", (sbyte)10);
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/SpecialQuery/NullableSByte", requestPath);
                        Assert.Equal("?value=10", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // sbyte? as NULL

                        await client.CallAsync("NullableSByte", new object[] { null });
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/SpecialQuery/NullableSByte", requestPath);
                        Assert.Equal("", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // short? with value

                        await client.CallAsync("NullableShort", (short)10);
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/SpecialQuery/NullableShort", requestPath);
                        Assert.Equal("?value=10", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // short? as NULL

                        await client.CallAsync("NullableSByte", new object[] { null });
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/SpecialQuery/NullableSByte", requestPath);
                        Assert.Equal("", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // ushort? with value

                        await client.CallAsync("NullableUShort", (ushort)10);
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/SpecialQuery/NullableUShort", requestPath);
                        Assert.Equal("?value=10", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // ushort? as NULL

                        await client.CallAsync("NullableUShort", new object[] { null });
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/SpecialQuery/NullableUShort", requestPath);
                        Assert.Equal("", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // int? with value

                        await client.CallAsync("NullableInt", 10);
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/SpecialQuery/NullableInt", requestPath);
                        Assert.Equal("?value=10", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // int? as NULL

                        await client.CallAsync("NullableInt", new object[] { null });
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/SpecialQuery/NullableInt", requestPath);
                        Assert.Equal("", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // uint? with value

                        await client.CallAsync("NullableUInt", (uint)10);
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/SpecialQuery/NullableUInt", requestPath);
                        Assert.Equal("?value=10", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // uint? as NULL

                        await client.CallAsync("NullableUInt", new object[] { null });
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/SpecialQuery/NullableUInt", requestPath);
                        Assert.Equal("", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // long? with value

                        await client.CallAsync("NullableLong", (long)10);
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/SpecialQuery/NullableLong", requestPath);
                        Assert.Equal("?value=10", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // long? as NULL

                        await client.CallAsync("NullableLong", new object[] { null });
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/SpecialQuery/NullableLong", requestPath);
                        Assert.Equal("", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // ulong? with value

                        await client.CallAsync("NullableULong", (ulong)10);
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/SpecialQuery/NullableULong", requestPath);
                        Assert.Equal("?value=10", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // ulong? as NULL

                        await client.CallAsync("NullableULong", new object[] { null });
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/SpecialQuery/NullableULong", requestPath);
                        Assert.Equal("", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // float? with value

                        await client.CallAsync("NullableFloat", (float)123.456);
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/SpecialQuery/NullableFloat", requestPath);
                        Assert.Equal("?value=123.456", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // float? as NULL

                        await client.CallAsync("NullableFloat", new object[] { null });
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/SpecialQuery/NullableFloat", requestPath);
                        Assert.Equal("", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // double? with value

                        await client.CallAsync("NullableDouble", (double)123.456);
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/SpecialQuery/NullableDouble", requestPath);
                        Assert.Equal("?value=123.456", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // double? as NULL

                        await client.CallAsync("NullableDouble", new object[] { null });
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/SpecialQuery/NullableDouble", requestPath);
                        Assert.Equal("", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // decimal? with value

                        await client.CallAsync("NullableDecimal", (decimal)123.456);
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/SpecialQuery/NullableDecimal", requestPath);
                        Assert.Equal("?value=123.456", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // decimal? as NULL

                        await client.CallAsync("NullableDecimal", new object[] { null });
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/SpecialQuery/NullableDecimal", requestPath);
                        Assert.Equal("", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // bool? with value

                        await client.CallAsync("NullableBool", true);
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/SpecialQuery/NullableBool", requestPath);
                        Assert.Equal("?value=true", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // bool? as NULL

                        await client.CallAsync("NullableBool", new object[] { null });
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/SpecialQuery/NullableBool", requestPath);
                        Assert.Equal("", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);
                    }
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonModelGen)]
        public async Task FromBody()
        {
            // Verify that we can transmit [FromBody] parameters properly.

            var settings = new ModelGeneratorSettings("Default")
            {
                SourceNamespace = typeof(Test_ServiceModel).Namespace,
            };

            var generator = new ModelGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = ModelGenerator.Compile(output.SourceCode, "test-assembly", references => ModelGenTestHelper.ReferenceHandler(references));

            // Spin up a mock service and a service client and then call the service
            // via the client.  The mock service will record the HTTP method, URI, and
            // JSON text received in the request body and then return so that the
            // caller can verify that these were passed correctly.

            var requestMethod      = string.Empty;
            var requestPath        = string.Empty;
            var requestQueryString = string.Empty;
            var requestContentType = string.Empty;
            var requestBody        = string.Empty;

            using (new MockHttpServer(TestSettings.BaseAddress,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    requestMethod      = request.Method;
                    requestPath        = request.Path;
                    requestQueryString = request.QueryString;
                    requestContentType = request.ContentType;

                    if (request.HasEntityBody)
                    {
                        requestBody = request.GetBodyText();
                    }
                    else
                    {
                        requestBody = null;
                    }

                    response.ContentType = "application/json";

                    await Task.CompletedTask;
                }))
            {
                using (var context = new AssemblyContext("Neon.ModelGen.Output", assemblyStream))
                {
                    using (var client = context.CreateServiceWrapper<FromBodyService>(TestSettings.BaseAddress))
                    {
                        var simpleData = context.CreateDataWrapper<SimpleData>();

                        simpleData["Name"] = "Joe Bloe";
                        simpleData["Age"]  = 45;
                        simpleData["Enum"] = MyEnum.Two;

                        // Call: Test1()

                        await client.CallAsync("Test1", simpleData.__Instance);
                        Assert.Equal("POST", requestMethod);
                        Assert.Equal("/api/v1/frombody/Test1", requestPath);
                        Assert.Equal("", requestQueryString);
                        Assert.Equal("application/json; charset=utf-8", requestContentType);
                        Assert.Equal("{\"Name\":\"Joe Bloe\",\"Age\":45,\"Enum\":\"Two\"}", requestBody);

                        // Call: Test2()

                        await client.CallAsync("Test2", 1, "two", MyEnum.Three, simpleData.__Instance);
                        Assert.Equal("POST", requestMethod);
                        Assert.Equal("/api/v1/frombody/Test2", requestPath);
                        Assert.Equal("?p1=1&p2=two&p3=Three", requestQueryString);
                        Assert.Equal("application/json; charset=utf-8", requestContentType);
                        Assert.Equal("{\"Name\":\"Joe Bloe\",\"Age\":45,\"Enum\":\"Two\"}", requestBody);

                        // Call: Test3()

                        await client.CallAsync("Test3", new byte[] { 0, 1, 2, 3, 4 });
                        Assert.Equal("POST", requestMethod);
                        Assert.Equal("/api/v1/frombody/Test3", requestPath);
                        Assert.Equal("", requestQueryString);
                        Assert.Equal("application/json; charset=utf-8", requestContentType);
                        Assert.Equal($"\"{Convert.ToBase64String(new byte[] { 0, 1, 2, 3, 4 })}\"", requestBody);

                        // Call: Test4()

                        await client.CallAsync("Test4", new List<string>() { "zero", "one", "two", "three", "four" });
                        Assert.Equal("POST", requestMethod);
                        Assert.Equal("/api/v1/frombody/Test4", requestPath);
                        Assert.Equal("", requestQueryString);
                        Assert.Equal("application/json; charset=utf-8", requestContentType);
                        Assert.Equal($"[\"zero\",\"one\",\"two\",\"three\",\"four\"]", requestBody);
                    }
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonModelGen)]
        public async Task Composed()
        {
            // Verify that a client composed from multiple service models work.

            var settings = new ModelGeneratorSettings("Default")
            {
                SourceNamespace = typeof(Test_ServiceModel).Namespace,
            };

            var generator = new ModelGenerator(settings);
            var output = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = ModelGenerator.Compile(output.SourceCode, "test-assembly", references => ModelGenTestHelper.ReferenceHandler(references));

            // Spin up a mock service and a service client and then call the service
            // via the client.  The mock service will record the HTTP method, URI, and
            // JSON text received in the request body and then return so that the
            // caller can verify that these were passed correctly.

            var requestMethod      = string.Empty;
            var requestPath        = string.Empty;
            var requestQueryString = string.Empty;
            var requestContentType = string.Empty;
            var requestBody        = string.Empty;

            using (new MockHttpServer(TestSettings.BaseAddress,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    requestMethod = request.Method;
                    requestPath = request.Path;
                    requestQueryString = request.QueryString;
                    requestContentType = request.ContentType;

                    if (request.HasEntityBody)
                    {
                        requestBody = request.GetBodyText();
                    }
                    else
                    {
                        requestBody = null;
                    }

                    response.ContentType = "application/json";

                    await Task.CompletedTask;
                }))
            {
                using (var context = new AssemblyContext("Neon.ModelGen.Output", assemblyStream))
                {
                    using (var client = context.CreateServiceWrapper<ComposedController>(TestSettings.BaseAddress))
                    {
                        // Call: GetVersion()

                        await client.CallAsync("GetVersion");
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/api/v1/GetVersion", requestPath);
                        Assert.Equal("", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // Call: User.Get()

                        await client.ComposedCallAsync("User", "Get", 10);
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/api/v1/user/10", requestPath);
                        Assert.Equal("", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // Call: User.List()

                        await client.ComposedCallAsync("User", "List");
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/api/v1/user/List", requestPath);
                        Assert.Equal("", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // Call: Delivery.Get()

                        await client.ComposedCallAsync("Delivery", "Get", 10);
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/api/v1/delivery/10", requestPath);
                        Assert.Equal("", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);

                        // Call: Delivery.List()

                        await client.ComposedCallAsync("Delivery", "List");
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/api/v1/delivery/List", requestPath);
                        Assert.Equal("", requestQueryString);
                        Assert.Null(requestContentType);
                        Assert.Null(requestBody);
                    }
                }
            }
        }
    }

    [Target("OptionalParamController_Header")]
    [ServiceModel]
    public interface DefaultParamController_AsHeader
    {
        [HttpPost]
        void Test([FromHeader(Name = "X-Test")] string hello = "world");
    }

    [Target("OptionalParamController_Query")]
    [ServiceModel]
    public interface DefaultParamController_AsQuery
    {
        [HttpPost]
        void Test([FromQuery] string hello = "world");
    }

    [Target("OptionalParamController_Body")]
    [ServiceModel]
    public interface DefaultParamController_AsBody
    {
        [HttpPost]
        void Test([FromBody] string hello = "world");
    }

    [Target("OptionalParamController_Route")]
    [ServiceModel]
    public interface DefaultParamController_InRoute
    {
        [HttpPost]
        [Route("{hello}")]
        void Test([FromRoute] string hello = "world");
    }

    [Target("OptionalParamController_Default")]
    [ServiceModel]
    public interface DefaultParamController_Default
    {
        [HttpPost]
        [Route("{hello}")]
        void Test(string hello = "world");
    }

    public partial class Test_ServiceModel
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonModelGen)]
        public void OptionalParams()
        {
            var settings = new ModelGeneratorSettings()
            {
                SourceNamespace = typeof(Test_ServiceModel).Namespace
            };

            // Verify that only [FromHeader] and [FromBody] parameters are allowed
            // to be defined as optional.

            // [Default(route)]: should work

            settings.Targets.Clear();
            settings.Targets.Add("OptionalParamController_Default");

            var generator = new ModelGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.True(output.HasErrors);

            // [FromHeader]: should work

            settings.Targets.Clear();
            settings.Targets.Add("OptionalParamController_Header");

            generator = new ModelGenerator(settings);
            output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);
            ModelGenerator.Compile(output.SourceCode, "test-assembly", references => ModelGenTestHelper.ReferenceHandler(references));

            // [FromQuery]: should work

            settings.Targets.Clear();
            settings.Targets.Add("OptionalParamController_Query");

            generator = new ModelGenerator(settings);
            output = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);
            ModelGenerator.Compile(output.SourceCode, "test-assembly", references => ModelGenTestHelper.ReferenceHandler(references));

            // [FromBody]: should work

            settings.Targets.Clear();
            settings.Targets.Add("OptionalParamController_Body");

            generator = new ModelGenerator(settings);
            output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);
            ModelGenerator.Compile(output.SourceCode, "test-assembly", references => ModelGenTestHelper.ReferenceHandler(references));

            // [FromRoute]: should fail

            settings.Targets.Clear();
            settings.Targets.Add("OptionalParamController_Route");

            generator = new ModelGenerator(settings);
            output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.True(output.HasErrors);
        }
    }
}
