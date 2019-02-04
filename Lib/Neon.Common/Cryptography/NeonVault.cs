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
    /// This class provides several convient methods for encrypting and decrypting
    /// files.  Encrypted files are encoded as ASCII and are formatted like:
    /// </para>
    /// <code>
    /// $NEON_VAULT;4c823a36774ca4ac760f31dd8abe7bd3;1.0;AES256;mypassword
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
    /// for decryption.  The remaining lines is the encrypted data formatted as HEX split
    /// across 80 character lines.
    /// </para>
    /// <para>
    /// This class considers files starting <b>$NEON_VAULT;4c823a36774ca4ac760f31dd8abe7bd3</b>
    /// to be encrypted.  This essentially acts as a very unique magic number.  This is followed 
    /// by the NeonVault format version (currently <b>1.0</b>), the encryption cypher (currently
    /// <b>AES256</b>), and the name of the password that was used for encryption.
    /// </para>
    /// <para>
    /// The decrypt method are smart enough to determine whether a file is not encrypted
    /// and simply write the unencrypted data to the target.  This means that you can
    /// safely call these methods on unencrypted data.
    /// </para>
    /// </remarks>
    public class NeonVault
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Implements file encryption.
        /// </summary>
        private class EncryptStream : Stream
        {
            private Stream      target;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="target">The target stream.</param>
            /// <param name="password">The encryption password.</param>
            public EncryptStream(Stream target, string password)
            {
                Covenant.Requires<ArgumentNullException>(target != null);
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(password));
            }

            protected override void Dispose(bool disposing)
            {
                if (target != null)
                {
                    target.Flush();
                    target.Dispose();
                    target = null;
                }
            }

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotImplementedException();
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

            public override void Write(byte[] buffer, int offset, int count)
            {
                // IMPLEMENT THIS!
            }
        }

        /// <summary>
        /// Implements file decryption.
        /// </summary>
        private class DecryptStream : Stream
        {
            private bool        isEncrypted;
            private Stream      source;

            /// <summary>
            /// Constuctor.
            /// </summary>
            /// <param name="source">The source stream.</param>
            public DecryptStream(Stream source)
            {
                Covenant.Requires<ArgumentNullException>(source != null);
            }

            protected override void Dispose(bool disposing)
            {
                if (source != null)
                {
                    source.Flush();
                    source.Dispose();
                    source = null;
                }
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotImplementedException();
            public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public override void Flush()
            {
                throw new NotImplementedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                // IMPLEMENT THIS!

                return 0;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotImplementedException();
            }

            public override void SetLength(long value)
            {
                throw new NotImplementedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }
        }

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Ensures that a password name is valid.
        /// </summary>
        /// <param name="passwordName">The password name.</param>
        /// <returns>The password name converted to lowercase.</returns>
        /// <exception cref="NeonVaultException">Thrown if the name is invalid.</exception>
        public static string ValidatePasswordName(string passwordName)
        {
            if (string.IsNullOrEmpty(passwordName))
            {
                throw new NeonVaultException("Password name cannot be empty.");
            }

            foreach (var ch in passwordName)
            {
                if (!char.IsLetterOrDigit(ch) && ch != '.' && ch != '-' && ch != '_')
                {
                    throw new NeonVaultException($"Password name [{passwordName}] contains invalid characters.  Only letters, digits, underscores, dashs and dots are allowed.");
                }
            }

            return passwordName.ToLowerInvariant();
        }

        //---------------------------------------------------------------------
        // Instance members

        private Func<string, string> passwordProvider;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="passwordProvider">
        /// Specifies the function that returns the password value for 
        /// a named password.
        /// </param>
        public NeonVault(Func<string, string> passwordProvider)
        {
            Covenant.Requires<ArgumentNullException>(passwordProvider != null);

            this.passwordProvider = passwordProvider;
        }

        /// <summary>
        /// Looks up a password.
        /// </summary>
        /// <param name="passwordName">The password name.</param>
        /// <returns>The password value.</returns>
        /// <exception cref="NeonVaultException">Thrown for problems.</exception>
        private string LookupPassword(string passwordName)
        {
            try
            {
                if (string.IsNullOrEmpty(passwordName))
                {
                    throw new ArgumentNullException(nameof(passwordName));
                }

                passwordName = ValidatePasswordName(passwordName);

                return passwordProvider(passwordName);
            }
            catch (NeonVaultException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new NeonVaultException(e.Message, e);
            }
        }

        /// <summary>
        /// Decrypts a stream as a byte array.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <returns>The decrypted byte array.</returns>
        /// <exception cref="NeonVaultException">Thrown if the password was not found or for other decryption problems.</exception>
        public byte[] DecryptAsBytes(Stream source)
        {
            return null;
        }

        /// <summary>
        /// Decrypte a file as a byte array.
        /// </summary>
        /// <param name="sourcePath">The source path.</param>
        /// <returns>The decrypted bytes.</returns>
        /// <exception cref="NeonVaultException">Thrown if the password was not found or for other decryption problems.</exception>
        public byte[] DecryptAsBytes(string sourcePath)
        {
            return null;
        }

        /// <summary>
        /// Decrypts a stream as a string.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <param name="encoding">Optionally specifies the encoding (defaults to <see cref="Encoding.ASCII"/>).</param>
        /// <returns>The decrypted string.</returns>
        /// <exception cref="NeonVaultException">Thrown if the password was not found or for other decryption problems.</exception>
        public string DecryptAsString(Stream source, Encoding encoding = default)
        {
            return null;
        }

        /// <summary>
        /// Decrypts a file as a string.
        /// </summary>
        /// <param name="sourcePath">The source path.</param>
        /// <param name="encoding">Optionally specifies the encoding (defaults to <see cref="Encoding.ASCII"/>).</param>
        /// <returns>The decrypted string.</returns>
        /// <exception cref="NeonVaultException">Thrown if the password was not found or for other decryption problems.</exception>
        public string DecryptAsString(string sourcePath, Encoding encoding = default)
        {
            return null;
        }

        /// <summary>
        /// Decrypts a stream to another stream.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <param name="target">The target stream.</param>
        /// <exception cref="NeonVaultException">Thrown if the password was not found or for other decryption problems.</exception>
        public void DecryptTo(Stream source, Stream target)
        {
        }

        /// <summary>
        /// Decrypts a file to a stream.
        /// </summary>
        /// <param name="sourcePath">The source path.</param>
        /// <param name="target">The target stream.</param>
        /// <exception cref="NeonVaultException">Thrown if the password was not found or for other decryption problems.</exception>
        public void DecryptTo(string sourcePath, Stream target)
        {
        }

        /// <summary>
        /// Decrypts a file to another file.
        /// </summary>
        /// <param name="sourcePath">The source path.</param>
        /// <param name="targetPath">The target path.</param>
        /// <exception cref="NeonVaultException">Thrown if the password was not found or for other decryption problems.</exception>
        public void DecryptTo(string sourcePath, string targetPath)
        {
        }

        /// <summary>
        /// Encrypts a stream to a byte array.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <param name="passwordName">Identifies the password.</param>
        /// <returns>The encrypted bytes.</returns>
        /// <exception cref="NeonVaultException">Thrown if the password was not found or for other encryption problems.</exception>
        public byte[] EncryptAsBytes(Stream source, string passwordName)
        {
            return null;
        }

        /// <summary>
        /// Encrypts a file to a byte array.
        /// </summary>
        /// <param name="sourcePath">The source path.</param>
        /// <param name="passwordName">Identifies the password.</param>
        /// <returns>The encrypted bytes.</returns>
        /// <exception cref="NeonVaultException">Thrown if the password was not found or for other encryption problems.</exception>
        public byte[] EncryptAsBytes(string sourcePath, string passwordName)
        {
            return null;
        }

        /// <summary>
        /// Encrypts a stream to a string.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <param name="passwordName">Identifies the password.</param>
        /// <returns>The encrypted string.</returns>
        /// <exception cref="NeonVaultException">Thrown if the password was not found or for other encryption problems.</exception>
        public string EncryptAsString(Stream source, string passwordName)
        {
            return null;
        }

        /// <summary>
        /// Encrypts a file to a string.
        /// </summary>
        /// <param name="sourcePath">The source path.</param>
        /// <param name="passwordName">Identifies the password.</param>
        /// <returns>The encrypted string.</returns>
        /// <exception cref="NeonVaultException">Thrown if the password was not found or for other encryption problems.</exception>
        public string EncryptAsString(string sourcePath, string passwordName)
        {
            return null;
        }

        /// <summary>
        /// Encrypts a stream to another stream.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <param name="target">The target stream.</param>
        /// <param name="passwordName">Identifies the password.</param>
        /// <exception cref="NeonVaultException">Thrown if the password was not found or for other encryption problems.</exception>
        public void EncryptTo(Stream source, Stream target, string passwordName)
        {
        }

        /// <summary>
        /// Encrypts a stream to a file.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <param name="targetPath">The target path.</param>
        /// <param name="passwordName">Identifies the password.</param>
        /// <exception cref="NeonVaultException">Thrown if the password was not found or for other encryption problems.</exception>
        public void EncryptTo(Stream source, string targetPath, string passwordName)
        {
        }

        /// <summary>
        /// Encrypts a file to another file.
        /// </summary>
        /// <param name="sourcePath">The source path.</param>
        /// <param name="targetPath">The target path.</param>
        /// <param name="passwordName">Identifies the password.</param>
        /// <exception cref="NeonVaultException">Thrown if the password was not found or for other encryption problems.</exception>
        public void EncryptTo(string sourcePath, string targetPath, string passwordName)
        {
        }
    }
}
