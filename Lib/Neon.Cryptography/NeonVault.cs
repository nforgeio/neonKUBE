//-----------------------------------------------------------------------------
// FILE:	    NeonVault.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
    /// Password names are case insensitive and will always be converted to lowercase
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
        // $todo(jefflill):
        //
        // I'm not super happy with this implementation because it first encrypts
        // the data to a MemoryStream and then it writes the header line followed
        // by the encypted data formatted as lines of HEX.
        // 
        // This won't work well for large files and probably won't scale thay well
        // for services.
        //
        // I tried implementing a tricky stream that wrote the HEX lines directly
        // in a single pass, but it couldn't handle the first (non-HEX) line and
        // the seek AesCipher does to write the HMAC was also a problem.  I don't
        // want to mess with this right now.  Perhaps something to look into again
        // in the future.

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
                if (!char.IsLetterOrDigit(ch) && ch != '.' && ch != '-' && ch != '_' || (int)ch > 127)
                {
                    throw new CryptographicException($"Password name [{passwordName}] contains invalid characters.  Only ASCII letters, digits, underscores, dashs and dots are allowed.");
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
            return IsEncrypted(path, out var passwordName);
        }

        /// <summary>
        /// Determines if a file is encrypted via <see cref="NeonVault"/> and returns
        /// the name of the password used.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="passwordName">For encrypted files, this returns as the name of the password used.</param>
        /// <returns><c>true</c> if the file is encrypted.</returns>
        public static bool IsEncrypted(string path, out string passwordName)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                return IsEncrypted(stream, out passwordName);
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
            return IsEncrypted(stream, out var passwordName);
        }

        /// <summary>
        /// Determines if a stream is encrypted via <see cref="NeonVault"/> and returns
        /// the name of the password used.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="passwordName">For encrypted files, this returns as the name of the password used.</param>
        /// <returns><c>true</c> if the stream is encrypted.</returns>
        /// <remarks>
        /// <note>
        /// The stream position must be at the beginning of the stream for this to work.
        /// </note>
        /// </remarks>
        public static bool IsEncrypted(Stream stream, out string passwordName)
        {
            Covenant.Requires<ArgumentNullException>(stream != null, nameof(stream));

            passwordName = null;

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

                if (!line.StartsWith(MagicString))
                {
                    return false;
                }

                // Extract the password name:

                var fields = line.Split(';');

                if (fields.Length < 5)
                {
                    throw new CryptographicException("Invalid encrypted file format.");
                }

                passwordName = fields[4];

                return true;
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
            Covenant.Requires<ArgumentNullException>(passwordProvider != null, nameof(passwordProvider));
            Covenant.Requires<ArgumentException>(lineEnding == null || lineEnding == "\r\n" || lineEnding == "\n", nameof(lineEnding));

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
                    throw new CryptographicException($"Password named [{passwordName}] not found or is blank or whitespace.");
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
            Covenant.Requires<ArgumentNullException>(source != null, nameof(source));
            Covenant.Requires<ArgumentException>(source.CanRead && source.CanSeek, nameof(source));
            Covenant.Requires<ArgumentNullException>(target != null, nameof(target));

            var key = GetKeyFromPassword(passwordName);

            passwordName = passwordName.ToLowerInvariant();

            using (var cipher = new AesCipher(key, maxPaddingBytes: 128))
            {
                using (var encrypted = new MemoryStream())
                {
                    cipher.EncryptStream(source, encrypted);

                    // Write the header line to the target.

                    var header = $"{MagicString}1.0;AES256;{passwordName}{lineEnding}";

                    target.Write(Encoding.ASCII.GetBytes(header));

                    // Write the encrypted data as HEX (80 characters per line).

                    var buffer     = new byte[1];
                    var lineLength = 0;

                    encrypted.Position = 0;

                    while (true)
                    {
                        if (encrypted.Read(buffer, 0, 1) == 0)
                        {
                            break;
                        }

                        target.Write(Encoding.ASCII.GetBytes(NeonHelper.ToHex(buffer[0], uppercase: true)));
                        lineLength += 2;

                        if (lineLength == 80)
                        {
                            target.Write(Encoding.ASCII.GetBytes(lineEnding));
                            lineLength = 0;
                        }
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
            using (var target = new FileStream(targetPath, FileMode.Create, FileAccess.ReadWrite))
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
                using (var target = new FileStream(targetPath, FileMode.Create, FileAccess.ReadWrite))
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

            var passwordName = fields[4].ToLowerInvariant();
            var key          = GetKeyFromPassword(passwordName);

            // Read the HEX lines and convert them into bytes and then write then
            // to a MemoryStream to be decrypted.

            source.Position = startPos + lfPos;

            using (var encrypted = new MemoryStream())
            {
                while (true)
                {
                    if (source.Read(buffer, 0, 1) == 0)
                    {
                        break;
                    }

                    var firstHex = char.ToUpperInvariant((char)buffer[0]);

                    if (char.IsWhiteSpace(firstHex))
                    {
                        continue;
                    }

                    if (source.Read(buffer, 0, 1) == 0)
                    {
                        throw new CryptographicException($"Invalid [{nameof(NeonVault)}] file: Odd numer of HEX digits on a line.");
                    }

                    var secondHex = char.ToUpperInvariant((char)buffer[0]);

                    if (char.IsWhiteSpace(firstHex))
                    {
                        throw new CryptographicException($"Invalid [{nameof(NeonVault)}] file: Odd numer of HEX digits on a line.");
                    }

                    if (!NeonHelper.IsHex(firstHex) || !NeonHelper.IsHex(secondHex))
                    {
                        throw new CryptographicException($"Invalid [{nameof(NeonVault)}] file: Invalid HEX digit.");
                    }

                    byte value;

                    if ('0' <= firstHex && firstHex <= '9')
                    {
                        value = (byte)(firstHex - '0');
                    }
                    else
                    {
                        value = (byte)(firstHex - 'A' + 10);
                    }

                    value <<= 4;

                    if ('0' <= secondHex && secondHex <= '9')
                    {
                        value |= (byte)(secondHex - '0');
                    }
                    else
                    {
                        value |= (byte)(secondHex - 'A' + 10);
                    }

                    encrypted.WriteByte(value);
                }

                encrypted.Position = 0;

                using (var cipher = new AesCipher(key))
                {
                    cipher.DecryptStream(encrypted, target);
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
                using (var target = new FileStream(targetPath, FileMode.Create, FileAccess.ReadWrite))
                {
                    Decrypt(source, target);
                }
            }
        }
    }
}
