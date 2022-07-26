//-----------------------------------------------------------------------------
// FILE:	    Test_NeonHelper.Misc.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public partial class Test_NeonHelper
    {
        [Fact]
        public void ParseBool()
        {
            Assert.False(NeonHelper.ParseBool("0"));
            Assert.False(NeonHelper.ParseBool("off"));
            Assert.False(NeonHelper.ParseBool("no"));
            Assert.False(NeonHelper.ParseBool("disabled"));
            Assert.False(NeonHelper.ParseBool("false"));

            Assert.False(NeonHelper.ParseBool("0"));
            Assert.False(NeonHelper.ParseBool("Off"));
            Assert.False(NeonHelper.ParseBool("No"));
            Assert.False(NeonHelper.ParseBool("Disabled"));
            Assert.False(NeonHelper.ParseBool("False"));

            Assert.True(NeonHelper.ParseBool("1"));
            Assert.True(NeonHelper.ParseBool("on"));
            Assert.True(NeonHelper.ParseBool("yes"));
            Assert.True(NeonHelper.ParseBool("enabled"));
            Assert.True(NeonHelper.ParseBool("true"));

            Assert.True(NeonHelper.ParseBool("1"));
            Assert.True(NeonHelper.ParseBool("On"));
            Assert.True(NeonHelper.ParseBool("Yes"));
            Assert.True(NeonHelper.ParseBool("Enabled"));
            Assert.True(NeonHelper.ParseBool("True"));

            Assert.Throws<ArgumentNullException>(() => NeonHelper.ParseBool(null));
            Assert.Throws<ArgumentNullException>(() => NeonHelper.ParseBool(""));
            Assert.Throws<FormatException>(() => NeonHelper.ParseBool("   "));
            Assert.Throws<FormatException>(() => NeonHelper.ParseBool("ILLEGAL"));
        }

        [Fact]
        public void TryParseBool()
        {
            bool value;

            Assert.True(NeonHelper.TryParseBool("0", out value));
            Assert.False(value);
            Assert.True(NeonHelper.TryParseBool("off", out value));
            Assert.False(value);
            Assert.True(NeonHelper.TryParseBool("no", out value));
            Assert.False(value);
            Assert.True(NeonHelper.TryParseBool("disabled", out value));
            Assert.False(value);
            Assert.True(NeonHelper.TryParseBool("false", out value));
            Assert.False(value);

            Assert.True(NeonHelper.TryParseBool("0", out value));
            Assert.False(value);
            Assert.True(NeonHelper.TryParseBool("Off", out value));
            Assert.False(value);
            Assert.True(NeonHelper.TryParseBool("No", out value));
            Assert.False(value);
            Assert.True(NeonHelper.TryParseBool("Disabled", out value));
            Assert.False(value);
            Assert.True(NeonHelper.TryParseBool("False", out value));
            Assert.False(value);

            Assert.True(NeonHelper.TryParseBool("1", out value));
            Assert.True(value);
            Assert.True(NeonHelper.TryParseBool("on", out value));
            Assert.True(value);
            Assert.True(NeonHelper.TryParseBool("yes", out value));
            Assert.True(value);
            Assert.True(NeonHelper.TryParseBool("enabled", out value));
            Assert.True(value);
            Assert.True(NeonHelper.TryParseBool("true", out value));
            Assert.True(value);

            Assert.True(NeonHelper.TryParseBool("1", out value));
            Assert.True(value);
            Assert.True(NeonHelper.TryParseBool("On", out value));
            Assert.True(value);
            Assert.True(NeonHelper.TryParseBool("Yes", out value));
            Assert.True(value);
            Assert.True(NeonHelper.TryParseBool("Enabled", out value));
            Assert.True(value);
            Assert.True(NeonHelper.TryParseBool("True", out value));
            Assert.True(value);

            Assert.False(NeonHelper.TryParseBool(null, out value));
            Assert.False(NeonHelper.TryParseBool("", out value));
            Assert.False(NeonHelper.TryParseBool("   ", out value));
            Assert.False(NeonHelper.TryParseBool("ILLEGAL", out value));
        }

        [Fact]
        public void ParseNullableBool()
        {
            Assert.False(NeonHelper.ParseNullableBool("0"));
            Assert.False(NeonHelper.ParseNullableBool("off"));
            Assert.False(NeonHelper.ParseNullableBool("no"));
            Assert.False(NeonHelper.ParseNullableBool("disabled"));
            Assert.False(NeonHelper.ParseNullableBool("false"));

            Assert.False(NeonHelper.ParseNullableBool("0"));
            Assert.False(NeonHelper.ParseNullableBool("Off"));
            Assert.False(NeonHelper.ParseNullableBool("No"));
            Assert.False(NeonHelper.ParseNullableBool("Disabled"));
            Assert.False(NeonHelper.ParseNullableBool("False"));

            Assert.True(NeonHelper.ParseNullableBool("1"));
            Assert.True(NeonHelper.ParseNullableBool("on"));
            Assert.True(NeonHelper.ParseNullableBool("yes"));
            Assert.True(NeonHelper.ParseNullableBool("enabled"));
            Assert.True(NeonHelper.ParseNullableBool("true"));

            Assert.True(NeonHelper.ParseNullableBool("1"));
            Assert.True(NeonHelper.ParseNullableBool("On"));
            Assert.True(NeonHelper.ParseNullableBool("Yes"));
            Assert.True(NeonHelper.ParseNullableBool("Enabled"));
            Assert.True(NeonHelper.ParseNullableBool("True"));

            Assert.Null(NeonHelper.ParseNullableBool(null));
            Assert.Null(NeonHelper.ParseNullableBool(""));

            Assert.Throws<FormatException>(() => NeonHelper.ParseNullableBool("   "));
            Assert.Throws<FormatException>(() => NeonHelper.ParseNullableBool("ILLEGAL"));
        }

        [Fact]
        public void TryParseNullableBool()
        {
            bool? value;

            Assert.True(NeonHelper.TryParseNullableBool("0", out value));
            Assert.False(value);
            Assert.True(NeonHelper.TryParseNullableBool("off", out value));
            Assert.False(value);
            Assert.True(NeonHelper.TryParseNullableBool("no", out value));
            Assert.False(value);
            Assert.True(NeonHelper.TryParseNullableBool("disabled", out value));
            Assert.False(value);
            Assert.True(NeonHelper.TryParseNullableBool("false", out value));
            Assert.False(value);

            Assert.True(NeonHelper.TryParseNullableBool("0", out value));
            Assert.False(value);
            Assert.True(NeonHelper.TryParseNullableBool("Off", out value));
            Assert.False(value);
            Assert.True(NeonHelper.TryParseNullableBool("No", out value));
            Assert.False(value);
            Assert.True(NeonHelper.TryParseNullableBool("Disabled", out value));
            Assert.False(value);
            Assert.True(NeonHelper.TryParseNullableBool("False", out value));
            Assert.False(value);

            Assert.True(NeonHelper.TryParseNullableBool("1", out value));
            Assert.True(value);
            Assert.True(NeonHelper.TryParseNullableBool("on", out value));
            Assert.True(value);
            Assert.True(NeonHelper.TryParseNullableBool("yes", out value));
            Assert.True(value);
            Assert.True(NeonHelper.TryParseNullableBool("enabled", out value));
            Assert.True(value);
            Assert.True(NeonHelper.TryParseNullableBool("true", out value));
            Assert.True(value);

            Assert.True(NeonHelper.TryParseNullableBool("1", out value));
            Assert.True(value);
            Assert.True(NeonHelper.TryParseNullableBool("On", out value));
            Assert.True(value);
            Assert.True(NeonHelper.TryParseNullableBool("Yes", out value));
            Assert.True(value);
            Assert.True(NeonHelper.TryParseNullableBool("Enabled", out value));
            Assert.True(value);
            Assert.True(NeonHelper.TryParseNullableBool("True", out value));
            Assert.True(value);

            Assert.True(NeonHelper.TryParseNullableBool(null, out value));
            Assert.Null(value);
            Assert.True(NeonHelper.TryParseNullableBool("", out value));
            Assert.Null(value);

            Assert.False(NeonHelper.TryParseNullableBool("   ", out value));
            Assert.False(NeonHelper.TryParseNullableBool("ILLEGAL", out value));
        }

        [Fact]
        public void WaitAll()
        {
            // Verify NOP when thread is NULL.

            NeonHelper.WaitAll(null);

            // Verify NOP when there are no threads.

            NeonHelper.WaitAll(new Thread[0]);

            // Verify NOP when a NULL thread is passed.

            NeonHelper.WaitAll(new Thread[] { null });

            // Verify that we can wait on a couple of threads.
            // This is a bit fragile due to hardcoded sleep delays.

            var threads = new List<Thread>();

            threads.Add(
                new Thread(
                    new ThreadStart(
                        () =>
                        {
                            Thread.Sleep(TimeSpan.FromSeconds(2));
                        })
                    ));

            threads.Add(
                new Thread(
                    new ThreadStart(
                        () =>
                        {
                            Thread.Sleep(TimeSpan.FromSeconds(2));
                        })
                    ));

            // Start the threads.

            foreach (var thread in threads)
            {
                thread.Start();
            }

            // The threads should still be running due to the sleep delays.

            foreach (var thread in threads)
            {
                Assert.True(thread.IsAlive);
            }

            NeonHelper.WaitAll(threads);

            // The threads should both be terminated now.

            foreach (var thread in threads)
            {
                Assert.False(thread.IsAlive);
            }
        }

        [Fact]
        public void Within_DateTime()
        {
            var expected = new DateTime(2020, 12, 4, 10, 58, 0);
            var value    = expected;

            Assert.True(NeonHelper.IsWithin(expected, value, TimeSpan.Zero));
            Assert.False(NeonHelper.IsWithin(expected, value + TimeSpan.FromMilliseconds(1), TimeSpan.Zero));
            Assert.False(NeonHelper.IsWithin(expected, value - TimeSpan.FromMilliseconds(1), TimeSpan.Zero));

            Assert.True(NeonHelper.IsWithin(expected, value, TimeSpan.Zero));
            Assert.True(NeonHelper.IsWithin(expected, value + TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1)));
            Assert.True(NeonHelper.IsWithin(expected, value - TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1)));
            Assert.False(NeonHelper.IsWithin(expected, value + TimeSpan.FromMilliseconds(1.5), TimeSpan.FromMilliseconds(1)));
            Assert.False(NeonHelper.IsWithin(expected, value - TimeSpan.FromMilliseconds(1.5), TimeSpan.FromMilliseconds(1)));
        }

        [Fact]
        public void Within_DateTimeOffset()
        {
            var expected = new DateTimeOffset(2020, 12, 4, 10, 58, 0, TimeSpan.FromHours(-7));
            var value    = expected;

            Assert.True(NeonHelper.IsWithin(expected, value, TimeSpan.Zero));
            Assert.False(NeonHelper.IsWithin(expected, value + TimeSpan.FromMilliseconds(1), TimeSpan.Zero));
            Assert.False(NeonHelper.IsWithin(expected, value - TimeSpan.FromMilliseconds(1), TimeSpan.Zero));

            Assert.True(NeonHelper.IsWithin(expected, value, TimeSpan.Zero));
            Assert.True(NeonHelper.IsWithin(expected, value + TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1)));
            Assert.True(NeonHelper.IsWithin(expected, value - TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1)));
            Assert.False(NeonHelper.IsWithin(expected, value + TimeSpan.FromMilliseconds(1.5), TimeSpan.FromMilliseconds(1)));
            Assert.False(NeonHelper.IsWithin(expected, value - TimeSpan.FromMilliseconds(1.5), TimeSpan.FromMilliseconds(1)));
        }

        [Fact]
        public void Base64Encoding()
        {
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("Hello World!"));

            Assert.Equal("Hello World!", NeonHelper.FromBase64(encoded));

            encoded = NeonHelper.ToBase64("Hello World!");

            Assert.Equal("Hello World!", NeonHelper.FromBase64(encoded));
        }

        [Fact]
        public void PartitionCount_Int()
        {
            Assert.Equal((int)0, NeonHelper.PartitionCount((int)0, (int)1));
            Assert.Equal((int)1, NeonHelper.PartitionCount((int)1, (int)1));
            Assert.Equal((int)2, NeonHelper.PartitionCount((int)2, (int)1));

            Assert.Equal((int)1, NeonHelper.PartitionCount((int)1, (int)2));
            Assert.Equal((int)1, NeonHelper.PartitionCount((int)2, (int)2));
            Assert.Equal((int)2, NeonHelper.PartitionCount((int)3, (int)2));
            Assert.Equal((int)2, NeonHelper.PartitionCount((int)4, (int)2));

            // Errors

            Assert.Throws<ArgumentException>(() => NeonHelper.PartitionCount((int)-1, (int)1));
            Assert.Throws<ArgumentException>(() => NeonHelper.PartitionCount((int)1, (int)0));
        }

        [Fact]
        public void PartitionCount_UInt()
        {
            Assert.Equal((uint)0, NeonHelper.PartitionCount((uint)0, (uint)1));
            Assert.Equal((uint)1, NeonHelper.PartitionCount((uint)1, (uint)1));
            Assert.Equal((uint)2, NeonHelper.PartitionCount((uint)2, (uint)1));

            Assert.Equal((uint)1, NeonHelper.PartitionCount((uint)1, (uint)2));
            Assert.Equal((uint)1, NeonHelper.PartitionCount((uint)2, (uint)2));
            Assert.Equal((uint)2, NeonHelper.PartitionCount((uint)3, (uint)2));
            Assert.Equal((uint)2, NeonHelper.PartitionCount((uint)4, (uint)2));

            // Errors

            Assert.Throws<ArgumentException>(() => NeonHelper.PartitionCount((uint)1, (uint)0));
        }

        [Fact]
        public void PartitionCount_Long()
        {
            Assert.Equal((long)0, NeonHelper.PartitionCount((long)0, (long)1));
            Assert.Equal((long)1, NeonHelper.PartitionCount((long)1, (long)1));
            Assert.Equal((long)2, NeonHelper.PartitionCount((long)2, (long)1));

            Assert.Equal((long)1, NeonHelper.PartitionCount((long)1, (long)2));
            Assert.Equal((long)1, NeonHelper.PartitionCount((long)2, (long)2));
            Assert.Equal((long)2, NeonHelper.PartitionCount((long)3, (long)2));
            Assert.Equal((long)2, NeonHelper.PartitionCount((long)4, (long)2));

            // Errors

            Assert.Throws<ArgumentException>(() => NeonHelper.PartitionCount((long)-1, (long)1));
            Assert.Throws<ArgumentException>(() => NeonHelper.PartitionCount((long)1, (long)0));
        }

        [Fact]
        public void PartitionCount_ULong()
        {
            Assert.Equal((ulong)0, NeonHelper.PartitionCount((ulong)0, (ulong)1));
            Assert.Equal((ulong)1, NeonHelper.PartitionCount((ulong)1, (ulong)1));
            Assert.Equal((ulong)2, NeonHelper.PartitionCount((ulong)2, (ulong)1));

            Assert.Equal((ulong)1, NeonHelper.PartitionCount((ulong)1, (ulong)2));
            Assert.Equal((ulong)1, NeonHelper.PartitionCount((ulong)2, (ulong)2));
            Assert.Equal((ulong)2, NeonHelper.PartitionCount((ulong)3, (ulong)2));
            Assert.Equal((ulong)2, NeonHelper.PartitionCount((ulong)4, (ulong)2));

            // Errors

            Assert.Throws<ArgumentException>(() => NeonHelper.PartitionCount((ulong)1, (ulong)0));
        }

        [Fact]
        public void TimeSpan_Min()
        {
            Assert.Equal(TimeSpan.Zero, NeonHelper.Min());
            Assert.Equal(TimeSpan.Zero, NeonHelper.Min(new TimeSpan[0]));
            Assert.Equal(TimeSpan.FromSeconds(-1), NeonHelper.Min(TimeSpan.FromSeconds(-1)));
            Assert.Equal(TimeSpan.FromSeconds(-1), NeonHelper.Min(TimeSpan.FromSeconds(-1), TimeSpan.Zero));
            Assert.Equal(TimeSpan.FromSeconds(-1), NeonHelper.Min(TimeSpan.Zero, TimeSpan.FromSeconds(-1)));
            Assert.Equal(TimeSpan.FromSeconds(1), NeonHelper.Min(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3)));
            Assert.Equal(TimeSpan.FromSeconds(1), NeonHelper.Min(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1)));
        }

        [Fact]
        public void TimeSpan_Max()
        {
            Assert.Equal(TimeSpan.Zero, NeonHelper.Max());
            Assert.Equal(TimeSpan.Zero, NeonHelper.Max(new TimeSpan[0]));
            Assert.Equal(TimeSpan.FromSeconds(-1), NeonHelper.Max(TimeSpan.FromSeconds(-1)));
            Assert.Equal(TimeSpan.Zero, NeonHelper.Max(TimeSpan.FromSeconds(-1), TimeSpan.Zero));
            Assert.Equal(TimeSpan.Zero, NeonHelper.Max(TimeSpan.Zero, TimeSpan.FromSeconds(-1)));
            Assert.Equal(TimeSpan.FromSeconds(3), NeonHelper.Max(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3)));
            Assert.Equal(TimeSpan.FromSeconds(3), NeonHelper.Max(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1)));
        }

        private enum TestEnum
        {
            Zero,
            One,
            Two
        }

        private enum TestEnum_WithAttributes
        {
            [EnumMember(Value = "_ZERO")]
            Zero,

            [EnumMember(Value = "_ONE")]
            One,

            [EnumMember(Value = "_TWO")]
            Two
        }

        private enum TestEnum_MixedAttributes
        {
            [EnumMember(Value = "_ZERO")]
            Zero,

            One,

            [EnumMember(Value = "_TWO")]
            Two
        }

        [Fact]
        public void EnumMember_WithoutAttributes()
        {
            //-----------------------------------------------------------------
            // Parsing

            Assert.Equal(TestEnum.Zero, NeonHelper.ParseEnum<TestEnum>("Zero"));
            Assert.Equal(TestEnum.One, NeonHelper.ParseEnum<TestEnum>("One"));
            Assert.Equal(TestEnum.Two, NeonHelper.ParseEnum<TestEnum>("Two"));

            Assert.True(NeonHelper.TryParseEnum(typeof(TestEnum), "Zero", out var untyped));
            Assert.Equal(TestEnum.Zero, (TestEnum)untyped);

            Assert.True(NeonHelper.TryParseEnum(typeof(TestEnum), "One", out untyped));
            Assert.Equal(TestEnum.One, (TestEnum)untyped);

            Assert.True(NeonHelper.TryParseEnum(typeof(TestEnum), "Two", out untyped));
            Assert.Equal(TestEnum.Two, (TestEnum)untyped);

            Assert.True(NeonHelper.TryParseEnum<TestEnum>("Zero", out var value));
            Assert.Equal(TestEnum.Zero, value);

            Assert.True(NeonHelper.TryParseEnum<TestEnum>("One", out value));
            Assert.Equal(TestEnum.One, value);

            Assert.True(NeonHelper.TryParseEnum<TestEnum>("Two", out value));
            Assert.Equal(TestEnum.Two, value);

            //-----------------------------------------------------------------
            // Parsing Errors

            Assert.Throws<ArgumentException>(() => NeonHelper.ParseEnum<TestEnum>("ABC"));
            Assert.False(NeonHelper.TryParseEnum(typeof(TestEnum), "ABC", out untyped));
            Assert.False(NeonHelper.TryParseEnum<TestEnum>("ABC", out value));

            //-----------------------------------------------------------------
            // Enum names

            Assert.Equal(new string[] { "Zero", "One", "Two" }, NeonHelper.GetEnumNames<TestEnum>());
        }

        [Fact]
        public void EnumMember_WithAttributes()
        {
            //-----------------------------------------------------------------
            // Parsing

            Assert.Equal(TestEnum_WithAttributes.Zero, NeonHelper.ParseEnum<TestEnum_WithAttributes>("_ZERO"));
            Assert.Equal(TestEnum_WithAttributes.One, NeonHelper.ParseEnum<TestEnum_WithAttributes>("_ONE"));
            Assert.Equal(TestEnum_WithAttributes.Two, NeonHelper.ParseEnum<TestEnum_WithAttributes>("_TWO"));

            Assert.True(NeonHelper.TryParseEnum(typeof(TestEnum_WithAttributes), "_ZERO", out var untyped));
            Assert.Equal(TestEnum_WithAttributes.Zero, (TestEnum_WithAttributes)untyped);

            Assert.True(NeonHelper.TryParseEnum(typeof(TestEnum_WithAttributes), "_ONE", out untyped));
            Assert.Equal(TestEnum_WithAttributes.One, (TestEnum_WithAttributes)untyped);

            Assert.True(NeonHelper.TryParseEnum(typeof(TestEnum_WithAttributes), "_TWO", out untyped));
            Assert.Equal(TestEnum_WithAttributes.Two, (TestEnum_WithAttributes)untyped);

            Assert.True(NeonHelper.TryParseEnum<TestEnum_WithAttributes>("_ZERO", out var value));
            Assert.Equal(TestEnum_WithAttributes.Zero, value);

            Assert.True(NeonHelper.TryParseEnum<TestEnum_WithAttributes>("_ONE", out value));
            Assert.Equal(TestEnum_WithAttributes.One, value);

            Assert.True(NeonHelper.TryParseEnum<TestEnum_WithAttributes>("_TWO", out value));
            Assert.Equal(TestEnum_WithAttributes.Two, value);

            //-----------------------------------------------------------------
            // Parsing Errors

            Assert.Throws<ArgumentException>(() => NeonHelper.ParseEnum<TestEnum_WithAttributes>("ABC"));
            Assert.False(NeonHelper.TryParseEnum(typeof(TestEnum_WithAttributes), "ABC", out untyped));
            Assert.False(NeonHelper.TryParseEnum<TestEnum_WithAttributes>("ABC", out value));

            //-----------------------------------------------------------------
            // Enum names

            Assert.Equal(new string[] { "_ZERO", "_ONE", "_TWO" }, NeonHelper.GetEnumNames<TestEnum_WithAttributes>());
        }

        [Fact]
        public void EnumMember_MixedAttributes()
        {
            //-----------------------------------------------------------------
            // Parsing

            Assert.Equal(TestEnum_MixedAttributes.Zero, NeonHelper.ParseEnum<TestEnum_MixedAttributes>("_ZERO"));
            Assert.Equal(TestEnum_MixedAttributes.One, NeonHelper.ParseEnum<TestEnum_MixedAttributes>("One"));
            Assert.Equal(TestEnum_MixedAttributes.Two, NeonHelper.ParseEnum<TestEnum_MixedAttributes>("_TWO"));

            Assert.True(NeonHelper.TryParseEnum(typeof(TestEnum_MixedAttributes), "_ZERO", out var untyped));
            Assert.Equal(TestEnum_MixedAttributes.Zero, (TestEnum_MixedAttributes)untyped);

            Assert.True(NeonHelper.TryParseEnum(typeof(TestEnum_MixedAttributes), "One", out untyped));
            Assert.Equal(TestEnum_MixedAttributes.One, (TestEnum_MixedAttributes)untyped);

            Assert.True(NeonHelper.TryParseEnum(typeof(TestEnum_MixedAttributes), "_TWO", out untyped));
            Assert.Equal(TestEnum_MixedAttributes.Two, (TestEnum_MixedAttributes)untyped);

            Assert.True(NeonHelper.TryParseEnum<TestEnum_MixedAttributes>("_ZERO", out var value));
            Assert.Equal(TestEnum_MixedAttributes.Zero, value);

            Assert.True(NeonHelper.TryParseEnum<TestEnum_MixedAttributes>("One", out value));
            Assert.Equal(TestEnum_MixedAttributes.One, value);

            Assert.True(NeonHelper.TryParseEnum<TestEnum_MixedAttributes>("_TWO", out value));
            Assert.Equal(TestEnum_MixedAttributes.Two, value);

            //-----------------------------------------------------------------
            // Parsing Errors

            Assert.Throws<ArgumentException>(() => NeonHelper.ParseEnum<TestEnum_MixedAttributes>("ABC"));
            Assert.False(NeonHelper.TryParseEnum(typeof(TestEnum_MixedAttributes), "ABC", out untyped));
            Assert.False(NeonHelper.TryParseEnum<TestEnum_MixedAttributes>("ABC", out value));

            //-----------------------------------------------------------------
            // Enum names

            Assert.Equal(new string[] { "_ZERO", "One", "_TWO" }, NeonHelper.GetEnumNames<TestEnum_MixedAttributes>());
        }
    }
}
