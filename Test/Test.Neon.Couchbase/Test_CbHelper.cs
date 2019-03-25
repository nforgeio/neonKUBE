//-----------------------------------------------------------------------------
// FILE:	    Test_CbHelper.cs
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

using Couchbase;

using Neon.Common;
using Neon.Xunit;
using Neon.Xunit.Couchbase;

using Xunit;

namespace TestCouchbase
{
    public class Test_CbHelper
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void Literal_String()
        {
            Assert.Equal("NULL", CbHelper.Literal(null));
            Assert.Equal("\"Hello World!\"", CbHelper.Literal("Hello World!"));
            Assert.Equal("\"'\"", CbHelper.Literal("'"));
            Assert.Equal("\"\\\"\"", CbHelper.Literal("\""));
            Assert.Equal("\"\\\\\"", CbHelper.Literal("\\"));
            Assert.Equal("\"\\b\"", CbHelper.Literal("\b"));
            Assert.Equal("\"\\f\"", CbHelper.Literal("\f"));
            Assert.Equal("\"\\n\"", CbHelper.Literal("\n"));
            Assert.Equal("\"\\r\"", CbHelper.Literal("\r"));
            Assert.Equal("\"\\t\"", CbHelper.Literal("\t"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void Literal_Name()
        {
            Assert.Throws<ArgumentNullException>(() => CbHelper.LiteralName(null));
            Assert.Throws<ArgumentNullException>(() => CbHelper.LiteralName(string.Empty));

            Assert.Equal("`test`", CbHelper.LiteralName("test"));
            Assert.Equal("`test\\\\foo`", CbHelper.LiteralName("test\\foo"));
        }
    }
}
