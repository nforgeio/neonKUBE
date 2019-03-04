//-----------------------------------------------------------------------------
// FILE:	    Test_FileCommands.cs
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon;
using Neon.Common;
using Neon.Kube;
using Neon.IO;
using NShell;
using Neon.Xunit;
using Neon.Xunit.Kube;

using Xunit;
using Neon.Cryptography;

namespace Test.NShell
{
    public class Test_FileCommands
    {
        private const string testPassword        = "don't forget your bitcoin password!";
        private const string missingPasswordName = "missing-password-123456";
        private const string badPasswordName     = "bad/password";
        private const string plainText           = "The quick brown fox jumped over the lazy dog.";

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void FileNone()
        {
            using (var runner = new ProgramRunner())
            {
                var result = runner.Execute(Program.Main, "file");

                Assert.Equal(0, result.ExitCode);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void FileCreate()
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
                            // Verify that the PASSWORD-NAME argument is required when there's
                            // no default [.password-name] file.

                            var result = runner.Execute(Program.Main, "file", "create", "test1.txt");

                            Assert.NotEqual(0, result.ExitCode);
                            Assert.Contains("*** ERROR: A PASSWORD-NAME argument or [.password-name] file is required.", result.ErrorText);

                            // Verify that we can create an encrypted file with an explicitly 
                            // named password.

                            result = runner.Execute(Program.Main, "file", "create", "test2.txt", passwordFile.Name);

                            Assert.Equal(0, result.ExitCode);
                            Assert.True(NeonVault.IsEncrypted("test2.txt"));
                            Assert.Equal(plainText, Encoding.UTF8.GetString(vault.Decrypt("test2.txt")));

                            // Verify that we see an error for a missing password.

                            result = runner.Execute(Program.Main, "file", "create", "test3.txt", missingPasswordName);

                            Assert.NotEqual(0, result.ExitCode);
                            Assert.Contains($"*** ERROR: [CryptographicException]: Password named [{missingPasswordName}] not found or is blank, or whitespace.", result.ErrorText);

                            // Verify that we see an error for an invalid password.

                            result = runner.Execute(Program.Main, "file", "create", "test4.txt", badPasswordName);

                            Assert.NotEqual(0, result.ExitCode);
                            Assert.Contains($"*** ERROR: [CryptographicException]: Password name [bad/password] contains invalid characters.  Only ASCII letters, digits, underscores, dashs and dots are allowed.", result.ErrorText);

                            // Verify that a local [.password-name] file is used successfully when we don't 
                            // explicitly pass a password name.

                            File.WriteAllText(".password-name", passwordFile.Name);

                            result = runner.Execute(Program.Main, "file", "create", "test5.txt");
                            Assert.Equal(0, result.ExitCode);
                            Assert.True(NeonVault.IsEncrypted("test5.txt"));
                            Assert.Equal(plainText, Encoding.UTF8.GetString(vault.Decrypt("test5.txt")));

                            // Verify that a [.password-name] file in the parent directory is used successfully
                            // when we don't explicitly pass a password name.

                            Directory.CreateDirectory("subfolder");
                            Environment.CurrentDirectory = Path.Combine(Environment.CurrentDirectory, "subfolder");

                            result = runner.Execute(Program.Main, "file", "create", "test6.txt");
                            Assert.Equal(0, result.ExitCode);
                            Assert.True(NeonVault.IsEncrypted("test6.txt"));
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
}
