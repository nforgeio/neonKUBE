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
    /// This class works by using <see cref="AesCipher"/> with a <b>256-bit key</b> 
    /// to encrypt and decrypt files using a Neon standard ASCII text file format. 
    /// This encryption is performed using the value of a named password as the encryption
    /// key.  The class depends on a password provider function like <c>string LookupPassword(string)</c>
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
    /// for decryption.  The remaining lines encode the encrypted <see cref="AesCipher"/> 
    /// output encoded as 80 character lines of HEX digits.
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
    /// support writing as well as reading and seeking to support HMAC signatures.
    /// </note>
    /// </remarks>
    public class NeonVault
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Handles serializing byte data as 80 character lines of HEX digits.
        /// </summary>
        private sealed class HexLineStream : Stream
        {
            //-----------------------------------------------------------------
            // Static members

            /// <summary>
            /// Creates a <see cref="HexLineStream"/> for writing.
            /// </summary>
            /// <param name="output">The output stream.</param>
            /// <param name="lineEnding">The line ending to use.</param>
            /// <returns>The <see cref="HexLineStream"/>.</returns>
            public static HexLineStream Create(Stream output, string lineEnding)
            {
                return new HexLineStream(output, lineEnding);
            }

            /// <summary>
            /// Creates a <see cref="HexLineStream"/> for reading.
            /// </summary>
            /// <param name="input">The input stream.</param>
            /// <returns>The <see cref="HexLineStream"/>.</returns>
            public static HexLineStream Open(Stream input)
            {
                return new HexLineStream(input);
            }

            //-----------------------------------------------------------------
            // Instance members

            private byte[] lineBuffer = new byte[40];  // 80 HEX digits == 40 bytes
            private int bufferPos = 0;
            private Stream output;
            private Stream input;
            private string lineEnding;

            /// <summary>
            /// Constructs an instance for writing.
            /// </summary>
            /// <param name="input">The output stream.</param>
            /// <param name="lineEnding">The line ending to use.</param>
            private HexLineStream(Stream output, string lineEnding)
            {
                this.output = output;
                this.lineEnding = lineEnding;
            }

            /// <summary>
            /// Constructs an instance for reading.
            /// </summary>
            /// <param name="input">The input stream.</param>
            private HexLineStream(Stream input)
            {
                this.input = input;
            }

            /// <inheritdoc/>
            protected override void Dispose(bool disposing)
            {
                if (bufferPos != 0)
                {
                    FlushLastLine();
                    bufferPos = 0;
                }
            }

            /// <summary>
            /// Writes bytes to the output stream.
            /// </summary>
            /// <param name="bytes">The bytes to be written.</param>
            /// <param name="offset">The position of the first byte to write.</param>
            /// <param name="count">The number of bytes to write.</param>
            public override void Write(byte[] bytes, int offset, int count)
            {
                Covenant.Requires<ArgumentNullException>(bytes != null);
                Covenant.Requires<ArgumentException>(count == 0 || 0 <= offset && offset < bytes.Length);
                Covenant.Requires<ArgumentException>(offset + count <= bytes.Length);

                if (count == 0)
                {
                    return;
                }

                // $hack(jeff.lill):
                //
                // I'm creating an extra buffer to make the code more understandable.
                // This could be optimized out in the future (if necessary).

                var outputBytes = new byte[count];

                for (int i = 0; i < count; i++)
                {
                    outputBytes[i] = bytes[offset + i];
                }

                var outputPos = 0;

                while (outputPos < outputBytes.Length)
                {
                    // Copy as many bytes as will fit to the line buffer.

                    var cbCopied = Math.Min(lineBuffer.Length - bufferPos, outputBytes.Length - outputPos);

                    for (int i = outputPos; i < cbCopied; i++)
                    {
                        lineBuffer[bufferPos] = outputBytes[i];
                        bufferPos++;
                    }

                    // Write the buffered line as HEX if it's full.

                    if (bufferPos >= lineBuffer.Length)
                    {
                        output.Write(Encoding.ASCII.GetBytes(NeonHelper.ToHex(lineBuffer, uppercase: true) + lineEnding));
                        bufferPos = 0;
                    }

                    outputPos += cbCopied;
                }
            }

            /// <summary>
            /// Reads bytes from the output stream.
            /// </summary>
            /// <param name="bytes">The bytes to be written.</param>
            /// <param name="offset">The position of the first byte to write.</param>
            /// <param name="count">The number of bytes to write.</param>
            /// <returns>The number of bytes read or <b>0</b> when the end of the stream has been reached.</returns>
            public override int Read(byte[] buffer, int offset, int count)
            {
                Covenant.Requires<ArgumentNullException>(buffer != null);
                Covenant.Requires<ArgumentException>(0 <= offset && offset < buffer.Length);
                Covenant.Requires<ArgumentException>(offset + count < buffer.Length);

                // We're going to keep this simple by reading the hex digits 
                // from the underling stream (two at a time).  This will be
                // somewhat slow and we should look into improving this in
                // the future via buffered reads.

                var cbRead = 0;

                for (int i = 0; i < count; i++)
                {
                    char firstHex;
                    char secondHex;

                    // Read the first hex digit (ignoring any whitespace, like CRLF).

                    while (true)
                    {
                        if (input.Read(lineBuffer, 0, 1) == 0)
                        {
                            return cbRead;
                        }

                        firstHex = (char)lineBuffer[0];
                        firstHex = char.ToUpperInvariant(firstHex);

                        if (char.IsWhiteSpace(firstHex))
                        {
                            continue;
                        }

                        if ('0' <= firstHex && firstHex <= '9' ||
                            'A' <= firstHex && firstHex <= 'F')
                        {
                            break;
                        }

                        throw new CryptographicException($"Invalid HEX character in [{nameof(NeonVault)}] input.");
                    }

                    // Read the second hex digit (ignoring any whitespace, like CRLF).

                    while (true)
                    {
                        if (input.Read(lineBuffer, 0, 1) == 0)
                        {
                            throw new CryptographicException($"Odd number of HEX characters in [{nameof(NeonVault)}] input.");
                        }

                        secondHex = (char)lineBuffer[0];
                        secondHex = char.ToUpperInvariant(secondHex);

                        if (char.IsWhiteSpace(secondHex))
                        {
                            continue;
                        }

                        if ('0' <= secondHex && secondHex <= '9' ||
                            'A' <= secondHex && secondHex <= 'F')
                        {
                            break;
                        }

                        throw new CryptographicException($"Invalid HEX character in [{nameof(NeonVault)}] input.");
                    }

                    buffer[cbRead++] = (byte)((NeonHelper.HexValue(firstHex) << 4) | NeonHelper.HexValue(secondHex));
                }

                return cbRead;
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

                    output.Write(Encoding.ASCII.GetBytes(NeonHelper.ToHex(partialLine, uppercase: true) + lineEnding));
                    bufferPos = 0;
                }
            }

            /// <inheritdoc/>
            public override bool CanRead => true;

            /// <inheritdoc/>
            public override bool CanSeek => output != null;

            /// <inheritdoc/>
            public override bool CanWrite => true;

            /// <inheritdoc/>
            public override void Flush()
            {
                if (output != null)
                {
                    output.Flush();
                }
            }

            /// <inheritdoc/>
            public override long Position
            {
                get
                {
                    if (output == null)
                    {
                        throw new NotImplementedException();
                    }

                    return output.Position;
                }

                set
                {
                    if (output == null)
                    {
                        throw new NotImplementedException();
                    }

                    output.Position = value;
                }
            }

            /// <inheritdoc/>
            public override long Seek(long offset, SeekOrigin origin)
            {
                if (output == null)
                {
                    throw new NotImplementedException();
                }

                return output.Seek(offset, origin);
            }

            //-----------------------------------------------------------------
            // Unimplemented stream members.

            /// <inheritdoc/>
            public override long Length => throw new NotImplementedException();

            /// <inheritdoc/>
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
        /// <returns>The password encoded as base-64.</returns>
        /// <exception cref="CryptographicException">Thrown for problems.</exception>
        private string GetKeyFromPassword(string passwordName)
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

                return Convert.ToBase64String(CryptoHelper.DeriveKeyFromPassword(password, KeySize));
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
            using (var target = new MemoryStream())
            {
                Encrypt(source, target, passwordName);

                return target.ToArray();
            }
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
            using (var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read))
            {
                return Encrypt(source, passwordName);
            }
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

            var key = GetKeyFromPassword(passwordName);

            using (var cipher = new AesCipher(key, maxPaddingBytes: 128))
            {
                using (var hexEncrypted = HexLineStream.Create(target, lineEnding))
                {
                    cipher.EncryptStream(source, hexEncrypted);
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
            using (var target = new FileStream(targetPath, FileMode.Open, FileAccess.Read))
            {
                Encrypt(source, target, passwordName);
            }
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
            using (var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read))
            {
                using (var target = new FileStream(targetPath, FileMode.Open, FileAccess.Read))
                {
                    Encrypt(source, target, passwordName);
                }
            }
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
            using (var target = new MemoryStream())
            {
                Decrypt(source, target);

                return target.ToArray();
            }
        }

        /// <summary>
        /// Decrypts file to a byte array.
        /// </summary>
        /// <param name="sourcePath">The source path.</param>
        /// <returns>The decrypted bytes.</returns>
        /// <exception cref="CryptographicException">Thrown if the password was not found or for other decryption problems.</exception>
        public byte[] Decrypt(string sourcePath)
        {
            using (var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read))
            {
                return Decrypt(source);
            }
        }

        /// <summary>
        /// Decrypts a stream to another stream.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <param name="target">The target stream.</param>
        /// <exception cref="CryptographicException">Thrown if the password was not found or for other decryption problems.</exception>
        public void Decrypt(Stream source, Stream target)
        {
            // Read the first 512 bytes of the source stream to detect whether
            // this is a [NeonVault] encrypted file.  We'll simply copy the
            // source stream to the target if it isn't one of our files.

            var isVaultFile = true;
            var startPos    = source.Position;
            var buffer      = new byte[512];
            var cb          = source.Read(buffer, 0, buffer.Length);
            var afterBOMPos = 0;

            // Skip over any UTF-8 byte order marker (BOM) bytes.

            if (cb > 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
            {
                afterBOMPos = 3;
            }

            if (cb - afterBOMPos < MagicBytes.Length)
            {
                isVaultFile = false;
            }

            for (int i = 0; i < MagicBytes.Length; i++)
            {
                if (buffer[i + afterBOMPos] != MagicBytes[i])
                {
                    isVaultFile = false;
                    break;
                }
            }

            if (!isVaultFile)
            {
                source.Position = startPos;

                source.CopyTo(target);
                return;
            }

            // This is a [NeonVault] file.  We need to validate the remaining parameters
            // on the header line, extract the password name.

            var lfPos = -1;

            for (int i = afterBOMPos; i < buffer.Length; i++)
            {
                if (buffer[i] == (char)'\n')
                {
                    lfPos = i + 1;
                    break;
                }
            }

            if (lfPos == -1)
            {
                throw new CryptographicException($"Invalid [{nameof(NeonVault)}] file: Invalid header line.");
            }

            var headerLine = Encoding.UTF8.GetString(buffer, 0, lfPos).Trim();
            var fields     = headerLine.Split(';');

            if (fields.Length != 5)
            {
                throw new CryptographicException($"Invalid [{nameof(NeonVault)}] file: Unexpected number of header line parameters.");
            }

            if (fields[2] != "1.0")
            {
                throw new CryptographicException($"Unsupported [{nameof(NeonVault)}] file: Unexpected version number: {fields[2]}");
            }

            if (fields[3] != "AES256")
            {
                throw new CryptographicException($"Unsupported [{nameof(NeonVault)}] file: Unexpected cipher: {fields[3]}");
            }

            var passwordName = fields[4];
            var key          = GetKeyFromPassword(passwordName);

            // We're going to read and parse the HEX lines using [HexLineStream].

            source.Position = startPos + lfPos + 1;

            using (var cipher = new AesCipher(key))
            {
                using (var hexEncrypted = HexLineStream.Open(source))
                {
                    cipher.DecryptStream(hexEncrypted, target);
                }
            }
        }

        /// <summary>
        /// Decrypts a file to a stream.
        /// </summary>
        /// <param name="sourcePath">The source path.</param>
        /// <param name="target">The target stream.</param>
        /// <exception cref="CryptographicException">Thrown if the password was not found or for other decryption problems.</exception>
        public void Decrypt(string sourcePath, Stream target)
        {
            using (var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read))
            {
                Decrypt(source, target);
            }
        }

        /// <summary>
        /// Decrypts a file to another file.
        /// </summary>
        /// <param name="sourcePath">The source path.</param>
        /// <param name="targetPath">The target path.</param>
        /// <exception cref="CryptographicException">Thrown if the password was not found or for other decryption problems.</exception>
        public void Decrypt(string sourcePath, string targetPath)
        {
            using (var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read))
            {
                using (var target = new FileStream(targetPath, FileMode.Open, FileAccess.Read))
                {
                    Decrypt(source, target);
                }
            }
        }
    }
}
