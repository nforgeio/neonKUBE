//-----------------------------------------------------------------------------
// FILE:	    Test_SemanticVersion.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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
    }
}