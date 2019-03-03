//-----------------------------------------------------------------------------
// FILE:	    NeonVault.cs
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.IO;

namespace Neon.Cryptography
{
    /// <summary>
    /// Manages the encryption and decryption of files using passwords.  This works
    /// a lot like Ansible Vault.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class works by using AES256 to encrypt and decrypt files using
    /// a Neon standard ASCII text file format.  This encryption is performed
    /// using the value of a named password as the encryption key.  The class
    /// depends on a password provider function like <c>string LookupPassword(string)</c>
    /// that will return the value for a named password.
    /// </para>
    /// <para>
    /// The idea here is that applications will define one or more named passwords
    /// like: <b>mypassword1=GU6qc2vsJgmCWmdL</b> and <b>mypassword2=GBRDUqsX3GSKJ2af</b>
    /// and then implement a password provider that returns the value of a password
    /// based on its name.  You'll pass this provider to the <see cref="NeonVault"/>
    /// constructor.
    /// </para>
    /// <note>
    /// Password names are case insenstive and will always be converted to lowercase
    /// using the invariant culture.  Password names may include alphanumeric characters
    /// plus dashs, dots, or underscores.
    /// </note>
    /// <para>
    /// Password providers should throw an exception whenever the named password
    /// cannot be located.  Most providers will throw a <see cref="KeyNotFoundException"/>
    /// when this happens.
    /// </para>
    /// <para>
    /// Encrypted files are encoded as ASCII and are formatted like:
    /// </para>
    /// <code>
    /// $NEON_VAULT;4C823A36774CA4AC760F31DD8ABE7BD3;1.0;AES256;PASSWORD-NAME
    /// HMAC512:84d323032634452714e5467314e7a4131627a4e47527a6859564730724e3368
    /// 4c5330744c5331435255644a5469424452564a5553555a4a51304655525330744c533074436b314a
    /// 53554e3552454e4451574a445a30463353554a425a306c4351555242546b4a6e6133466f61326c48
    /// 4f586377516b465263305a425245465754564a4e64305652575552575556464552586477636d5258
    /// 536d774b5932303162475248566e704e516a5259524652464e5531455358644e616b557954587072
    /// 4d4535736231684556456b3154555246656b314552544a4e656d7377546d787664305a555256524e
    /// 516b564851544656525170426545314c59544e5761567059536e566157464a735933704451304654
    /// 5358644555566c4b53323961535768325930354255555643516c46425247646e5256424252454e44
    /// 515646765132646e52554a42536d6c50436c6b345a45395163324a454f466379526c6b30566a5274
    /// 595570584d323032634452714e5467314e7a4131627a4e47527a6859564730724e33686957465130
    /// 546b68775645686d646e686161584e685a6e6f304f54414b4c325a6a53454d32546b4d3464697445
    /// 4e7a5a355931685156564a3164576f724f56646e51335133555670735a574d7954474a364b7a5a6f
    /// 55466f7a4c32347962544e7a51573952536c527253574e7565485172625170575458527157554d35
    /// 57573970633145305a453877634646444c3141784d6d4d7951586c46515663334d314a4555314256
    /// 526e597a555770365a47777255577052564556784b3068305257704a52544659626b4a70436e4a42
    /// 563078334d323872656d5a4f4e30684559555534596d7061636a4a765a7a687459574a454e566444
    /// 4c30395656
    /// </code>
    /// <para>
    /// The first line of the file holds metadata that is used to identify encrypted files
    /// and also to identify the encryption method and name of the password to be used
    /// for decryption.  The second line is an HMAC-512 over the entire file and the remaining
    /// lines is the encrypted data formatted as HEX split across 80 character lines.
    /// </para>
    /// <para>
    /// This class considers files starting <b>$NEON_VAULT;4C823A36774CA4AC760F31DD8ABE7BD3</b>
    /// to be encrypted.  This essentially acts as a very unique magic number.  This is followed 
    /// by the NeonVault format version (currently <b>1.0</b>), the encryption cypher (currently
    /// <b>AES256</b>), and the name of the password that was used for encryption.
    /// </para>
    /// <para>
    /// The decrypt methods are smart enough to determine whether a file is not encrypted
    /// and simply write the unencrypted data to the target.  This means that you can
    /// safely call these methods on unencrypted data.
    /// </para>
    /// <para>
    /// This class provides several methods to encrypt and decrypt data given a password.
    /// </para>
    /// <note>
    /// Source <see cref="Stream"/> instances passed to encryption and decryption methods
    /// must support reading and seeking and target <see cref="Stream"/> instances must
    /// support writing as well as reading and seeking (to support HMAC generation).
    /// </note>
    /// </remarks>
    public class NeonVault
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Handles writing of byte data as 80 character lines of HEX text.
        /// </summary>
        private sealed class HexLineStream : Stream
        {
            private byte[]      lineBuffer = new byte[40];  // 80 HEX digits == 40 bytes
            private int         bufferPos  = 0;
            private Stream      output;
            private string      lineEnding;

            /// <summary>
            /// Constructor,
            /// </summary>
            /// <param name="output">The output stream.</param>
            /// <param name="lineEnding">The line ending to use.</param>
            public HexLineStream(Stream output, string lineEnding)
            {
                this.output     = output;
                this.lineEnding = lineEnding;
            }

            /// <inheritdoc/>
            protected override void Dispose(bool disposing)
            {
                if (bufferPos != 0)
                {
                    throw new InvalidOperationException($"The last line was not flushed via: {nameof(FlushLastLine)}()");
                }
            }

            /// <summary>
            /// Writes bytes to the output stream.
            /// </summary>
            /// <param name="bytes">The bytes to be written.</param>
            /// <param name="pos">The position of the first byte to write.</param>
            /// <param name="count">The number of bytes to write.</param>
            public override void Write(byte[] bytes, int pos, int count)
            {
                Covenant.Requires<ArgumentNullException>(bytes != null);
                Covenant.Requires<ArgumentException>(0 <= pos && pos < bytes.Length);
                Covenant.Requires<ArgumentException>(pos + count < bytes.Length);

                if (count == 0)
                {
                    return;
                }

                // $hack(jeff.lill):
                //
                // I'm creating an extra buffer to make the code more understanable.
                // This could ne optimized out in the future (if necessary).

                var outputBytes = new byte[count];

                for (int i = 0; i < count; i++)
                {
                    outputBytes[i] = bytes[pos + i];
                }

                var outputPos = 0;

                while (outputPos < outputBytes.Length)
                {
                    // Copy as many bytes as will fit to the line buffer.

                    var cbCopied = Math.Min(lineBuffer.Length - bufferPos, outputBytes.Length - outputPos);

                    for (int i = outputPos; i < cbCopied; i++)
                    {
                        lineBuffer[bufferPos + i] = outputBytes[i];
                        bufferPos++;
                    }

                    // Write the buffered line as HEX if it's full.

                    if (bufferPos >= lineBuffer.Length)
                    {
                        output.Write(Encoding.ASCII.GetBytes(NeonHelper.ToHex(lineBuffer) + lineEnding));
                        bufferPos = 0;
                    }

                    outputPos += cbCopied;
                }
            }

            /// <summary>
            /// Flushes any remaining buffered data to the output stream.
            /// </summary>
            public void FlushLastLine()
            {
                if (bufferPos > 0)
                {
                    var partialLine = new byte[bufferPos];

                    for (int i = 0; i < bufferPos; i++)
                    {
                        partialLine[i] = lineBuffer[i];
                    }

                    output.Write(Encoding.ASCII.GetBytes(NeonHelper.ToHex(partialLine) + lineEnding));
                    bufferPos = 0;
                }
            }

            //-----------------------------------------------------------------
            // Unimplemented stream members.

            /// <inheritdoc/>
            public override bool CanRead => false;

            /// <inheritdoc/>
            public override bool CanSeek => false;

            /// <inheritdoc/>
            public override bool CanWrite => true;

            /// <inheritdoc/>
            public override long Length => throw new NotImplementedException();

            /// <inheritdoc/>
            public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public override void Flush()
            {
                throw new NotImplementedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotImplementedException();
            }

            public override void SetLength(long value)
            {
                throw new NotImplementedException();
            }
        }

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// The string at the beginning of all files encrypted by <see cref="NeonVault"/>.
        /// This is used to identify these files.
        /// </summary>
        public const string MagicString = "$NEON_VAULT;4C823A36774CA4AC760F31DD8ABE7BD3;";

        /// <summary>
        /// Returns <see cref="MagicString"/> encoded as a byte array for ease of use.
        /// </summary>
        public static byte[] MagicBytes { get; private set; }

        /// <summary>
        /// The AES key size in buts.
        /// </summary>
        private int KeySize = 256;

        /// <summary>
        /// The HMAC size in bits.
        /// </summary>
        private int HMACSize = 512;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static NeonVault()
        {
            MagicBytes = Encoding.ASCII.GetBytes(MagicString);
        }

        /// <summary>
        /// Ensures that a password name is valid.
        /// </summary>
        /// <param name="passwordName">The password name.</param>
        /// <returns>The password name converted to lowercase.</returns>
        /// <exception cref="CryptographicException">Thrown if the name is invalid.</exception>
        public static string ValidatePasswordName(string passwordName)
        {
            if (string.IsNullOrEmpty(passwordName))
            {
                throw new CryptographicException("Password name cannot be empty.");
            }

            foreach (var ch in passwordName)
            {
                if (!char.IsLetterOrDigit(ch) && ch != '.' && ch != '-' && ch != '_')
                {
                    throw new CryptographicException($"Password name [{passwordName}] contains invalid characters.  Only letters, digits, underscores, dashs and dots are allowed.");
                }
            }

            return passwordName.ToLowerInvariant();
        }

        /// <summary>
        /// Determines if a file is encrypted via <see cref="NeonVault"/>.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <returns><c>true</c> if the file is encrypted.</returns>
        public static bool IsEncrypted(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                return IsEncrypted(stream);
            }
        }

        /// <summary>
        /// Determines if a stream is encrypted via <see cref="NeonVault"/>.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <returns><c>true</c> if the stream is encrypted.</returns>
        /// <remarks>
        /// <note>
        /// The stream position must be at the beginning of the stream for this to work.
        /// </note>
        /// </remarks>
        public static bool IsEncrypted(Stream stream)
        {
            Covenant.Requires<ArgumentNullException>(stream != null);

            if (stream.Position != 0)
            {
                throw new InvalidOperationException("The stream position is not located at the beginning of the stream.");
            }

            using (var reader = new StreamReader(stream))
            {
                var line = reader.ReadLine();

                if (line == null)
                {
                    return false;
                }

                return line.StartsWith(MagicString);
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private Func<string, string>    passwordProvider;
        private string                  lineEnding;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="passwordProvider">
        /// Specifies the function that returns the password value for 
        /// a named password.
        /// </param>
        /// <param name="lineEnding">
        /// Optionally specifies line ending to be used when writing the output
        /// file.  This defaults to the current platform's line ending: "\r\n"
        /// for Windows and "\n" for Linux, OS/X, etc.
        /// </param>
        public NeonVault(Func<string, string> passwordProvider, string lineEnding = null)
        {
            Covenant.Requires<ArgumentNullException>(passwordProvider != null);
            Covenant.Requires<ArgumentException>(lineEnding == null || lineEnding == "\r\n" || lineEnding == "\r");

            if (lineEnding == null)
            {
                this.lineEnding = NeonHelper.LineEnding;
            }
            else
            {
                this.lineEnding = lineEnding;
            }

            this.passwordProvider = passwordProvider;
        }

        /// <summary>
        /// Looks up a password and generates an AES256 key from it.
        /// </summary>
        /// <param name="passwordName">The password name.</param>
        /// <returns>The password value.</returns>
        /// <exception cref="CryptographicException">Thrown for problems.</exception>
        private byte[] GetKeyFromPassword(string passwordName)
        {
            try
            {
                if (string.IsNullOrEmpty(passwordName))
                {
                    throw new ArgumentNullException(nameof(passwordName));
                }

                passwordName = ValidatePasswordName(passwordName);

                var password = passwordProvider(passwordName);

                if (string.IsNullOrWhiteSpace(password))
                {
                    throw new CryptographicException("Password cannot be [null], blank, or whitespace.");
                }

                return CryptoHelper.DeriveKeyFromPassword(password, KeySize);
            }
            catch (CryptographicException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new CryptographicException(e.Message, e);
            }
        }

        //---------------------------------------------------------------------
        // Encryption methods

        /// <summary>
        /// Encrypts a stream to a byte array.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <param name="passwordName">Identifies the password.</param>
        /// <returns>The encrypted bytes.</returns>
        /// <exception cref="CryptographicException">Thrown if the password was not found or for other encryption problems.</exception>
        public byte[] Encrypt(Stream source, string passwordName)
        {
            return null;
        }

        /// <summary>
        /// Encrypts a file to a byte array.
        /// </summary>
        /// <param name="sourcePath">The source path.</param>
        /// <param name="passwordName">Identifies the password.</param>
        /// <returns>The encrypted bytes.</returns>
        /// <exception cref="CryptographicException">Thrown if the password was not found or for other encryption problems.</exception>
        public byte[] Encrypt(string sourcePath, string passwordName)
        {
            return null;
        }

        /// <summary>
        /// Encrypts a stream to another stream.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <param name="target">The target stream.</param>
        /// <param name="passwordName">Identifies the password.</param>
        /// <exception cref="CryptographicException">Thrown if the password was not found or for other encryption problems.</exception>
        public void Encrypt(Stream source, Stream target, string passwordName)
        {
            Covenant.Requires<ArgumentNullException>(source != null);
            Covenant.Requires<ArgumentException>(source.CanRead && source.CanSeek);
            Covenant.Requires<ArgumentNullException>(target != null);
            Covenant.Requires<ArgumentException>(source.CanRead && source.CanWrite && source.CanSeek);

            // Here's how this works:
            //
            //      1. Lookup the password and derive a 256-bit AES key from it.
            //
            //      2. Write the header line.
            //
            //      3. The next line will be the HMAC line.  We haven't generated
            //         the HMAC yet so we'll make a note of the current stream position
            //         (which will be at the first digit of the HMAC HEX).
            //
            //      4. Write a HMAC with all zeros.  We'll come back and replace
            //         this after we've computed the HMAC.
            //
            //      5. Encrypt the data and write it out as 80 character lines of HEX.
            //         We'll also be computing the HMAC at the same time.  The HMAC
            //         computation will include the first line, skip the second HMAC
            //         line, and then include the encrypted data.  Note that the HMAC
            //         does not include the line endings, so these can be changed
            //         as required for different platforms and tools.
            //
            //      6. After all of the data has been encrypted and the HMAC is computed
            //         we'll seek the output stream back to the beginning of the HMAC
            //         line and rewrite the line data with the computed HMAC value.

            var key = GetKeyFromPassword(passwordName);
            var hmacPos = 0L;

            target.Write(Encoding.ASCII.GetBytes($"{MagicString}1.0;AES256;{passwordName}{lineEnding}"));

            hmacPos = target.Position + "HMAC512:".Length;
            target.Write(Encoding.ASCII.GetBytes($"HMAC512:{new string('0', (512/8) * 2)}{lineEnding}"));

            // Encrypt the source data and write it out as 80 character lines of 
            // HEX digits and compute the HMAC as we're doing this.

            using (var hmac = new HMACSHA512(key))
            {
                using (var aes = Aes.Create())
                {
                    // Initialize the key and IV.

                    aes.Key = key;
                    aes.GenerateIV();

                    using (var hexLineStream = new HexLineStream(target, lineEnding))
                    {
                        // Encrypt the source stream to the target while also
                        // computing the HMAC.

                        using (var hmacStream = new CryptoStream(hexLineStream, hmac, CryptoStreamMode.Write))
                        {
                            using (var encryptor = aes.CreateEncryptor())
                            {
                                using (var encryptorStream = new CryptoStream(hmacStream, encryptor, CryptoStreamMode.Write))
                                {
                                    var buffer = new byte[8192];

                                    while (true)
                                    {
                                        var cb = source.Read(buffer, 0, buffer.Length);

                                        if (cb == 0)
                                        {
                                            break;
                                        }

                                        encryptorStream.Write(buffer, 0, cb);
                                    }
                                }
                            }
                        }

                        // Flush any remaining partial line.

                        hexLineStream.FlushLastLine();

                        // Go back and write the HMAC hash into the space we left for it
                        // (on the second output line).

                        var endPos = target.Position;

                        target.Position = hmacPos;
                        Covenant.Assert(hmac.Hash.Length == HMACSize/8);
                        target.Write(Encoding.ASCII.GetBytes(NeonHelper.ToHex(hmac.Hash)));
                        target.Position = endPos;
                    }
                }
            }
        }

        /// <summary>
        /// Encrypts a stream to a file.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <param name="targetPath">The target path.</param>
        /// <param name="passwordName">Identifies the password.</param>
        /// <exception cref="CryptographicException">Thrown if the password was not found or for other encryption problems.</exception>
        public void Encrypt(Stream source, string targetPath, string passwordName)
        {
        }

        /// <summary>
        /// Encrypts a file to another file.
        /// </summary>
        /// <param name="sourcePath">The source path.</param>
        /// <param name="targetPath">The target path.</param>
        /// <param name="passwordName">Identifies the password.</param>
        /// <exception cref="CryptographicException">Thrown if the password was not found or for other encryption problems.</exception>
        public void Encrypt(string sourcePath, string targetPath, string passwordName)
        {
        }

        //---------------------------------------------------------------------
        // Decryption methods

        /// <summary>
        /// Decrypts a stream to a byte array.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <returns>The decrypted byte array.</returns>
        /// <exception cref="CryptographicException">Thrown if the password was not found or for other decryption problems.</exception>
        public byte[] Decrypt(Stream source)
        {
            return null;
        }

        /// <summary>
        /// Decrypts file to a byte array.
        /// </summary>
        /// <param name="sourcePath">The source path.</param>
        /// <returns>The decrypted bytes.</returns>
        /// <exception cref="CryptographicException">Thrown if the password was not found or for other decryption problems.</exception>
        public byte[] Decrypt(string sourcePath)
        {
            return null;
        }

        /// <summary>
        /// Decrypts a stream to another stream.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <param name="target">The target stream.</param>
        /// <exception cref="CryptographicException">Thrown if the password was not found or for other decryption problems.</exception>
        public void Decrypt(Stream source, Stream target)
        {
        }

        /// <summary>
        /// Decrypts a file to a stream.
        /// </summary>
        /// <param name="sourcePath">The source path.</param>
        /// <param name="target">The target stream.</param>
        /// <exception cref="CryptographicException">Thrown if the password was not found or for other decryption problems.</exception>
        public void Decrypt(string sourcePath, Stream target)
        {
        }

        /// <summary>
        /// Decrypts a file to another file.
        /// </summary>
        /// <param name="sourcePath">The source path.</param>
        /// <param name="targetPath">The target path.</param>
        /// <exception cref="CryptographicException">Thrown if the password was not found or for other decryption problems.</exception>
        public void Decrypt(string sourcePath, string targetPath)
        {
        }
    }
}
