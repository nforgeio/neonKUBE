//-----------------------------------------------------------------------------
// FILE:        Test_CryptoHelper.cs
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
    public class Test_CryptoHelper
    {
        private string hashInputString1 = "how now brown cow. how now brown cow. how now brown cow. how now brown cow. how now brown cow. how now brown cow.";
        private string hashInputString2 = "a stitch  in time saves nine. a stitch  in time saves nine. a stitch  in time saves nine. a stitch  in time saves nine.";
        private byte[] hashInputBytes1;
        private byte[] hashInputBytes2;

        public Test_CryptoHelper()
        {
            hashInputBytes1 = Encoding.UTF8.GetBytes(hashInputString1);
            hashInputBytes2 = Encoding.UTF8.GetBytes(hashInputString2);
        }

        [Fact]
        [Trait(TestTrait.Project, TestProject.NeonCryptography)]
        public void ComputeMD5()
        {
            var zeroString = new string('0', CryptoHelper.MD5ByteSize * 2);
            var zeroBytes  = new byte[CryptoHelper.MD5ByteSize];

            // Verify that we get a zero hash when there's no input data.

            Assert.Equal(zeroString, CryptoHelper.ComputeMD5String((string)null));
            Assert.Equal(zeroString, CryptoHelper.ComputeMD5String(string.Empty));
            Assert.Equal(zeroString, CryptoHelper.ComputeMD5String((byte[])null));
            Assert.Equal(zeroString, CryptoHelper.ComputeMD5String(Array.Empty<byte>()));

            using (var ms = new MemoryStream())
            {
                Assert.Equal(zeroString, CryptoHelper.ComputeMD5String(ms));
            }

            Assert.Equal(zeroBytes, CryptoHelper.ComputeMD5Bytes((string)null));
            Assert.Equal(zeroBytes, CryptoHelper.ComputeMD5Bytes(string.Empty));
            Assert.Equal(zeroBytes, CryptoHelper.ComputeMD5Bytes((byte[])null));
            Assert.Equal(zeroBytes, CryptoHelper.ComputeMD5Bytes(Array.Empty<byte>()));

            using (var ms = new MemoryStream())
            {
                Assert.Equal(zeroBytes, CryptoHelper.ComputeMD5Bytes(ms));
            }

            // Verify that we get a decent looking hash when there's input data.

            var hash1Bytes  = CryptoHelper.ComputeMD5Bytes(hashInputString1);
            var hash1String = NeonHelper.ToHex(hash1Bytes);

            Assert.NotEmpty(hash1Bytes);
            Assert.Equal(CryptoHelper.MD5ByteSize, hash1Bytes.Length);
            Assert.True(hash1Bytes.Where(b => b != 0).Count() > 0);       // Ensure that there's at least 1 non-zero byte.

            // Verify that all methods return the same hash for the same input.

            Assert.Equal(hash1Bytes, CryptoHelper.ComputeMD5Bytes(hashInputBytes1));
            Assert.Equal(hash1Bytes, CryptoHelper.ComputeMD5Bytes(hashInputString1));

            using (var ms = new MemoryStream(hashInputBytes1))
            {
                Assert.Equal(hash1Bytes, CryptoHelper.ComputeMD5Bytes(ms));
            }

            Assert.Equal(hash1String, CryptoHelper.ComputeMD5String(hashInputBytes1));
            Assert.Equal(hash1String, CryptoHelper.ComputeMD5String(hashInputString1));

            using (var ms = new MemoryStream(hashInputBytes1))
            {
                Assert.Equal(hash1String, CryptoHelper.ComputeMD5String(ms));
            }

            // Verify that we get a different hash for a different input.

            Assert.NotEqual(hash1Bytes, CryptoHelper.ComputeMD5Bytes(hashInputBytes2));
            Assert.NotEqual(hash1Bytes, CryptoHelper.ComputeMD5Bytes(hashInputString2));

            using (var ms = new MemoryStream(hashInputBytes1))
            {
                Assert.Equal(hash1Bytes, CryptoHelper.ComputeMD5Bytes(ms));
            }

            Assert.NotEqual(hash1String, CryptoHelper.ComputeMD5String(hashInputBytes2));
            Assert.NotEqual(hash1String, CryptoHelper.ComputeMD5String(hashInputString2));

            using (var ms = new MemoryStream(hashInputBytes2))
            {
                Assert.NotEqual(hash1String, CryptoHelper.ComputeMD5String(ms));
            }
        }

        [Fact]
        [Trait(TestTrait.Project, TestProject.NeonCryptography)]
        public void ComputeSHA1()
        {
            var zeroString = new string('0', CryptoHelper.SHA1ByteSize * 2);
            var zeroBytes  = new byte[CryptoHelper.SHA1ByteSize];

            // Verify that we get a zero hash when there's no input data.

            Assert.Equal(zeroString, CryptoHelper.ComputeSHA1String((string)null));
            Assert.Equal(zeroString, CryptoHelper.ComputeSHA1String(string.Empty));
            Assert.Equal(zeroString, CryptoHelper.ComputeSHA1String((byte[])null));
            Assert.Equal(zeroString, CryptoHelper.ComputeSHA1String(Array.Empty<byte>()));

            using (var ms = new MemoryStream())
            {
                Assert.Equal(zeroString, CryptoHelper.ComputeSHA1String(ms));
            }

            Assert.Equal(zeroBytes, CryptoHelper.ComputeSHA1Bytes((string)null));
            Assert.Equal(zeroBytes, CryptoHelper.ComputeSHA1Bytes(string.Empty));
            Assert.Equal(zeroBytes, CryptoHelper.ComputeSHA1Bytes((byte[])null));
            Assert.Equal(zeroBytes, CryptoHelper.ComputeSHA1Bytes(Array.Empty<byte>()));

            using (var ms = new MemoryStream())
            {
                Assert.Equal(zeroBytes, CryptoHelper.ComputeSHA1Bytes(ms));
            }

            // Verify that we get a decent looking hash when there's input data.

            var hash1Bytes  = CryptoHelper.ComputeSHA1Bytes(hashInputString1);
            var hash1String = NeonHelper.ToHex(hash1Bytes);

            Assert.NotEmpty(hash1Bytes);
            Assert.Equal(CryptoHelper.SHA1ByteSize, hash1Bytes.Length);
            Assert.True(hash1Bytes.Where(b => b != 0).Count() > 0);       // Ensure that there's at least 1 non-zero byte.

            // Verify that all methods return the same hash for the same input.

            Assert.Equal(hash1Bytes, CryptoHelper.ComputeSHA1Bytes(hashInputBytes1));
            Assert.Equal(hash1Bytes, CryptoHelper.ComputeSHA1Bytes(hashInputString1));

            using (var ms = new MemoryStream(hashInputBytes1))
            {
                Assert.Equal(hash1Bytes, CryptoHelper.ComputeSHA1Bytes(ms));
            }

            Assert.Equal(hash1String, CryptoHelper.ComputeSHA1String(hashInputBytes1));
            Assert.Equal(hash1String, CryptoHelper.ComputeSHA1String(hashInputString1));

            using (var ms = new MemoryStream(hashInputBytes1))
            {
                Assert.Equal(hash1String, CryptoHelper.ComputeSHA1String(ms));
            }

            // Verify that we get a different hash for a different input.

            Assert.NotEqual(hash1Bytes, CryptoHelper.ComputeSHA1Bytes(hashInputBytes2));
            Assert.NotEqual(hash1Bytes, CryptoHelper.ComputeSHA1Bytes(hashInputString2));

            using (var ms = new MemoryStream(hashInputBytes1))
            {
                Assert.Equal(hash1Bytes, CryptoHelper.ComputeSHA1Bytes(ms));
            }

            Assert.NotEqual(hash1String, CryptoHelper.ComputeSHA1String(hashInputBytes2));
            Assert.NotEqual(hash1String, CryptoHelper.ComputeSHA1String(hashInputString2));

            using (var ms = new MemoryStream(hashInputBytes2))
            {
                Assert.NotEqual(hash1String, CryptoHelper.ComputeSHA1String(ms));
            }
        }

        [Fact]
        [Trait(TestTrait.Project, TestProject.NeonCryptography)]
        public void ComputeSHA256()
        {
            var zeroString = new string('0', CryptoHelper.SHA256ByteSize * 2);
            var zeroBytes = new byte[CryptoHelper.SHA256ByteSize];

            // Verify that we get a zero hash when there's no input data.

            Assert.Equal(zeroString, CryptoHelper.ComputeSHA256String((string)null));
            Assert.Equal(zeroString, CryptoHelper.ComputeSHA256String(string.Empty));
            Assert.Equal(zeroString, CryptoHelper.ComputeSHA256String((byte[])null));
            Assert.Equal(zeroString, CryptoHelper.ComputeSHA256String(Array.Empty<byte>()));

            using (var ms = new MemoryStream())
            {
                Assert.Equal(zeroString, CryptoHelper.ComputeSHA256String(ms));
            }

            Assert.Equal(zeroBytes, CryptoHelper.ComputeSHA256Bytes((string)null));
            Assert.Equal(zeroBytes, CryptoHelper.ComputeSHA256Bytes(string.Empty));
            Assert.Equal(zeroBytes, CryptoHelper.ComputeSHA256Bytes((byte[])null));
            Assert.Equal(zeroBytes, CryptoHelper.ComputeSHA256Bytes(Array.Empty<byte>()));

            using (var ms = new MemoryStream())
            {
                Assert.Equal(zeroBytes, CryptoHelper.ComputeSHA256Bytes(ms));
            }

            // Verify that we get a decent looking hash when there's input data.

            var hash1Bytes  = CryptoHelper.ComputeSHA256Bytes(hashInputString1);
            var hash1String = NeonHelper.ToHex(hash1Bytes);

            Assert.NotEmpty(hash1Bytes);
            Assert.Equal(CryptoHelper.SHA256ByteSize, hash1Bytes.Length);
            Assert.True(hash1Bytes.Where(b => b != 0).Count() > 0);       // Ensure that there's at least 1 non-zero byte.

            // Verify that all methods return the same hash for the same input.

            Assert.Equal(hash1Bytes, CryptoHelper.ComputeSHA256Bytes(hashInputBytes1));
            Assert.Equal(hash1Bytes, CryptoHelper.ComputeSHA256Bytes(hashInputString1));

            using (var ms = new MemoryStream(hashInputBytes1))
            {
                Assert.Equal(hash1Bytes, CryptoHelper.ComputeSHA256Bytes(ms));
            }

            Assert.Equal(hash1String, CryptoHelper.ComputeSHA256String(hashInputBytes1));
            Assert.Equal(hash1String, CryptoHelper.ComputeSHA256String(hashInputString1));

            using (var ms = new MemoryStream(hashInputBytes1))
            {
                Assert.Equal(hash1String, CryptoHelper.ComputeSHA256String(ms));
            }

            // Verify that we get a different hash for a different input.

            Assert.NotEqual(hash1Bytes, CryptoHelper.ComputeSHA256Bytes(hashInputBytes2));
            Assert.NotEqual(hash1Bytes, CryptoHelper.ComputeSHA256Bytes(hashInputString2));

            using (var ms = new MemoryStream(hashInputBytes1))
            {
                Assert.Equal(hash1Bytes, CryptoHelper.ComputeSHA256Bytes(ms));
            }

            Assert.NotEqual(hash1String, CryptoHelper.ComputeSHA256String(hashInputBytes2));
            Assert.NotEqual(hash1String, CryptoHelper.ComputeSHA256String(hashInputString2));

            using (var ms = new MemoryStream(hashInputBytes2))
            {
                Assert.NotEqual(hash1String, CryptoHelper.ComputeSHA256String(ms));
            }
        }

        [Fact]
        [Trait(TestTrait.Project, TestProject.NeonCryptography)]
        public void ComputeSHA512()
        {
            var zeroString = new string('0', CryptoHelper.SHA512ByteSize * 2);
            var zeroBytes = new byte[CryptoHelper.SHA512ByteSize];

            // Verify that we get a zero hash when there's no input data.

            Assert.Equal(zeroString, CryptoHelper.ComputeSHA512String((string)null));
            Assert.Equal(zeroString, CryptoHelper.ComputeSHA512String(string.Empty));
            Assert.Equal(zeroString, CryptoHelper.ComputeSHA512String((byte[])null));
            Assert.Equal(zeroString, CryptoHelper.ComputeSHA512String(Array.Empty<byte>()));

            using (var ms = new MemoryStream())
            {
                Assert.Equal(zeroString, CryptoHelper.ComputeSHA512String(ms));
            }

            Assert.Equal(zeroBytes, CryptoHelper.ComputeSHA512Bytes((string)null));
            Assert.Equal(zeroBytes, CryptoHelper.ComputeSHA512Bytes(string.Empty));
            Assert.Equal(zeroBytes, CryptoHelper.ComputeSHA512Bytes((byte[])null));
            Assert.Equal(zeroBytes, CryptoHelper.ComputeSHA512Bytes(Array.Empty<byte>()));

            using (var ms = new MemoryStream())
            {
                Assert.Equal(zeroBytes, CryptoHelper.ComputeSHA512Bytes(ms));
            }

            // Verify that we get a decent looking hash when there's input data.

            var hash1Bytes  = CryptoHelper.ComputeSHA512Bytes(hashInputString1);
            var hash1String = NeonHelper.ToHex(hash1Bytes);

            Assert.NotEmpty(hash1Bytes);
            Assert.Equal(CryptoHelper.SHA512ByteSize, hash1Bytes.Length);
            Assert.True(hash1Bytes.Where(b => b != 0).Count() > 0);       // Ensure that there's at least 1 non-zero byte.

            // Verify that all methods return the same hash for the same input.

            Assert.Equal(hash1Bytes, CryptoHelper.ComputeSHA512Bytes(hashInputBytes1));
            Assert.Equal(hash1Bytes, CryptoHelper.ComputeSHA512Bytes(hashInputString1));

            using (var ms = new MemoryStream(hashInputBytes1))
            {
                Assert.Equal(hash1Bytes, CryptoHelper.ComputeSHA512Bytes(ms));
            }

            Assert.Equal(hash1String, CryptoHelper.ComputeSHA512String(hashInputBytes1));
            Assert.Equal(hash1String, CryptoHelper.ComputeSHA512String(hashInputString1));

            using (var ms = new MemoryStream(hashInputBytes1))
            {
                Assert.Equal(hash1String, CryptoHelper.ComputeSHA512String(ms));
            }

            // Verify that we get a different hash for a different input.

            Assert.NotEqual(hash1Bytes, CryptoHelper.ComputeSHA512Bytes(hashInputBytes2));
            Assert.NotEqual(hash1Bytes, CryptoHelper.ComputeSHA512Bytes(hashInputString2));

            using (var ms = new MemoryStream(hashInputBytes1))
            {
                Assert.Equal(hash1Bytes, CryptoHelper.ComputeSHA512Bytes(ms));
            }

            Assert.NotEqual(hash1String, CryptoHelper.ComputeSHA512String(hashInputBytes2));
            Assert.NotEqual(hash1String, CryptoHelper.ComputeSHA512String(hashInputString2));

            using (var ms = new MemoryStream(hashInputBytes2))
            {
                Assert.NotEqual(hash1String, CryptoHelper.ComputeSHA512String(ms));
            }
        }

        [Fact]
        [Trait(TestTrait.Project, TestProject.NeonCryptography)]
        public void DeriveKeyFromPassword()
        {
            // Verify the argument error cases.

            Assert.Throws<ArgumentNullException>(() => CryptoHelper.DeriveKeyFromPassword(null, 256));
            Assert.Throws<ArgumentNullException>(() => CryptoHelper.DeriveKeyFromPassword(string.Empty, 256));
            Assert.Throws<ArgumentException>(() => CryptoHelper.DeriveKeyFromPassword("test", 0));
            Assert.Throws<ArgumentException>(() => CryptoHelper.DeriveKeyFromPassword("test", -1));
            Assert.Throws<ArgumentException>(() => CryptoHelper.DeriveKeyFromPassword("test", 1));
            Assert.Throws<ArgumentException>(() => CryptoHelper.DeriveKeyFromPassword("test", 7));
            Assert.Throws<ArgumentException>(() => CryptoHelper.DeriveKeyFromPassword("test", 512 + 8));

            // Verify that keys are generated with the correct size.

            Assert.Equal(128 / 8, CryptoHelper.DeriveKeyFromPassword("test", 128).Length);
            Assert.Equal(256 / 8, CryptoHelper.DeriveKeyFromPassword("test", 256).Length);
            Assert.Equal(512 / 8, CryptoHelper.DeriveKeyFromPassword("test", 512).Length);

            // Generate keys from the same password and verify that they match.

            var key = CryptoHelper.DeriveKeyFromPassword("test", 256);

            Assert.Equal(key, CryptoHelper.DeriveKeyFromPassword("test", 256));

            // Verify that the key looks reasonable (e.g. it has at least one non-zero byte).

            Assert.True(key.Where(b => b != 0).Count() > 0);
        }
    }
}
