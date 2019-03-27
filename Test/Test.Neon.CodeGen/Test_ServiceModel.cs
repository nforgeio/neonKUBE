//-----------------------------------------------------------------------------
// FILE:	    Test_ServiceModel.cs
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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using Neon.CodeGen;
using Neon.Common;
using Neon.Xunit;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Xunit;

namespace TestCodeGen.ServiceModel
{
    internal static class TestSettings
    {
        public const string BaseAddress = "http://127.0.0.1:888/";
    }

    public enum MyEnum
    {
        One,
        Two,
        Three
    }

    public interface SimpleData
    {
        string Name { get; set; }
        int Age { get; set; }
        MyEnum Enum { get; set; }
    }

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

    [ServiceModel]
    public interface EmptyServiceController
    {
    }

    [ServiceModel(name: "EmptyOverride")]
    public interface Empty2ServiceController
    {
    }

    [ServiceModel]
    public interface VoidServiceController
    {
        void VoidResult();
        IActionResult VoidAction();
        Task VoidTask();
    }

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

    [ServiceModel]
    [RoutePrefix("/api/v1/service2")]
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

    [ServiceModel(name: "MyRouteService")]
    [RoutePrefix("/api/v1/service3")]
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
    public interface MyRouteService
    {
    }

    /// <summary>
    /// Used for testing [FromBody] method parameters.
    /// </summary>
    [ServiceModel]
    [RoutePrefix("/api/v1/frombody")]
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
    [ServiceModel(name: "Composed", group: "User")]
    [RoutePrefix("/api/v1/user")]
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
    [ServiceModel(name: "Composed", group: "Delivery")]
    [RoutePrefix("/api/v1/delivery")]
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
    [ServiceModel(name: "Composed")]
    [RoutePrefix("/api/v1")]
    public interface ComposedController
    {
        [HttpGet]
        string GetVersion();
    }

    [NoCodeGen]
    public class Test_ServiceModel
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public void EmptyService()
        {
            // Verify a controller with no methods.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_ServiceModel).Namespace,
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

            Assert.Contains("public partial class Empty", output.SourceCode);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public void RenameService()
        {
            // Verify that we can rename a service controller.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_ServiceModel).Namespace,
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

            Assert.Contains("public partial class EmptyOverride", output.SourceCode);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public async Task VoidService()
        {
            // Verify that we can generate and call a service defined without
            // any special routing (etc) attributes and where all methods
            // return VOID.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_ServiceModel).Namespace,
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

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
                using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
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
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public async Task MethodService()
        {
            // Verify that the correct HTTP methods are used.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_ServiceModel).Namespace,
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

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
                using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
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
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public async Task UnsafeMethodService()
        {
            // Verify that the correct HTTP methods are used when performing unsafe
            // operations.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_ServiceModel).Namespace,
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

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
                using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
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
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public async Task ResultsService()
        {
            // Verify that we can parse various types of service results.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_ServiceModel).Namespace,
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

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
                using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
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
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public async Task RouteService1()
        {
            // Verify that we do URI routing properly.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_ServiceModel).Namespace,
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

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
                using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
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
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public async Task RouteService2()
        {
            // Verify that we do URI routing properly.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_ServiceModel).Namespace,
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

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
                using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
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
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public async Task RouteService3()
        {
            // Verify that we do URI routing properly.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_ServiceModel).Namespace,
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

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
                using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
                {
                    using (var client = context.CreateServiceWrapper<MyRouteService>(TestSettings.BaseAddress))
                    {
                        // Call: Test1()

                        await client.CallAsync("Test1", 1, "two", MyEnum.Three);
                        Assert.Equal("GET", requestMethod);
                        Assert.Equal("/api/v1/service3", requestPath);
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
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public async Task FromBody()
        {
            // Verify that we can transmit [FromBody] parameters properly.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_ServiceModel).Namespace,
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

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
                using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
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
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public async Task Composed()
        {
            // Verify that a client composed from multiple service models work.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_ServiceModel).Namespace,
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

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
                using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
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
}
