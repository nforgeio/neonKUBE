//-----------------------------------------------------------------------------
// FILE:	    Test_TestContext.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Neon.Docker;
using Neon.Kube;
using Neon.Kube.Xunit;
using Neon.IO;
using Neon.Xunit;

using Xunit;

namespace TestXunit
{
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_TestContext
    {
        private const string password = "don't forget your bitcoin password!";

        private Dictionary<string, string> testSettings;

        public Test_TestContext()
        {
            testSettings = new Dictionary<string, string>()
            {
                { "_NEON_XUNIT_TEST_SETTING_0", "zero" },
                { "_NEON_XUNIT_TEST_SETTING_1", "one" },
                { "_NEON_XUNIT_TEST_SETTING_2", "two" },
            };
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonXunit)]
        public void LoadSettingsDecrypted()
        {
            // Verify that we can load a decrypted settings file into the
            // [TestContext.Settings] dictionary.

            using (var manager = new KubeTestManager())
            {
                using (var tempFile = new TempFile())
                {
                    // Initialze the file.

                    using (var writer = new StreamWriter(tempFile.Path))
                    {
                        foreach (var item in testSettings)
                        {
                            writer.WriteLine($"{item.Key}={item.Value}");
                        }
                    }

                    // Perform the test.

                    Assert.Null(TestContext.Current);

                    using (var testContext = new TestContext())
                    {
                        Assert.Same(testContext, TestContext.Current);

                        testContext.LoadSettings(tempFile.Path);

                        foreach (var item in testSettings)
                        {
                            Assert.Equal(item.Value, testContext.Settings[item.Key]);
                        }
                    }
                }
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonXunit)]
        public void LoadSettingsEncrypted()
        {
            // Verify that we can load an encrypted settings file into the
            // [TestContext.Settings] dictionary.

            using (var manager = new KubeTestManager())
            {
                using (var tempFile = new TempFile())
                {
                    // Initialze the file.

                    using (var writer = new StreamWriter(tempFile.Path))
                    {
                        foreach (var item in testSettings)
                        {
                            writer.WriteLine($"{item.Key}={item.Value}");
                        }
                    }

                    var passwordPath = Path.Combine(KubeHelper.PasswordsFolder, "test");

                    File.WriteAllText(passwordPath, password);

                    var vault = new NeonVault(KubeHelper.LookupPassword);

                    File.WriteAllBytes(tempFile.Path, vault.Encrypt(tempFile.Path, "test"));
                    Assert.True(NeonVault.IsEncrypted(tempFile.Path));

                    // Perform the test.

                    Assert.Null(TestContext.Current);

                    using (var testContext = new TestContext())
                    {
                        Assert.Same(testContext, TestContext.Current);

                        testContext.LoadSettings(tempFile.Path, KubeHelper.LookupPassword);

                        foreach (var item in testSettings)
                        {
                            Assert.Equal(item.Value, testContext.Settings[item.Key]);
                        }
                    }
                }
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonXunit)]
        public void LoadEnvironmentDecrypted()
        {
            // Verify that we can load a decrypted settings file into the
            // environment variables.

            using (var manager = new KubeTestManager())
            {
                try
                {
                    using (var tempFile = new TempFile())
                    {
                        // Initialze the file.

                        using (var writer = new StreamWriter(tempFile.Path))
                        {
                            foreach (var item in testSettings)
                            {
                                writer.WriteLine($"{item.Key}={item.Value}");
                            }
                        }

                        // Perform the test.

                        Assert.Null(TestContext.Current);

                        using (var testContext = new TestContext())
                        {
                            Assert.Same(testContext, TestContext.Current);

                            testContext.LoadEnvironment(tempFile.Path);

                            foreach (var item in testSettings)
                            {
                                Assert.Equal(item.Value, Environment.GetEnvironmentVariable(item.Key));
                            }
                        }
                    }
                }
                finally
                {
                    // Be sure to remove the test enmvironment variables.

                    foreach (var item in testSettings)
                    {
                        Environment.SetEnvironmentVariable(item.Key, null);
                    }
                }
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonXunit)]
        public void LoadEnvironmentEncrypted()
        {
            // Verify that we can load an encryoted settings file into the
            // environment variables.

            using (var manager = new KubeTestManager())
            {
                try
                {
                    using (var tempFile = new TempFile())
                    {
                        // Initialze the file.

                        using (var writer = new StreamWriter(tempFile.Path))
                        {
                            foreach (var item in testSettings)
                            {
                                writer.WriteLine($"{item.Key}={item.Value}");
                            }
                        }

                        var passwordPath = Path.Combine(KubeHelper.PasswordsFolder, "test");

                        File.WriteAllText(passwordPath, password);

                        var vault = new NeonVault(KubeHelper.LookupPassword);

                        File.WriteAllBytes(tempFile.Path, vault.Encrypt(tempFile.Path, "test"));
                        Assert.True(NeonVault.IsEncrypted(tempFile.Path));

                        // Perform the test.

                        Assert.Null(TestContext.Current);

                        using (var testContext = new TestContext())
                        {
                            Assert.Same(testContext, TestContext.Current);

                            testContext.LoadEnvironment(tempFile.Path, KubeHelper.LookupPassword);

                            foreach (var item in testSettings)
                            {
                                Assert.Equal(item.Value, Environment.GetEnvironmentVariable(item.Key));
                            }
                        }
                    }
                }
                finally
                {
                    // Be sure to remove the test enmvironment variables.

                    foreach (var item in testSettings)
                    {
                        Environment.SetEnvironmentVariable(item.Key, null);
                    }
                }
            }
        }
    }
}
