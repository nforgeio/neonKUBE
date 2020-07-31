//-----------------------------------------------------------------------------
// FILE:	    Test_ComplexService.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Kube;
using Neon.Service;
using Neon.Xunit;

using Xunit;

namespace TestNeonService
{
    /// <summary>
    /// Demonstrates how to test the <see cref="ComplexService"/> that has a single
    /// HTTP endpoint and that also exercises environment variable and file based 
    /// configuration.
    /// </summary>
    public class Test_ComplexService : IClassFixture<NeonServiceFixture<ComplexService>>
    {
        private NeonServiceFixture<ComplexService>   fixture;

        public Test_ComplexService(NeonServiceFixture<ComplexService> fixture)
        {
            fixture.Start(() => CreateService());

            this.fixture = fixture;
        }

        /// <summary>
        /// Returns the service map.
        /// </summary>
        private ServiceMap CreateServiceMap()
        {
            var description = new ServiceDescription()
            {
                Name    = "complex-service",
                Address = "127.0.0.10"
            };

            description.Endpoints.Add(
                new ServiceEndpoint()
                {
                    Protocol   = ServiceEndpointProtocol.Http,
                    PathPrefix = "/",
                    Port       = 666
                });

            var serviceMap = new ServiceMap();

            serviceMap.Add(description);

            return serviceMap;
        }

        /// <summary>
        /// Creates a <see cref="ComplexService"/> instance.
        /// </summary>
        /// <returns>The service instance.</returns>
        private ComplexService CreateService()
        {
            return new ComplexService(CreateServiceMap(), "complex-service");
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonKube)]
        public async Task NoConfig()
        {
            // Restart the service without specifying a configuration variable 
            // or file and verify that its endpoint returns the default response.

            var service = CreateService();

            fixture.Restart(() => service);
            Assert.True(fixture.IsRunning);

            var client = fixture.GetHttpClient();

            Assert.Equal("UNCONFIGURED", await client.GetStringAsync("/"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonKube)]
        public async Task EnvironmentConfig()
        {
            // Restart the service specifying the configuration via
            // an environment variable.

            var service = CreateService();

            service.SetEnvironmentVariable("WEB_RESULT", "From: ENVIRONMENT");

            fixture.Restart(() => service);
            Assert.True(fixture.IsRunning);

            var client = fixture.GetHttpClient();

            Assert.Equal("From: ENVIRONMENT", await client.GetStringAsync("/"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonKube)]
        public async Task EmulatedFileConfig()
        {
            // Restart the service specifying the configuration via
            // a emulated configuration file.

            var service = CreateService();

            service.SetConfigFile("/etc/complex/response", "From: VIRTUAL FILE");

            fixture.Restart(() => service);
            Assert.True(fixture.IsRunning);

            var client = fixture.GetHttpClient();

            Assert.Equal("From: VIRTUAL FILE", await client.GetStringAsync("/"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonKube)]
        public async Task PhysicalFileConfig()
        {
            // Restart the service specifying the configuration via
            // a physical configuration file.

            using (var tempFile = new TempFile())
            {
                File.WriteAllText(tempFile.Path, "From: PHYSICAL FILE");

                var service = CreateService();

                service.SetConfigFilePath("/etc/complex/response", tempFile.Path);

                fixture.Restart(() => service);
                Assert.True(fixture.IsRunning);

                var client = fixture.GetHttpClient();

                Assert.Equal("From: PHYSICAL FILE", await client.GetStringAsync("/"));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonKube)]
        public async Task EncryptedFileConfig()
        {
            // Restart the service specifying the configuration via
            // an encrypted physical configuration file.

            var password = "foobar";
            var vault    = new NeonVault(passwordName => password);

            using (var tempFolder = new TempFolder())
            {
                var decryptedPath = Path.Combine(tempFolder.Path, "decrypted");
                var encryptedPath = Path.Combine(tempFolder.Path, "encrypted");

                File.WriteAllText(decryptedPath, "From: ENCRYPTED FILE");
                vault.Encrypt(decryptedPath, encryptedPath, "foo");
                Assert.True(NeonVault.IsEncrypted(encryptedPath));

                var service = CreateService();

                service.SetConfigFilePath("/etc/complex/response", encryptedPath, passwordName => password);

                fixture.Restart(() => service);
                Assert.True(fixture.IsRunning);

                var client = fixture.GetHttpClient();

                Assert.Equal("From: ENCRYPTED FILE", await client.GetStringAsync("/"));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonKube)]
        public async Task EnvironmentFileConfig()
        {
            // Restart the service specifying by loading a file with
            // the environment variable assignment.

            using (var tempFile = new TempFile())
            {
                File.WriteAllText(tempFile.Path,
@"# This is a comment.

WEB_RESULT=HELLO WORLD!
");

                var service = CreateService();

                service.LoadEnvironmentVariables(tempFile.Path);

                fixture.Restart(() => service);
                Assert.True(fixture.IsRunning);

                var client = fixture.GetHttpClient();

                Assert.Equal("HELLO WORLD!", await client.GetStringAsync("/"));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonKube)]
        public async Task EncryptedEnvironmentFileConfig()
        {
            // Restart the service specifying by loading an encrypted file with
            // the environment variable assignment.

            var password = "foobar";
            var vault    = new NeonVault(passwordName => password);

            using (var tempFolder = new TempFolder())
            {
                var decryptedPath = Path.Combine(tempFolder.Path, "decrypted");
                var encryptedPath = Path.Combine(tempFolder.Path, "encrypted");

                File.WriteAllText(decryptedPath,
@"# This is a comment.

WEB_RESULT=HELLO WORLD! (encrypted)
");

                var service = CreateService();

                service.LoadEnvironmentVariables(decryptedPath);
                vault.Encrypt(decryptedPath, encryptedPath, "foo");
                Assert.True(NeonVault.IsEncrypted(encryptedPath));

                fixture.Restart(() => service);
                Assert.True(fixture.IsRunning);

                var client = fixture.GetHttpClient();

                Assert.Equal("HELLO WORLD! (encrypted)", await client.GetStringAsync("/"));
            }
        }
    }
}
