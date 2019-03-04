//-----------------------------------------------------------------------------
// FILE:        Test_NeonVault.cs
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
    public class Test_NeonVault
    {
        private static string   unencryptedText =
@"One bright morning in the middle of the night
two dead boys got up to fight
Back to back they faced each other
Drew their swords and shot each other
A deaf policeman heard the noise
and ran to save the two dead boys
If you don't believe this lie is true
ask the blind man, he saw it, too.
";
        private static byte[]   unencryptedBytes = Encoding.UTF8.GetBytes(unencryptedText);
        private static string   password1        = NeonHelper.GetCryptoRandomPassword(20);
        private static string   password2        = NeonHelper.GetCryptoRandomPassword(20);

        private string GetPassword(string name)
        {
            if (name == "password-1")
            {
                return password1;
            }
            else if (name == "password-2")
            {
                return password2;
            }
            else
            {
                throw new KeyNotFoundException();
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void NoPassword()
        {
            // Verify the proper exception when a named password cannot be found.

            var vault = new NeonVault(passwordName => throw new KeyNotFoundException());

            using (var source = new MemoryStream(unencryptedBytes))
            {
                Assert.Throws<CryptographicException>(() => vault.Encrypt(source, "password-1"));
            }

            // $todo(jeff.lill)

            // Verify the exception when an encrypted file references a password
            // that doesn't exist.


        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void BadPasswordNames()
        {
            // Verify the proper exception when a password name is invalid.

            var vault = new NeonVault(passwordName => NeonHelper.GetCryptoRandomPassword(20));

            using (var source = new MemoryStream(unencryptedBytes))
            {
                Assert.Throws<CryptographicException>(() => vault.Encrypt(source, null));
                Assert.Throws<CryptographicException>(() => vault.Encrypt(source, string.Empty));
                Assert.Throws<CryptographicException>(() => vault.Encrypt(source, "bad\\name"));
                Assert.Throws<CryptographicException>(() => vault.Encrypt(source, "bad/name"));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void GoodPasswordNames()
        {
            // Verify valid password names.

            var vault = new NeonVault(passwordName => NeonHelper.GetCryptoRandomPassword(20));

            using (var source = new MemoryStream(unencryptedBytes))
            {
                vault.Encrypt(source, "a");
                vault.Encrypt(source, "a_b");
                vault.Encrypt(source, "a.b");
                vault.Encrypt(source, "a-b");
                vault.Encrypt(source, "a1");
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void StreamToBytes()
        {
            var vault     = new NeonVault(GetPassword);
            var encrypted = (byte[])null;

            using (var source = new MemoryStream(unencryptedBytes))
            {
                encrypted = vault.Encrypt(source, "password-1");
            }

            // $todo(jeff.lill): DELETE THIS!

            var text = Encoding.ASCII.GetString(encrypted);

            using (var source = new MemoryStream(encrypted))
            {
                var decrypted = vault.Decrypt(source);

                Assert.Equal(unencryptedBytes, decrypted);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void FileToBytes()
        {
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void StreamToStream()
        {
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void StreamToFile()
        {
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void FileToFile()
        {
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void LargeStreamToStream()
        {
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void TamperDetect()
        {
            // Verify that we can detect that the data has been tampered with.
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void WrongPassword()
        {
            // Verify that we can detect that the wrong password was used.
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void Unencrypted()
        {
            // Verify that "decrypting" an unencrypted file simply copies
            // the data to the output.
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void BadHEX()
        {
            // Verify that an invalid character in the HEX part is detected.

            // Verify that an odd number of HEX digits is detected.
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void LowercaseHEX()
        {
            // Verify that we can process lower-case HEX digits.
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void SwitchLineEndings()
        {
            // Verify that we can still decrypt en encrypted file after 
            // changing the line endings from CRLF --> LF:

            // Verify that we can still decrypt en encrypted file after 
            // changing the line endings from LF --> CRLF:

        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void BOM()
        {
            // Verify that [NeonVault] can ignore UTF-8 BOM markers.
        }
    }
}
