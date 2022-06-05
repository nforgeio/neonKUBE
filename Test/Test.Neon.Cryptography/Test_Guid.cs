//-----------------------------------------------------------------------------
// FILE:        Test_Guid.cs
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
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Xunit;

using Xunit;

namespace TestCryptography
{
    [Trait(TestTrait.Category, TestArea.NeonCryptography)]
    public class Test_Guid
    {
        private static void EnsureLowercase(string value)
        {
            Assert.NotNull(value);
            Assert.DoesNotContain(value, ch => char.IsUpper(ch));
        }

        [Fact]
        public void ToHex()
        {
            var guid = Guid.NewGuid();
            var hex  = guid.ToHex();

            Assert.NotNull(hex);
            Assert.Equal(32, hex.Length);
            EnsureLowercase(hex);

            byte[] bytes;

            Assert.True(NeonHelper.TryParseHex(hex, out bytes));
            Assert.Equal(guid.ToByteArray(), bytes);
        }

        [Fact]
        public void ToMd5Bytes()
        {
            var guid     = Guid.NewGuid();
            var md5Bytes = guid.ToMd5Bytes();

            Assert.NotNull(md5Bytes);
            Assert.Equal(16, md5Bytes.Length);
            Assert.Equal(CryptoHelper.ComputeMD5Bytes(guid.ToByteArray()), md5Bytes);
        }

        [Fact]
        public void ToMd5Hex()
        {
            var guid     = Guid.NewGuid();
            var md5Bytes = CryptoHelper.ComputeMD5Bytes(guid.ToByteArray());
            var md5Hex   = guid.ToMd5Hex();

            Assert.NotNull(md5Bytes);
            Assert.Equal(32, md5Hex.Length);
            EnsureLowercase(md5Hex);

            byte[] bytes;

            Assert.True(NeonHelper.TryParseHex(md5Hex, out bytes));
            Assert.Equal(md5Bytes, bytes);
        }

        [Fact]
        public void ToFoldedBytes()
        {
            var guid        = Guid.NewGuid();
            var md5Bytes    = CryptoHelper.ComputeMD5Bytes(guid.ToByteArray());
            var foldedBytes = guid.ToFoldedBytes();

            Assert.NotNull(foldedBytes);
            Assert.Equal(8, foldedBytes.Length);

            var expectedFolded = new byte[8];

            for (int i = 0; i < 8; i++)
            {
                expectedFolded[i] = (byte)( md5Bytes[i] ^ md5Bytes[i + 8]);
            }

            Assert.Equal(expectedFolded, foldedBytes);
        }

        [Fact]
        public void ToFoldedHex()
        {
            var guid       = Guid.NewGuid();
            var md5Bytes   = CryptoHelper.ComputeMD5Bytes(guid.ToByteArray());
            var foldedHex = guid.ToFoldedHex();

            Assert.NotNull(foldedHex);
            Assert.Equal(16, foldedHex.Length);
            EnsureLowercase(foldedHex);

            var expectedFolded = new byte[8];

            for (int i = 0; i < 8; i++)
            {
                expectedFolded[i] = (byte)( md5Bytes[i] ^ md5Bytes[i + 8]);
            }

            Assert.Equal(NeonHelper.ToHex(expectedFolded, uppercase: false), foldedHex);
        }
    }
}
