//-----------------------------------------------------------------------------
// FILE:	    Test_NeonAssistantServer.cs
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
    public partial class Test_NeonAssistantServer
    {
        /// <summary>
        /// Sets handlers that return reasonable defauilt values.
        /// </summary>
        /// <param name="server">The assistant erver.</param>
        private void SetDefaultHandlers(NeonAssistantServer server)
        {
            server.GetMasterPasswordHandler = () => NeonAssistantHandlerResult.Create("master");
            server.GetProfileValueHandler   = name => NeonAssistantHandlerResult.Create($"{name}-profile");
            server.GetSecretPasswordHandler = (name, vault) => NeonAssistantHandlerResult.Create(vault == null ? $"{name}-password" : $"{name}-{vault}-password");
            server.GetSecretValueHandler    = (name, vault) => NeonAssistantHandlerResult.Create(vault == null ? $"{name}-secret" : $"{name}-{vault}-secret");
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonDeployment)]
        public void GetMasterPassword()
        {
            var client = new NeonAssistantClient();

            using (var server = new NeonAssistantServer())
            {
                SetDefaultHandlers(server);
                server.Start();

                Assert.Equal("master", client.GetMasterPassword());
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonDeployment)]
        public void GetMasterPassword_Exception()
        {
            var client = new NeonAssistantClient();

            using (var server = new NeonAssistantServer())
            {
                SetDefaultHandlers(server);

                server.GetMasterPasswordHandler = () => throw new Exception("test exception");

                server.Start();

                Assert.Throws<NeonAssistantException>(() => client.GetMasterPassword());
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonDeployment)]
        public void GetProfileValue()
        {
            var client = new NeonAssistantClient();

            using (var server = new NeonAssistantServer())
            {
                SetDefaultHandlers(server);
                server.Start();

                Assert.Equal("test-profile", client.GetProfileValue("test"));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonDeployment)]
        public void GetProfileValue_Exception()
        {
            var client = new NeonAssistantClient();

            using (var server = new NeonAssistantServer())
            {
                SetDefaultHandlers(server);

                server.GetProfileValueHandler = name => throw new Exception("test exception");

                server.Start();

                Assert.Throws<NeonAssistantException>(() => client.GetProfileValue("test"));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonDeployment)]
        public void GetSecretPassword()
        {
            var client = new NeonAssistantClient();

            using (var server = new NeonAssistantServer())
            {
                SetDefaultHandlers(server);
                server.Start();

                Assert.Equal("test-password", client.GetSecretPassword("test"));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonDeployment)]
        public void GetSecretPassword_Exception()
        {
            var client = new NeonAssistantClient();

            using (var server = new NeonAssistantServer())
            {
                SetDefaultHandlers(server);

                server.GetSecretPasswordHandler = (name, value) => throw new Exception("test exception");

                server.Start();

                Assert.Throws<NeonAssistantException>(() => client.GetSecretPassword("test"));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonDeployment)]
        public void GetSecretValue()
        {
            var client = new NeonAssistantClient();

            using (var server = new NeonAssistantServer())
            {
                SetDefaultHandlers(server);
                server.Start();

                Assert.Equal("test-secret", client.GetSecretValue("test"));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonDeployment)]
        public void GetSecretValue_Exception()
        {
            var client = new NeonAssistantClient();

            using (var server = new NeonAssistantServer())
            {
                SetDefaultHandlers(server);

                server.GetSecretValueHandler = (name, value) => throw new Exception("test exception");

                server.Start();

                Assert.Throws<NeonAssistantException>(() => client.GetSecretValue("test"));
            }
        }
    }
}
