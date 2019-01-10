//-----------------------------------------------------------------------------
// FILE:	    Test_CbHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

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
