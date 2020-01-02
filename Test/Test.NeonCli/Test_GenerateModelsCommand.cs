//-----------------------------------------------------------------------------
// FILE:	    Test_GenerateModelsCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Neon.ModelGen;
using Neon.Common;
using Neon.Kube;
using Neon.IO;
using Neon.Xunit;
using Neon.Xunit.Kube;

using Xunit;
using NeonCli;

namespace FooBar
{
    [Target("4")]
    public interface Class4
    {
        string Field { get; set; }
    }
}

namespace Test.NeonCli
{
    [Target("1")]
    [Target("client")]
    public interface Class1
    {
        string Field { get; set; }
    }

    [Target("2")]
    [Target("client")]
    public interface Class2
    {
        string Field { get; set; }
    }

    [Target("3")]
    public interface Class3
    {
        string Field { get; set; }
    }

    [Target("1")]
    [Target("client")]
    [ServiceModel]
    public interface Service1Controller
    {
        void Hello();
    }

    [Target("2")]
    [ServiceModel]
    public interface Service2Controller
    {
        void World();
    }

    /// <summary>
    /// Test helpers.
    /// </summary>
    internal static class ModelGenTestHelper
    {
        /// <summary>
        /// Adds the assembly references required to compile the generated code.
        /// </summary>
        /// <param name="references">The assembly references.</param>
        public static void ReferenceHandler(MetadataReferences references)
        {
            references.Add(typeof(System.Dynamic.CallInfo));
            references.Add(typeof(Newtonsoft.Json.JsonToken));
            references.Add(typeof(Couchbase.Linq.BucketContext));
        }
    }

    /// <summary>
    /// Tests <b>neon generate models</b> commands.
    /// </summary>
    public class Test_GenerateDataModelsCommand
    {
        private string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void AllTargets()
        {
            using (new KubeTestManager())
            {
                using (var runner = new ProgramRunner())
                {
                    // Verify that all types are generated when no targets
                    // are specified.

                    var result = runner.Execute(Program.Main, "generate", "models", $"--source-namespace={typeof(Test_GenerateDataModelsCommand).Namespace}", thisAssemblyPath);
                    Assert.Equal(0, result.ExitCode);

                    var sourceCode = result.OutputText;

                    ModelGenerator.Compile(sourceCode, "test-assembly", references => ModelGenTestHelper.ReferenceHandler(references));

                    Assert.Contains("class Class1", sourceCode);
                    Assert.Contains("class Class2", sourceCode);
                    Assert.Contains("class Class3", sourceCode);
                    Assert.DoesNotContain("class Class4", sourceCode);
                    Assert.Contains("class Service1", sourceCode);
                    Assert.Contains("class Service2", sourceCode);
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Filter1()
        {
            using (new KubeTestManager())
            {
                using (var runner = new ProgramRunner())
                {
                    // Verify that all types are generated when no targets
                    // are specified.

                    var result = runner.Execute(Program.Main, "generate", "models", $"--source-namespace={typeof(Test_GenerateDataModelsCommand).Namespace}", "--targets=1", thisAssemblyPath);
                    Assert.Equal(0, result.ExitCode);

                    var sourceCode = result.OutputText;

                    ModelGenerator.Compile(sourceCode, "test-assembly", references => ModelGenTestHelper.ReferenceHandler(references));

                    Assert.Contains("class Class1", sourceCode);
                    Assert.DoesNotContain("class Class2", sourceCode);
                    Assert.DoesNotContain("class Class3", sourceCode);
                    Assert.DoesNotContain("class Class4", sourceCode);
                    Assert.Contains("class Service1", sourceCode);
                    Assert.DoesNotContain("class Service2", sourceCode);
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Filter2()
        {
            using (new KubeTestManager())
            {
                using (var runner = new ProgramRunner())
                {
                    // Verify that all types are generated when no targets
                    // are specified.

                    var result = runner.Execute(Program.Main, "generate", "models", $"--source-namespace={typeof(Test_GenerateDataModelsCommand).Namespace}", "--targets=2", thisAssemblyPath);
                    Assert.Equal(0, result.ExitCode);

                    var sourceCode = result.OutputText;

                    ModelGenerator.Compile(sourceCode, "test-assembly", references => ModelGenTestHelper.ReferenceHandler(references));

                    Assert.DoesNotContain("class Class1", sourceCode);
                    Assert.Contains("class Class2", sourceCode);
                    Assert.DoesNotContain("class Class3", sourceCode);
                    Assert.DoesNotContain("class Class4", sourceCode);
                    Assert.DoesNotContain("class Service1", sourceCode);
                    Assert.Contains("class Service2", sourceCode);
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void FilterClient()
        {
            using (new KubeTestManager())
            {
                using (var runner = new ProgramRunner())
                {
                    // Verify that all types are generated when no targets
                    // are specified.

                    var result = runner.Execute(Program.Main, "generate", "models", $"--source-namespace={typeof(Test_GenerateDataModelsCommand).Namespace}", "--targets=client", thisAssemblyPath);
                    Assert.Equal(0, result.ExitCode);

                    var sourceCode = result.OutputText;

                    ModelGenerator.Compile(sourceCode, "test-assembly", references => ModelGenTestHelper.ReferenceHandler(references));

                    Assert.Contains("class Class1", sourceCode);
                    Assert.Contains("class Class2", sourceCode);
                    Assert.DoesNotContain("class Class3", sourceCode);
                    Assert.DoesNotContain("class Class4", sourceCode);
                    Assert.Contains("class Service1", sourceCode);
                    Assert.DoesNotContain("class Service2", sourceCode);
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Filter3AndClient()
        {
            using (new KubeTestManager())
            {
                using (var runner = new ProgramRunner())
                {
                    // Verify that all types are generated when no targets
                    // are specified.

                    var result = runner.Execute(Program.Main, "generate", "models", $"--source-namespace={typeof(Test_GenerateDataModelsCommand).Namespace}", "--targets=3,client", thisAssemblyPath);
                    Assert.Equal(0, result.ExitCode);

                    var sourceCode = result.OutputText;

                    ModelGenerator.Compile(sourceCode, "test-assembly", references => ModelGenTestHelper.ReferenceHandler(references));

                    Assert.Contains("class Class1", sourceCode);
                    Assert.Contains("class Class2", sourceCode);
                    Assert.Contains("class Class3", sourceCode);
                    Assert.DoesNotContain("class Class4", sourceCode);
                    Assert.Contains("class Service1", sourceCode);
                    Assert.DoesNotContain("class Service2", sourceCode);
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void SourceNamespace()
        {
            using (new KubeTestManager())
            {
                using (var runner = new ProgramRunner())
                {
                    // Verify that all types are generated when no targets
                    // are specified.

                    var result = runner.Execute(Program.Main, "generate", "models", $"--source-namespace={typeof(FooBar.Class4).Namespace}", thisAssemblyPath);
                    Assert.Equal(0, result.ExitCode);

                    var sourceCode = result.OutputText;

                    ModelGenerator.Compile(sourceCode, "test-assembly", references => ModelGenTestHelper.ReferenceHandler(references));

                    Assert.DoesNotContain("class Class1", sourceCode);
                    Assert.DoesNotContain("class Class2", sourceCode);
                    Assert.DoesNotContain("class Class3", sourceCode);
                    Assert.Contains("class Class4", sourceCode);
                    Assert.DoesNotContain("class Service1", sourceCode);
                    Assert.DoesNotContain("class Service2", sourceCode);
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void NoServices()
        {
            using (new KubeTestManager())
            {
                using (var runner = new ProgramRunner())
                {
                    // Verify that all types are generated when no targets
                    // are specified.

                    var result = runner.Execute(Program.Main, "generate", "models", $"--source-namespace={typeof(Test_GenerateDataModelsCommand).Namespace}", "--no-services", thisAssemblyPath);
                    Assert.Equal(0, result.ExitCode);

                    var sourceCode = result.OutputText;

                    ModelGenerator.Compile(sourceCode, "test-assembly", references => ModelGenTestHelper.ReferenceHandler(references));

                    Assert.Contains("class Class1", sourceCode);
                    Assert.Contains("class Class2", sourceCode);
                    Assert.Contains("class Class3", sourceCode);
                    Assert.DoesNotContain("class Service1", sourceCode);
                    Assert.DoesNotContain("class Service2", sourceCode);
                }
            }
        }
    }
}