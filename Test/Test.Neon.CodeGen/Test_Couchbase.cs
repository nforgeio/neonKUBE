//-----------------------------------------------------------------------------
// FILE:	    Test_Couchbase.cs
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

using Couchbase;
using Couchbase.Core;

using Neon.CodeGen;
using Neon.Common;
using Neon.Xunit;
using Neon.Xunit.Couchbase;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Xunit;

namespace TestCodeGen.Couchbase
{
    public enum MyEnum1
    {
        One,
        Two,
        Three
    }

    [Entity]
    public interface Person
    {
        [EntityKey]
        string Name { get; set; }
        int Age { get; set; }
        MyEnum1 Enum { get; set; }
    }

    [NoCodeGen]
    public class Test_Couchbase // : IClassFixture<CouchbaseFixture>
    {
        //private const string username = "Administrator";
        //private const string password = "password";

        //private CouchbaseFixture    couchbase;
        //private NeonBucket          bucket;

        //public Test_Couchbase(CouchbaseFixture couchbase)
        //{
        //    this.couchbase = couchbase;

        //    couchbase.Start();

        //    bucket = couchbase.Bucket;
        //}

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public void Person()
        {
            // Verify that we can generate code for a simple data model.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_Couchbase).Namespace,
                Entities        = true
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

            using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
            {
            }
        }
    }
}
