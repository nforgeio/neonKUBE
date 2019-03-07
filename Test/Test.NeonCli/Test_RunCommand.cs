//-----------------------------------------------------------------------------
// FILE:	    Test_RunCommand.cs
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
using Neon.Cryptography;
using Neon.Kube;
using Neon.IO;
using Neon.Xunit;
using Neon.Xunit.Kube;

using Xunit;
using NeonCli;

namespace Test.NeonCli
{
    public class Test_RunCommand
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void ReadVariables_Decrypted()
        {
            var orgDirectory = Environment.CurrentDirectory;

            try
            {
                using (var manager = new KubeTestManager())
                {
                    using (var runner = new ProgramRunner())
                    {
                        using (var tempFolder = new TempFolder())
                        {
                            Environment.CurrentDirectory = tempFolder.Path;

                            var vault = new NeonVault(Program.LookupPassword);

                            // Create a test password and a [.password-name] file in the
                            // temp test folder.

                            var result = runner.Execute(Program.Main, $"password", "set", "test");
                            Assert.Equal(0, result.ExitCode);

                            File.WriteAllText(".password-name", "test");

                            //-------------------------------------------------
                            // Verify that we can read variables from a couple of 
                            // unencrypted variable files.

                            File.WriteAllText("test.cmd", "echo %* > output.txt");

                            File.WriteAllText("var1.txt",
@"# This is a comment.

TEST_A=A-VALUE
TEST_B=B-VALUE
");

                            File.WriteAllText("var2.txt",
@"# This is a comment.

TEST_C=C-VALUE
TEST_D=D-VALUE
");
                            result = runner.Execute(Program.Main, $"run", "var1.txt", "var2.txt", "--", "test.cmd", "^TEST_A", "^TEST_B", "^TEST_C", "^TEST_D");
                            Assert.Equal(0, result.ExitCode);

                            var output = File.ReadAllText("output.txt");

                            Assert.Contains("A-VALUE", output);
                            Assert.Contains("B-VALUE", output);
                            Assert.Contains("C-VALUE", output);
                            Assert.Contains("D-VALUE", output);
                            File.Delete("output.txt");
                            File.Delete("var1.txt");
                            File.Delete("var2.txt");
                        }
                    }
                }
            }
            finally
            {
                Environment.CurrentDirectory = orgDirectory;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void ReadVariables_Ecrypted()
        {
            var orgDirectory = Environment.CurrentDirectory;

            try
            {
                using (var manager = new KubeTestManager())
                {
                    using (var runner = new ProgramRunner())
                    {
                        using (var tempFolder = new TempFolder())
                        {
                            Environment.CurrentDirectory = tempFolder.Path;

                            var vault = new NeonVault(Program.LookupPassword);

                            // Create a test password and a [.password-name] file in the
                            // temp test folder.

                            var result = runner.Execute(Program.Main, $"password", "set", "test");
                            Assert.Equal(0, result.ExitCode);

                            File.WriteAllText(".password-name", "test");

                            //-------------------------------------------------
                            // Verify that we can read settings from a couple of 
                            // ENCRYPTED variable files.

                            File.WriteAllText("test.cmd", "echo %* > output.txt");

                            File.WriteAllText("var1.txt",
@"# This is a comment.

TEST_A=A-VALUE
TEST_B=B-VALUE
");
                            File.WriteAllBytes("var1.txt", vault.Encrypt("var1.txt", "test"));
                            Assert.True(NeonVault.IsEncrypted("var1.txt"));

                            File.WriteAllText("var2.txt",
@"# This is a comment.

TEST_C=C-VALUE
TEST_D=D-VALUE
");
                            File.WriteAllBytes("var2.txt", vault.Encrypt("var2.txt", "test"));
                            Assert.True(NeonVault.IsEncrypted("var2.txt"));

                            result = runner.Execute(Program.Main, $"run", "var1.txt", "var2.txt", "--", "test.cmd", "^TEST_A", "^TEST_B", "^TEST_C", "^TEST_D");
                            Assert.Equal(0, result.ExitCode);

                            var output = File.ReadAllText("output.txt");

                            Assert.Contains("A-VALUE", output);
                            Assert.Contains("B-VALUE", output);
                            Assert.Contains("C-VALUE", output);
                            Assert.Contains("D-VALUE", output);
                            File.Delete("output.txt");
                            File.Delete("var1.txt");
                            File.Delete("var2.txt");
                        }
                    }
                }
            }
            finally
            {
                Environment.CurrentDirectory = orgDirectory;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void VariableOptions()
        {
            var orgDirectory = Environment.CurrentDirectory;

            try
            {
                using (var manager = new KubeTestManager())
                {
                    using (var runner = new ProgramRunner())
                    {
                        using (var tempFolder = new TempFolder())
                        {
                            Environment.CurrentDirectory = tempFolder.Path;

                            var vault = new NeonVault(Program.LookupPassword);

                            //-------------------------------------------------
                            // Verify that we can process [--name=value] run command options.

                            File.WriteAllText("test.cmd", "echo %* > output.txt");

                            var result = runner.Execute(Program.Main, $"run", "--TEST_A=A-VALUE", "--TEST_B=B-VALUE", "--TEST_C=C-VALUE", "--TEST_D=D-VALUE", "--", "test.cmd", "^TEST_A", "^TEST_B", "^TEST_C", "^TEST_D");
                            Assert.Equal(0, result.ExitCode);

                            var output = File.ReadAllText("output.txt");

                            Assert.Contains("A-VALUE", output);
                            Assert.Contains("B-VALUE", output);
                            Assert.Contains("C-VALUE", output);
                            Assert.Contains("D-VALUE", output);
                            File.Delete("output.txt");
                        }
                    }
                }
            }
            finally
            {
                Environment.CurrentDirectory = orgDirectory;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void VariableInjectDecrypted()
        {
            var orgDirectory = Environment.CurrentDirectory;

            try
            {
                using (var manager = new KubeTestManager())
                {
                    using (var runner = new ProgramRunner())
                    {
                        using (var tempFolder = new TempFolder())
                        {
                            Environment.CurrentDirectory = tempFolder.Path;

                            var vault = new NeonVault(Program.LookupPassword);

                            // Create a test password and a [.password-name] file in the
                            // temp test folder.

                            var result = runner.Execute(Program.Main, $"password", "set", "test");
                            Assert.Equal(0, result.ExitCode);

                            //-------------------------------------------------
                            // Verify that we can inject variables into an
                            // unencrypted file.

                            File.WriteAllText("test.cmd", "type %1 > output.txt");

                            File.WriteAllText("file.txt",
@"
$<<TEST_A>>
$<<TEST_B>>
$<<TEST_C>>
$<<TEST_D>>
");
                            result = runner.Execute(Program.Main, $"run", "--TEST_A=A-VALUE", "--TEST_B=B-VALUE", "--TEST_C=C-VALUE", "--TEST_D=D-VALUE", "--", "test.cmd", "^^file.txt");
                            Assert.Equal(0, result.ExitCode);

                            var output = File.ReadAllText("output.txt");

                            Assert.Contains("A-VALUE", output);
                            Assert.Contains("B-VALUE", output);
                            Assert.Contains("C-VALUE", output);
                            Assert.Contains("D-VALUE", output);
                            File.Delete("output.txt");
                            File.Delete("file.txt");
                        }
                    }
                }
            }
            finally
            {
                Environment.CurrentDirectory = orgDirectory;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void VariableInjectEncrypted()
        {
            var orgDirectory = Environment.CurrentDirectory;

            try
            {
                using (var manager = new KubeTestManager())
                {
                    using (var runner = new ProgramRunner())
                    {
                        using (var tempFolder = new TempFolder())
                        {
                            Environment.CurrentDirectory = tempFolder.Path;

                            var vault = new NeonVault(Program.LookupPassword);

                            // Create a test password and a [.password-name] file in the
                            // temp test folder.

                            var result = runner.Execute(Program.Main, $"password", "set", "test");
                            Assert.Equal(0, result.ExitCode);

                            //-------------------------------------------------
                            // Verify that we can inject variables into an
                            // ENCRYPTED file.

                            File.WriteAllText("test.cmd", "type %1 > output.txt");

                            File.WriteAllText("file.txt",
@"
$<<TEST_A>>
$<<TEST_B>>
$<<TEST_C>>
$<<TEST_D>>
");
                            File.WriteAllBytes("file.txt", vault.Encrypt("file.txt", "test"));
                            Assert.True(NeonVault.IsEncrypted("file.txt"));

                            result = runner.Execute(Program.Main, $"run", "--TEST_A=A-VALUE", "--TEST_B=B-VALUE", "--TEST_C=C-VALUE", "--TEST_D=D-VALUE", "--", "test.cmd", "^^file.txt");
                            Assert.Equal(0, result.ExitCode);

                            var output = File.ReadAllText("output.txt");

                            Assert.Contains("A-VALUE", output);
                            Assert.Contains("B-VALUE", output);
                            Assert.Contains("C-VALUE", output);
                            Assert.Contains("D-VALUE", output);
                            File.Delete("output.txt");
                            File.Delete("file.txt");
                        }
                    }
                }
            }
            finally
            {
                Environment.CurrentDirectory = orgDirectory;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void DecryptFile()
        {
            var orgDirectory = Environment.CurrentDirectory;

            try
            {
                using (var manager = new KubeTestManager())
                {
                    using (var runner = new ProgramRunner())
                    {
                        using (var tempFolder = new TempFolder())
                        {
                            Environment.CurrentDirectory = tempFolder.Path;

                            var vault = new NeonVault(Program.LookupPassword);

                            // Create a test password and a [.password-name] file in the
                            // temp test folder.

                            var result = runner.Execute(Program.Main, $"password", "set", "test");
                            Assert.Equal(0, result.ExitCode);

                            //-------------------------------------------------
                            // Verify that we can decrypt a file.

                            File.WriteAllText("test.cmd", "type %1 > output.txt");

                            const string plainText = "The quick brown fox jumped over the lazy dog.";

                            File.WriteAllText("file.txt", plainText);
                            File.WriteAllBytes("file.txt", vault.Encrypt("file.txt", "test"));
                            Assert.True(NeonVault.IsEncrypted("file.txt"));

                            result = runner.Execute(Program.Main, $"run", "--", "test.cmd", "@file.txt");
                            Assert.Equal(0, result.ExitCode);

                            var output = File.ReadAllText("output.txt");

                            Assert.Contains(plainText, output);
                            File.Delete("output.txt");
                            File.Delete("file.txt");
                        }
                    }
                }
            }
            finally
            {
                Environment.CurrentDirectory = orgDirectory;
            }
        }
    }
}
