//-----------------------------------------------------------------------------
// FILE:	    Test_Enum.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
