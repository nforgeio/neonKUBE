//-----------------------------------------------------------------------------
// FILE:	    Test_NeonHelper.IO.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public partial class Test_NeonHelper
    {
        [Fact]
        public void FileWildcardRegex()
        {
#pragma warning disable xUnit2008

            // Match everything: *

            var regex = NeonHelper.FileWildcardRegex("*");

            Assert.True(regex.IsMatch("a"));
            Assert.True(regex.IsMatch("ab"));
            Assert.True(regex.IsMatch("abcd.txt"));

            // Match everything with: *.*

            regex = NeonHelper.FileWildcardRegex("*.*");

            Assert.True(regex.IsMatch("a"));
            Assert.True(regex.IsMatch("ab"));
            Assert.True(regex.IsMatch("abcd.txt"));

            // Match single characters.

            regex = NeonHelper.FileWildcardRegex("test.???");

            Assert.True(regex.IsMatch("test.abc"));

            Assert.False(regex.IsMatch("test.ab"));
            Assert.False(regex.IsMatch("test.a"));
            Assert.False(regex.IsMatch("test."));
            Assert.False(regex.IsMatch("test"));

            // Match more single characters.

            regex = NeonHelper.FileWildcardRegex("test?.???");

            Assert.True(regex.IsMatch("test1.abc"));
            Assert.True(regex.IsMatch("test2.def"));

            Assert.False(regex.IsMatch("test.ab"));
            Assert.False(regex.IsMatch("abcd.a"));

            // Match multiple characters.

            regex = NeonHelper.FileWildcardRegex("test.*");

            Assert.True(regex.IsMatch("test.a"));
            Assert.True(regex.IsMatch("test.ab"));
            Assert.True(regex.IsMatch("test.abcd"));
            Assert.True(regex.IsMatch("test.def_xxx"));
            Assert.True(regex.IsMatch("test."));

            Assert.False(regex.IsMatch("foo.ab"));
            Assert.False(regex.IsMatch("test"));

            // Match a combination

            regex = NeonHelper.FileWildcardRegex("test?.*");

            Assert.True(regex.IsMatch("test1.a"));
            Assert.True(regex.IsMatch("test1.abc"));

            Assert.False(regex.IsMatch("test.a"));
            Assert.False(regex.IsMatch("abcd1.a"));

#pragma warning restore xUnit2008
        }

        [Fact]
        public void FileWildcardRegex_Errors()
        {
            Assert.ThrowsAny<ArgumentNullException>(() => NeonHelper.FileWildcardRegex(null));
            Assert.ThrowsAny<ArgumentNullException>(() => NeonHelper.FileWildcardRegex(""));
            Assert.ThrowsAny<ArgumentNullException>(() => NeonHelper.FileWildcardRegex("     "));
            Assert.ThrowsAny<ArgumentException>(() => NeonHelper.FileWildcardRegex(new string((char)0, 1)));
            Assert.ThrowsAny<ArgumentException>(() => NeonHelper.FileWildcardRegex(new string((char)30, 1)));
            Assert.ThrowsAny<ArgumentException>(() => NeonHelper.FileWildcardRegex("/test.txt"));
            Assert.ThrowsAny<ArgumentException>(() => NeonHelper.FileWildcardRegex("\\test.txt"));
        }
    }
}
