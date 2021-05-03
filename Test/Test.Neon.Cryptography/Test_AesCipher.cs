//-----------------------------------------------------------------------------
// FILE:        Test_AesCipher.cs
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
    public class Test_AesCipher
    {
        private int[] sizes = new int[] { 128, 192, 256 };

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCryptography)]
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
        [Trait(TestTrait.Area, TestArea.NeonCryptography)]
        public void ActuallyEncrypted()
        {
            // Attempt to verify that the encrypted output doesn't actually
            // include the plaintext data.

            using (var cipher = new AesCipher())
            {
                var decrypted = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
                var encrypted = cipher.EncryptToBytes(decrypted);
                var pos       = 0;
                var match     = false;

                while (pos <= encrypted.Length - decrypted.Length)
                {
                    match = true;

                    for (int i = 0; i < decrypted.Length; i++)
                    {
                        if (encrypted[pos + i] != decrypted[i])
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                    {
                        break;
                    }

                    pos++;
                }

                // If we reach this and [match==true] then we found the
                // original plaintext embedded in the encypted output
                // (which is bad).

                Assert.False(match);
            }
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCryptography)]
        public void UniqueKeys()
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
        [Trait(TestTrait.Area, TestArea.NeonCryptography)]
        public void UniqueInstanceKeyIVs()
        {
            const int iterations = 1000;

            // Instantiate a number of AesCipher instances and verify that
            // the each start out with a unique key and IV.

            var keys = new HashSet<string>();
            var IVs  = new HashSet<string>();

            for (int i = 0; i < iterations; i++)
            {
                using (var aes = new AesCipher())
                {
                    Assert.DoesNotContain(aes.Key, keys);
                    keys.Add(aes.Key);

                    Assert.DoesNotContain(aes.IV, keys);
                    IVs.Add(aes.IV);
                }
            }
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCryptography)]
        public void DefaultKeySize()
        {
            // Verify that the default key size is 256 bits.

            var key = AesCipher.GenerateKey();

            Assert.NotNull(key);
            Assert.Equal(256, Convert.FromBase64String(key).Length * 8);
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCryptography)]
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
        [Trait(TestTrait.Area, TestArea.NeonCryptography)]
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
        [Trait(TestTrait.Area, TestArea.NeonCryptography)]
        public void Streams()
        {
            // Verify that we can encrypt and decrypt streams.

            var decryptedBytes = new byte[32 * 1024];
            var encryptedBytes = (byte[])null;
            var key            = String.Empty;

            // Encrypt some bytes.

            for (int i = 0; i < decryptedBytes.Length; i++)
            {
                decryptedBytes[i] = (byte)i;
            }

            using (var decryptedStream = new MemoryStream(decryptedBytes))
            {
                using (var encryptedStream = new MemoryStream())
                {
                    using (var cipher = new AesCipher())
                    {
                        cipher.EncryptStream(decryptedStream, encryptedStream);

                        // Save the key and encrypted data so we can test decryption below.

                        encryptedBytes = encryptedStream.ToArray();
                        key            = cipher.Key;
                    }

                    // Verify that the two streams haven't been dispoed.

                    decryptedStream.Position = 0;
                    encryptedStream.Position = 0;
                }
            }

            // Decrypt the encypted data and verify.

            using (var encryptedStream = new MemoryStream(encryptedBytes))
            {
                using (var decryptedStream = new MemoryStream())
                {
                    using (var cipher = new AesCipher(key))
                    {
                        cipher.DecryptStream(encryptedStream, decryptedStream);
                    }

                    // Verify that the decrypted data matches the original.

                    Assert.Equal(decryptedBytes, decryptedStream.ToArray());
                }
            }
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCryptography)]
        public void UniqueOutput()
        {
            // Verify that we generate and use a unique IV for
            // every encryption run such that encrypting the same
            // data will return different results.  This is an
            // important security best practice.

            const int iterations = 1000;

            var decrypted   = "We hold these truths to be self-evident, that all men are created equal.";
            var encryptions = new HashSet<string>();

            using (var cipher = new AesCipher())
            {
                for (int i = 0; i < iterations; i++)
                {
                    var encrypted = cipher.EncryptToBase64(decrypted);

                    Assert.DoesNotContain(encrypted, encryptions);
                    Assert.Equal(decrypted, cipher.DecryptStringFrom(encrypted));

                    encryptions.Add(encrypted);
                }
            }
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCryptography)]
        public void BadKeySize()
        {
            // Verify that we throw for an invalid key size.

            Assert.Throws<ArgumentException>(() => new AesCipher(11));
        }

        /// <summary>
        /// Makes a copy of a byte array. 
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        private byte[] Clone(byte[] bytes)
        {
            var clone = new byte[bytes.Length];

            for (int i = 0; i < bytes.Length; i++)
            {
                clone[i] = bytes[i];
            }

            return clone;
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCryptography)]
        public void TamperDetection()
        {
            // Tamper with an encrypted payload and verify that this
            // is detected via the HMAC signature.

            using (var cipher = new AesCipher())
            {
                var decrypted = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
                var encrypted = cipher.EncryptToBytes(decrypted);

                Assert.Equal(decrypted, cipher.DecryptBytesFrom(encrypted));

                // Modify the last byte and ensure that decryption fails.

                var tampered = Clone(encrypted);

                tampered[encrypted.Length - 1] = (byte)(~tampered[encrypted.Length - 1]);

                Assert.Throws<CryptographicException>(() => cipher.DecryptBytesFrom(tampered));

                // Remove the last byte and ensure that decryption fails.

                tampered = new byte[encrypted.Length - 1];

                for (int i = 0; i < tampered.Length; i++)
                {
                    tampered[i] = encrypted[i];
                }

                Assert.Throws<CryptographicException>(() => cipher.DecryptBytesFrom(tampered));
            }
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCryptography)]
        public void Padding()
        {
            // Ensure that [AesCypher] adding the requested random padding.
            // We're going to do this by requesting 2K of padding and doing
            // a bunch of encryption runs recording the sizes of the resulting
            // encrypted data.
            //
            // Then we'll verify that we're seeing at least 256 bytes of 
            // variation in the encrypted lengthss.

            // $note(jefflill):
            //
            // There's a very slight chance that this will fail if we're
            // incredibly unlucky and [AesCipher] happens to randomly 
            // add padding with lengths closer together than this.

            const int iterations = 1000;

            var minSize = int.MaxValue;
            var maxSize = int.MinValue;

            for (int i = 0; i < iterations; i++)
            {
                using (var cipher = new AesCipher(maxPaddingBytes: 2048))
                {
                    var encrypted = cipher.EncryptToBytes("the quick brown fox jumped over the lazy dog.");

                    minSize = Math.Min(minSize, encrypted.Length);
                    maxSize = Math.Max(maxSize, encrypted.Length);
                }
            }

            // Verify that padding was actually added.

            Assert.True(maxSize > 256);

            // Verify that the padding size is randmonized.

            Assert.True(maxSize - minSize >= 256);
        }
    }
}
