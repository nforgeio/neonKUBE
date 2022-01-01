//-----------------------------------------------------------------------------
// FILE:        Test_NeonVault.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
    [Trait(TestTrait.Category, TestArea.NeonCryptography)]
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
        public void NoPassword()
        {
            // Verify the proper exception when a named password cannot be found.

            var vault = new NeonVault(passwordName => throw new KeyNotFoundException());

            using (var source = new MemoryStream(unencryptedBytes))
            {
                Assert.Throws<CryptographicException>(() => vault.Encrypt(source, "password-1"));
            }

            // Verify the exception when an encrypted file references a password
            // that doesn't exist.

            using (var tempFolder = new TempFolder())
            {
                vault = new NeonVault(passwordName => password1);

                var sourcePath = Path.Combine(tempFolder.Path, "source.txt");

                File.WriteAllText(sourcePath, unencryptedText);

                var encrypted = vault.Encrypt(sourcePath, "password-1");

                vault = new NeonVault(passwordName => throw new KeyNotFoundException());

                using (var source = new MemoryStream(encrypted))
                {
                    Assert.Throws<CryptographicException>(() => vault.Decrypt(source));
                }
            }
        }

        [Fact]
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
                Assert.Throws<CryptographicException>(() => vault.Encrypt(source, "bad.name!"));
            }
        }

        [Fact]
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
        public void StreamToBytes()
        {
            var vault     = new NeonVault(GetPassword);
            var encrypted = (byte[])null;

            using (var source = new MemoryStream(unencryptedBytes))
            {
                encrypted = vault.Encrypt(source, "password-1");
            }

            using (var source = new MemoryStream(encrypted))
            {
                var decrypted = vault.Decrypt(source);

                Assert.Equal(unencryptedBytes, decrypted);
            }
        }

        [Fact]
        public void FileToBytes()
        {
            var vault     = new NeonVault(GetPassword);
            var encrypted = (byte[])null;

            using (var tempFolder = new TempFolder())
            {
                var sourcePath = Path.Combine(tempFolder.Path, "source.txt");

                File.WriteAllText(sourcePath, unencryptedText);

                encrypted = vault.Encrypt(sourcePath, "password-1");

                using (var source = new MemoryStream(encrypted))
                {
                    var decrypted = vault.Decrypt(source);

                    Assert.Equal(unencryptedBytes, decrypted);
                }
            }
        }

        [Fact]
        public void StreamToStream()
        {
            var vault = new NeonVault(GetPassword);

            using (var tempFolder = new TempFolder())
            {
                var sourcePath = Path.Combine(tempFolder.Path, "source.txt");
                var targetPath = Path.Combine(tempFolder.Path, "target.txt");

                File.WriteAllText(sourcePath, unencryptedText);

                using (var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read))
                {
                    using (var target = new FileStream(targetPath, FileMode.Create, FileAccess.ReadWrite))
                    {
                        vault.Encrypt(source, target, "password-1");
                    }
                }

                using (var target = new FileStream(targetPath, FileMode.Open, FileAccess.Read))
                {
                    var decrypted = vault.Decrypt(target);

                    Assert.Equal(unencryptedBytes, decrypted);
                }
            }
        }

        [Fact]
        public void StreamToFile()
        {
            var vault = new NeonVault(GetPassword);

            using (var tempFolder = new TempFolder())
            {
                var sourcePath = Path.Combine(tempFolder.Path, "source.txt");
                var targetPath = Path.Combine(tempFolder.Path, "target.txt");

                File.WriteAllText(sourcePath, unencryptedText);

                using (var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read))
                {
                    vault.Encrypt(source, targetPath, "password-1");
                }

                using (var target = new FileStream(targetPath, FileMode.Open, FileAccess.Read))
                {
                    var decrypted = vault.Decrypt(target);

                    Assert.Equal(unencryptedBytes, decrypted);
                }
            }
        }

        [Fact]
        public void FileToFile()
        {
            var vault = new NeonVault(GetPassword);

            using (var tempFolder = new TempFolder())
            {
                var sourcePath = Path.Combine(tempFolder.Path, "source.txt");
                var targetPath = Path.Combine(tempFolder.Path, "target.txt");

                File.WriteAllText(sourcePath, unencryptedText);

                vault.Encrypt(sourcePath, targetPath, "password-1");

                using (var target = new FileStream(targetPath, FileMode.Open, FileAccess.Read))
                {
                    var decrypted = vault.Decrypt(target);

                    Assert.Equal(unencryptedBytes, decrypted);
                }

                Assert.False(NeonVault.IsEncrypted(sourcePath));
                Assert.True(NeonVault.IsEncrypted(targetPath));
            }
        }

        [Fact]
        public void TamperDetect()
        {
            // Verify that we can detect that the data has been tampered with.

            var vault = new NeonVault(GetPassword);

            using (var tempFolder = new TempFolder())
            {
                var sourcePath = Path.Combine(tempFolder.Path, "source.txt");
                var targetPath = Path.Combine(tempFolder.Path, "target.txt");

                File.WriteAllText(sourcePath, unencryptedText);

                vault.Encrypt(sourcePath, targetPath, "password-1");

                using (var target = new FileStream(targetPath, FileMode.Open, FileAccess.Read))
                {
                    var decrypted = vault.Decrypt(target);

                    Assert.Equal(unencryptedBytes, decrypted);
                }

                // Modify the last HEX digit in the target file and verify
                // that decryption fails.

                using (var target = new FileStream(targetPath, FileMode.Open, FileAccess.ReadWrite))
                {
                    var encrypted = new byte[(int)target.Length];

                    target.Read(encrypted, 0, encrypted.Length);

                    var lastHexDigit = (char)encrypted[encrypted.Length - 1];

                    if (lastHexDigit == '0')
                    {
                        lastHexDigit = '1';
                    }
                    else
                    {
                        lastHexDigit = '0';
                    }

                    encrypted[encrypted.Length - 1] = (byte)lastHexDigit;

                    target.Position = 0;
                    target.Write(encrypted);
                }

                Assert.Throws<CryptographicException>(
                    () =>
                    {
                        using (var target = new FileStream(targetPath, FileMode.Open, FileAccess.Read))
                        {
                            var decrypted = vault.Decrypt(target);

                            Assert.Equal(unencryptedBytes, decrypted);
                        }
                    });
            }
        }

        [Fact]
        public void WrongPassword()
        {
            // Verify that we can detect that the wrong password was used.

            using (var tempFolder = new TempFolder())
            {
                var vault      = new NeonVault(passwordName => password1);
                var sourcePath = Path.Combine(tempFolder.Path, "source.txt");
                var targetPath = Path.Combine(tempFolder.Path, "target.txt");

                File.WriteAllText(sourcePath, unencryptedText);

                vault.Encrypt(sourcePath, targetPath, "password-1");

                using (var target = new FileStream(targetPath, FileMode.Open, FileAccess.Read))
                {
                    var decrypted = vault.Decrypt(target);

                    Assert.Equal(unencryptedBytes, decrypted);
                }

                vault = new NeonVault(passwordName => password2);   // This uses the wrong password.

                Assert.Throws<CryptographicException>(
                    () =>
                    {
                        using (var target = new FileStream(targetPath, FileMode.Open, FileAccess.Read))
                        {
                            vault.Decrypt(target);
                        }
                    });
            }
        }

        [Fact]
        public void Unencrypted()
        {
            // Verify that "decrypting" an unencrypted file simply copies
            // the data to the output.

            var vault = new NeonVault(GetPassword);

            using (var tempFolder = new TempFolder())
            {
                var sourcePath = Path.Combine(tempFolder.Path, "source.txt");
                var targetPath = Path.Combine(tempFolder.Path, "target.txt");

                File.WriteAllText(sourcePath, unencryptedText);

                vault.Decrypt(sourcePath, targetPath);

                Assert.Equal(unencryptedText, File.ReadAllText(targetPath));
                Assert.False(NeonVault.IsEncrypted(sourcePath));
                Assert.False(NeonVault.IsEncrypted(targetPath));
            }
        }

        [Fact]
        public void BadHEX()
        {
            var vault = new NeonVault(GetPassword);

            using (var tempFolder = new TempFolder())
            {
                // Verify that an invalid character in the HEX part is detected.

                var sourcePath = Path.Combine(tempFolder.Path, "source.txt");
                var targetPath = Path.Combine(tempFolder.Path, "target.txt");

                File.WriteAllText(sourcePath, unencryptedText);

                vault.Encrypt(sourcePath, targetPath, "password-1");

                using (var target = new FileStream(targetPath, FileMode.Open, FileAccess.Read))
                {
                    var decrypted = vault.Decrypt(target);

                    Assert.Equal(unencryptedBytes, decrypted);
                }

                // Modify the last HEX digit to be the invalid HEX digit 'Z'.

                using (var target = new FileStream(targetPath, FileMode.Open, FileAccess.ReadWrite))
                {
                    var encrypted = new byte[(int)target.Length];

                    target.Read(encrypted, 0, encrypted.Length);

                    encrypted[encrypted.Length - 1] = (byte)'Z';

                    target.Position = 0;
                    target.Write(encrypted);
                }

                Assert.Throws<CryptographicException>(
                    () =>
                    {
                        using (var target = new FileStream(targetPath, FileMode.Open, FileAccess.Read))
                        {
                            var decrypted = vault.Decrypt(target);

                            Assert.Equal(unencryptedBytes, decrypted);
                        }
                    });

                // Verify that an odd number of HEX digits is detected by removing
                // the last character of the file.

                using (var target = new FileStream(targetPath, FileMode.Open, FileAccess.ReadWrite))
                {
                    var encrypted = new byte[(int)target.Length];

                    target.Read(encrypted, 0, encrypted.Length);

                    target.Position = 0;
                    target.Write(encrypted, 0, encrypted.Length - 1);
                }

                Assert.Throws<CryptographicException>(
                    () =>
                    {
                        using (var target = new FileStream(targetPath, FileMode.Open, FileAccess.Read))
                        {
                            var decrypted = vault.Decrypt(target);

                            Assert.Equal(unencryptedBytes, decrypted);
                        }
                    });
            }
        }

        [Fact]
        public void LowercaseHEX()
        {
            // Verify that we can process lower-case HEX digits.

            var vault = new NeonVault(GetPassword);

            using (var tempFolder = new TempFolder())
            {
                var sourcePath  = Path.Combine(tempFolder.Path, "source.txt");
                var targetPath  = Path.Combine(tempFolder.Path, "target.txt");
                var target2Path = Path.Combine(tempFolder.Path, "target2.txt");

                File.WriteAllText(sourcePath, unencryptedText);

                vault.Encrypt(sourcePath, targetPath, "password-1");

                using (var targetReader = new StreamReader(targetPath))
                {
                    using (var target2Writer = new StreamWriter(target2Path))
                    {
                        // Copy the first line as-is and then write the remaining lines
                        // as lowercase.

                        var first = true;

                        foreach (var line in targetReader.Lines())
                        {
                            if (first)
                            {
                                target2Writer.WriteLine(line);
                                first = false;
                            }
                            else
                            {
                                target2Writer.WriteLine(line.ToUpperInvariant());
                            }
                        }
                    }
                }

                using (var target = new FileStream(targetPath, FileMode.Open, FileAccess.Read))
                {
                    var decrypted = vault.Decrypt(target);

                    Assert.Equal(unencryptedBytes, decrypted);
                }
            }
        }

        [Fact]
        public void SwitchLineEndings()
        {
            // Verify that we can still decrypt en encrypted file after 
            // changing the line endings from CRLF --> LF:

            var vault = new NeonVault(GetPassword, lineEnding: "\r\n");

            using (var tempFolder = new TempFolder())
            {
                var sourcePath  = Path.Combine(tempFolder.Path, "source.txt");
                var targetPath  = Path.Combine(tempFolder.Path, "target.txt");
                var target2Path = Path.Combine(tempFolder.Path, "target2.txt");

                File.WriteAllText(sourcePath, unencryptedText);

                vault.Encrypt(sourcePath, targetPath, "password-1");

                using (var targetReader = new StreamReader(targetPath))
                {
                    using (var target2Writer = new StreamWriter(target2Path))
                    {
                        foreach (var line in targetReader.Lines())
                        {
                            target2Writer.WriteLine(line + "\n");
                        }
                    }
                }

                using (var target = new FileStream(targetPath, FileMode.Open, FileAccess.Read))
                {
                    var decrypted = vault.Decrypt(target);

                    Assert.Equal(unencryptedBytes, decrypted);
                }
            }

            // Verify that we can still decrypt en encrypted file after 
            // changing the line endings from LF --> CRLF:

            vault = new NeonVault(GetPassword, lineEnding: "\n");

            using (var tempFolder = new TempFolder())
            {
                var sourcePath  = Path.Combine(tempFolder.Path, "source.txt");
                var targetPath  = Path.Combine(tempFolder.Path, "target.txt");
                var target2Path = Path.Combine(tempFolder.Path, "target2.txt");

                File.WriteAllText(sourcePath, unencryptedText);

                vault.Encrypt(sourcePath, targetPath, "password-1");

                using (var targetReader = new StreamReader(targetPath))
                {
                    using (var target2Writer = new StreamWriter(target2Path))
                    {
                        foreach (var line in targetReader.Lines())
                        {
                            target2Writer.WriteLine(line + "\r\n");
                        }
                    }
                }

                using (var target = new FileStream(targetPath, FileMode.Open, FileAccess.Read))
                {
                    var decrypted = vault.Decrypt(target);

                    Assert.Equal(unencryptedBytes, decrypted);
                }
            }
        }

        [Fact]
        public void BOM()
        {
            // Verify that [NeonVault] can ignore UTF-8 BOM markers.

            var vault = new NeonVault(GetPassword);

            using (var tempFolder = new TempFolder())
            {
                var sourcePath  = Path.Combine(tempFolder.Path, "source.txt");
                var targetPath  = Path.Combine(tempFolder.Path, "target.txt");
                var target2Path = Path.Combine(tempFolder.Path, "target2.txt");

                File.WriteAllText(sourcePath, unencryptedText);

                vault.Encrypt(sourcePath, targetPath, "password-1");

                using (var targetReader = new StreamReader(targetPath))
                {
                    using (var target2 = new FileStream(target2Path, FileMode.Create, FileAccess.ReadWrite))
                    {
                        // Write the BOM followed by the unmodified encrypted file lines.

                        target2.Write(new byte[] { 0xEF, 0xBB, 0xBF });  // The BOM

                        foreach (var line in targetReader.Lines())
                        {
                            target2.Write(Encoding.ASCII.GetBytes(line + "\r\n"));
                        }
                    }
                }

                using (var target = new FileStream(targetPath, FileMode.Open, FileAccess.Read))
                {
                    var decrypted = vault.Decrypt(target);

                    Assert.Equal(unencryptedBytes, decrypted);
                }
            }
        }
    }
}
