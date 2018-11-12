//-----------------------------------------------------------------------------
// FILE:	    Test_GlobPattern.cs
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
    public class Test_GlobPattern
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Static()
        {
            var glob = GlobPattern.Create("test.jpg");

            Assert.Matches(glob.RegexPattern, "test.jpg");
            Assert.DoesNotMatch(glob.RegexPattern, "foo.bar");
            Assert.DoesNotMatch(glob.RegexPattern, "/test.jpg");
            Assert.DoesNotMatch(glob.RegexPattern, "test.jpg/foo");
            Assert.DoesNotMatch(glob.RegexPattern, "/test.jpg/");

            glob = GlobPattern.Create("/one/two/three/test.jpg");

            Assert.Matches(glob.RegexPattern, "/one/two/three/test.jpg");
            Assert.DoesNotMatch(glob.RegexPattern, "/one//test.jpg");
            Assert.DoesNotMatch(glob.RegexPattern, "/one/two/test.jpg");

            glob = GlobPattern.Create("one/two/three/test.jpg");

            Assert.Matches(glob.RegexPattern, "one/two/three/test.jpg");
            Assert.DoesNotMatch(glob.RegexPattern, "one//test.jpg");
            Assert.DoesNotMatch(glob.RegexPattern, "one/two/test.jpg");
            Assert.DoesNotMatch(glob.RegexPattern, "/one/two/three/test.jpg");

            // Ensure that reserved regex characters are escaped.

            glob = GlobPattern.Create("^$()<>[]{}\\|.+");

            Assert.Matches(glob.RegexPattern, "^$()<>[]{}\\|.+");
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Static_RegexMatch()
        {
            var glob = GlobPattern.Create("test.jpg");

            Assert.True(glob.IsMatch("test.jpg"));
            Assert.False(glob.IsMatch("foo.bar"));
            Assert.False(glob.IsMatch("/test.jpg"));
            Assert.False(glob.IsMatch("test.jpg/foo"));
            Assert.False(glob.IsMatch("/test.jpg/"));

            glob = GlobPattern.Create("/one/two/three/test.jpg");

            Assert.True(glob.IsMatch("/one/two/three/test.jpg"));
            Assert.False(glob.IsMatch("/one//test.jpg"));
            Assert.False(glob.IsMatch("/one/two/test.jpg"));

            glob = GlobPattern.Create("one/two/three/test.jpg");

            Assert.True(glob.IsMatch("one/two/three/test.jpg"));
            Assert.False(glob.IsMatch("one//test.jpg"));
            Assert.False(glob.IsMatch("one/two/test.jpg"));
            Assert.False(glob.IsMatch("/one/two/three/test.jpg"));

            // Ensure that reserved regex characters are escaped.

            glob = GlobPattern.Create("^$()<>[]{}\\|.+");

            Assert.True(glob.IsMatch("^$()<>[]{}\\|.+"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Question()
        {
            var glob = GlobPattern.Create("test.?");

            Assert.True(glob.IsMatch("test.a"));
            Assert.True(glob.IsMatch("test.b"));

            Assert.False(glob.IsMatch("test"));
            Assert.False(glob.IsMatch("test."));
            Assert.False(glob.IsMatch("test.ab"));

            glob = GlobPattern.Create("test.???");

            Assert.True(glob.IsMatch("test.abc"));
            Assert.True(glob.IsMatch("test.def"));

            Assert.False(glob.IsMatch("test."));
            Assert.False(glob.IsMatch("test.a"));
            Assert.False(glob.IsMatch("test.ab"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Asterisk()
        {
            var glob = GlobPattern.Create("test.*");

            Assert.True(glob.IsMatch("test.jpg"));
            Assert.True(glob.IsMatch("test.png"));
            Assert.True(glob.IsMatch("test.bar.json"));
            Assert.True(glob.IsMatch("test."));

            Assert.False(glob.IsMatch("test"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void DoubleAsterisk()
        {
            var glob = GlobPattern.Create("/**/test.jpg");

            Assert.True(glob.IsMatch("/test.jpg"));
            Assert.True(glob.IsMatch("/foo/test.jpg"));
            Assert.True(glob.IsMatch("/foo/bar/test.jpg"));
            Assert.True(glob.IsMatch("/foo/bar/foobar/test.jpg"));

            Assert.False(glob.IsMatch("test.jpg"));
            Assert.False(glob.IsMatch("bad.jpg"));
            Assert.False(glob.IsMatch("/bad.jpg"));
            Assert.False(glob.IsMatch("/foo/bad.jpg"));
            Assert.False(glob.IsMatch("/foo/bar/bad.jpg"));
            Assert.False(glob.IsMatch("/foo/bar/foobar/bad.jpg"));

            glob = GlobPattern.Create("**/test.jpg");

            Assert.True(glob.IsMatch("test.jpg"));
            Assert.True(glob.IsMatch("/test.jpg"));
            Assert.True(glob.IsMatch("/foo/test.jpg"));
            Assert.True(glob.IsMatch("/foo/bar/test.jpg"));
            Assert.True(glob.IsMatch("/foo/bar/foobar/test.jpg"));
            Assert.False(glob.IsMatch("bad.jpg"));
            Assert.False(glob.IsMatch("/bad.jpg"));
            Assert.False(glob.IsMatch("/foo/bad.jpg"));
            Assert.False(glob.IsMatch("/foo/bar/bad.jpg"));
            Assert.False(glob.IsMatch("/foo/bar/foobar/bad.jpg"));

            glob = GlobPattern.Create("/foo/**");

            Assert.True(glob.IsMatch("/foo"));
            Assert.True(glob.IsMatch("/foo/test.jpg"));
            Assert.True(glob.IsMatch("/foo/bar/test.jpg"));
            Assert.False(glob.IsMatch("/test.jpg"));
            Assert.False(glob.IsMatch("/bar/test.jpg"));
            Assert.False(glob.IsMatch("/bar/foo/test.jpg"));

            glob = GlobPattern.Create("foo/**");

            Assert.True(glob.IsMatch("foo"));
            Assert.True(glob.IsMatch("foo/test.jpg"));
            Assert.True(glob.IsMatch("foo/bar/test.jpg"));
            Assert.False(glob.IsMatch("test.jpg"));
            Assert.False(glob.IsMatch("bar/test.jpg"));
            Assert.False(glob.IsMatch("bar/foo/test.jpg"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Errors()
        {
            Assert.Throws<ArgumentNullException>(() => GlobPattern.Create(null));
            Assert.Throws<ArgumentNullException>(() => GlobPattern.Create(string.Empty));
            Assert.Throws<FormatException>(() => GlobPattern.Create("//test.jpg"));
            Assert.Throws<FormatException>(() => GlobPattern.Create("/test/**xx/test.jpg"));
        }
    }
}