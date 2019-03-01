//-----------------------------------------------------------------------------
// FILE:        Test_AesCipher.cs
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
    public class Test_AesCipher
    {
        private int[] sizes = new int[] { 128, 192, 256 };


        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void LowLevel()
        {
            // Encrypt/decrypt using low-level methods to understand how this works.

            using (var aes = new AesManaged())
            {
                var key          = aes.Key;
                var IV           = aes.IV;
                var orgDecrypted = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
                var decrypted    = (byte[])null;
                var encrypted    = (byte[])null;

                using (var encryptor = aes.CreateEncryptor())
                {
                    using (var msEncrypted = new MemoryStream())
                    {
                        msEncrypted.Write(IV);

                        using (var csEncrypted = new CryptoStream(msEncrypted, encryptor, CryptoStreamMode.Write))
                        {
                            csEncrypted.Write(orgDecrypted, 0, orgDecrypted.Length);

                            if (!csEncrypted.HasFlushedFinalBlock)
                            {
                                csEncrypted.FlushFinalBlock();
                            }
                        }

                        encrypted = msEncrypted.ToArray();
                    }
                }

                Assert.NotEqual(orgDecrypted, encrypted);

                using (var decryptor = aes.CreateDecryptor())
                {
                    using (var msDecrypted = new MemoryStream(encrypted))
                    {
                        aes.IV = msDecrypted.ReadBytes(16);

                        using (var csDecrypted = new CryptoStream(msDecrypted, decryptor, CryptoStreamMode.Read))
                        {
                            decrypted = csDecrypted.ReadBytes(10);
                        }
                    }
                }

                Assert.Equal(orgDecrypted, decrypted);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void GenerateKeys()
        {
            const int iterations = 1000;

            // Generate a number of keys of each valid size and ensure that each key is unique.

            foreach (var size in sizes)
            {
                var keys = new HashSet<string>();

                for (int i = 0; i < iterations; i++)
                {
                    var key = AesCipher.GenerateKey(size);

                    Assert.NotNull(key);

                    var keyBytes = Convert.FromBase64String(key);

                    Assert.Equal(size, keyBytes.Length * 8);
                    Assert.DoesNotContain(key, keys);

                    keys.Add(key);
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void DefaultKey()
        {
            // Verify that the default key size is 256 bits.

            var key = AesCipher.GenerateKey();

            Assert.NotNull(key);
            Assert.Equal(256, Convert.FromBase64String(key).Length * 8);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void Encrypt_ToBytes()
        {
            // Encrypt a byte array:

            using (var cipher = new AesCipher())
            {
                var decrypted = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
                var encrypted = cipher.EncryptToBytes(decrypted);

                Assert.Equal(decrypted, cipher.DecryptBytesFrom(encrypted));
            }

            // Encrypt a string:

            using (var cipher = new AesCipher())
            {
                var decrypted = "Hello World!";
                var encrypted = cipher.EncryptToBytes(decrypted);

                Assert.Equal(decrypted, cipher.DecryptStringFrom(encrypted));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void Encrypt_ToBase64()
        {
            // Encrypt a byte array:

            using (var cipher = new AesCipher())
            {
                var decrypted = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
                var encrypted = cipher.EncryptToBase64(decrypted);

                Assert.Equal(decrypted, cipher.DecryptBytesFrom(encrypted));
            }

            // Encrypt a string:

            using (var cipher = new AesCipher())
            {
                var decrypted = "1234567890";
                var encrypted = cipher.EncryptToBase64(decrypted);

                Assert.Equal(decrypted, cipher.DecryptStringFrom(encrypted));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void Uniqueness()
        {
            // Verify that we generate and use a unique IV for
            // every encryption run such that encrypting the same
            // data will return different results.  This is an
            // important security best practice.

            const int iterations = 1000;

            var decrypted   = "We hold these truths to be self-evident, that all men are created equal.";
            var encryptions = new HashSet<string>();

            for (int i = 0; i < iterations; i++)
            {
                using (var cipher = new AesCipher())
                {
                    var encrypted = cipher.EncryptToBase64(decrypted);

                    Assert.DoesNotContain(encrypted, encryptions);
                    Assert.Equal(decrypted, cipher.DecryptStringFrom(encrypted));

                    encryptions.Add(encrypted);
                }
            }
        }
    }
}
