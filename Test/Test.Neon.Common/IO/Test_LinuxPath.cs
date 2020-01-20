//-----------------------------------------------------------------------------
// FILE:	    Test_LinuxPath.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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

using Neon.Common;
using Neon.IO;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public class Test_LinuxPath
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void ChangeExtension()
        {
            Assert.Equal("test.one", LinuxPath.ChangeExtension("test", "one"));
            Assert.Equal("test.one", LinuxPath.ChangeExtension("test.zero", "one"));
            Assert.Equal("/foo/test.one", LinuxPath.ChangeExtension("\\foo\\test", "one"));
            Assert.Equal("/foo/test.one", LinuxPath.ChangeExtension("\\foo\\test.zero", "one"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Combine()
        {
            Assert.Equal("/one/two/three.txt", LinuxPath.Combine("/one", "two", "three.txt"));
            Assert.Equal("/one/two/three.txt", LinuxPath.Combine("\\one", "two", "three.txt"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void GetDirectoryName()
        {
            Assert.Equal("/one/two", LinuxPath.GetDirectoryName("\\one\\two\\three.txt"));
            Assert.Equal("/one/two", LinuxPath.GetDirectoryName("/one/two/three.txt"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void GetExtension()
        {
            Assert.Equal(".txt", LinuxPath.GetExtension("\\one\\two\\three.txt"));
            Assert.Equal(".txt", LinuxPath.GetExtension("/one/two/three.txt"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void GetFileName()
        {
            Assert.Equal("three.txt", LinuxPath.GetFileName("\\one\\two\\three.txt"));
            Assert.Equal("three.txt", LinuxPath.GetFileName("/one/two/three.txt"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void GetFileNameWithoutExtension()
        {
            Assert.Equal("three", LinuxPath.GetFileNameWithoutExtension("\\one\\two\\three.txt"));
            Assert.Equal("three", LinuxPath.GetFileNameWithoutExtension("/one/two/three.txt"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void HasExtension()
        {
            Assert.True(LinuxPath.HasExtension("\\one\\two\\three.txt"));
            Assert.True(LinuxPath.HasExtension("/one/two/three.txt"));

            Assert.False(LinuxPath.HasExtension("\\one\\two\\three"));
            Assert.False(LinuxPath.HasExtension("/one/two/three"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void IsPathRooted()
        {
            Assert.True(LinuxPath.IsPathRooted("\\one\\two\\three.txt"));
            Assert.True(LinuxPath.IsPathRooted("/one/two/three.txt"));

            Assert.False(LinuxPath.IsPathRooted("one\\two\\three"));
            Assert.False(LinuxPath.IsPathRooted("one/two/three"));
        }
    }
}
