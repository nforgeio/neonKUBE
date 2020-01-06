//-----------------------------------------------------------------------------
// FILE:	    Test_NeonHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Threading.Tasks;

using Neon.Common;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public partial class Test_NeonHelper
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Framework()
        {
            var framework = RuntimeInformation.FrameworkDescription;

            if (framework.StartsWith(".NET Core"))
            {
                Assert.Equal(NetFramework.Core, NeonHelper.Framework);
            }
            else if (framework.StartsWith(".NET Framework"))
            {
                Assert.Equal(NetFramework.Framework, NeonHelper.Framework);
            }
            else if (framework.StartsWith(".NET Native"))
            {
                Assert.Equal(NetFramework.Native, NeonHelper.Framework);
            }
            else
            {
                Assert.Equal(NetFramework.Unknown, NeonHelper.Framework);
            }
        }

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

        private async Task GetNoResultAsync()
        {
            await Task.CompletedTask;
        }

        public async Task<string> GetResultAsync()
        {
            return await Task.FromResult("Hello World!");
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task GetObjectResultAsync()
        {
            // We should see an ArgumentException here because the task doesn't return a result.

            await Assert.ThrowsAsync<ArgumentException>(async () => await NeonHelper.GetTaskResultAsObjectAsync(GetNoResultAsync()));

            // This should succeed.

            Assert.Equal("Hello World!", await NeonHelper.GetTaskResultAsObjectAsync(GetResultAsync()));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Base64UrlEncoding()
        {
            // Verify that known values can be encoded with padding.

            Assert.Equal("", NeonHelper.Base64UrlEncode(new byte[0], retainPadding: true));
            Assert.Equal("AA%3D%3D", NeonHelper.Base64UrlEncode(new byte[] { 0 }, retainPadding: true));
            Assert.Equal("AAE%3D", NeonHelper.Base64UrlEncode(new byte[] { 0, 1 }, retainPadding: true));
            Assert.Equal("AAEC", NeonHelper.Base64UrlEncode(new byte[] { 0, 1, 2 }, retainPadding: true));
            Assert.Equal("AAECAw%3D%3D", NeonHelper.Base64UrlEncode(new byte[] { 0, 1, 2, 3 }, retainPadding: true));
            Assert.Equal("AAECAwQ%3D", NeonHelper.Base64UrlEncode(new byte[] { 0, 1, 2, 3, 4 }, retainPadding: true));

            // Verify that known values can be encoded without padding.

            Assert.Equal("", NeonHelper.Base64UrlEncode(new byte[0]));
            Assert.Equal("AA", NeonHelper.Base64UrlEncode(new byte[] { 0 }));
            Assert.Equal("AAE", NeonHelper.Base64UrlEncode(new byte[] { 0, 1 }));
            Assert.Equal("AAEC", NeonHelper.Base64UrlEncode(new byte[] { 0, 1, 2 }));
            Assert.Equal("AAECAw", NeonHelper.Base64UrlEncode(new byte[] { 0, 1, 2, 3 }));
            Assert.Equal("AAECAwQ", NeonHelper.Base64UrlEncode(new byte[] { 0, 1, 2, 3, 4 }));

            // Verify that we can decode known values with padding.

            Assert.Equal(new byte[0], NeonHelper.Base64UrlDecode(""));
            Assert.Equal(new byte[] { 0 }, NeonHelper.Base64UrlDecode("AA%3D%3D"));
            Assert.Equal(new byte[] { 0, 1 }, NeonHelper.Base64UrlDecode("AAE%3D"));
            Assert.Equal(new byte[] { 0, 1, 2 }, NeonHelper.Base64UrlDecode("AAEC"));
            Assert.Equal(new byte[] { 0, 1, 2, 3 }, NeonHelper.Base64UrlDecode("AAECAw%3D%3D"));
            Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, NeonHelper.Base64UrlDecode("AAECAwQ%3D"));

            // Verify that we can decode known values without padding.

            Assert.Equal(new byte[0], NeonHelper.Base64UrlDecode(""));
            Assert.Equal(new byte[] { 0 }, NeonHelper.Base64UrlDecode("AA"));
            Assert.Equal(new byte[] { 0, 1 }, NeonHelper.Base64UrlDecode("AAE"));
            Assert.Equal(new byte[] { 0, 1, 2 }, NeonHelper.Base64UrlDecode("AAEC"));
            Assert.Equal(new byte[] { 0, 1, 2, 3 }, NeonHelper.Base64UrlDecode("AAECAw"));
            Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, NeonHelper.Base64UrlDecode("AAECAwQ"));
        }
    }
}
