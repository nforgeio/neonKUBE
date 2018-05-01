//-----------------------------------------------------------------------------
// FILE:	    Test_String.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Retry;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public class Test_String
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void ToLines()
        {
            Assert.Empty(((string)null).ToLines());

            Assert.Equal(new string[] { "" }, "".ToLines());
            Assert.Equal(new string[] { "    " }, "    ".ToLines());
            Assert.Equal(new string[] { "one" }, "one".ToLines());

            Assert.Equal(new string[] { "one" }, "one\r\n".ToLines());
            Assert.Equal(new string[] { "one", "two" }, "one\r\ntwo".ToLines());
            Assert.Equal(new string[] { "one", "two", "three" }, "one\r\ntwo\r\nthree".ToLines());

            Assert.Equal(new string[] { "one" }, "one\n".ToLines());
            Assert.Equal(new string[] { "one", "two" }, "one\ntwo".ToLines());
            Assert.Equal(new string[] { "one", "two", "three" }, "one\ntwo\r\nthree".ToLines());
        }
    }
}
