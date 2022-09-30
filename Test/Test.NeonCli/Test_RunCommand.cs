//-----------------------------------------------------------------------------
// FILE:	    Test_RunCommand.cs
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
    public class Test_RunCommand
    {
        [Fact]
        public async Task Run()
        {
            using (var runner = new ProgramRunner())
            {
                // Verify that the help command works.

                var result = await runner.ExecuteAsync(Program.Main, "tool", "run", "--help");

                Assert.Equal(0, result.ExitCode);
                Assert.Contains("Runs a sub-command, optionally injecting settings and secrets and/or", result.OutputText);

                // Verify ther error we get when there's no right command line.

                result = await runner.ExecuteAsync(Program.Main, "tool", "run");

                Assert.NotEqual(0, result.ExitCode);
                Assert.Contains("*** ERROR: Expected a command after a [--] argument.", result.ErrorText);
            }
        }

        [Fact]
        public async Task ReadVariables_Decrypted()
        {
            var orgDirectory = Environment.CurrentDirectory;

            try
            {
                using (var testManager = new KubeTestManager())
                {
                    using (var runner = new ProgramRunner())
                    {
                        using (var tempFolder = new TempFolder())
                        {
                            Environment.CurrentDirectory = tempFolder.Path;

                            var vault = new NeonVault(Program.LookupPassword);

                            // Create a test password and a [.password-name] file in the
                            // temp test folder.

                            var result = await runner.ExecuteAsync(Program.Main, "tool", "password", "set", "test");
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
                            result = await runner.ExecuteAsync(Program.Main, "tool", "run", "var1.txt", "var2.txt", "--", "test.cmd", "_.TEST_A", "_.TEST_B", "_.TEST_C", "_.TEST_D");
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
        public async Task ReadVariables_Ecrypted()
        {
            var orgDirectory = Environment.CurrentDirectory;

            try
            {
                using (var testManager = new KubeTestManager())
                {
                    using (var runner = new ProgramRunner())
                    {
                        using (var tempFolder = new TempFolder())
                        {
                            Environment.CurrentDirectory = tempFolder.Path;

                            var vault = new NeonVault(Program.LookupPassword);

                            // Create a test password and a [.password-name] file in the
                            // temp test folder.

                            var result = await runner.ExecuteAsync(Program.Main, "tool", "password", "set", "test");
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

                            result = await runner.ExecuteAsync(Program.Main, "tool", "run", "var1.txt", "var2.txt", "--", "test.cmd", "_.TEST_A", "_.TEST_B", "_.TEST_C", "_.TEST_D");
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
        public async Task VariableOptions()
        {
            var orgDirectory = Environment.CurrentDirectory;

            try
            {
                using (var testManager = new KubeTestManager())
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

                            var result = await runner.ExecuteAsync(Program.Main, "tool", "run", "--TEST_A=A-VALUE", "--TEST_B=B-VALUE", "--TEST_C=C-VALUE", "--TEST_D=D-VALUE", "--", "test.cmd", "_.TEST_A", "_.TEST_B", "_.TEST_C", "_.TEST_D");
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
        public async Task VariableInjectDecrypted()
        {
            var orgDirectory = Environment.CurrentDirectory;

            try
            {
                using (var testManager = new KubeTestManager())
                {
                    using (var runner = new ProgramRunner())
                    {
                        using (var tempFolder = new TempFolder())
                        {
                            Environment.CurrentDirectory = tempFolder.Path;

                            var vault = new NeonVault(Program.LookupPassword);

                            // Create a test password and a [.password-name] file in the
                            // temp test folder.

                            var result = await runner.ExecuteAsync(Program.Main, "tool", "password", "set", "test");
                            Assert.Equal(0, result.ExitCode);

                            //-------------------------------------------------
                            // Verify that we can inject variables into an
                            // unencrypted file.

                            File.WriteAllText("test.cmd", "type %1 > output.txt");

                            File.WriteAllText("file.txt",
@"
$<env:TEST_A>
$<env:TEST_B>
$<env:TEST_C>
$<env:TEST_D>
");
                            result = await runner.ExecuteAsync(Program.Main, "tool", "run", "--TEST_A=A-VALUE", "--TEST_B=B-VALUE", "--TEST_C=C-VALUE", "--TEST_D=D-VALUE", "--", "test.cmd", "_..file.txt");
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
        public async Task VariableInjectEncrypted()
        {
            var orgDirectory = Environment.CurrentDirectory;

            try
            {
                using (var testManager = new KubeTestManager())
                {
                    using (var runner = new ProgramRunner())
                    {
                        using (var tempFolder = new TempFolder())
                        {
                            Environment.CurrentDirectory = tempFolder.Path;

                            var vault = new NeonVault(Program.LookupPassword);

                            // Create a test password and a [.password-name] file in the
                            // temp test folder.

                            var result = await runner.ExecuteAsync(Program.Main, "tool", "password", "set", "test");
                            Assert.Equal(0, result.ExitCode);

                            //-------------------------------------------------
                            // Verify that we can inject variables into an
                            // ENCRYPTED file.

                            File.WriteAllText("test.cmd", "type %1 > output.txt");

                            File.WriteAllText("file.txt",
@"
$<env:TEST_A>
$<env:TEST_B>
$<env:TEST_C>
$<env:TEST_D>
");
                            File.WriteAllBytes("file.txt", vault.Encrypt("file.txt", "test"));
                            Assert.True(NeonVault.IsEncrypted("file.txt"));

                            result = await runner.ExecuteAsync(Program.Main, "tool", "run", "--TEST_A=A-VALUE", "--TEST_B=B-VALUE", "--TEST_C=C-VALUE", "--TEST_D=D-VALUE", "--", "test.cmd", "_..file.txt");
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
        public async Task DecryptFile()
        {
            var orgDirectory = Environment.CurrentDirectory;

            try
            {
                using (var testManager = new KubeTestManager())
                {
                    using (var runner = new ProgramRunner())
                    {
                        using (var tempFolder = new TempFolder())
                        {
                            Environment.CurrentDirectory = tempFolder.Path;

                            var vault = new NeonVault(Program.LookupPassword);

                            // Create a test password and a [.password-name] file in the
                            // temp test folder.

                            var result = await runner.ExecuteAsync(Program.Main, "tool", "password", "set", "test");
                            Assert.Equal(0, result.ExitCode);

                            //-------------------------------------------------
                            // Verify that we can decrypt a file.

                            File.WriteAllText("test.cmd", "type %1 > output.txt");

                            const string plainText = "The quick brown fox jumped over the lazy dog.";

                            File.WriteAllText("file.txt", plainText);
                            File.WriteAllBytes("file.txt", vault.Encrypt("file.txt", "test"));
                            Assert.True(NeonVault.IsEncrypted("file.txt"));

                            result = await runner.ExecuteAsync(Program.Main, "tool", "run", "--", "test.cmd", "_...file.txt");
                            Assert.Equal(0, result.ExitCode);

                            var output = File.ReadAllText("output.txt");

                            Assert.Contains(plainText, output);
                            File.Delete("output.txt");
                            File.Delete("file.txt");

                            //-------------------------------------------------
                            // Try this again calling a command directly (batch files seem to have different behavior).

                            File.WriteAllText("type", "%1");

                            File.WriteAllText("file.txt", plainText);
                            File.WriteAllBytes("file.txt", vault.Encrypt("file.txt", "test"));
                            Assert.True(NeonVault.IsEncrypted("file.txt"));

                            result = await runner.ExecuteAsync(Program.Main, "tool", "run", "--", "cat", "_...file.txt");
                            Assert.Equal(0, result.ExitCode);

                            Assert.Contains(plainText, result.OutputText);
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
        public async Task StandardFiles()
        {
            // We had a problem with [NeonHelper_Execute] where standard output
            // and standard error streams weren't being relayed back to the
            // corresponding [neon-cli] streams when the sub-process was an
            // actual executable rather than a batch script.  So the unit tests
            // above that ran a script passed.
            // 
            // This test verifies that we get output from an actual executable.

            var orgDirectory = Environment.CurrentDirectory;

            try
            {
                using (var testManager = new KubeTestManager())
                {
                    using (var runner = new ProgramRunner())
                    {
                        using (var tempFolder = new TempFolder())
                        {
                            Environment.CurrentDirectory = tempFolder.Path;

                            File.WriteAllText("test.txt", "Hello World!");

                            var result = await runner.ExecuteAsync(Program.Main, "tool", "run", "--", "cat", "test.txt");
                            Assert.Equal(0, result.ExitCode);
                            Assert.Contains("Hello World!", result.AllText);
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
        public async Task Options()
        {
            var orgDirectory = Environment.CurrentDirectory;

            try
            {
                using (var testManager = new KubeTestManager())
                {
                    using (var runner = new ProgramRunner())
                    {
                        using (var tempFolder = new TempFolder())
                        {
                            Environment.CurrentDirectory = tempFolder.Path;

                            //-------------------------------------------------
                            // Verify that the [run] command handles command line options correctly.

                            File.WriteAllText("test.cmd", "echo %* > output.txt");

                            var result = await runner.ExecuteAsync(Program.Main, "tool", "run", "--", "test.cmd", "-foo", "--bar=foobar", "--hello", "world");
                            Assert.Equal(0, result.ExitCode);

                            Assert.Contains("-foo --bar=foobar --hello world", File.ReadAllText("output.txt"));
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
