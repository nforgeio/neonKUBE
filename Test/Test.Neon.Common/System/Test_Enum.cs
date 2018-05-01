//-----------------------------------------------------------------------------
// FILE:	    Test_Enum.cs
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
    public class Test_Enum
    {
        private enum Foo
        {
            Bar,

            [EnumMember(Value = "FOO-BAR")]
            FooBar
        };

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void ToMemberString()
        {
            Assert.Equal("Bar", Foo.Bar.ToMemberString());          // [Foo.Bar] doesn't have an [EnumMember] attribute so the enum identifer will be returned.
            Assert.Equal("FOO-BAR", Foo.FooBar.ToMemberString());   // [Foo.FooBar] has an [EnumMember] attribute so that value will be returned.
        }
    }
}
