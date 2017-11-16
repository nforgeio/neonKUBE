//-----------------------------------------------------------------------------
// FILE:	    Test_TextReader.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;

using Xunit;

namespace TestCommon
{
    public class Test_TextReader
    {
        [Fact]
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
    }
}
