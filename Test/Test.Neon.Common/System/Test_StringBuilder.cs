//-----------------------------------------------------------------------------
// FILE:	    Test_StringBuilder.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Retry;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public class Test_StringBuilder
    {
        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public void AppendLineLinux()
        {
            var sb = new StringBuilder();

            sb.AppendLineLinux("this is a test");

            Assert.Contains("\n", sb.ToString());
            Assert.DoesNotContain("\r", sb.ToString());
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public void AppendWithSeparator()
        {
            var sb = new StringBuilder();

            sb.AppendWithSeparator("one");
            sb.AppendWithSeparator("two");
            sb.AppendWithSeparator(null);
            sb.AppendWithSeparator(string.Empty);
            sb.AppendWithSeparator("three");

            Assert.Equal("one two three", sb.ToString());

            sb.Clear();

            sb.AppendWithSeparator("one", ", ");
            sb.AppendWithSeparator("two", ", ");
            sb.AppendWithSeparator(null, ", ");
            sb.AppendWithSeparator(string.Empty, ", ");
            sb.AppendWithSeparator("three", ", ");

            Assert.Equal("one, two, three", sb.ToString());
        }
    }
}
