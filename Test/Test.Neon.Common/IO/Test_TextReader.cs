//-----------------------------------------------------------------------------
// FILE:	    Test_TextReader.cs
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
    public class Test_TextReader
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Lines_Empty()
        {
            var lines = new List<string>();

            using (var reader = new StringReader(string.Empty))
            {
                foreach (var line in reader.Lines())
                {
                    lines.Add(line);
                }
            }

            Assert.Empty(lines);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Lines_OneEmpty()
        {
            var lines = new List<string>();

            using (var reader = new StringReader("\r\n"))
            {
                foreach (var line in reader.Lines())
                {
                    lines.Add(line);
                }
            }

            Assert.Single(lines);
            Assert.Equal(string.Empty, lines[0]);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Lines_Multiple()
        {
            var lines = new List<string>();

            using (var reader = new StringReader(
@"this
is
a
test

done
"))
            {
                foreach (var line in reader.Lines())
                {
                    lines.Add(line);
                }
            }

            Assert.Equal(6, lines.Count);
            Assert.Equal("this", lines[0]);
            Assert.Equal("is", lines[1]);
            Assert.Equal("a", lines[2]);
            Assert.Equal("test", lines[3]);
            Assert.Equal(string.Empty, lines[4]);
            Assert.Equal("done", lines[5]);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Lines_MultipleIgnoreBlank()
        {
            var lines = new List<string>();

            using (var reader = new StringReader(
$@"this
is
a
test

{"\t"}   {"\t"}
done
"))
            {
                foreach (var line in reader.Lines(ignoreBlank: true))
                {
                    lines.Add(line);
                }
            }

            Assert.Equal(5, lines.Count);
            Assert.Equal("this", lines[0]);
            Assert.Equal("is", lines[1]);
            Assert.Equal("a", lines[2]);
            Assert.Equal("test", lines[3]);
            Assert.Equal("done", lines[4]);
        }
    }
}
