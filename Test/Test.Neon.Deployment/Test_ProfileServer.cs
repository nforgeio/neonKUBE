//-----------------------------------------------------------------------------
// FILE:	    Test_ProfileServer.cs
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

using Microsoft.Extensions.DependencyInjection;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

using Neon.Common;
using Neon.Deployment;
using Neon.IO;
using Neon.Xunit;

namespace TestDeployment
{
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public partial class Test_ProfileServer
    {
        /// <summary>
        /// The profile server has the potential for race condition type bugs
        /// so we can use this constant to repeat the tests several times to
        // gain more confidence.
        //// </summary>
        private const int repeatCount = 10;

        /// <summary>
        // Use a unique pipe name so we won't conflict with the real profile
        // server if it's running on this machine.
        /// </summary>
        private const string pipeName = "9621a996-b35f-4f84-8c6c-7ff72cb69106";

        /// <summary>
        /// Delay to be introduced by the profile client between sending the request
        /// and reading the response.  This is used to detect potential race conditions
        /// when communicating with the server.
        /// </summary>
        private readonly TimeSpan ClientDebugDelay = TimeSpan.FromMilliseconds(10);

        /// <summary>
        /// Sets handlers that return reasonable default values.
        /// </summary>
        /// <param name="server">The assistant erver.</param>
        private void SetDefaultHandlers(ProfileServer server)
        {
            server.GetProfileValueHandler = name => ProfileHandlerResult.Create($"{name}-profile");

            server.GetSecretPasswordHandler = 
                (name, vault, masterPassword) =>
                {
                    var sb = new StringBuilder();

                    sb.Append(name);

                    if (vault != null)
                    {
                        sb.AppendWithSeparator(vault, "-");
                    }

                    if (masterPassword != null)
                    {
                        sb.AppendWithSeparator(masterPassword, "-");
                    }

                    sb.Append("-password");

                    return ProfileHandlerResult.Create(sb.ToString());
                };

            server.GetSecretValueHandler =
                (name, vault, masterPassword) =>
                {
                    var sb = new StringBuilder();

                    sb.Append(name);

                    if (vault != null)
                    {
                        sb.AppendWithSeparator(vault, "-");
                    }

                    if (masterPassword != null)
                    {
                        sb.AppendWithSeparator(masterPassword, "-");
                    }

                    sb.Append("-secret");

                    return ProfileHandlerResult.Create(sb.ToString());
                };
        }

        [Theory]
        [Repeat(repeatCount)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonDeployment)]
        public void MultipleRequests_Sequential(int repeatCount)
        {
            // Verify that the server is able to handle multiple requests
            // submitted one at a time.

            var client = new ProfileClient(pipeName);

            using (var server = new ProfileServer(pipeName))
            {
                SetDefaultHandlers(server);
                server.Start();

                Assert.Equal("zero-profile", client.GetProfileValue("zero"));
                Assert.Equal("one-profile", client.GetProfileValue("one"));
                Assert.Equal("two-profile", client.GetProfileValue("two"));
                Assert.Equal("three-profile", client.GetProfileValue("three"));
                Assert.Equal("four-profile", client.GetProfileValue("four"));
                Assert.Equal("five-profile", client.GetProfileValue("five"));
                Assert.Equal("six-profile", client.GetProfileValue("six"));
                Assert.Equal("seven-profile", client.GetProfileValue("seven"));
                Assert.Equal("eight-profile", client.GetProfileValue("eight"));
                Assert.Equal("nine-profile", client.GetProfileValue("nine"));
            }
        }

        [Theory]
        [Repeat(repeatCount)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonDeployment)]
        public async Task MultipleRequests_Parallel(int repeatCount)
        {
            // Verify that the server is able to handle multiple requests
            // submitted in parallel but with only one server thread.

            var client = new ProfileClient(pipeName);

            using (var server = new ProfileServer(pipeName))
            {
                SetDefaultHandlers(server);
                server.Start();

                await Task.Run(() => Assert.Equal("zero-profile", client.GetProfileValue("zero")));
                await Task.Run(() => Assert.Equal("one-profile", client.GetProfileValue("one")));
                await Task.Run(() => Assert.Equal("two-profile", client.GetProfileValue("two")));
                await Task.Run(() => Assert.Equal("three-profile", client.GetProfileValue("three")));
                await Task.Run(() => Assert.Equal("four-profile", client.GetProfileValue("four")));
                await Task.Run(() => Assert.Equal("five-profile", client.GetProfileValue("five")));
                await Task.Run(() => Assert.Equal("six-profile", client.GetProfileValue("six")));
                await Task.Run(() => Assert.Equal("seven-profile", client.GetProfileValue("seven")));
                await Task.Run(() => Assert.Equal("eight-profile", client.GetProfileValue("eight")));
                await Task.Run(() => Assert.Equal("nine-profile", client.GetProfileValue("nine")));
            }

            // Verify that the server is able to handle multiple requests
            // submitted in parallel with multiple server threads.

            using (var server = new ProfileServer(pipeName, threadCount: 10))
            {
                SetDefaultHandlers(server);
                server.Start();

                await Task.Run(() => Assert.Equal("zero-profile", client.GetProfileValue("zero")));
                await Task.Run(() => Assert.Equal("one-profile", client.GetProfileValue("one")));
                await Task.Run(() => Assert.Equal("two-profile", client.GetProfileValue("two")));
                await Task.Run(() => Assert.Equal("three-profile", client.GetProfileValue("three")));
                await Task.Run(() => Assert.Equal("four-profile", client.GetProfileValue("four")));
                await Task.Run(() => Assert.Equal("five-profile", client.GetProfileValue("five")));
                await Task.Run(() => Assert.Equal("six-profile", client.GetProfileValue("six")));
                await Task.Run(() => Assert.Equal("seven-profile", client.GetProfileValue("seven")));
                await Task.Run(() => Assert.Equal("eight-profile", client.GetProfileValue("eight")));
                await Task.Run(() => Assert.Equal("nine-profile", client.GetProfileValue("nine")));
            }
        }

        [Theory]
        [Repeat(repeatCount)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonDeployment)]
        public void GetProfileValue(int repeatCount)
        {
            var client = new ProfileClient(pipeName);

            using (var server = new ProfileServer(pipeName))
            {
                SetDefaultHandlers(server);
                server.Start();

                Assert.Equal("test-profile", client.GetProfileValue("test"));
            }
        }

        [Theory]
        [Repeat(repeatCount)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonDeployment)]
        public void GetProfileValue_Exception(int repeatCount)
        {
            var client = new ProfileClient(pipeName);

            using (var server = new ProfileServer(pipeName))
            {
                SetDefaultHandlers(server);

                server.GetProfileValueHandler = name => throw new Exception("test exception");

                server.Start();

                Assert.Throws<ProfileException>(() => client.GetProfileValue("test"));
            }
        }

        [Theory]
        [Repeat(repeatCount)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonDeployment)]
        public void GetSecretPassword(int repeatCount)
        {
            var client = new ProfileClient(pipeName);

            using (var server = new ProfileServer(pipeName))
            {
                SetDefaultHandlers(server);
                server.Start();

                Assert.Equal("test-password", client.GetSecretPassword("test"));
            }
        }

        [Theory]
        [Repeat(repeatCount)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonDeployment)]
        public void GetSecretPassword_UsingMasterPassword(int repeatCount)
        {
            var client = new ProfileClient(pipeName);

            using (var server = new ProfileServer(pipeName))
            {
                SetDefaultHandlers(server);
                server.Start();

                Assert.Equal("test-vault-master-password", client.GetSecretPassword("test", "vault", "master"));
            }
        }

        [Theory]
        [Repeat(repeatCount)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonDeployment)]
        public void GetSecretPassword_Exception(int repeatCount)
        {
            var client = new ProfileClient(pipeName);

            using (var server = new ProfileServer(pipeName))
            {
                SetDefaultHandlers(server);

                server.GetSecretPasswordHandler = (name, value, masterpassword) => throw new Exception("test exception");

                server.Start();

                Assert.Throws<ProfileException>(() => client.GetSecretPassword("test"));
            }
        }

        [Theory]
        [Repeat(repeatCount)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonDeployment)]
        public void GetSecretValue(int repeatCount)
        {
            var client = new ProfileClient(pipeName);

            using (var server = new ProfileServer(pipeName))
            {
                SetDefaultHandlers(server);
                server.Start();

                Assert.Equal("test-secret", client.GetSecretValue("test"));
            }
        }

        [Theory]
        [Repeat(repeatCount)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonDeployment)]
        public void GetSecretValue_UsingMasterPassword(int repeatCount)
        {
            var client = new ProfileClient(pipeName);

            using (var server = new ProfileServer(pipeName))
            {
                SetDefaultHandlers(server);
                server.Start();

                Assert.Equal("test-vault-master-secret", client.GetSecretValue("test", "vault", "master"));
            }
        }

        [Theory]
        [Repeat(repeatCount)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonDeployment)]
        public void GetSecretValue_Exception(int repeatCount)
        {
            var client = new ProfileClient(pipeName);

            using (var server = new ProfileServer(pipeName))
            {
                SetDefaultHandlers(server);

                server.GetSecretValueHandler = (name, value, masterpassword) => throw new Exception("test exception");

                server.Start();

                Assert.Throws<ProfileException>(() => client.GetSecretValue("test"));
            }
        }
    }
}
