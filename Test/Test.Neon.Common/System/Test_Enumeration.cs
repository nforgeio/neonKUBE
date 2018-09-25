//-----------------------------------------------------------------------------
// FILE:	    Test_Enumeration.cs
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
    public class Test_Enumeration
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void IsEmpty()
        {
            // Verify that both reference and value types work.

            Assert.True((new List<string>()).IsEmpty());
            Assert.True((new List<int>()).IsEmpty());

            Assert.False((new List<string>() { "one", "two" }).IsEmpty());
            Assert.False((new List<int>() { 1, 2 }).IsEmpty());
        }
    }
}
