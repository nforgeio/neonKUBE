//-----------------------------------------------------------------------------
// FILE:	    Test_NeonHelper.Misc.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.IO;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public partial class Test_Helper
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void FromHex()
        {
            Assert.Equal(new byte[0], NeonHelper.FromHex(""), new CollectionComparer<byte>());
            Assert.Equal(new byte[] { 0x00, 0x01, 0x02, 0xAA, 0xCF }, NeonHelper.FromHex("000102AACF"), new CollectionComparer<byte>());
            Assert.Equal(new byte[] { 0x00, 0x01, 0x02, 0xAA, 0xCF }, NeonHelper.FromHex("000102aacf"), new CollectionComparer<byte>());
            Assert.Equal(new byte[] { 0x00, 0x01, 0x02, 0xAA, 0xCF }, NeonHelper.FromHex("00 010\r\n2AA\tCF"), new CollectionComparer<byte>());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void NullableEquals()
        {
            Assert.True(NeonHelper.NullableEquals((int?)null, (int?)null));
            Assert.True(NeonHelper.NullableEquals((int?)1, (int?)1));

            Assert.False(NeonHelper.NullableEquals((int?)null, (int?)1));
            Assert.False(NeonHelper.NullableEquals((int?)1, (int?)null));
            Assert.False(NeonHelper.NullableEquals((int?)1, (int?)2));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void TryParseHex_Int()
        {
            int v;

            Assert.True(NeonHelper.TryParseHex("0", out v));
            Assert.Equal(0, v);

            Assert.True(NeonHelper.TryParseHex("0000", out v));
            Assert.Equal(0, v);

            Assert.True(NeonHelper.TryParseHex("FFFF", out v));
            Assert.Equal(0xFFFF, v);

            Assert.True(NeonHelper.TryParseHex("A", out v));
            Assert.Equal(0xA, v);

            Assert.True(NeonHelper.TryParseHex("Abcd", out v));
            Assert.Equal(0xABCD, v);

            Assert.False(NeonHelper.TryParseHex("", out v));
            Assert.False(NeonHelper.TryParseHex("1q", out v));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void TryParseHex_Array()
        {
            byte[] v;

            Assert.True(NeonHelper.TryParseHex("0000", out v));
            Assert.Equal(new byte[] { 0, 0 }, v, new CollectionComparer<byte>());

            Assert.True(NeonHelper.TryParseHex("FFFF", out v));
            Assert.Equal(new byte[] { 0xFF, 0xFF }, v, new CollectionComparer<byte>());

            Assert.True(NeonHelper.TryParseHex("A1", out v));
            Assert.Equal(new byte[] { 0xA1 }, v, new CollectionComparer<byte>());

            Assert.True(NeonHelper.TryParseHex("Abcd", out v));
            Assert.Equal(new byte[] { 0xAB, 0xCD }, v, new CollectionComparer<byte>());

            Assert.True(NeonHelper.TryParseHex("000102030405060708090A0B0C0D0E0F", out v));
            Assert.Equal(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F }, v, new CollectionComparer<byte>());

            Assert.False(NeonHelper.TryParseHex("", out v));
            Assert.False(NeonHelper.TryParseHex("1", out v));
            Assert.False(NeonHelper.TryParseHex("1q", out v));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void HexDump()
        {
            byte[] data = Encoding.ASCII.GetBytes("0123456789ABCDEF");

            Assert.Equal("", NeonHelper.HexDump(data, 0, 0, 16, HexDumpOption.None));
            Assert.Equal("", NeonHelper.HexDump(data, 0, 0, 16, HexDumpOption.ShowAll));

            Assert.Equal("30 31 32 33 \r\n", NeonHelper.HexDump(data, 0, 4, 4, HexDumpOption.None));
            Assert.Equal("30 31 \r\n32 33 \r\n", NeonHelper.HexDump(data, 0, 4, 2, HexDumpOption.None));
            Assert.Equal("0000: 31 32 33 34 - 1234\r\n", NeonHelper.HexDump(data, 1, 4, 4, HexDumpOption.ShowAll));
            Assert.Equal("0000: 30 31 32 33 34 35 36 37 - 01234567\r\n", NeonHelper.HexDump(data, 0, 8, 8, HexDumpOption.ShowAll));
            Assert.Equal("0000: 30 31 32 33 34 35 36 37 - 01234567\r\n0008: 38 39 41 42 43 44 45 46 - 89ABCDEF\r\n", NeonHelper.HexDump(data, 0, 16, 8, HexDumpOption.ShowAll));
            Assert.Equal("0000: 30 31 32 - 012\r\n0003: 33 34    - 34\r\n", NeonHelper.HexDump(data, 0, 5, 3, HexDumpOption.ShowAll));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void JTokenEquals()
        {
            // NULL tests

            Assert.True(NeonHelper.JTokenEquals(null, null));
            Assert.False(NeonHelper.JTokenEquals(null, new JProperty("test", 10)));
            Assert.False(NeonHelper.JTokenEquals(new JProperty("test", 10), null));

            // Different token type tests

            Assert.False(NeonHelper.JTokenEquals(new JProperty("test", 10), new JValue(10)));
            Assert.False(NeonHelper.JTokenEquals(new JProperty("test", 10), new JArray()));
            Assert.False(NeonHelper.JTokenEquals(new JValue(10.123), new JValue(10)));

            // JProperty tests

            Assert.True(NeonHelper.JTokenEquals(new JProperty("test", 10), new JProperty("test", 10)));
            Assert.True(NeonHelper.JTokenEquals(new JProperty("test", null), new JProperty("test", null)));
            Assert.True(NeonHelper.JTokenEquals(new JProperty("test", new JValue((string)null)), new JProperty("test", null)));
            Assert.False(NeonHelper.JTokenEquals(new JProperty("test", 10), new JProperty("test", 20)));
            Assert.False(NeonHelper.JTokenEquals(new JProperty("test", 10), new JProperty("different", 10)));
            Assert.False(NeonHelper.JTokenEquals(new JProperty("test", 10), new JProperty("test", new JObject())));

            // JObject tests

            Assert.True(
                NeonHelper.JTokenEquals(
                    new JObject(),
                    new JObject()));

            Assert.True(
                NeonHelper.JTokenEquals(
                    new JObject(
                        new JProperty("prop1", "one"),
                        new JProperty("prop2", "two")),
                    new JObject(
                        new JProperty("prop1", "one"),
                        new JProperty("prop2", "two"))));

            Assert.False(
                NeonHelper.JTokenEquals(
                    new JObject(
                        new JProperty("prop1", "one"),
                        new JProperty("prop2", "TWO")),
                    new JObject(
                        new JProperty("prop1", "one"),
                        new JProperty("prop2", "two"))));

            Assert.False(
                NeonHelper.JTokenEquals(
                    new JObject(
                        new JProperty("prop1", "one"),
                        new JProperty("PROP2", "two")),
                    new JObject(
                        new JProperty("prop1", "one"),
                        new JProperty("prop2", "two"))));

            Assert.False(
                NeonHelper.JTokenEquals(
                    new JObject(
                        new JProperty("prop1", "one")),
                    new JObject(
                        new JProperty("prop1", "one"),
                        new JProperty("prop2", "two"))));

            // JArray tests

            Assert.True(
                NeonHelper.JTokenEquals(
                    new JArray(),
                    new JArray()));

            Assert.True(
                NeonHelper.JTokenEquals(
                    new JArray(1, 2, 3, 4),
                    new JArray(1, 2, 3, 4)));

            Assert.False(
                NeonHelper.JTokenEquals(
                    new JArray(1, 2, 3, 4),
                    new JArray(1, 2, 3, 4, 5)));

            Assert.False(
                NeonHelper.JTokenEquals(
                    new JArray(1, 2, 3, 4),
                    new JArray(1, 2, "FOO", 4)));

            // Value tests

            Assert.True(NeonHelper.JTokenEquals(
                new JValue(true),
                new JValue(true)));

            Assert.False(NeonHelper.JTokenEquals(
                new JValue(true),
                new JValue(false)));

            Assert.True(NeonHelper.JTokenEquals(
                new JValue(10),
                new JValue(10)));

            Assert.False(NeonHelper.JTokenEquals(
                new JValue(10),
                new JValue(20)));

            Assert.True(NeonHelper.JTokenEquals(
                new JValue("HELLO"),
                new JValue("HELLO")));

            Assert.False(NeonHelper.JTokenEquals(
                new JValue("FOO"),
                new JValue("BAR")));

            Assert.False(NeonHelper.JTokenEquals(
                new JValue("FOO"),
                new JValue(20)));

            Assert.True(NeonHelper.JTokenEquals(
                new JValue(Guid.Empty),
                new JValue(Guid.Empty)));

            var guid1 = Guid.NewGuid();
            var guid2 = Guid.NewGuid();

            Assert.True(NeonHelper.JTokenEquals(
                new JValue(guid1),
                new JValue(guid1)));

            Assert.False(NeonHelper.JTokenEquals(
                new JValue(guid1),
                new JValue(guid2)));

            Assert.False(NeonHelper.JTokenEquals(
                new JValue(guid1),
                new JValue(Guid.Empty)));

            // Test value types that will roundtrip as strings.

            var now = DateTime.Now;
            var nowOffset = DateTimeOffset.Now;

            Assert.True(NeonHelper.JTokenEquals(
                new JValue(now),
                new JValue(now.ToString())));

            Assert.True(NeonHelper.JTokenEquals(
                new JValue(nowOffset),
                new JValue(nowOffset.ToString())));

            Assert.True(NeonHelper.JTokenEquals(
                new JValue(guid1),
                new JValue(guid1.ToString())));

            Assert.True(NeonHelper.JTokenEquals(
                new JValue(TimeSpan.FromMinutes(1.5)),
                new JValue(TimeSpan.FromMinutes(1.5).ToString())));

            Assert.True(NeonHelper.JTokenEquals(
                new JValue(new Uri("http://hello.com")),
                new JValue(new Uri("http://hello.com").ToString())));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void GetRandomPassword()
        {
            // Test generating passwords of various lengths.

            for (int i = 1; i < 100; i++)
            {
                Assert.Equal(i, NeonHelper.GetRandomPassword(i).Length);
            }

            // Generate a bunch of passwords and verify that we don't 
            // see any duplicates (I know this is a weak test).

            var existing = new HashSet<string>();

            for (int i = 0; i < 1000; i++)
            {
                var password = NeonHelper.GetRandomPassword(20);

                Assert.DoesNotContain(password, existing);

                existing.Add(password);
            }
        }

        public enum JsonTestEnum
        {
            Test,
            CamelCase
        }

        public class JsonTestClass
        {
            public JsonTestEnum EnumValue { get; set; }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void ArrayEquals_Byte()
        {
            Assert.True(NeonHelper.ArrayEquals(null, null));
            Assert.True(NeonHelper.ArrayEquals(new byte[0], new byte[0]));
            Assert.True(NeonHelper.ArrayEquals(new byte[] { 0, 1, 2, 3, 4 }, new byte[] { 0, 1, 2, 3, 4 }));

            Assert.False(NeonHelper.ArrayEquals(new byte[0], null));
            Assert.False(NeonHelper.ArrayEquals(null, new byte[0]));
            Assert.False(NeonHelper.ArrayEquals(new byte[] { 0, 1, 2, 3, 100 }, new byte[] { 0, 1, 2, 3, 4 }));
            Assert.False(NeonHelper.ArrayEquals(new byte[] { 0, 1, 2, 3 }, new byte[] { 0, 1, 2, 3, 4 }));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void DeflateString()
        {
            const string compressable = "This is a test of the emergency broadcasting system. This is a test of the emergency broadcasting system. This is a test of the emergency broadcasting system.";

            Assert.Null(NeonHelper.CompressString(null));
            Assert.Equal("", NeonHelper.DecompressString(NeonHelper.CompressString("")));
            Assert.True(compressable.Length > NeonHelper.CompressString(compressable).Length);
            Assert.Equal(compressable, NeonHelper.DecompressString(NeonHelper.CompressString(compressable)));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void DeflateBytes()
        {
            byte[] compressable = Encoding.UTF8.GetBytes("This is a test of the emergency broadcasting system. This is a test of the emergency broadcasting system. This is a test of the emergency broadcasting system.");

            Assert.Equal(new byte[0], NeonHelper.DecompressBytes(NeonHelper.CompressBytes(new byte[0])));
            Assert.True(compressable.Length > NeonHelper.CompressBytes(compressable).Length);
            Assert.Equal(compressable, NeonHelper.DecompressBytes(NeonHelper.CompressBytes(compressable)));
        }

        private enum TestEnum
        {
            Value1,
            Value2,

            [EnumMember(Value = "foo-bar")]
            FooBar
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void EnumSerialization()
        {
            //-----------------------------------------------------------------
            // Verify enum deserialization

            Assert.Throws<ArgumentNullException>(() => NeonHelper.ParseEnum<TestEnum>(null));
            Assert.Throws<ArgumentException>(() => NeonHelper.ParseEnum<TestEnum>("foo"));

            Assert.Equal(TestEnum.Value1, NeonHelper.ParseEnum<TestEnum>("value1"));
            Assert.Equal(TestEnum.Value2, NeonHelper.ParseEnum<TestEnum>("VALUE2"));

            // This method should honor [EnumMember] attributes too.

            Assert.Equal(TestEnum.Value1, NeonHelper.ParseEnum<TestEnum>("value1"));
            Assert.Equal(TestEnum.Value2, NeonHelper.ParseEnum<TestEnum>("VALUE2"));
            Assert.Equal(TestEnum.FooBar, NeonHelper.ParseEnum<TestEnum>("FooBar"));
            Assert.Equal(TestEnum.FooBar, NeonHelper.ParseEnum<TestEnum>("foo-bar"));
            Assert.Equal(TestEnum.FooBar, NeonHelper.ParseEnum<TestEnum>("FOO-BAR"));

            Assert.Throws<ArgumentNullException>(() => NeonHelper.ParseEnum<TestEnum>(null));
            Assert.Throws<ArgumentException>(() => NeonHelper.ParseEnum<TestEnum>("BAD"));

            TestEnum parsed;

            Assert.True(NeonHelper.TryParse<TestEnum>("foo-bar", out parsed));
            Assert.Equal(TestEnum.FooBar, parsed);

            Assert.True(NeonHelper.TryParse<TestEnum>("FOO-BAR", out parsed));
            Assert.Equal(TestEnum.FooBar, parsed);

            Assert.False(NeonHelper.TryParse<TestEnum>("BAD", out parsed));

            Assert.Equal(TestEnum.Value1, NeonHelper.ParseEnum<TestEnum>(null, TestEnum.Value1));
            Assert.Equal(TestEnum.Value1, NeonHelper.ParseEnum<TestEnum>(string.Empty, TestEnum.Value1));
            Assert.Equal(TestEnum.Value1, NeonHelper.ParseEnum<TestEnum>("UNKNOWN", TestEnum.Value1));

            Assert.Equal(TestEnum.Value2, NeonHelper.ParseEnum<TestEnum>(null, TestEnum.Value2));
            Assert.Equal(TestEnum.Value2, NeonHelper.ParseEnum<TestEnum>(string.Empty, TestEnum.Value2));
            Assert.Equal(TestEnum.Value2, NeonHelper.ParseEnum<TestEnum>("UNKNOWN", TestEnum.Value2));

            //-----------------------------------------------------------------
            // Verify enum serialization too.

            Assert.Equal("Value1", NeonHelper.EnumToString(TestEnum.Value1));
            Assert.Equal("Value2", NeonHelper.EnumToString(TestEnum.Value2));
            Assert.Equal("foo-bar", NeonHelper.EnumToString(TestEnum.FooBar));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void CopyFolder()
        {
            using (var tempFolder = new TempFolder())
            {
                var sourceFolder    = Path.Combine(tempFolder.Path, "source");
                var sourceSubFolder = Path.Combine(sourceFolder, "subfolder");

                Directory.CreateDirectory(sourceFolder);
                File.WriteAllText(Path.Combine(sourceFolder, "test1.txt"), "test1");
                File.WriteAllText(Path.Combine(sourceFolder, "test2.txt"), "test2");

                Directory.CreateDirectory(sourceSubFolder);
                File.WriteAllText(Path.Combine(sourceSubFolder, "test3.txt"), "test3");

                var targetFolder = Path.Combine(tempFolder.Path, "target");

                NeonHelper.CopyFolder(sourceFolder, targetFolder);

                Assert.True(Directory.Exists(targetFolder));
                Assert.True(Directory.Exists(Path.Combine(targetFolder, "subfolder")));

                Assert.Equal("test1", File.ReadAllText(Path.Combine(targetFolder, "test1.txt")));
                Assert.Equal("test2", File.ReadAllText(Path.Combine(targetFolder, "test2.txt")));
                Assert.Equal("test3", File.ReadAllText(Path.Combine(targetFolder, "subfolder", "test3.txt")));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void NormalizeExecArgs()
        {
            Assert.Equal("", NeonHelper.NormalizeExecArgs());
            Assert.Equal("", NeonHelper.NormalizeExecArgs(null));
            Assert.Equal("", NeonHelper.NormalizeExecArgs(new object[] { null }));
            Assert.Equal("", NeonHelper.NormalizeExecArgs(new object[] { null, null }));
            Assert.Equal("", NeonHelper.NormalizeExecArgs(string.Empty));
            Assert.Equal("0 1 2 3", NeonHelper.NormalizeExecArgs(0, 1, 2, 3));
            Assert.Equal("0 1 2 3", NeonHelper.NormalizeExecArgs("0", "1", "2", "3"));
            Assert.Equal(@"""\""""", NeonHelper.NormalizeExecArgs("\""));
            Assert.Equal(@"""one two"" three", NeonHelper.NormalizeExecArgs("one two", "three"));

            Assert.Equal(@"one two three", NeonHelper.NormalizeExecArgs(new string[] { "one", "two" }, "three"));
            Assert.Equal(@"one two three", NeonHelper.NormalizeExecArgs(new string[] { "one", "two", null }, "three"));
            Assert.Equal(@"one two three", NeonHelper.NormalizeExecArgs(new string[] { "one", "two", "" }, "three"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void ParseBool()
        {
            Assert.True(NeonHelper.ParseBool("1"));
            Assert.True(NeonHelper.ParseBool("on"));
            Assert.True(NeonHelper.ParseBool("yes"));
            Assert.True(NeonHelper.ParseBool("enabled"));
            Assert.True(NeonHelper.ParseBool("true"));

            Assert.True(NeonHelper.ParseBool("1"));
            Assert.True(NeonHelper.ParseBool("ON"));
            Assert.True(NeonHelper.ParseBool("YES"));
            Assert.True(NeonHelper.ParseBool("ENABLED"));
            Assert.True(NeonHelper.ParseBool("TRUE"));

            Assert.False(NeonHelper.ParseBool("0"));
            Assert.False(NeonHelper.ParseBool("off"));
            Assert.False(NeonHelper.ParseBool("no"));
            Assert.False(NeonHelper.ParseBool("disabled"));
            Assert.False(NeonHelper.ParseBool("false"));

            Assert.False(NeonHelper.ParseBool("0"));
            Assert.False(NeonHelper.ParseBool("OFF"));
            Assert.False(NeonHelper.ParseBool("NO"));
            Assert.False(NeonHelper.ParseBool("DISABLED"));
            Assert.False(NeonHelper.ParseBool("FALSE"));

            Assert.Throws<FormatException>(() => NeonHelper.ParseBool("invalid input"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void WaitParallel()
        {
            // Run a few tasks to completion in parallel.

            // $todo(jeff.lill):
            //
            // We're not actually verifying that the tasks actually
            // ran in parallel.

            var didTask0 = false;
            var didTask1 = false;
            var didTask2 = false;
            var didTask3 = false;
            var delaySec = 2;

            NeonHelper.WaitForParallel(
                new Action[]
                {
                    async () => {

                        await Task.Delay(TimeSpan.FromSeconds(delaySec));
                        didTask0 = true;
                    },
                    async () => {

                        await Task.Delay(TimeSpan.FromSeconds(delaySec) * 2);
                        didTask1 = true;
                    },
                    async () => {

                        await Task.Delay(TimeSpan.FromSeconds(delaySec) * 3);
                        didTask2 = true;
                    },
                    () => {

                        Thread.Sleep(TimeSpan.FromSeconds(delaySec) * 4);
                        didTask3 = true;
                    }
                });

            Assert.True(didTask0);
            Assert.True(didTask1);
            Assert.True(didTask2);
            Assert.True(didTask3);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void StripAnsibleWarnings()
        {
            var text =
@" [WARNING] Ansible is being run in a world writable directory (/cwd), ignoring it as an ansible.cfg source. For more information see https://docs.ansible.com/ansible/devel/reference_appendices/config.html#cfg-in-world-writable-dir
# WARNING! 
#
# This file includes the secrets required to setup and configure the 
# WRT-00-MAIN cluster.  This must always be encrypted at rest and
# also when committed to Git.  You can use the following commands to
# view or edit this file using the [tarukino-git] encryption password:
#
#		neon file view secrets.yaml tarukino-git
#		neon file edit secrets.yaml tarukino-git
# Devops credentials
DEVOPS:
";
            var stripped = NeonHelper.StripAnsibleWarnings(text);

            Assert.StartsWith("# WARNING!", stripped);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void BoolString()
        {
            Assert.Equal("true", NeonHelper.ToBoolString(true));
            Assert.Equal("false", NeonHelper.ToBoolString(false));
        }
    }
}
