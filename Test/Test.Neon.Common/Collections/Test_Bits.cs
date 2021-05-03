//-----------------------------------------------------------------------------
// FILE:	    Test_Bits.cs
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
using System.Net;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Collections;
using Neon.Net;
using Neon.Retry;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public class Test_Bits
    {
        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCommon)]
        public void Bits_Basic()
        {
            Bits b1;
            Bits b2;
            Bits b3;

            b1 = new Bits(10);
            Assert.Equal(10, b1.Length);
            Assert.True(b1.IsAllZeros);
            Assert.False(b1.IsAllOnes);

            for (int i = 0; i < 10; i++)
            {
                Assert.False(b1[i]);
            }

            b1 = new Bits(new bool[] { true, false, true, true });
            Assert.Equal(4, b1.Length);
            Assert.True(b1[0]);
            Assert.False(b1[1]);
            Assert.True(b1[2]);
            Assert.True(b1[3]);
            Assert.False(b1.IsAllZeros);
            Assert.False(b1.IsAllOnes);

            b1 = new Bits(10);
            b2 = b1.Not();
            Assert.Equal(10, b2.Length);
            Assert.True(b2.IsAllOnes);

            b2 = new Bits(10);
            for (int i = 0; i < 10; i++)
            {
                b2[i] = true;
            }

            b3 = b1.Or(b2);
            Assert.Equal(10, b3.Length);
            Assert.True(b3.IsAllOnes);

            b1 = (Bits)"010100001111";
            Assert.Equal(12, b1.Length);
            Assert.False(b1[0]);
            Assert.True(b1[1]);
            Assert.False(b1[2]);
            Assert.True(b1[11]);

            Assert.Equal("010100001111", b1.ToString());
            Assert.Equal("010100001111", (string)b1);

            Assert.True(new Bits("000000000000000000000000000000000000000000000000000000000000000").IsAllZeros);
            Assert.False(new Bits("000000000000000000000000000000000000000000000000000000000000000").IsAllOnes);
            Assert.False(new Bits("000000000000000000000000000000000000000000000010000000000000000").IsAllZeros);
            Assert.True(new Bits("1111111111111111111111111111111111111111111111111111111111111111").IsAllOnes);
            Assert.False(new Bits("111111111111111111111111111111101111111111111111111111111111111").IsAllOnes);

            // Make sure we get correct bit positions for bitmaps with
            // lengths up to 256 bits.

            for (int i = 0; i < 256; i++)
            {
                for (int j = 0; j < i; j++)
                {
                    bool[] array = new bool[i];
                    StringBuilder sb;

                    array[j] = true;
                    b1 = new Bits(array);

                    sb = new StringBuilder(i);
                    for (int k = 0; k < i; k++)
                        sb.Append(k == j ? '1' : '0');

                    Assert.Equal(sb.ToString(), (string)b1);
                    b2 = new Bits(sb.ToString());
                    Assert.Equal(b1, b2);

                    for (int k = 0; k < i; k++)
                        if (k == j)
                            Assert.True(b1[k]);
                        else
                            Assert.False(b1[k]);
                }
            }
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCommon)]
        public void Bits_Not()
        {
            Bits b1;
            Bits b2;

            b1 = (Bits)"0000111100001111000011110000111100001111000011110000111100001111";
            b2 = b1.Not();
            Assert.Equal("1111000011110000111100001111000011110000111100001111000011110000", b2.ToString());

            b1 = (Bits)"0000111100001111000011110000111100001111000011110000111100001111";
            b2 = ~b1;
            Assert.Equal("1111000011110000111100001111000011110000111100001111000011110000", b2.ToString());
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCommon)]
        public void Bits_Or()
        {
            Bits b1;
            Bits b2;

            b1 = (Bits)"11001100";
            b2 = (Bits)"00110011";
            Assert.Equal("11111111", (string)(b1 | b2));

            b1 = (Bits)"01010101";
            b2 = (Bits)"00000000";
            Assert.Equal("01010101", (string)(b1 | b2));

            b1 = (Bits)"01010101";
            b2 = (Bits)"00000000";
            Assert.Equal("01010101", (string)(b1 | b2));

            b1 = (Bits)"11110000111100001111000011110000111100001111000011110000";
            b2 = (Bits)"01010101010101010101010101010101010101010101010101010101";
            Assert.Equal("11110101111101011111010111110101111101011111010111110101", (string)b1.Or(b2));

            b1 = (Bits)"01010101";
            b2 = (Bits)"00000000";
            Assert.Equal("01010101", (string)b1.Or(b2));

            Assert.Throws<InvalidOperationException>(() => b1 = (Bits)"01" | (Bits)"0");
            Assert.Throws<InvalidOperationException>(() => b1 = ((Bits)"01").Or((Bits)"0"));
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCommon)]
        public void Bits_And()
        {
            Bits b1;
            Bits b2;

            b1 = (Bits)"11001100";
            b2 = (Bits)"00110011";
            Assert.Equal("00000000", (string)(b1 & b2));

            b1 = (Bits)"01010101";
            b2 = (Bits)"01000100";
            Assert.Equal("01000100", (string)(b1 & b2));

            b1 = (Bits)"11001100";
            b2 = (Bits)"11111111";
            Assert.Equal("11001100", (string)(b1 & b2));

            b1 = (Bits)"11110000111100001111000011110000111100001111000011110000";
            b2 = (Bits)"01010101010101010101010101010101010101010101010101010101";
            Assert.Equal("01010000010100000101000001010000010100000101000001010000", (string)b1.And(b2));

            b1 = (Bits)"01010101";
            b2 = (Bits)"00001111";
            Assert.Equal("00000101", (string)b1.And(b2));

            Assert.Throws<InvalidOperationException>(() => b1 = (Bits)"01" & (Bits)"0");
            Assert.Throws<InvalidOperationException>(() => b1 = ((Bits)"01").And((Bits)"0"));
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCommon)]
        public void Bits_Xor()
        {
            Bits b1;
            Bits b2;

            b1 = (Bits)"11001100";
            b2 = (Bits)"00110011";
            Assert.Equal("11111111", (string)(b1 ^ b2));

            b1 = (Bits)"01010101";
            b2 = (Bits)"01000100";
            Assert.Equal("00010001", (string)(b1 ^ b2));

            b1 = (Bits)"11001100";
            b2 = (Bits)"11111111";
            Assert.Equal("00110011", (string)(b1 ^ b2));

            b1 = (Bits)"11110000111100001111000011110000111100001111000011110000";
            b2 = (Bits)"01010101010101010101010101010101010101010101010101010101";
            Assert.Equal("10100101101001011010010110100101101001011010010110100101", (string)b1.Xor(b2));

            b1 = (Bits)"01010101";
            b2 = (Bits)"00001111";
            Assert.Equal("01011010", (string)b1.Xor(b2));

            Assert.Throws<InvalidOperationException>(() => b1 = (Bits)"01" ^ (Bits)"0");
            Assert.Throws<InvalidOperationException>(() => b1 = ((Bits)"01").Xor((Bits)"0"));
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCommon)]
        public void Bits_EQU()
        {
            Assert.True((Bits)null == (Bits)null);
            Assert.False((Bits)null == (Bits)"0011");
            Assert.False((Bits)"0011" == (Bits)null);
            Assert.False((Bits)"0011" == (Bits)"00110");
            Assert.True((Bits)"0011" == (Bits)"0011");
            Assert.True((Bits)"0011001100110011001100110011000000000000" == (Bits)"0011001100110011001100110011000000000000");
            Assert.False((Bits)"0011001100110011001100110011000000000000" == (Bits)"0011001100110011001100110011000000000001");
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCommon)]
        public void Bits_NEQ()
        {
            Assert.False((Bits)null != (Bits)null);
            Assert.True((Bits)null != (Bits)"0011");
            Assert.True((Bits)"0011" != (Bits)null);
            Assert.True((Bits)"0011" != (Bits)"00110");
            Assert.False((Bits)"0011" != (Bits)"0011");
            Assert.False((Bits)"0011001100110011001100110011000000000000" != (Bits)"0011001100110011001100110011000000000000");
            Assert.True((Bits)"0011001100110011001100110011000000000000" != (Bits)"0011001100110011001100110011000000000001");
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCommon)]
        public void Bits_Set()
        {
            var b = new Bits(64);

            b.SetAll();
            Assert.Equal("1111111111111111111111111111111111111111111111111111111111111111", (string)b);

            b.ClearAll();
            b.SetRange(1, 5);
            Assert.Equal("0111110000000000000000000000000000000000000000000000000000000000", (string)b);
            b.SetRange(10, 5);
            Assert.Equal("0111110000111110000000000000000000000000000000000000000000000000", (string)b);
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCommon)]
        public void Bits_Clear()
        {
            var b = new Bits(64);

            b.SetAll();
            Assert.Equal("1111111111111111111111111111111111111111111111111111111111111111", (string)b);
            b.ClearAll();
            Assert.Equal("0000000000000000000000000000000000000000000000000000000000000000", (string)b);

            b.SetAll();
            b.ClearRange(1, 5);
            Assert.Equal("1000001111111111111111111111111111111111111111111111111111111111", (string)b);
            b.ClearRange(10, 5);
            Assert.Equal("1000001111000001111111111111111111111111111111111111111111111111", (string)b);
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCommon)]
        public void Bits_ShiftLeft()
        {
            var b = new Bits("001100111001111001111100111111001111111");

            Assert.Equal("001100111001111001111100111111001111111", (string)(b << 0));
            Assert.Equal("011001110011110011111001111110011111110", (string)(b << 1));
            Assert.Equal("110011100111100111110011111100111111100", (string)(b << 2));
            Assert.Equal("100111001111001111100111111001111111000", (string)(b << 3));
            Assert.Equal("001110011110011111001111110011111110000", (string)(b << 4));
            Assert.Equal("011100111100111110011111100111111100000", (string)(b << 5));

            var s = b.ToString();

            for (int i = 0; i < 100; i++)
            {
                Assert.Equal(s, (string)(b << i));
                s = s.Substring(1) + "0";
            }

            Assert.Throws<ArgumentException>(() => { var output = b << -1; });
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCommon)]
        public void Bits_ShiftRight()
        {
            var b = new Bits("001100111001111001111100111111001111111");

            Assert.Equal("001100111001111001111100111111001111111", (string)(b >> 0));
            Assert.Equal("000110011100111100111110011111100111111", (string)(b >> 1));
            Assert.Equal("000011001110011110011111001111110011111", (string)(b >> 2));
            Assert.Equal("000001100111001111001111100111111001111", (string)(b >> 3));
            Assert.Equal("000000110011100111100111110011111100111", (string)(b >> 4));
            Assert.Equal("000000011001110011110011111001111110011", (string)(b >> 5));

            var s = b.ToString();

            for (int i = 0; i < 100; i++)
            {
                Assert.Equal(s, (string)(b >> i));
                s = "0" + s.Substring(0, s.Length - 1);
            }

            Assert.Throws<ArgumentException>(() => { var output = b >> -1; });
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCommon)]
        public void Bits_SerializeBytes()
        {
            byte[] input;
            Bits bits;

            input = Array.Empty<byte>();
            bits = new Bits(input, 0);
            Assert.Empty(input);
            Assert.Equal("", bits.ToString());
            Assert.Equal(input, bits.ToBytes());

            input = new byte[] { 0x80, 0x00 };
            bits = new Bits(input);
            Assert.Equal(16, bits.Length);
            Assert.Equal("1000000000000000", bits.ToString());
            Assert.Equal(input, bits.ToBytes());

            input = new byte[] { 0x80 };
            bits = new Bits(input, 4);
            Assert.Equal(4, bits.Length);
            Assert.Equal("1000", bits.ToString());
            Assert.Equal(input, bits.ToBytes());

            input = new byte[] { 0x80 };
            bits = new Bits(input, 8);
            Assert.Equal(8, bits.Length);
            Assert.Equal("10000000", bits.ToString());
            Assert.Equal(input, bits.ToBytes());

            input = new byte[] { 0x83 };
            bits = new Bits(input, 8);
            Assert.Equal(8, bits.Length);
            Assert.Equal("10000011", bits.ToString());
            Assert.Equal(input, bits.ToBytes());

            input = new byte[] { 0x83, 0x0F };
            bits = new Bits(input, 16);
            Assert.Equal(16, bits.Length);
            Assert.Equal("1000001100001111", bits.ToString());
            Assert.Equal(input, bits.ToBytes());

            input = new byte[] { 0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80, 0xFF };
            bits = new Bits(input, input.Length * 8);
            Assert.Equal(input.Length * 8, bits.Length);
            Assert.Equal("000000010000001000000100000010000001000000100000010000001000000011111111", bits.ToString());
            Assert.Equal(input, bits.ToBytes());
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCommon)]
        public void Bits_Resize()
        {
            Bits input;
            Bits output;

            input = new Bits("10101010");

            output = input.Resize(0);
            Assert.Equal("", output.ToString());

            output = input.Resize(1);
            Assert.Equal("1", output.ToString());

            output = input.Resize(2);
            Assert.Equal("10", output.ToString());

            output = input.Resize(4);
            Assert.Equal("1010", output.ToString());

            output = input.Resize(8);
            Assert.Equal("10101010", output.ToString());

            output = input.Resize(16);
            Assert.Equal("1010101000000000", output.ToString());
        }
    }
}

