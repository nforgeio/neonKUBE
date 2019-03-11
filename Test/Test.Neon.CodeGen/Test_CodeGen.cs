//-----------------------------------------------------------------------------
// FILE:	    Test_CodeGen
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Neon.CodeGen;
using Neon.Common;
using Neon.Xunit;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Xunit;

namespace TestCodeGen.CodeGen
{
    public interface EmptyData
    {
    }

    public interface SimpleData
    {
        string Name { get; set; }
        int Age { get; set; }
    }

    [NoCodeGen]
    public class Test_CodeGen
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public void Empty()
        {
            // Verify that we can generate code for an empty data model.

            var settings = new CodeGeneratorSettings()
            {
                 SourceNamespace = typeof(Test_CodeGen).Namespace,
                 ServiceClients  = false
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);
        }
    }
}
