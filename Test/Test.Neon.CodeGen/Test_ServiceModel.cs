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
    public interface QueryServiceController
    {
        void Test1(int p1, string p2, MyEnum p3);

        [HttpPost]
        void Test2([FromQuery]int p1, [FromQuery]string p2, [FromBody]SimpleData p3);
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
        public void VoidService()
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
