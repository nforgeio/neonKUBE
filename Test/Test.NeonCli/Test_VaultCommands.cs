//-----------------------------------------------------------------------------
// FILE:	    Test_VaultCommands.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon;
using Neon.Common;
using Neon.Cryptography;
using Neon.Kube;
using Neon.Kube.Xunit;
using Neon.IO;
using Neon.Xunit;

using Xunit;
using NeonCli;

namespace Test.NeonCli
{
    [Trait(TestTrait.Category, TestArea.NeonCli)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_VaultCommands
    {
        private const string testPassword        = "don't forget your bitcoin password!";
        private const string missingPasswordName = "missing-password-123456";
        private const string badPasswordName     = "bad/password";
        private const string plainText           = "The quick brown fox jumped over the lazy dog.";
        private const string editedPlainText     = "This is a test of the emergency broadcasting system. This is only a test.";

        [Fact]
        public async Task Vault()
        {
            using (var runner = new ProgramRunner())
            {
                var result = await runner.ExecuteAsync(Program.Main, "tool", "vault");

                Assert.Equal(0, result.ExitCode);
            }
        }

        [Fact]
        public async Task VaultCreate()
        {
            using (var testManager = new KubeTestManager())
            {
                using (var tempFolder = new TempFolder())
                {
                    var orgDir = Environment.CurrentDirectory;

                    Environment.CurrentDirectory = tempFolder.Path;
                    NeonHelper.OpenEditorHandler = path => File.WriteAllText(path, plainText);

                    try
                    {
                        using (var passwordFile = new TempFile(folder: KubeHelper.PasswordsFolder))
                        {
                            File.WriteAllText(passwordFile.Path, testPassword);

                            var vault = new NeonVault(passwordName => testPassword);

                            using (var runner = new ProgramRunner())
                            {
                                // Verify that the PATH argument is required.

                                var result = await runner.ExecuteAsync(Program.Main, "tool", "vault", "create");

                                Assert.NotEqual(0, result.ExitCode);
                                Assert.Contains("*** ERROR: The PATH argument is required.", result.ErrorText);

                                // Verify that the PASSWORD-NAME argument is required when there's
                                // no default [.password-name] file.

                                result = await runner.ExecuteAsync(Program.Main, "tool", "vault", "create", "test1.txt");

                                Assert.NotEqual(0, result.ExitCode);
                                Assert.Contains("*** ERROR: A PASSWORD-NAME argument or [.password-name] file is required.", result.ErrorText);

                                // Verify that we can create an encrypted file with an explicitly 
                                // named password.

                                result = await runner.ExecuteAsync(Program.Main, "tool", "vault", "create", "test2.txt", passwordFile.Name);

                                Assert.Equal(0, result.ExitCode);
                                Assert.True(NeonVault.IsEncrypted("test2.txt", out var passwordName));
                                Assert.Equal(passwordFile.Name, passwordName);
                                Assert.Equal(plainText, Encoding.UTF8.GetString(vault.Decrypt("test2.txt")));

                                // Verify that we see an error for a missing password.

                                result = await runner.ExecuteAsync(Program.Main, "tool", "vault", "create", "test3.txt", missingPasswordName);

                                Assert.NotEqual(0, result.ExitCode);
                                Assert.Contains($"*** ERROR: [System.Security.Cryptography.CryptographicException]: Password named [{missingPasswordName}] not found or is blank or whitespace.", result.ErrorText);

                                // Verify that we see an error for an invalid password.

                                result = await runner.ExecuteAsync(Program.Main, "tool", "vault", "create", "test4.txt", badPasswordName);

                                Assert.NotEqual(0, result.ExitCode);
                                Assert.Contains($"*** ERROR: [System.Security.Cryptography.CryptographicException]: Password name [bad/password] contains invalid characters.  Only ASCII letters, digits, underscores, dashs and dots are allowed.", result.ErrorText);

                                // Verify that a local [.password-name] file is used successfully when we don't 
                                // explicitly pass a password name.

                                File.WriteAllText(".password-name", passwordFile.Name);

                                result = await runner.ExecuteAsync(Program.Main, "tool", "vault", "create", "test5.txt");
                                Assert.Equal(0, result.ExitCode);
                                Assert.True(NeonVault.IsEncrypted("test5.txt", out passwordName));
                                Assert.Equal(passwordFile.Name, passwordName);
                                Assert.Equal(plainText, Encoding.UTF8.GetString(vault.Decrypt("test5.txt")));

                                // Verify that a [.password-name] file in the parent directory is used successfully
                                // when we don't explicitly pass a password name.

                                Directory.CreateDirectory("subfolder");
                                Environment.CurrentDirectory = Path.Combine(Environment.CurrentDirectory, "subfolder");

                                result = await runner.ExecuteAsync(Program.Main, "tool", "vault", "create", "test6.txt");
                                Assert.Equal(0, result.ExitCode);
                                Assert.True(NeonVault.IsEncrypted("test6.txt", out passwordName));
                                Assert.Equal(passwordFile.Name, passwordName);
                                Assert.Equal(plainText, Encoding.UTF8.GetString(vault.Decrypt("test6.txt")));
                            }
                        }
                    }
                    finally
                    {
                        Environment.CurrentDirectory = orgDir;
                        NeonHelper.OpenEditorHandler = null;
                    }
                }
            }
        }

        [Fact]
        public async Task VaulteEdit()
        {
            using (var testManager = new KubeTestManager())
            {
                using (var tempFolder = new TempFolder())
                {
                    var orgDir = Environment.CurrentDirectory;

                    Environment.CurrentDirectory = tempFolder.Path;
                    NeonHelper.OpenEditorHandler = path => File.WriteAllText(path, plainText);

                    try
                    {
                        using (var passwordFile = new TempFile(folder: KubeHelper.PasswordsFolder))
                        {
                            File.WriteAllText(passwordFile.Path, testPassword);

                            var vault = new NeonVault(passwordName => testPassword);

                            using (var runner = new ProgramRunner())
                            {
                                // Verify that the PATH argument is required.

                                var result = await runner.ExecuteAsync(Program.Main, "tool", "vault", "edit");

                                Assert.NotEqual(0, result.ExitCode);
                                Assert.Contains("*** ERROR: The PATH argument is required.", result.ErrorText);

                                // Verify that we can create an encrypted file with an explicitly 
                                // named password.

                                result = await runner.ExecuteAsync(Program.Main, "tool", "vault", "create", "test1.txt", passwordFile.Name);

                                Assert.Equal(0, result.ExitCode);
                                Assert.True(NeonVault.IsEncrypted("test1.txt", out var passwordName));
                                Assert.Equal(passwordFile.Name, passwordName);
                                Assert.Equal(plainText, Encoding.UTF8.GetString(vault.Decrypt("test1.txt")));

                                // Verify that we can edit the file.

                                NeonHelper.OpenEditorHandler = path => File.WriteAllText(path, editedPlainText);

                                result = await runner.ExecuteAsync(Program.Main, "tool", "vault", "edit", "test1.txt", passwordFile.Name);

                                Assert.Equal(0, result.ExitCode);
                                Assert.True(NeonVault.IsEncrypted("test1.txt", out passwordName));
                                Assert.Equal(passwordFile.Name, passwordName);
                                Assert.Equal(editedPlainText, Encoding.UTF8.GetString(vault.Decrypt("test1.txt")));

                                // Verify that we're not allowed to edit a non-encypted file.

                                File.WriteAllText("unencrypted.txt", plainText);

                                result = await runner.ExecuteAsync(Program.Main, "tool", "vault", "edit", "unencrypted.txt");

                                Assert.NotEqual(0, result.ExitCode);
                                Assert.Contains("*** ERROR: The [unencrypted.txt] file is not encrypted.", result.ErrorText);
                            }
                        }
                    }
                    finally
                    {
                        Environment.CurrentDirectory = orgDir;
                        NeonHelper.OpenEditorHandler = null;
                    }
                }
            }
        }

        [Fact]
        public async Task VaultDecrypt()
        {
            using (var testManager = new KubeTestManager())
            {
                using (var tempFolder = new TempFolder())
                {
                    var orgDir = Environment.CurrentDirectory;

                    Environment.CurrentDirectory = tempFolder.Path;
                    NeonHelper.OpenEditorHandler = path => File.WriteAllText(path, plainText);

                    try
                    {
                        using (var passwordFile = new TempFile(folder: KubeHelper.PasswordsFolder))
                        {
                            File.WriteAllText(passwordFile.Path, testPassword);

                            var vault = new NeonVault(passwordName => testPassword);

                            using (var runner = new ProgramRunner())
                            {
                                // Verify that the SOURCE argument is required.

                                var result = await runner.ExecuteAsync(Program.Main, "tool", "vault", "decrypt");

                                Assert.NotEqual(0, result.ExitCode);
                                Assert.Contains("*** ERROR: The SOURCE argument is required.", result.ErrorText);

                                // Verify that the TARGET argument is required.

                                result = await runner.ExecuteAsync(Program.Main, "tool", "vault", "decrypt");

                                Assert.NotEqual(0, result.ExitCode);
                                Assert.Contains("*** ERROR: The SOURCE argument is required.", result.ErrorText);

                                // Verify that the SOURCE-PATH argument is required.

                                result = await runner.ExecuteAsync(Program.Main, "tool", "vault", "decrypt", "test.txt");

                                Assert.NotEqual(0, result.ExitCode);
                                Assert.Contains("*** ERROR: The TARGET argument is required.", result.ErrorText);

                                // Verify that we can create an encrypted file with an explicitly 
                                // named password.

                                result = await runner.ExecuteAsync(Program.Main, "tool", "vault", "create", "test1.txt", passwordFile.Name);

                                Assert.Equal(0, result.ExitCode);
                                Assert.True(NeonVault.IsEncrypted("test1.txt", out var passwordName));
                                Assert.Equal(passwordFile.Name, passwordName);
                                Assert.Equal(plainText, Encoding.UTF8.GetString(vault.Decrypt("test1.txt")));

                                // Verify that we can decrypt the file.

                                result = await runner.ExecuteAsync(Program.Main, "tool", "vault", "decrypt", "test1.txt", "decrypted.txt");

                                Assert.Equal(0, result.ExitCode);
                                Assert.True(NeonVault.IsEncrypted("test1.txt", out passwordName));
                                Assert.True(!NeonVault.IsEncrypted("decrypted.txt", out passwordName));
                                Assert.Equal(plainText, File.ReadAllText("decrypted.txt"));
                            }
                        }
                    }
                    finally
                    {
                        Environment.CurrentDirectory = orgDir;
                        NeonHelper.OpenEditorHandler = null;
                    }
                }
            }
        }

        [Fact]
        public async Task VaultEncrypt()
        {
            using (var testManager = new KubeTestManager())
            {
                using (var tempFolder = new TempFolder())
                {
                    var orgDir = Environment.CurrentDirectory;

                    Environment.CurrentDirectory = tempFolder.Path;
                    NeonHelper.OpenEditorHandler = path => File.WriteAllText(path, plainText);

                    try
                    {
                        using (var passwordFile = new TempFile(folder: KubeHelper.PasswordsFolder))
                        {
                            File.WriteAllText(passwordFile.Path, testPassword);

                            var vault = new NeonVault(passwordName => testPassword);

                            using (var runner = new ProgramRunner())
                            {
                                // Verify that the PATH argument is required.

                                var result = await runner.ExecuteAsync(Program.Main, "tool", "vault", "encrypt");

                                Assert.NotEqual(0, result.ExitCode);
                                Assert.Contains("*** ERROR: The PATH argument is required.", result.ErrorText);

                                // Verify that the TARGET argument is required when [--password-name]
                                // or [--p] is not present.

                                result = await runner.ExecuteAsync(Program.Main, "tool", "vault", "decrypt", "source.txt");

                                Assert.NotEqual(0, result.ExitCode);
                                Assert.Contains("*** ERROR: The TARGET argument is required.", result.ErrorText);

                                // Verify that we can encrypt a file in-place, specifying an
                                // explicit password name (using --password-name=NAME).

                                File.WriteAllText("test1.txt", plainText);

                                result = await runner.ExecuteAsync(Program.Main, "tool", "vault", "encrypt", "test1.txt", $"--password-name={passwordFile.Name}");

                                Assert.Equal(0, result.ExitCode);
                                Assert.True(NeonVault.IsEncrypted("test1.txt", out var passwordName));
                                Assert.Equal(passwordFile.Name, passwordName);
                                Assert.Equal(plainText, Encoding.UTF8.GetString(vault.Decrypt("test1.txt")));

                                // Verify that we can encrypt a file in-place, specifying an
                                // explicit password name (using --p=NAME).

                                File.WriteAllText("test2.txt", plainText);

                                result = await runner.ExecuteAsync(Program.Main, "tool", "vault", "encrypt", "test2.txt", $"-p={passwordFile.Name}");

                                Assert.Equal(0, result.ExitCode);
                                Assert.True(NeonVault.IsEncrypted("test2.txt", out passwordName));
                                Assert.Equal(passwordFile.Name, passwordName);
                                Assert.Equal(plainText, Encoding.UTF8.GetString(vault.Decrypt("test2.txt")));

                                // Verify that we get an error trying to encrypt in-place without a
                                // password name being explicitly specified and also without a
                                // [.password-name] file present.

                                File.WriteAllText("test3.txt", plainText);

                                result = await runner.ExecuteAsync(Program.Main, "tool", "vault", "encrypt", "test3.txt");

                                Assert.NotEqual(0, result.ExitCode);
                                Assert.Contains("*** ERROR: A PASSWORD-NAME argument or [.password-name] file is required.", result.ErrorText);

                                // Verify that we get an error trying to encrypt (not in-place) without a
                                // password name being explicitly specified and also without a
                                // [.password-name] file present.

                                File.WriteAllText("test4.txt", plainText);

                                result = await runner.ExecuteAsync(Program.Main, "tool", "vault", "encrypt", "test4.txt", "test4.encypted.txt");

                                Assert.NotEqual(0, result.ExitCode);
                                Assert.Contains("*** ERROR: A PASSWORD-NAME argument or [.password-name] file is required.", result.ErrorText);

                                // Verify that we can encrypt a file to another with
                                // and explicit password argument.

                                File.WriteAllText("test5.txt", plainText);

                                result = await runner.ExecuteAsync(Program.Main, "tool", "vault", "encrypt", "test5.txt", "test5.encypted.txt", passwordName);

                                Assert.Equal(0, result.ExitCode);
                                Assert.True(NeonVault.IsEncrypted("test5.encypted.txt", out passwordName));
                                Assert.Equal(passwordFile.Name, passwordName);
                                Assert.Equal(plainText, Encoding.UTF8.GetString(vault.Decrypt("test5.encypted.txt")));

                                // Verify that we can encrypt a file to another with
                                // and explicit [--password-name] option.

                                File.WriteAllText("test6.txt", plainText);

                                result = await runner.ExecuteAsync(Program.Main, "tool", "vault", "encrypt", "test6.txt", "test6.encypted.txt", $"--password-name={passwordName}");

                                Assert.Equal(0, result.ExitCode);
                                Assert.True(NeonVault.IsEncrypted("test6.encypted.txt", out passwordName));
                                Assert.Equal(passwordFile.Name, passwordName);
                                Assert.Equal(plainText, Encoding.UTF8.GetString(vault.Decrypt("test6.encypted.txt")));

                                // Verify that we can encrypt a file in-place using a [.password-name] file.

                                File.WriteAllText("test7.txt", plainText);
                                File.WriteAllText(".password-name", passwordName);

                                result = await runner.ExecuteAsync(Program.Main, "tool", "vault", "encrypt", "test7.txt");

                                Assert.Equal(0, result.ExitCode);
                                Assert.True(NeonVault.IsEncrypted("test7.txt", out passwordName));
                                Assert.Equal(passwordFile.Name, passwordName);
                                Assert.Equal(plainText, Encoding.UTF8.GetString(vault.Decrypt("test7.txt")));

                                // Verify that we can encrypt a file (not in-place) to another where 
                                // the source file is located in a different directory from the target
                                // to ensure that we look for the [.password-name] file starting at
                                // the target directory.

                                using (var tempFile = new TempFile())
                                {
                                    File.WriteAllText(tempFile.Path, plainText);

                                    result = await runner.ExecuteAsync(Program.Main, "tool", "vault", "encrypt", tempFile.Path, "test8.encrypted.txt");

                                    Assert.Equal(0, result.ExitCode);
                                    Assert.True(NeonVault.IsEncrypted("test8.encrypted.txt", out passwordName));
                                    Assert.Equal(passwordFile.Name, passwordName);
                                    Assert.Equal(plainText, Encoding.UTF8.GetString(vault.Decrypt("test8.encrypted.txt")));
                                }
                            }
                        }
                    }
                    finally
                    {
                        Environment.CurrentDirectory = orgDir;
                        NeonHelper.OpenEditorHandler = null;
                    }
                }
            }
        }

        [Fact]
        public async Task VaultPasswordName()
        {
            using (var testManager = new KubeTestManager())
            {
                using (var tempFolder = new TempFolder())
                {
                    var orgDir = Environment.CurrentDirectory;

                    Environment.CurrentDirectory = tempFolder.Path;
                    NeonHelper.OpenEditorHandler = path => File.WriteAllText(path, plainText);

                    try
                    {
                        using (var passwordFile = new TempFile(folder: KubeHelper.PasswordsFolder))
                        {
                            File.WriteAllText(passwordFile.Path, testPassword);

                            var vault = new NeonVault(passwordName => testPassword);

                            using (var runner = new ProgramRunner())
                            {
                                // Verify that the PATH argument is required.

                                var result = await runner.ExecuteAsync(Program.Main, "tool", "vault", "password-name");

                                Assert.NotEqual(0, result.ExitCode);
                                Assert.Contains("*** ERROR: The PATH argument is required.", result.ErrorText);

                                // Verify that we can create an encrypted file with an explicitly 
                                // named password.

                                result = await runner.ExecuteAsync(Program.Main, "tool", "vault", "create", "test1.txt", passwordFile.Name);

                                Assert.Equal(0, result.ExitCode);
                                Assert.True(NeonVault.IsEncrypted("test1.txt", out var passwordName));
                                Assert.Equal(passwordFile.Name, passwordName);
                                Assert.Equal(plainText, Encoding.UTF8.GetString(vault.Decrypt("test1.txt")));

                                // Verify that we can get the password with a line ending.

                                result = await runner.ExecuteAsync(Program.Main, "tool", "vault", "password-name", "test1.txt", passwordFile.Name);

                                Assert.Equal(0, result.ExitCode);
                                Assert.Contains(passwordFile.Name, result.OutputText);
                                Assert.Contains('\n', result.OutputText);

                                // Verify that we can get the password without a line ending.

                                result = await runner.ExecuteAsync(Program.Main, "tool", "vault", "password-name", "-n", "test1.txt", passwordFile.Name);

                                Assert.Equal(0, result.ExitCode);
                                Assert.Equal(passwordFile.Name, result.OutputText);
                            }
                        }
                    }
                    finally
                    {
                        Environment.CurrentDirectory = orgDir;
                        NeonHelper.OpenEditorHandler = null;
                    }
                }
            }
        }
    }
}
