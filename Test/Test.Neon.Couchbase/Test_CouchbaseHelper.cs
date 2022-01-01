//-----------------------------------------------------------------------------
// FILE:	    Test_CouchbaseHelper.cs
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
    [Trait(TestTrait.Category, TestArea.NeonCouchbase)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_CouchbaseHelper
    {
        [Fact]
        public void Literal_String()
        {
            Assert.Equal("NULL", CouchbaseHelper.Literal(null));
            Assert.Equal("\"Hello World!\"", CouchbaseHelper.Literal("Hello World!"));
            Assert.Equal("\"'\"", CouchbaseHelper.Literal("'"));
            Assert.Equal("\"\\\"\"", CouchbaseHelper.Literal("\""));
            Assert.Equal("\"\\\\\"", CouchbaseHelper.Literal("\\"));
            Assert.Equal("\"\\b\"", CouchbaseHelper.Literal("\b"));
            Assert.Equal("\"\\f\"", CouchbaseHelper.Literal("\f"));
            Assert.Equal("\"\\n\"", CouchbaseHelper.Literal("\n"));
            Assert.Equal("\"\\r\"", CouchbaseHelper.Literal("\r"));
            Assert.Equal("\"\\t\"", CouchbaseHelper.Literal("\t"));
        }

        [Fact]
        public void Literal_Name()
        {
            Assert.Throws<ArgumentNullException>(() => CouchbaseHelper.LiteralName(null));
            Assert.Throws<ArgumentNullException>(() => CouchbaseHelper.LiteralName(string.Empty));

            Assert.Equal("`test`", CouchbaseHelper.LiteralName("test"));
            Assert.Equal("`test\\\\foo`", CouchbaseHelper.LiteralName("test\\foo"));
        }
    }
}
