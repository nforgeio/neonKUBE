//-----------------------------------------------------------------------------
// FILE:	    Test_Helper.cs
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
    public partial class Test_Helper
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void ParseCsv()
        {
            string[] fields;

            fields = NeonHelper.ParseCsv("");
            Assert.Equal<string>(new string[] { "" }, fields);

            fields = NeonHelper.ParseCsv("1");
            Assert.Equal<string>(new string[] { "1" }, fields);

            fields = NeonHelper.ParseCsv("1,2,3,4");
            Assert.Equal<string>(new string[] { "1", "2", "3", "4" }, fields);

            fields = NeonHelper.ParseCsv("abc,def");
            Assert.Equal<string>(new string[] { "abc", "def" }, fields);

            fields = NeonHelper.ParseCsv("abc,def,");
            Assert.Equal<string>(new string[] { "abc", "def", "" }, fields);

            fields = NeonHelper.ParseCsv("\"\"");
            Assert.Equal<string>(new string[] { "" }, fields);

            fields = NeonHelper.ParseCsv("\"abc\"");
            Assert.Equal<string>(new string[] { "abc" }, fields);

            fields = NeonHelper.ParseCsv("\"abc,def\"");
            Assert.Equal<string>(new string[] { "abc,def" }, fields);

            fields = NeonHelper.ParseCsv("\"a,b\",\"c,d\"");
            Assert.Equal<string>(new string[] { "a,b", "c,d" }, fields);

            fields = NeonHelper.ParseCsv("\"a,b\",\"c,d\",e");
            Assert.Equal<string>(new string[] { "a,b", "c,d", "e" }, fields);

            fields = NeonHelper.ParseCsv("\"abc\r\ndef\"");
            Assert.Equal<string>(new string[] { "abc\r\ndef" }, fields);

            fields = NeonHelper.ParseCsv("0,1,,,4");
            Assert.Equal<string>(new string[] { "0", "1", "", "", "4" }, fields);

            fields = NeonHelper.ParseCsv(",,,,");
            Assert.Equal<string>(new string[] { "", "", "", "", "" }, fields);

            Assert.Throws<FormatException>(() => NeonHelper.ParseCsv("\"abc"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void DoesNotThrow()
        {
            Assert.True(NeonHelper.DoesNotThrow(() => { }));
            Assert.True(NeonHelper.DoesNotThrow<ArgumentException>(() => { }));
            Assert.True(NeonHelper.DoesNotThrow<ArgumentException>(() => { throw new FormatException(); }));

            Assert.False(NeonHelper.DoesNotThrow(() => { throw new ArgumentException(); }));
            Assert.False(NeonHelper.DoesNotThrow<ArgumentException>(() => { throw new ArgumentException(); }));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void ExpandTabs()
        {
            // Test input without line endings.

            Assert.Equal("text", NeonHelper.ExpandTabs("text"));
            Assert.Equal("    text", NeonHelper.ExpandTabs("\ttext"));
            Assert.Equal("-   text", NeonHelper.ExpandTabs("-\ttext"));
            Assert.Equal("--  text", NeonHelper.ExpandTabs("--\ttext"));
            Assert.Equal("--- text", NeonHelper.ExpandTabs("---\ttext"));
            Assert.Equal("        text", NeonHelper.ExpandTabs("\t\ttext"));
            Assert.Equal("-       text", NeonHelper.ExpandTabs("-\t\ttext"));
            Assert.Equal("--      text", NeonHelper.ExpandTabs("--\t\ttext"));
            Assert.Equal("---     text", NeonHelper.ExpandTabs("---\t\ttext"));
            Assert.Equal("            text", NeonHelper.ExpandTabs("\t\t\ttext"));

            Assert.Equal("1   2   3", NeonHelper.ExpandTabs("1\t2\t3"));

            // Verify that a zero tab stop returns and unchanged string.

            Assert.Equal("    text", NeonHelper.ExpandTabs("    text", tabStop: 0));

            // Test input with line endings.

            var sb = new StringBuilder();

            sb.Clear();
            sb.AppendLine("line1");
            sb.AppendLine("\tline2");
            sb.AppendLine("-\tline3");
            sb.AppendLine("--\tline4");
            sb.AppendLine("---\tline5");
            sb.AppendLine("\t\tline6");
            sb.AppendLine("-    \tline7");
            sb.AppendLine("--   \tline8");
            sb.AppendLine("---  \tline9");
            sb.AppendLine("\t\t\tline10");

            Assert.Equal(
@"line1
    line2
-   line3
--  line4
--- line5
        line6
-       line7
--      line8
---     line9
            line10
", NeonHelper.ExpandTabs(sb.ToString()));

            // Test a non-default tab stop.

            Assert.Equal("text", NeonHelper.ExpandTabs("text", 8));
            Assert.Equal("        text", NeonHelper.ExpandTabs("\ttext", 8));
            Assert.Equal("-       text", NeonHelper.ExpandTabs("-\ttext", 8));
            Assert.Equal("--      text", NeonHelper.ExpandTabs("--\ttext", 8));
            Assert.Equal("---     text", NeonHelper.ExpandTabs("---\ttext", 8));
            Assert.Equal("                text", NeonHelper.ExpandTabs("\t\ttext", 8));
            Assert.Equal("-               text", NeonHelper.ExpandTabs("-\t\ttext", 8));
            Assert.Equal("--              text", NeonHelper.ExpandTabs("--\t\ttext", 8));
            Assert.Equal("---             text", NeonHelper.ExpandTabs("---\t\ttext", 8));
            Assert.Equal("                        text", NeonHelper.ExpandTabs("\t\t\ttext", 8));

            Assert.Equal("1       2       3", NeonHelper.ExpandTabs("1\t2\t3", 8));

            // Verify that a negative tab stop converts spaces into TABs.

            Assert.Equal("text", NeonHelper.ExpandTabs("text", -4));
            Assert.Equal("\ttext", NeonHelper.ExpandTabs("    text", -4));
            Assert.Equal("\t\ttext", NeonHelper.ExpandTabs("        text", -4));

            // Verify that we can handle left-over spaces.

            Assert.Equal("  text", NeonHelper.ExpandTabs("  text", -4));
            Assert.Equal("\t text", NeonHelper.ExpandTabs("     text", -4));
            Assert.Equal("\t  text", NeonHelper.ExpandTabs("      text", -4));

            // Verify that we don't convert spaces after the first non-space character.

            Assert.Equal("\ttext        x", NeonHelper.ExpandTabs("    text        x", -4));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void SequenceEquals_Enumerable()
        {
            Assert.True(NeonHelper.SequenceEqual((IEnumerable<string>)null, (IEnumerable<string>)null));
            Assert.True(NeonHelper.SequenceEqual((IEnumerable<string>)new string[0], (IEnumerable<string>)new string[0]));
            Assert.True(NeonHelper.SequenceEqual((IEnumerable<string>)new string[] { "0", "1" }, (IEnumerable<string>)new string[] { "0", "1" }));
            Assert.True(NeonHelper.SequenceEqual((IEnumerable<string>)new string[] { "0", null }, (IEnumerable<string>)new string[] { "0", null }));

            Assert.False(NeonHelper.SequenceEqual((IEnumerable<string>)new string[] { "0", "1" }, (IEnumerable<string>)null));
            Assert.False(NeonHelper.SequenceEqual((IEnumerable<string>)null, (IEnumerable<string>)new string[] { "0", "1" }));
            Assert.False(NeonHelper.SequenceEqual((IEnumerable<string>)new string[] { "0", "1" }, (IEnumerable<string>)new string[] { "0" }));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void SequenceEquals_Array()
        {
            Assert.True(NeonHelper.SequenceEqual((string[])null, (string[])null));
            Assert.True(NeonHelper.SequenceEqual(new string[0], new string[0]));
            Assert.True(NeonHelper.SequenceEqual(new string[] { "0", "1" }, new string[] { "0", "1" }));
            Assert.True(NeonHelper.SequenceEqual(new string[] { "0", null }, new string[] { "0", null }));

            Assert.False(NeonHelper.SequenceEqual(new string[] { "0", "1" }, (string[])null));
            Assert.False(NeonHelper.SequenceEqual((string[])null, new string[] { "0", "1" }));
            Assert.False(NeonHelper.SequenceEqual(new string[] { "0", "1" }, new string[] { "0" }));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void SequenceEquals_List()
        {
            Assert.True(NeonHelper.SequenceEqual((List<string>)null, (List<string>)null));
            Assert.True(NeonHelper.SequenceEqual(new List<string>(), new List<string>()));
            Assert.True(NeonHelper.SequenceEqual(new List<string>() { "0", "1" }, new List<string>() { "0", "1" }));
            Assert.True(NeonHelper.SequenceEqual(new List<string>() { "0", null }, new List<string>() { "0", null }));

            Assert.False(NeonHelper.SequenceEqual(new List<string>() { "0", "1" }, (List<string>)null));
            Assert.False(NeonHelper.SequenceEqual((List<string>)null, new List<string>() { "0", "1" }));
            Assert.False(NeonHelper.SequenceEqual(new List<string>() { "0", "1" }, new List<string>() { "0" }));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void ParseCounts()
        {
            // Verify that the units are correct.

            Assert.Equal(Math.Pow(2, 10), NeonHelper.Kilo);
            Assert.Equal(Math.Pow(2, 20), NeonHelper.Mega);
            Assert.Equal(Math.Pow(2, 30), NeonHelper.Giga);

            double value;

            // Parse whole values.

            Assert.True(NeonHelper.TryParseCount("0", out value));
            Assert.Equal(0.0, value);

            Assert.True(NeonHelper.TryParseCount("10b", out value));
            Assert.Equal(10.0, value);

            Assert.True(NeonHelper.TryParseCount("20B", out value));
            Assert.Equal(20.0, value);

            Assert.True(NeonHelper.TryParseCount("1K", out value));
            Assert.Equal((double)NeonHelper.Kilo, value);

            Assert.True(NeonHelper.TryParseCount("2KB", out value));
            Assert.Equal((double)NeonHelper.Kilo * 2, value);

            Assert.True(NeonHelper.TryParseCount("3k", out value));
            Assert.Equal((double)NeonHelper.Kilo * 3, value);

            Assert.True(NeonHelper.TryParseCount("4kb", out value));
            Assert.Equal((double)NeonHelper.Kilo * 4, value);

            Assert.True(NeonHelper.TryParseCount("1M", out value));
            Assert.Equal((double)NeonHelper.Mega, value);

            Assert.True(NeonHelper.TryParseCount("2MB", out value));
            Assert.Equal((double)NeonHelper.Mega * 2, value);

            Assert.True(NeonHelper.TryParseCount("3m", out value));
            Assert.Equal((double)NeonHelper.Mega * 3, value);

            Assert.True(NeonHelper.TryParseCount("4mb", out value));
            Assert.Equal((double)NeonHelper.Mega * 4, value);

            Assert.True(NeonHelper.TryParseCount("1G", out value));
            Assert.Equal((double)NeonHelper.Giga, value);

            Assert.True(NeonHelper.TryParseCount("2TB", out value));
            Assert.Equal((double)NeonHelper.Tera * 2, value);

            Assert.True(NeonHelper.TryParseCount("1T", out value));
            Assert.Equal((double)NeonHelper.Tera, value);

            Assert.True(NeonHelper.TryParseCount("2GB", out value));
            Assert.Equal((double)NeonHelper.Giga * 2, value);

            Assert.True(NeonHelper.TryParseCount("3g", out value));
            Assert.Equal((double)NeonHelper.Giga * 3, value);

            Assert.True(NeonHelper.TryParseCount("4gb", out value));
            Assert.Equal((double)NeonHelper.Giga * 4, value);

            Assert.True(NeonHelper.TryParseCount("3t", out value));
            Assert.Equal((double)NeonHelper.Tera * 3, value);

            Assert.True(NeonHelper.TryParseCount("4tb", out value));
            Assert.Equal((double)NeonHelper.Tera * 4, value);

            // Test fractional values.

            Assert.True(NeonHelper.TryParseCount("0.5", out value));
            Assert.Equal(0.5, value);

            Assert.True(NeonHelper.TryParseCount("0.5B", out value));
            Assert.Equal(0.5, value);

            Assert.True(NeonHelper.TryParseCount("1.5KB", out value));
            Assert.Equal((double)NeonHelper.Kilo * 1.5, value);

            Assert.True(NeonHelper.TryParseCount("1.5MB", out value));
            Assert.Equal((double)NeonHelper.Mega * 1.5, value);

            Assert.True(NeonHelper.TryParseCount("1.5GB", out value));
            Assert.Equal((double)NeonHelper.Giga * 1.5, value);

            // Test invalid inputs.

            Assert.False(NeonHelper.TryParseCount(null, out value));
            Assert.False(NeonHelper.TryParseCount("", out value));
            Assert.False(NeonHelper.TryParseCount("   ", out value));
            Assert.False(NeonHelper.TryParseCount("ABC", out value));
            Assert.False(NeonHelper.TryParseCount("-10", out value));
            Assert.False(NeonHelper.TryParseCount("-20KB", out value));
            Assert.False(NeonHelper.TryParseCount("10a", out value));
            Assert.False(NeonHelper.TryParseCount("10akb", out value));
        }
    }
}
