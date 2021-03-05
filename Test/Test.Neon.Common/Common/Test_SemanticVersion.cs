//-----------------------------------------------------------------------------
// FILE:	    Test_SemanticVersion.cs
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
using System.Threading.Tasks;

using Neon.Common;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_SemanticVersion
    {
        [Fact]
        public void ParseErrors()
        {
            Assert.Throws<ArgumentNullException>(() => SemanticVersion.Parse(null));
            Assert.Throws<ArgumentNullException>(() => SemanticVersion.Parse(string.Empty));
            Assert.Throws<FormatException>(() => SemanticVersion.Parse("x"));
            Assert.Throws<FormatException>(() => SemanticVersion.Parse("-1"));
            Assert.Throws<FormatException>(() => SemanticVersion.Parse("."));
            Assert.Throws<FormatException>(() => SemanticVersion.Parse(".."));
            Assert.Throws<FormatException>(() => SemanticVersion.Parse("0."));
            Assert.Throws<FormatException>(() => SemanticVersion.Parse("0.1.2.3"));
            Assert.Throws<FormatException>(() => SemanticVersion.Parse("1.2.3-"));
            Assert.Throws<FormatException>(() => SemanticVersion.Parse("1.2.3-alpha."));
            Assert.Throws<FormatException>(() => SemanticVersion.Parse("1.2.3-alpha.01"));
            Assert.Throws<FormatException>(() => SemanticVersion.Parse("1.2.3+"));
            Assert.Throws<FormatException>(() => SemanticVersion.Parse("1.2.3-alpha+"));
        }

        [Fact]
        public void TryParseErrors()
        {
            SemanticVersion v;

            Assert.False(SemanticVersion.TryParse(null, out v));
            Assert.False(SemanticVersion.TryParse(string.Empty, out v));
            Assert.False(SemanticVersion.TryParse("x", out v));
            Assert.False(SemanticVersion.TryParse("-1", out v));
            Assert.False(SemanticVersion.TryParse(".", out v));
            Assert.False(SemanticVersion.TryParse("..", out v));
            Assert.False(SemanticVersion.TryParse("0.", out v));
            Assert.False(SemanticVersion.TryParse("0.1.2.3", out v));
            Assert.False(SemanticVersion.TryParse("1.2.3-", out v));
            Assert.False(SemanticVersion.TryParse("1.2.3-alpha.", out v));
            Assert.False(SemanticVersion.TryParse("1.2.3-alpha.01", out v));
            Assert.False(SemanticVersion.TryParse("1.2.3+", out v));
            Assert.False(SemanticVersion.TryParse("1.2.3-alpha+", out v));
        }

        [Fact]
        public void Parse()
        {
            var v = SemanticVersion.Parse("1");

            Assert.Equal(1, v.Major);
            Assert.Equal(0, v.Minor);
            Assert.Equal(0, v.Patch);
            Assert.Null(v.Prerelease);
            Assert.Null(v.Build);

            v = SemanticVersion.Parse("1.2");

            Assert.Equal(1, v.Major);
            Assert.Equal(2, v.Minor);
            Assert.Equal(0, v.Patch);
            Assert.Null(v.Prerelease);
            Assert.Null(v.Build);

            v = SemanticVersion.Parse("1.2.3");

            Assert.Equal(1, v.Major);
            Assert.Equal(2, v.Minor);
            Assert.Equal(3, v.Patch);
            Assert.Null(v.Prerelease);
            Assert.Null(v.Build);

            v = SemanticVersion.Parse("11.22.33");

            Assert.Equal(11, v.Major);
            Assert.Equal(22, v.Minor);
            Assert.Equal(33, v.Patch);
            Assert.Null(v.Prerelease);
            Assert.Null(v.Build);

            v = SemanticVersion.Parse("1.2.3-alpha");

            Assert.Equal(1, v.Major);
            Assert.Equal(2, v.Minor);
            Assert.Equal(3, v.Patch);
            Assert.Equal("alpha", v.Prerelease);
            Assert.Null(v.Build);

            v = SemanticVersion.Parse("1.2.3-alpha-0");

            Assert.Equal(1, v.Major);
            Assert.Equal(2, v.Minor);
            Assert.Equal(3, v.Patch);
            Assert.Equal("alpha-0", v.Prerelease);
            Assert.Null(v.Build);

            v = SemanticVersion.Parse("1.2.3-alpha.0.1");

            Assert.Equal(1, v.Major);
            Assert.Equal(2, v.Minor);
            Assert.Equal(3, v.Patch);
            Assert.Equal("alpha.0.1", v.Prerelease);
            Assert.Null(v.Build);

            v = SemanticVersion.Parse("1.2.3+build-27");

            Assert.Equal(1, v.Major);
            Assert.Equal(2, v.Minor);
            Assert.Equal(3, v.Patch);
            Assert.Null(v.Prerelease);
            Assert.Equal("build-27", v.Build);

            v = SemanticVersion.Parse("1.2.3-alpha.0.1+build-27");

            Assert.Equal(1, v.Major);
            Assert.Equal(2, v.Minor);
            Assert.Equal(3, v.Patch);
            Assert.Equal("alpha.0.1", v.Prerelease);
            Assert.Equal("build-27", v.Build);
        }

        [Fact]
        public void CompareNull()
        {
            Assert.True((SemanticVersion)null == (SemanticVersion)null);
            Assert.False((SemanticVersion)null != (SemanticVersion)null);

            Assert.False((SemanticVersion)"1.2" == (SemanticVersion)null);
            Assert.True((SemanticVersion)"1.2" != (SemanticVersion)null);
            Assert.True((SemanticVersion)"1.2" > (SemanticVersion)null);
            Assert.True((SemanticVersion)"1.2" >= (SemanticVersion)null);
            Assert.False((SemanticVersion)"1.2" < (SemanticVersion)null);
            Assert.False((SemanticVersion)"1.2" <= (SemanticVersion)null);

            Assert.False((SemanticVersion)null == (SemanticVersion)"1.2");
            Assert.True((SemanticVersion)null != (SemanticVersion)"1.2");
            Assert.False((SemanticVersion)null > (SemanticVersion)"1.2");
            Assert.False((SemanticVersion)null >= (SemanticVersion)"1.2");
            Assert.True((SemanticVersion)null < (SemanticVersion)"1.2");
            Assert.True((SemanticVersion)null <= (SemanticVersion)"1.2");
        }

        [Fact]
        public void CompareSimpleSame()
        {
            var v1 = SemanticVersion.Parse("1");
            var v2 = SemanticVersion.Parse("1");

            Assert.True(v1 == v2);
            Assert.True(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.False(v1 != v2);
            Assert.False(v1 < v2);
            Assert.False(v2 > v1);

            v1 = SemanticVersion.Parse("1.2");
            v2 = SemanticVersion.Parse("1.2");

            Assert.True(v1 == v2);
            Assert.True(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.False(v1 != v2);
            Assert.False(v1 < v2);
            Assert.False(v2 > v1);

            v1 = SemanticVersion.Parse("1.2.3");
            v2 = SemanticVersion.Parse("1.2.3");

            Assert.True(v1 == v2);
            Assert.True(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.False(v1 != v2);
            Assert.False(v1 < v2);
            Assert.False(v2 > v1);

            v1 = SemanticVersion.Parse("11.22.33");
            v2 = SemanticVersion.Parse("11.22.33");

            Assert.True(v1 == v2);
            Assert.True(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.False(v1 != v2);
            Assert.False(v1 < v2);
            Assert.False(v2 > v1);
        }

        [Fact]
        public void CompareSimpleDifferent()
        {
            var v1 = SemanticVersion.Parse("1");
            var v2 = SemanticVersion.Parse("2");

            Assert.False(v1 == v2);
            Assert.False(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.True(v1 != v2);
            Assert.True(v1 < v2);
            Assert.True(v2 > v1);

            v1 = SemanticVersion.Parse("1");
            v2 = SemanticVersion.Parse("1.2");

            Assert.False(v1 == v2);
            Assert.False(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.True(v1 != v2);
            Assert.True(v1 < v2);
            Assert.True(v2 > v1);

            v1 = SemanticVersion.Parse("1");
            v2 = SemanticVersion.Parse("1.2.3");

            Assert.False(v1 == v2);
            Assert.False(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.True(v1 != v2);
            Assert.True(v1 < v2);
            Assert.True(v2 > v1);

            v1 = SemanticVersion.Parse("1.2");
            v2 = SemanticVersion.Parse("1.2.3");

            Assert.False(v1 == v2);
            Assert.False(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.True(v1 != v2);
            Assert.True(v1 < v2);
            Assert.True(v2 > v1);

            v1 = SemanticVersion.Parse("1.2");
            v2 = SemanticVersion.Parse("2");

            Assert.False(v1 == v2);
            Assert.False(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.True(v1 != v2);
            Assert.True(v1 < v2);
            Assert.True(v2 > v1);
        }

        [Fact]
        public void CompareSimpleBuildSame()
        {
            var v1 = SemanticVersion.Parse("1+build-0000");
            var v2 = SemanticVersion.Parse("1+build-0000");

            Assert.True(v1 == v2);
            Assert.True(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.False(v1 != v2);
            Assert.False(v1 < v2);
            Assert.False(v2 > v1);

            v1 = SemanticVersion.Parse("1+build-0000");
            v2 = SemanticVersion.Parse("1+build-0001");

            Assert.True(v1 == v2);
            Assert.False(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.False(v1 != v2);
            Assert.False(v1 < v2);
            Assert.False(v2 > v1);

            v1 = SemanticVersion.Parse("1.2+build-0000");
            v2 = SemanticVersion.Parse("1.2");

            Assert.True(v1 == v2);
            Assert.False(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.False(v1 != v2);
            Assert.False(v1 < v2);
            Assert.False(v2 > v1);

            v1 = SemanticVersion.Parse("1.2.3+build-0000");
            v2 = SemanticVersion.Parse("1.2.3+build-0001");

            Assert.True(v1 == v2);
            Assert.False(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.False(v1 != v2);
            Assert.False(v1 < v2);
            Assert.False(v2 > v1);

            v1 = SemanticVersion.Parse("11.22.33+build-0000");
            v2 = SemanticVersion.Parse("11.22.33+build-0001");

            Assert.True(v1 == v2);
            Assert.False(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.False(v1 != v2);
            Assert.False(v1 < v2);
            Assert.False(v2 > v1);
        }

        [Fact]
        public void CompareSimpleBuildDifferent()
        {
            var v1 = SemanticVersion.Parse("1+build-0000");
            var v2 = SemanticVersion.Parse("2+build-0000");

            Assert.False(v1 == v2);
            Assert.False(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.True(v1 != v2);
            Assert.True(v1 < v2);
            Assert.True(v2 > v1);

            v1 = SemanticVersion.Parse("1+build-0000");
            v2 = SemanticVersion.Parse("1.2+build-0000");

            Assert.False(v1 == v2);
            Assert.False(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.True(v1 != v2);
            Assert.True(v1 < v2);
            Assert.True(v2 > v1);

            v1 = SemanticVersion.Parse("1+build-0000");
            v2 = SemanticVersion.Parse("1.2.3+build-0000");

            Assert.False(v1 == v2);
            Assert.False(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.True(v1 != v2);
            Assert.True(v1 < v2);
            Assert.True(v2 > v1);

            v1 = SemanticVersion.Parse("1.2+build-0000");
            v2 = SemanticVersion.Parse("1.2.3+build-0000");

            Assert.False(v1 == v2);
            Assert.False(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.True(v1 != v2);
            Assert.True(v1 < v2);
            Assert.True(v2 > v1);

            v1 = SemanticVersion.Parse("1.2+build-0000");
            v2 = SemanticVersion.Parse("2+build-0000");

            Assert.False(v1 == v2);
            Assert.False(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.True(v1 != v2);
            Assert.True(v1 < v2);
            Assert.True(v2 > v1);
        }

        [Fact]
        public void ComparePrereleaseSame()
        {
            var v1 = SemanticVersion.Parse("1-alpha");
            var v2 = SemanticVersion.Parse("1-alpha");

            Assert.True(v1 == v2);
            Assert.True(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.False(v1 != v2);
            Assert.False(v1 < v2);
            Assert.False(v2 > v1);

            v1 = SemanticVersion.Parse("1.2-alpha-1");
            v2 = SemanticVersion.Parse("1.2-alpha-1");

            Assert.True(v1 == v2);
            Assert.True(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.False(v1 != v2);
            Assert.False(v1 < v2);
            Assert.False(v2 > v1);

            v1 = SemanticVersion.Parse("1.2.3-alpha.0");
            v2 = SemanticVersion.Parse("1.2.3-alpha.0");

            Assert.True(v1 == v2);
            Assert.True(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.False(v1 != v2);
            Assert.False(v1 < v2);
            Assert.False(v2 > v1);

            v1 = SemanticVersion.Parse("11.22.33-alpha.1.2.3.4");
            v2 = SemanticVersion.Parse("11.22.33-alpha.1.2.3.4");

            Assert.True(v1 == v2);
            Assert.True(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.False(v1 != v2);
            Assert.False(v1 < v2);
            Assert.False(v2 > v1);

            v1 = SemanticVersion.Parse("11.22.33-alpha.1.2.3.4");
            v2 = SemanticVersion.Parse("11.22.33-ALPHA.1.2.3.4");

            Assert.True(v1 == v2);
            Assert.True(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.False(v1 != v2);
            Assert.False(v1 < v2);
            Assert.False(v2 > v1);
        }

        [Fact]
        public void ComparePrereleaseDifferent()
        {
            var v1 = SemanticVersion.Parse("1-alpha");
            var v2 = SemanticVersion.Parse("2-beta");

            Assert.False(v1 == v2);
            Assert.False(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.True(v1 != v2);
            Assert.True(v1 < v2);
            Assert.True(v2 > v1);

            v1 = SemanticVersion.Parse("1-alpha0");
            v2 = SemanticVersion.Parse("1-alpha1");

            Assert.False(v1 == v2);
            Assert.False(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.True(v1 != v2);
            Assert.True(v1 < v2);
            Assert.True(v2 > v1);

            v1 = SemanticVersion.Parse("1-alpha-0");
            v2 = SemanticVersion.Parse("1-alpha-1");

            Assert.False(v1 == v2);
            Assert.False(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.True(v1 != v2);
            Assert.True(v1 < v2);
            Assert.True(v2 > v1);

            v1 = SemanticVersion.Parse("1-alpha");
            v2 = SemanticVersion.Parse("1-alpha.0");

            Assert.False(v1 == v2);
            Assert.False(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.True(v1 != v2);
            Assert.True(v1 < v2);
            Assert.True(v2 > v1);

            v1 = SemanticVersion.Parse("1-alpha.0");
            v2 = SemanticVersion.Parse("2-alpha.1");

            Assert.False(v1 == v2);
            Assert.False(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.True(v1 != v2);
            Assert.True(v1 < v2);
            Assert.True(v2 > v1);
        }

        [Fact]
        public void ComparePrereleaseBuildSame()
        {
            var v1 = SemanticVersion.Parse("1-alpha+build-0000");
            var v2 = SemanticVersion.Parse("1-alpha+build-0000");

            Assert.True(v1 == v2);
            Assert.True(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.False(v1 != v2);
            Assert.False(v1 < v2);
            Assert.False(v2 > v1);

            v1 = SemanticVersion.Parse("1.2-alpha-1+build-0000");
            v2 = SemanticVersion.Parse("1.2-alpha-1+build-0000");

            Assert.True(v1 == v2);
            Assert.True(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.False(v1 != v2);
            Assert.False(v1 < v2);
            Assert.False(v2 > v1);

            v1 = SemanticVersion.Parse("1.2.3-alpha.0+build-0000");
            v2 = SemanticVersion.Parse("1.2.3-alpha.0+build-0000");

            Assert.True(v1 == v2);
            Assert.True(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.False(v1 != v2);
            Assert.False(v1 < v2);
            Assert.False(v2 > v1);

            v1 = SemanticVersion.Parse("11.22.33-alpha.1.2.3.4+build-0000");
            v2 = SemanticVersion.Parse("11.22.33-alpha.1.2.3.4+build-0000");

            Assert.True(v1 == v2);
            Assert.True(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.False(v1 != v2);
            Assert.False(v1 < v2);
            Assert.False(v2 > v1);

            v1 = SemanticVersion.Parse("11.22.33-alpha.1.2.3.4+build-0001");
            v2 = SemanticVersion.Parse("11.22.33-alpha.1.2.3.4+build-0000");

            Assert.True(v1 == v2);
            Assert.False(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.False(v1 != v2);
            Assert.False(v1 < v2);
            Assert.False(v2 > v1);

            v1 = SemanticVersion.Parse("11.22.33-alpha.1.2.3.4+build-0001");
            v2 = SemanticVersion.Parse("11.22.33-alpha.1.2.3.4+BUILD-0000");

            Assert.True(v1 == v2);
            Assert.False(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.False(v1 != v2);
            Assert.False(v1 < v2);
            Assert.False(v2 > v1);
        }

        [Fact]
        public void ComparePrereleaseBuildDifferent()
        {
            var v1 = SemanticVersion.Parse("1-alpha+build-0000");
            var v2 = SemanticVersion.Parse("2-beta+build-0001");

            Assert.False(v1 == v2);
            Assert.False(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.True(v1 != v2);
            Assert.True(v1 < v2);
            Assert.True(v2 > v1);

            v1 = SemanticVersion.Parse("1-alpha0+build-0000");
            v2 = SemanticVersion.Parse("1-alpha1+build-0001");

            Assert.False(v1 == v2);
            Assert.False(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.True(v1 != v2);
            Assert.True(v1 < v2);
            Assert.True(v2 > v1);

            v1 = SemanticVersion.Parse("1-alpha-0+build-0001");
            v2 = SemanticVersion.Parse("1-alpha-1+build-0000");

            Assert.False(v1 == v2);
            Assert.False(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.True(v1 != v2);
            Assert.True(v1 < v2);
            Assert.True(v2 > v1);

            v1 = SemanticVersion.Parse("1-alpha+build-0001");
            v2 = SemanticVersion.Parse("1-alpha.0+build-0000");

            Assert.False(v1 == v2);
            Assert.False(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.True(v1 != v2);
            Assert.True(v1 < v2);
            Assert.True(v2 > v1);

            v1 = SemanticVersion.Parse("1-alpha.0+build-0001");
            v2 = SemanticVersion.Parse("2-alpha.1+build-0000");

            Assert.False(v1 == v2);
            Assert.False(v1.Equals(v2));
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.True(v1 != v2);
            Assert.True(v1 < v2);
            Assert.True(v2 > v1);
        }

        [Fact]
        public void Cast()
        {
            Assert.Null((string)(SemanticVersion)null);
            Assert.Null((SemanticVersion)(string)null);

            Assert.Equal("1.2.3-alpha+build", (string)SemanticVersion.Parse("1.2.3-alpha+build"));
            Assert.Equal(SemanticVersion.Parse("1.2.3-alpha+build"), (SemanticVersion)"1.2.3-alpha+build");
        }

        [Fact]
        public void RoundTrip()
        {
            Assert.Equal("1", (string)SemanticVersion.Parse("1"));
            Assert.Equal("1-alpha", (string)SemanticVersion.Parse("1-alpha"));
            Assert.Equal("1+build", (string)SemanticVersion.Parse("1+build"));
            Assert.Equal("1-alpha+build", (string)SemanticVersion.Parse("1-alpha+build"));

            Assert.Equal("1.2", (string)SemanticVersion.Parse("1.2"));
            Assert.Equal("1.02", (string)SemanticVersion.Parse("1.02"));
            Assert.Equal("1.2-alpha", (string)SemanticVersion.Parse("1.2-alpha"));
            Assert.Equal("1.02-alpha", (string)SemanticVersion.Parse("1.02-alpha"));
            Assert.Equal("1.2+build", (string)SemanticVersion.Parse("1.2+build"));
            Assert.Equal("1.02+build", (string)SemanticVersion.Parse("1.02+build"));
            Assert.Equal("1.2-alpha+build", (string)SemanticVersion.Parse("1.2-alpha+build"));
            Assert.Equal("1.02-alpha+build", (string)SemanticVersion.Parse("1.02-alpha+build"));

            Assert.Equal("1.2.3", (string)SemanticVersion.Parse("1.2.3"));
            Assert.Equal("1.02.03", (string)SemanticVersion.Parse("1.02.03"));
            Assert.Equal("1.2.3-alpha", (string)SemanticVersion.Parse("1.2.3-alpha"));
            Assert.Equal("1.02.03-alpha", (string)SemanticVersion.Parse("1.02.03-alpha"));
            Assert.Equal("1.2.3+build", (string)SemanticVersion.Parse("1.2.3+build"));
            Assert.Equal("1.02.03+build", (string)SemanticVersion.Parse("1.02.03+build"));
            Assert.Equal("1.2.3-alpha+build", (string)SemanticVersion.Parse("1.2.3-alpha+build"));
            Assert.Equal("1.02.03-alpha+build", (string)SemanticVersion.Parse("1.02.03-alpha+build"));
        }

        [Fact]
        public void Comparable()
        {
            // Test IComparaible by sorting a list of versions.

            var list = new List<SemanticVersion>()
            {
                (SemanticVersion)"1.0.0",
                (SemanticVersion)"2.0.0",
                (SemanticVersion)"10.0.0",
                (SemanticVersion)"3.0.0",
                (SemanticVersion)"4.0.0",
                (SemanticVersion)"5.0.0",
                (SemanticVersion)"9.0.0",
                (SemanticVersion)"8.0.0",
                (SemanticVersion)"7.0.0",
                (SemanticVersion)"6.0.0"
            };

            list = list.OrderBy(item => item).ToList();

            Assert.Equal(10, list.Count);

            for (int i = 0; i < list.Count; i++)
            {
                Assert.Equal($"{i + 1}.0.0", (string)list[i]);
            }
        }
    }
}