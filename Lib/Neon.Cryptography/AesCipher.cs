//-----------------------------------------------------------------------------
// FILE:	    AesCipher.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Security.Cryptography;
using System.Text;

using Neon.Common;
using Neon.IO;

// $todo(jefflill):
//
// This needs a review to ensure that we're not leaving decrypted
// data laying about in RAM unnecessarily.  One thing I should really
// look into is using [SecureString] where possible.

namespace Neon.Cryptography
{
    /// <summary>
    /// Implements a convienent wrapper over <see cref="AesManaged"/> that handles
    /// the encryption and decryption of data using the AES algorthim using many
    /// security best practices.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class uses the <see cref="BinaryWriter"/> to generate the encrypted
    /// output and <see cref="BinaryReader"/> to read it.
    /// </para>
    /// <para>
    /// The data is formatted with an unencrypted header that specifies the
    /// initialization vector (IV), as well as the HMAC512 that will be used
    /// to validate the encrypted data.  The encrypted data includes variable
    /// length psuedo random padding followed by the encrypted user data.
    /// </para>
    /// <code>
    ///  Header (plaintext)
    /// +------------------+
    /// |    0x3BBAA035    |    32-bit magic number (for verification)
    /// +------------------+
    /// |     IV Size      |    16-bits
    /// +------------------+
    /// |                  |
    /// |     IV Bytes     |    IV Size bytes
    /// |                  |
    /// +------------------+
    /// |    HMAC Size     |    16-bits
    /// +------------------+
    /// |                  |
    /// |    HMAC Bytes    |    HMAC Size bytes
    /// |                  |
    /// +-------------------
    /// 
    ///   AES256 Encrypted:
    /// +------------------+
    /// |   Padding Size   |    16-bits
    /// +------------------+
    /// |                  |
    /// |   Padding Bytes  |    Padding Size bytes
    /// |                  |
    /// +------------------+
    /// |                  |
    /// |                  |
    /// |                  |
    /// |    User Data     |
    /// |                  |
    /// |                  |
    /// |                  |
    /// +------------------+
    /// </code>
    /// <note>
    /// Note that this encodes multi-byte integers using <b>little endian</b>
    /// byte ordering via <see cref="BinaryWriter"/> and <see cref="BinaryReader"/>.
    /// </note>
    /// <para>
    /// This class automatically generates a new initialization vector for every
    /// encyption operation.  This ensures that every encryption operation will
    /// generate different ciphertext even when the key and data haven't changed
    /// to enhance security.
    /// </para>
    /// <para>
    /// The class is designed to be easier to use than the .NET Core <see cref="AesManaged"/>
    /// base implementation.
    /// </para>
    /// <para>
    /// To encrypt data:
    /// </para>
    /// <list type="number">
    /// <item>
    /// Generate an encryption key via <see cref="GenerateKey(int)"/> and create an instance
    /// via <see cref="AesCipher(String, int)"/> passing the key, or just call <see cref="AesCipher(int, int)"/>
    /// to create with a generated key of the specified size.
    /// </item>
    /// <item>
    /// You can always obtain the key via the <see cref="Key"/> property.
    /// </item>
    /// <item>
    /// Call one of <see cref="EncryptToBase64(byte[])"/>, <see cref="EncryptToBase64(byte[])"/>, 
    /// <see cref="EncryptToBytes(string)"/>, or <see cref="EncryptToBytes(byte[])"/> to perform
    /// the encryption with varying input and output formats.
    /// </item>
    /// </list>
    /// <para>
    /// To decrypt data:
    /// </para>
    /// <list type="number">
    /// <item>
    /// Use <see cref="AesCipher(string, int)"/> to construct and instance using the key originally
    /// used to encrypt the data.
    /// </item>
    /// <item>
    /// Call one of <see cref="DecryptBytesFrom(byte[])"/>, <see cref="DecryptBytesFrom(string)"/>,
    /// <see cref="DecryptStringFrom(byte[])"/>, or <see cref="DecryptStringFrom(byte[])"/>.
    /// to decrypt data.
    /// </item>
    /// </list>
    /// </remarks>
    /// <threadsafety instance="false"/>
    public sealed class AesCipher : IDisposable
    {
        //---------------------------------------------------------------------
        // Static members

        private static byte[] hmacZeros = new byte[CryptoHelper.HMAC512ByteCount];

        /// <summary>
        /// The 32-bit magic number that will be written in plaintext to the
        /// beginning of the encrypted output to be used to verify that 
        /// encrypted buffers will generated by this class.
        /// </summary>
        public const int Magic = 0x3BBAA035;

        /// <summary>
        /// Generates a random encryption key with the specified size in bits.
        /// </summary>
        /// <param name="keySize">The key size in bits (default <b>256</b>).</param>
        /// <returns>The key encoded as base-64.</returns>
        /// <remarks>
        /// Note that only these key sizes are currently supported: <b>128</b>, <b>192</b>,
        /// and <b>256</b> bits.  Only 256 bits is currently considered to be secure.
        /// </remarks>
        public static string GenerateKey(int keySize = 256)
        {
            Covenant.Requires<ArgumentException>(keySize == 128 || keySize == 192 || keySize == 256, nameof(keySize));

            using (var aes = new AesManaged())
            {
                aes.KeySize = keySize;

                aes.GenerateKey();

                return Convert.ToBase64String(aes.Key);
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private AesManaged  aes;
        private int         maxPaddingBytes;
        private Random      random;

        /// <summary>
        /// Constructs an AES cypher using a specific encryption key.
        /// </summary>
        /// <param name="key">The base-64 encoded key.</param>
        /// <param name="maxPaddingBytes">
        /// The maximum number of padding bytes.  This must be less than or equal
        /// to 32767.  This defaults to 64.
        /// </param>
        public AesCipher(string key, int maxPaddingBytes = 64)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(key), nameof(key));
            Covenant.Requires<ArgumentException>(0 <= maxPaddingBytes && maxPaddingBytes <= short.MaxValue, nameof(maxPaddingBytes));

            var keyBytes = Convert.FromBase64String(key);

            switch (keyBytes.Length * 8)
            {
                case 128:
                case 192:
                case 256:

                    break;

                default:

                    throw new ArgumentException($"Invalid key [size={keyBytes.Length * 8}].  Only these sizes are currently supported: 128, 192, and 256.", nameof(key));
            }

            aes = new AesManaged()
            {
                Key = keyBytes
            };

            this.maxPaddingBytes = maxPaddingBytes;
            this.random          = NeonHelper.CreateSecureRandom();
        }

        /// <summary>
        /// Constructs an AES cypher using a randomly generated encyption key.
        /// </summary>
        /// <param name="keySize">Optionally specifies the key size (defaults to <b>256 bits</b>).</param>
        /// <param name="maxPaddingBytes">
        /// The maximum number of padding bytes.  This must be less than or equal
        /// to 32767.  This defaults to 64.
        /// </param>
        /// <remarks>
        /// Note that only these key sizes are currently supported: <b>128</b>, <b>192</b>,
        /// and <b>256</b> bits.  Only 256 bits is currently considered to be secure.
        /// </remarks>
        public AesCipher(int keySize = 256, int maxPaddingBytes = 64)
        {
            Covenant.Requires<ArgumentException>(keySize == 128 || keySize == 192 || keySize == 256, nameof(keySize));
            Covenant.Requires<ArgumentException>(0 <= maxPaddingBytes && maxPaddingBytes <= short.MaxValue, nameof(maxPaddingBytes));

            aes = new AesManaged()
            {
                KeySize = keySize
            };

            aes.GenerateKey();

            this.maxPaddingBytes = maxPaddingBytes;
            this.random          = NeonHelper.CreateSecureRandom();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (aes != null)
            {
                aes.Dispose();
                aes = null;
            }
        }

        /// <summary>
        /// Ensures that the instance hasn't been disposed.
        /// </summary>
        private void EnsureNotDisposed()
        {
            if (aes == null)
            {
                throw new ObjectDisposedException(nameof(AesCipher));
            }
        }

        /// <summary>
        /// Returns the encyption key encoded as base-64.
        /// </summary>
        public string Key
        {
            get
            {
                EnsureNotDisposed();

                return Convert.ToBase64String(aes.Key);
            }
        }

        /// <summary>
        /// Returns the encyption initialization vector encoded as base-64.
        /// </summary>
        public string IV
        {
            get
            {
                EnsureNotDisposed();

                return Convert.ToBase64String(aes.IV);
            }
        }

        //---------------------------------------------------------------------
        // Encryption methods:

        /// <summary>
        /// Encrypts the text passed returning the result encoded as
        /// a byte array.
        /// </summary>
        /// <param name="decryptedBytes">The unencrypted bytes.</param>
        /// <returns>The encrypted result as bytes.</returns>
        public byte[] EncryptToBytes(byte[] decryptedBytes)
        {
            Covenant.Requires<ArgumentNullException>(decryptedBytes != null, nameof(decryptedBytes));

            using (var decrypted = new MemoryStream(decryptedBytes))
            {
                using (var encrypted = new MemoryStream())
                {
                    EncryptStream(decrypted, encrypted);

                    return encrypted.ToArray();
                }
            }
        }

        /// <summary>
        /// Encrypts the text passed returning the result encoded as
        /// a byte array.
        /// </summary>
        /// <param name="decryptedText">The unencrypted text.</param>
        /// <returns>The encrypted result as bytes.</returns>
        public byte[] EncryptToBytes(string decryptedText)
        {
            Covenant.Requires<ArgumentNullException>(decryptedText != null, nameof(decryptedText));

            return EncryptToBytes(Encoding.UTF8.GetBytes(decryptedText));
        }

        /// <summary>
        /// Encrypts the text passed returning the result encoded as base-64.
        /// </summary>
        /// <param name="decryptedText">The unencrypted text.</param>
        /// <returns>The encrypted result as base-64.</returns>
        public string EncryptToBase64(string decryptedText)
        {
            Covenant.Requires<ArgumentNullException>(decryptedText != null, nameof(decryptedText));

            var encryptedBytes = EncryptToBytes(Encoding.UTF8.GetBytes(decryptedText));

            return Convert.ToBase64String(encryptedBytes);
        }

        /// <summary>
        /// Encrypts the bytes passed returning the result encoded as base-64.
        /// </summary>
        /// <param name="decryptedBytes">The unencrypted text.</param>
        /// <returns>The encrypted result as base-64.</returns>
        public string EncryptToBase64(byte[] decryptedBytes)
        {
            Covenant.Requires<ArgumentNullException>(decryptedBytes != null, nameof(decryptedBytes));

            var encryptedBytes = EncryptToBytes(decryptedBytes);

            return Convert.ToBase64String(encryptedBytes);
        }

        /// <summary>
        /// Encrypts one stream to another.
        /// </summary>
        /// <param name="decrypted">The decrypted input stream.</param>
        /// <param name="encrypted">The encrypted output stream.</param>
        public void EncryptStream(Stream decrypted, Stream encrypted)
        {
            Covenant.Requires<ArgumentNullException>(decrypted != null, nameof(decrypted));
            Covenant.Requires<ArgumentNullException>(encrypted != null, nameof(encrypted));

            aes.GenerateIV();   // Always generate a new IV before encrypting.

            // Wrap the input and output streams with [RelayStream] instances
            // so that we can prevent the [CryptoStream] instances from disposing 
            // them (since these don't implement [leaveOpen]).

            using (var hmac = new HMACSHA512(aes.Key))
            {
                using (var decryptedRelay = new RelayStream(decrypted, leaveOpen: true))
                {
                    using (var encryptedRelay = new RelayStream(encrypted, leaveOpen: true))
                    {
                        long    hmacPos;

                        // Write the unencrypted header.

                        using (var writer = new BinaryWriter(encryptedRelay, Encoding.UTF8, leaveOpen: true))
                        {
                            // Write the magic number and IV to the output stream.

                            writer.Write((int)Magic);
                            writer.Write((short)aes.IV.Length);
                            writer.Write(aes.IV);

                            // Write the HMAC512 length followed by that many zeros
                            // as a placeholder for the computed HMAC.  We'll record
                            // the absolute position of these bytes so we can easily 
                            // go back and overwrite them with the actual HMAC after 
                            // we completed the data encryption.

                            writer.Write((short)hmacZeros.Length);
                            writer.Flush();     // Ensure that the underlying stream position is up-to-date

                            hmacPos = encryptedRelay.Position;

                            writer.Write(hmacZeros);
                        }

                        // Encrypt the input stream to the output while also computing the HMAC.

                        using (var hmacStream = new CryptoStream(encryptedRelay, hmac, CryptoStreamMode.Write))
                        {
                            using (var encryptor = aes.CreateEncryptor())
                            {
                                using (var encryptorStream = new CryptoStream(hmacStream, encryptor, CryptoStreamMode.Write))
                                {
                                    // Write the variable length random padding.

                                    var paddingLength = random.NextIndex(maxPaddingBytes);
                                    var paddingBytes  = new byte[paddingLength];

                                    random.NextBytes(paddingBytes);

                                    using (var writer = new BinaryWriter(encryptorStream, Encoding.UTF8, leaveOpen: true))
                                    {
                                        writer.Write((short)paddingLength);
                                        writer.Write(paddingBytes);
                                    }

                                    // Encrypt the user data:

                                    decryptedRelay.CopyTo(encryptorStream);
                                }
                            }
                        }

                        // Go back and persist the computed HMAC.

                        encryptedRelay.Position = hmacPos;

                        Covenant.Assert(hmac.Hash.Length == CryptoHelper.HMAC512ByteCount);
                        encrypted.Write(hmac.Hash);
                    }
                }
            }
        }

        //---------------------------------------------------------------------
        // Decryption methods:

        /// <summary>
        /// Decrypts the encrypted base-64 text passed returning the result as 
        /// a byte array.
        /// </summary>
        /// <param name="encryptedBytes">The encrypted bytes.</param>
        /// <returns>The encrypted result as a string.</returns>
        public byte[] DecryptBytesFrom(byte[] encryptedBytes)
        {
            Covenant.Requires<ArgumentNullException>(encryptedBytes != null, nameof(encryptedBytes));

            using (var encrypted = new MemoryStream(encryptedBytes))
            {
                using (var decrypted = new MemoryStream())
                {
                    DecryptStream(encrypted, decrypted);

                    return decrypted.ToArray();
                }
            }
        }

        /// <summary>
        /// Decrypts the encrypted base-64 text passed returning the result as 
        /// a byte array.
        /// </summary>
        /// <param name="encryptedBase64">The encrypted base-64 text.</param>
        /// <returns>The encrypted result as a string.</returns>
        public byte[] DecryptBytesFrom(string encryptedBase64)
        {
            Covenant.Requires<ArgumentNullException>(encryptedBase64 != null, nameof(encryptedBase64));

            var encryptedBytes = Convert.FromBase64String(encryptedBase64);

            return DecryptBytesFrom(encryptedBytes);
        }

        /// <summary>
        /// Decrypts the encrypted bytes passed returning the result as a string.
        /// </summary>
        /// <param name="encryptedBytes">The encrypted base-64 text.</param>
        /// <returns>The encrypted result as a base-64 string.</returns>
        public string DecryptStringFrom(byte[] encryptedBytes)
        {
            Covenant.Requires<ArgumentNullException>(encryptedBytes != null, nameof(encryptedBytes));

            var decryptedBytes = DecryptBytesFrom(encryptedBytes);

            return Encoding.UTF8.GetString(decryptedBytes);
        }

        /// <summary>
        /// Decrypts the encrypted base-64 text passed returning the result as a string.
        /// </summary>
        /// <param name="encryptedBase64">The encrypted base-64 text.</param>
        /// <returns>The encrypted result as a base-64 string.</returns>
        public string DecryptStringFrom(string encryptedBase64)
        {
            Covenant.Requires<ArgumentNullException>(encryptedBase64 != null, nameof(encryptedBase64));

            var encryptedBytes = Convert.FromBase64String(encryptedBase64);
            var decryptedBytes = DecryptBytesFrom(encryptedBytes);

            return Encoding.UTF8.GetString(decryptedBytes);
        }

        /// <summary>
        /// Decrypts one stream to another.
        /// </summary>
        /// <param name="encrypted">The encrypted input stream.</param>
        /// <param name="decrypted">The decrypted output stream.</param>
        public void DecryptStream(Stream encrypted, Stream decrypted)
        {
            Covenant.Requires<ArgumentNullException>(encrypted != null, nameof(encrypted));
            Covenant.Requires<ArgumentNullException>(decrypted != null, nameof(decrypted));

            // Wrap the input and output streams with [RelayStream] instances
            // so that we can prevent the [CryptoStream] instances from disposing 
            // them (since these don't implement [leaveOpen]).

            using (var hmac = new HMACSHA512(aes.Key))
            {
                byte[]  persistedHMAC;

                using (var encryptedRelay = new RelayStream(encrypted, leaveOpen: true))
                {
                    using (var decryptedRelay = new RelayStream(decrypted, leaveOpen: true))
                    {
                        // Process the encrypted header.

                        using (var reader = new BinaryReader(encryptedRelay, Encoding.UTF8, leaveOpen: true))
                        {
                            // Read and verify the unencrypted magic number:

                            try
                            {
                                if (reader.ReadInt32() != Magic)
                                {
                                    throw new FormatException($"The encrypted data was not generated by [{nameof(AesCipher)}].");
                                }
                            }
                            catch (IOException e)
                            {
                                throw new FormatException($"The encrypted data has been truncated or was not generated by [{nameof(AesCipher)}].", e);
                            }

                            // Read IV:

                            var ivLength = reader.ReadInt16();

                            if (ivLength < 0 || ivLength > 1024)
                            {
                                throw new FormatException("Invalid IV length.");
                            }

                            aes.IV = reader.ReadBytes(ivLength);

                            // Read the HMAC:

                            var hmacLength = reader.ReadInt16();

                            if (hmacLength < 0 || hmacLength > 1024)
                            {
                                throw new FormatException("Invalid HMAC length.");
                            }

                            persistedHMAC = reader.ReadBytes(hmacLength);
                        }

                        // Decrypt the input stream to the output while also 
                        // computing the HMAC.

                        using (var hmacStream = new CryptoStream(encryptedRelay, hmac, CryptoStreamMode.Read))
                        {
                            using (var decryptor = aes.CreateDecryptor())
                            {
                                using (var decryptorStream = new CryptoStream(hmacStream, decryptor, CryptoStreamMode.Read))
                                {
                                    // Decrypt the random padding.

                                    using (var reader = new BinaryReader(decryptorStream, Encoding.UTF8, leaveOpen: true))
                                    {
                                        var paddingLength = reader.ReadInt16();

                                        if (paddingLength < 0 || paddingLength > short.MaxValue)
                                        {
                                            throw new CryptographicException("The encrypted data has been tampered with or is corrupt: Invalid padding size.");
                                        }

                                        reader.ReadBytes(paddingLength);
                                    }

                                    // Decrypt the user data:

                                    decryptorStream.CopyTo(decryptedRelay);
                                }
                            }
                        }
                    }
                }

                // Ensure that the encrypted data hasn't been tampered with by
                // comparing the peristed and computed HMAC values.

                if (!NeonHelper.ArrayEquals(persistedHMAC, hmac.Hash))
                {
                    throw new CryptographicException("The encrypted data has been tampered with or is corrupt: The persisted and computed HMAC hashes don't match.  ");
                }
            }
        }
    }
}
