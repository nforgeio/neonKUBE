//-----------------------------------------------------------------------------
// FILE:	    Test_GlobPattern.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
    [Trait(TestTrait.Category, TestArea.NeonCommon)]
    public class Test_GlobPattern
    {
        [Fact]
        public void Static()
        {
            var glob = GlobPattern.Parse("test.jpg");

            Assert.Matches(glob.RegexPattern, "test.jpg");
            Assert.DoesNotMatch(glob.RegexPattern, "foo.bar");
            Assert.DoesNotMatch(glob.RegexPattern, "/test.jpg");
            Assert.DoesNotMatch(glob.RegexPattern, "test.jpg/foo");
            Assert.DoesNotMatch(glob.RegexPattern, "/test.jpg/");

            glob = GlobPattern.Parse("/one/two/three/test.jpg");

            Assert.Matches(glob.RegexPattern, "/one/two/three/test.jpg");
            Assert.DoesNotMatch(glob.RegexPattern, "/one//test.jpg");
            Assert.DoesNotMatch(glob.RegexPattern, "/one/two/test.jpg");

            glob = GlobPattern.Parse("one/two/three/test.jpg");

            Assert.Matches(glob.RegexPattern, "one/two/three/test.jpg");
            Assert.DoesNotMatch(glob.RegexPattern, "one//test.jpg");
            Assert.DoesNotMatch(glob.RegexPattern, "one/two/test.jpg");
            Assert.DoesNotMatch(glob.RegexPattern, "/one/two/three/test.jpg");

            // Ensure that reserved regex characters are escaped.

            glob = GlobPattern.Parse("^$()<>[]{}\\|.+");

            Assert.Matches(glob.RegexPattern, "^$()<>[]{}\\|.+");
        }

        [Fact]
        public void Static_RegexMatch()
        {
            var glob = GlobPattern.Parse("test.jpg");

            Assert.True(glob.IsMatch("test.jpg"));
            Assert.False(glob.IsMatch("foo.bar"));
            Assert.False(glob.IsMatch("/test.jpg"));
            Assert.False(glob.IsMatch("test.jpg/foo"));
            Assert.False(glob.IsMatch("/test.jpg/"));

            glob = GlobPattern.Parse("/one/two/three/test.jpg");

            Assert.True(glob.IsMatch("/one/two/three/test.jpg"));
            Assert.False(glob.IsMatch("/one//test.jpg"));
            Assert.False(glob.IsMatch("/one/two/test.jpg"));

            glob = GlobPattern.Parse("one/two/three/test.jpg");

            Assert.True(glob.IsMatch("one/two/three/test.jpg"));
            Assert.False(glob.IsMatch("one//test.jpg"));
            Assert.False(glob.IsMatch("one/two/test.jpg"));
            Assert.False(glob.IsMatch("/one/two/three/test.jpg"));

            // Ensure that reserved regex characters are escaped.

            glob = GlobPattern.Parse("^$()<>[]{}\\|.+");

            Assert.True(glob.IsMatch("^$()<>[]{}\\|.+"));
        }

        [Fact]
        public void Asterisk()
        {
            var glob = GlobPattern.Parse("test.*");

            Assert.True(glob.IsMatch("test.jpg"));
            Assert.True(glob.IsMatch("test.png"));
            Assert.True(glob.IsMatch("test.bar.json"));
            Assert.True(glob.IsMatch("test."));

            Assert.False(glob.IsMatch("test"));
        }

        [Fact]
        public void DoubleAsterisk()
        {
            var glob = GlobPattern.Parse("/**/test.jpg");

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

            glob = GlobPattern.Parse("**/test.jpg");

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

            glob = GlobPattern.Parse("/foo/**");

            Assert.True(glob.IsMatch("/foo"));
            Assert.True(glob.IsMatch("/foo/test.jpg"));
            Assert.True(glob.IsMatch("/foo/bar/test.jpg"));
            Assert.False(glob.IsMatch("/test.jpg"));
            Assert.False(glob.IsMatch("/bar/test.jpg"));
            Assert.False(glob.IsMatch("/bar/foo/test.jpg"));

            glob = GlobPattern.Parse("foo/**");

            Assert.True(glob.IsMatch("foo"));
            Assert.True(glob.IsMatch("foo/test.jpg"));
            Assert.True(glob.IsMatch("foo/bar/test.jpg"));
            Assert.False(glob.IsMatch("test.jpg"));
            Assert.False(glob.IsMatch("bar/test.jpg"));
            Assert.False(glob.IsMatch("bar/foo/test.jpg"));
        }

        [Fact]
        public void Backslash()
        {
            var glob = GlobPattern.Parse("/**/test.jpg");

            Assert.True(glob.IsMatch(@"\test.jpg"));
            Assert.True(glob.IsMatch(@"\foo\test.jpg"));
            Assert.True(glob.IsMatch(@"\foo\bar\test.jpg"));
            Assert.True(glob.IsMatch(@"\foo\bar\foobar\test.jpg"));

            Assert.False(glob.IsMatch("test.jpg"));
            Assert.False(glob.IsMatch("bad.jpg"));
            Assert.False(glob.IsMatch(@"\bad.jpg"));
            Assert.False(glob.IsMatch(@"\foo\bad.jpg"));
            Assert.False(glob.IsMatch(@"\foo\bar\bad.jpg"));
            Assert.False(glob.IsMatch(@"\foo\bar\foobar\bad.jpg"));

            glob = GlobPattern.Parse(@"**/test.jpg");

            Assert.True(glob.IsMatch("test.jpg"));
            Assert.True(glob.IsMatch(@"\test.jpg"));
            Assert.True(glob.IsMatch(@"\foo\test.jpg"));
            Assert.True(glob.IsMatch(@"\foo\bar\test.jpg"));
            Assert.True(glob.IsMatch(@"\foo\bar\foobar\test.jpg"));
            Assert.False(glob.IsMatch(@"bad.jpg"));
            Assert.False(glob.IsMatch(@"\bad.jpg"));
            Assert.False(glob.IsMatch(@"\foo\bad.jpg"));
            Assert.False(glob.IsMatch(@"\foo\bar\bad.jpg"));
            Assert.False(glob.IsMatch(@"\foo\bar\foobar\bad.jpg"));

            glob = GlobPattern.Parse(@"/foo/**");

            Assert.True(glob.IsMatch(@"\foo"));
            Assert.True(glob.IsMatch(@"\foo\test.jpg"));
            Assert.True(glob.IsMatch(@"\foo\bar\test.jpg"));
            Assert.False(glob.IsMatch(@"\test.jpg"));
            Assert.False(glob.IsMatch(@"\bar\test.jpg"));
            Assert.False(glob.IsMatch(@"\bar\foo\test.jpg"));

            glob = GlobPattern.Parse(@"foo/**");

            Assert.True(glob.IsMatch(@"foo"));
            Assert.True(glob.IsMatch(@"foo\test.jpg"));
            Assert.True(glob.IsMatch(@"foo\bar\test.jpg"));
            Assert.False(glob.IsMatch(@"test.jpg"));
            Assert.False(glob.IsMatch(@"bar\test.jpg"));
            Assert.False(glob.IsMatch(@"bar\foo\test.jpg"));
        }

        [Fact]
        public void Errors()
        {
            Assert.Throws<ArgumentNullException>(() => GlobPattern.Parse(null));
            Assert.Throws<ArgumentNullException>(() => GlobPattern.Parse(string.Empty));
            Assert.Throws<FormatException>(() => GlobPattern.Parse("//test.jpg"));
            Assert.Throws<FormatException>(() => GlobPattern.Parse("/test/**xx/test.jpg"));
        }

        [Fact]
        public void TryCreate()
        {
            GlobPattern glob;

            Assert.True(GlobPattern.TryParse("foo/**", out glob));

            Assert.True(glob.IsMatch("foo"));
            Assert.True(glob.IsMatch("foo/test.jpg"));
            Assert.True(glob.IsMatch("foo/bar/test.jpg"));
            Assert.False(glob.IsMatch("test.jpg"));
            Assert.False(glob.IsMatch("bar/test.jpg"));
            Assert.False(glob.IsMatch("bar/foo/test.jpg"));

            Assert.False(GlobPattern.TryParse(null, out glob));
            Assert.False(GlobPattern.TryParse(string.Empty, out glob));
            Assert.False(GlobPattern.TryParse("//test.jpg", out glob));
            Assert.False(GlobPattern.TryParse("/test/**xx/test.jpg", out glob));
        }

        [Fact]
        public void Escapes()
        {
            Assert.Equal("^\\?$", GlobPattern.Parse("?").RegexPattern);
            Assert.Equal("^\\^$", GlobPattern.Parse("^").RegexPattern);
            Assert.Equal("^\\$$", GlobPattern.Parse("$").RegexPattern);
            Assert.Equal("^\\|$", GlobPattern.Parse("|").RegexPattern);
            Assert.Equal("^\\+$", GlobPattern.Parse("+").RegexPattern);
            Assert.Equal("^\\($", GlobPattern.Parse("(").RegexPattern);
            Assert.Equal("^\\)$", GlobPattern.Parse(")").RegexPattern);
            Assert.Equal("^\\.$", GlobPattern.Parse(".").RegexPattern);
        }

        [Fact]
        public void CaseInsensitive()
        {
            var glob = GlobPattern.Parse("test.jpg", caseInsensitive: true);

            Assert.True(glob.IsMatch("test.jpg"));
            Assert.True(glob.IsMatch("TEST.JPG"));
        }

        [Fact]
        public void CaseSensitive()
        {
            var glob = GlobPattern.Parse("test.jpg", caseInsensitive: false);

            Assert.True(glob.IsMatch("test.jpg"));
            Assert.False(glob.IsMatch("TEST.JPG"));
        }
    }
}
