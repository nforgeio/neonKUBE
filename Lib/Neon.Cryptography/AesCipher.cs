//-----------------------------------------------------------------------------
// FILE:	    AesCipher.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using Neon.Common;

// $todo(jeff.lill):
//
// This needs a review to ensure that we're not leaving decrypted
// data laying about in RAM unnecessarily.  One thing I should really
// look into is using [SecureString] where possible.

namespace Neon.Cryptography
{
    /// <summary>
    /// Implements a convienent wrapper over <see cref="AesManaged"/> that handles
    /// the encryption and decryption of data using the AES algorthim.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class uses the <see cref="BinaryWriter"/> to generate the encrypted
    /// output and <see cref="BinaryReader"/> to read it.
    /// </para>
    /// <para>
    /// The encrypted data is formatted like:
    /// </para>
    /// <code>
    /// +------------------+
    /// |     IV Size      |    16-bits
    /// +------------------+
    /// |                  |
    /// |                  |
    /// |     IV Bytes     |    IV Size bytes
    /// |                  |
    /// |                  |
    /// +------------------+
    /// |                  |
    /// |                  |
    /// |    Data Bytes    |    Data bytes
    /// |                  |
    /// |                  |
    /// +------------------+
    /// </code>
    /// <note>
    /// Note that this encodes multi-byte integers using little endian byte ordering
    /// via <see cref="BinaryWriter"/> and <see cref="BinaryReader"/>.
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
    /// via <see cref="AesCipher(string)"/> passing the key, or just call <see cref="AesCipher(int)"/>
    /// to create an instance with an already populated random key.
    /// </item>
    /// <item>
    /// You can always obtain the key via the <see cref="Key"/> property.
    /// </item>
    /// <item>
    /// Call one of <see cref="EncryptToBase64(string)"/> <see cref="EncryptToBase64(byte[])"/>, 
    /// <see cref="EncryptToBytes(string)"/>, <see cref="EncryptToBytes(byte[])"/>, or
    /// <see cref="EncryptStream(Stream, Stream)"/> to perform the encryption with varying
    /// input and output formats.
    /// </item>
    /// </list>
    /// <para>
    /// To decrypt data:
    /// </para>
    /// <list type="number">
    /// <item>
    /// Use <see cref="AesCipher(string)"/> to construct and instance using the key originally
    /// used to encrypt the data.
    /// </item>
    /// <item>
    /// Call one of <see cref="DecryptBytesFromBase64(byte[])"/>, <see cref="DecryptStringFromBase64(string)"/>,
    /// or <see cref="DecryptToStream(Stream, Stream)"/> to decrypt data.
    /// </item>
    /// </list>
    /// </remarks>
    public sealed class AesCipher : IDisposable
    {
        //---------------------------------------------------------------------
        // Static members

        private static byte[] zeros = new byte[1024];

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
            Covenant.Requires<ArgumentException>(keySize == 128 || keySize == 192 || keySize == 256);

            using (var aes = new AesManaged())
            {
                aes.KeySize = keySize;

                aes.GenerateKey();

                return Convert.ToBase64String(aes.Key);
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private AesManaged aes;

        /// <summary>
        /// Constructs an AES cypher using a specific encryption key.
        /// </summary>
        /// <param name="key">The base-64 encoded key.</param>
        public AesCipher(string key)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(key));

            var keyBytes = Convert.FromBase64String(key);

            switch (keyBytes.Length * 8)
            {
                case 128:
                case 192:
                case 256:

                    break;

                default:

                    throw new ArgumentException($"Invalid key [size={keyBytes.Length * 8}].  Only these sizes are currently supported: 128, 192, and 256.");
            }

            aes = new AesManaged()
            {
                Key = keyBytes
            };
        }

        /// <summary>
        /// Constructs an AES cypher using a randomly generated encyption key.
        /// </summary>
        /// <param name="keySize">Optionally specifies the key size (defaults to <b>256 bits</b>).</param>
        /// <remarks>
        /// Note that only these key sizes are currently supported: <b>128</b>, <b>192</b>,
        /// and <b>256</b> bits.  Only 256 bits is currently considered to be secure.
        /// </remarks>
        public AesCipher(int keySize = 256)
        {
            Covenant.Requires<ArgumentException>(keySize == 128 || keySize == 192 || keySize == 256);

            aes = new AesManaged()
            {
                KeySize = keySize
            };

            aes.GenerateKey();
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
        /// Zeros the contents of a byte array.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        private void Zero(byte[] bytes)
        {
            Covenant.Requires<ArgumentNullException>(bytes != null);

            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = 0;
            }
        }

        /// <summary>
        /// Zeros the contents of a stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        private void Zero(Stream stream)
        {
            Covenant.Requires<ArgumentNullException>(stream != null);

            var orgPos      = stream.Position;
            var cbRemaining = stream.Length;

            stream.Position = 0;

            while (cbRemaining > 0)
            {
                var cbWritten = (int)Math.Min(zeros.Length, cbRemaining);

                stream.Write(zeros, 0, cbWritten);
                cbRemaining -= cbWritten;
            }

            stream.Position = orgPos;
        }

        //---------------------------------------------------------------------
        // Encryption methods:

        /// <summary>
        /// Encrypts the text passed returning the result encoded as base-64.
        /// </summary>
        /// <param name="decryptedText">The unencrypted text.</param>
        /// <returns>The encrypted result as base-64.</returns>
        public string EncryptToBase64(string decryptedText)
        {
            Covenant.Requires<ArgumentNullException>(decryptedText != null);

            return EncryptToBase64(Encoding.UTF8.GetBytes(decryptedText), zeroDecrypted: true);
        }

        /// <summary>
        /// Encrypts the bytes passed returning the result encoded as base-64.
        /// </summary>
        /// <param name="decryptedBytes">The unencrypted bytes.</param>
        /// <param name="zeroDecrypted">Optionally indicates the the <paramref name="decrypted"/> should be zeroed before returning.</param>
        /// <returns>The encrypted result as base-64.</returns>
        public string EncryptToBase64(byte[] decryptedBytes, bool zeroDecrypted = false)
        {
            Covenant.Requires<ArgumentNullException>(decryptedBytes != null);

            try
            {
                return Convert.ToBase64String(EncryptToBytes(decryptedBytes));
            }
            finally
            {
                Zero(decryptedBytes);
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
            Covenant.Requires<ArgumentNullException>(decryptedText != null);

            var decrypted = Encoding.UTF8.GetBytes(decryptedText);

            try
            {
                return EncryptToBytes(Encoding.UTF8.GetBytes(decryptedText), zeroDecrypted: true);
            }
            finally
            {
                Zero(decrypted);
            }
        }

        /// <summary>
        /// Encrypts the bytes passed returning the result encoded as
        /// a byte array.
        /// </summary>
        /// <param name="decryptedBytes">The unencrypted bytes.</param>
        /// <param name="zeroDecrypted">Optionally indicates the the <paramref name="decrypted"/> should be zeroed before returning.</param>
        /// <returns>The encrypted result as bytes.</returns>
        public byte[] EncryptToBytes(byte[] decryptedBytes, bool zeroDecrypted = false)
        {
            Covenant.Requires<ArgumentNullException>(decryptedBytes != null);

            using (var decrypted = new MemoryStream(decryptedBytes))
            {
                using (var encrypted = new MemoryStream())
                {
                    try
                    {
                        EncryptToStream(decrypted, encrypted);

                        encrypted.Position = 0;
                        return encrypted.ReadBytes((int)(encrypted.Length));
                    }
                    finally
                    {
                        // Zero the input stream so that sensitive data won't be 
                        // hanging around in RAM.

                        Zero(decrypted);

                        if (zeroDecrypted)
                        {
                            Zero(decryptedBytes);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Encrypts an input stream from the current position to the
        /// end of the stream, appending the encrypted data to an
        /// output stream.
        /// </summary>
        /// <param name="decrypted">The plaintext input stream.</param>
        /// <param name="encrypted">The encrypted output stream.</param>
        /// <param name="zeroDecrypted">Optionally indicates the the <paramref name="decrypted"/> should be zeroed before returning.</param>
        public void EncryptToStream(Stream decrypted, Stream encrypted, bool zeroDecrypted = false)
        {
            Covenant.Requires<ArgumentNullException>(decrypted != null);
            Covenant.Requires<ArgumentNullException>(encrypted != null);

            try
            {
                using (var writer = new BinaryWriter(encrypted, Encoding.UTF8, leaveOpen: true))
                {
                    // Write the IV to the encrypted output first in the clear.
                    // We'll need to be able to read this before decrypting.

                    writer.Write((short)aes.IV.Length);
                    writer.Write(aes.IV);
                    writer.Flush();

                    // Encrypt the IV and data:

                    var encryptor = new CryptoStream(encrypted, aes.CreateEncryptor(aes.Key, aes.IV), CryptoStreamMode.Write);
                       
                    decrypted.CopyTo(encryptor);
                    encryptor.FlushFinalBlock();
                }
            }
            finally
            {
                if (zeroDecrypted)
                {
                    Zero(decrypted);
                }
            }
        }

        //---------------------------------------------------------------------
        // Decryption methods:

        /// <summary>
        /// Decrypts the encrypted base-64 text passed returning the result as a string.
        /// </summary>
        /// <param name="encryptedBase64">The encrypted base-64 text.</param>
        /// <returns>The encrypted result as a string.</returns>
        public string DecryptStringFromBase64(string encryptedBase64)
        {
            Covenant.Requires<ArgumentNullException>(encryptedBase64 != null);

            var bytes = Convert.FromBase64String(encryptedBase64);

            try
            {
                return Convert.ToBase64String(DecryptBytes(bytes));
            }
            finally
            {
                Zero(bytes);
            }
        }

        /// <summary>
        /// Decrypts the encrypted base-64 text passed returning the result as a byte array.
        /// </summary>
        /// <param name="encryptedBase64">The encrypted base-64 text.</param>
        /// <returns>The encrypted result as base-64.</returns>
        public byte[] DecryptBytesFromBase64(string encryptedBase64)
        {
            Covenant.Requires<ArgumentNullException>(encryptedBase64 != null);

            using (var encrypted = new MemoryStream(Convert.FromBase64String(encryptedBase64)))
            {
                using (var decrypted = new MemoryStream())
                {
                    try
                    {
                        DecryptToStream(encrypted, decrypted);

                        decrypted.Position = 0;
                        return decrypted.ReadBytes((int)(decrypted.Length));
                    }
                    finally
                    {
                        // Zero the output stream so that sensitive data won't be 
                        // hanging around in RAM.

                        Zero(decrypted);
                    }
                }
            }
        }

        /// <summary>
        /// Decrypts the encrypted bytes passed returning the result as a byte array.
        /// </summary>
        /// <param name="encryptedBytes">The unencrypted bytes.</param>
        /// <returns>The encrypted result as base-64.</returns>
        public byte[] DecryptBytes(byte[] encryptedBytes)
        {
            Covenant.Requires<ArgumentNullException>(encryptedBytes != null);

            using (var encrypted = new MemoryStream(encryptedBytes))
            {
                using (var decrypted = new MemoryStream())
                {
                    try
                    {
                        DecryptToStream(encrypted, decrypted);

                        decrypted.Position = 0;
                        return decrypted.ReadBytes((int)(decrypted.Length));
                    }
                    finally
                    {
                        // Zero the output stream so that sensitive data won't be 
                        // hanging around in RAM.

                        Zero(decrypted);
                    }
                }
            }
        }

        /// <summary>
        /// Decrypts an input stream from the current position to the
        /// end of the stream, appending the decrypted data to an
        /// output stream.
        /// </summary>
        /// <param name="encrypted">The encrypted input stream.</param>
        /// <param name="decrypted">The decrypted output stream.</param>
        public void DecryptToStream(Stream encrypted, Stream decrypted)
        {
            Covenant.Requires<ArgumentNullException>(encrypted != null);
            Covenant.Requires<ArgumentNullException>(decrypted != null);

            using (var reader = new BinaryReader(encrypted, Encoding.UTF8, leaveOpen: true))
            {
                // Read the IV:

                var ivLength = reader.ReadInt16();
                var ivBytes  = reader.ReadBytes(ivLength);

                // Decrypt the data:

                var decryptor = new CryptoStream(encrypted, aes.CreateEncryptor(aes.Key, ivBytes), CryptoStreamMode.Read);

                decryptor.CopyTo(decrypted);
            }
        }
    }
}
