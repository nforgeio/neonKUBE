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

    [ServiceModel(Name = "EmptyOverride")]
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
        void TestDelete([FromQuery]int p1, [FromQuery]string p2);

        [HttpGet]
        void TestHead([FromQuery]int p1, [FromQuery]string p2);

        [HttpOptions]
        void TestOptions([FromQuery]int p1, [FromQuery]string p2);

        [HttpPatch]
        void TestPatch([FromQuery]int p1, [FromQuery]string p2, [FromBody]SimpleData p3);

        [HttpPost]
        void TestPost([FromQuery]int p1, [FromQuery]string p2, [FromBody]SimpleData p3);

        [HttpPost]
        void TestPut([FromQuery]int p1, [FromQuery]string p2, [FromBody]SimpleData p3);
    }

    [ServiceModel]
    public interface QueryServiceController
    {
        void Test1(int p1, string p2, MyEnum p3);

        void Test2(int p1, string p2, MyEnum p3);

        [HttpPost]
        void Test3([FromQuery]int p1, [FromQuery]string p2, [FromBody]SimpleData p3);
    }

    [ServiceModel]
    public interface RouteService1
    {
        void Test1(int p1, string p2, MyEnum p3);

        [Route("{p1}/{p2}/{p3}")]
        void Test2(int p1, string p2, MyEnum p3);
    }

    [ServiceModel]
    [RoutePrefix("/api/v1/service2")]
    public interface RouteService2
    {
        void Test1(int p1, string p2, MyEnum p3);

        [Route("{p1}/{p2}/{p3}")]
        void Test2(int p1, string p2, MyEnum p3);
    }

    [ServiceModel(Name = "MyRouteService")]
    [RoutePrefix("/api/v1/service3")]
    public interface RouteService3
    {
        [Route("")]
        void Test1(int p1, string p2, MyEnum p3);

        [Route("/{p1}/{p2}/{p3}")]
        void Test2(int p1, string p2, MyEnum p3);
    }

    [ServiceModel]
    public interface ComplexService
    {
        [Route("/{p1}/{p2}/{p3}")]
        ComplexData Test2(int p1, string p2, MyEnum p3);
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

                    await response.WriteAsync(NeonHelper.JsonSerialize(output));
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
        public void RouteService1()
        {
            // Verify that we can generate and call a service defined without
            // any special routing (etc) attributes.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_ServiceModel).Namespace,
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));
        }
    }
}
