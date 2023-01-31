//-----------------------------------------------------------------------------
// FILE:	    Test_ContainerRegistries.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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

using Microsoft.Extensions.DependencyInjection;

using Neon.Kube.Operator.Xunit;
using Neon.Kube.Resources.Cluster;
using NeonNodeAgent;

using k8s;
using k8s.Models;

using Moq;

using Neon.Kube;

namespace TestNeonNodeAgent
{
    public class Test_ContainerRegistries : IClassFixture<TestOperatorFixture>
    {
        private TestOperatorFixture fixture;

        public Test_ContainerRegistries(TestOperatorFixture fixture)
        {
            this.fixture = fixture;

            var service = new Service(KubeService.NeonNodeAgent);
            service.SetEnvironmentVariable("CONTAINERREGISTRY_RELOGIN_INTERVAL", TimeSpan.FromHours(1).ToString());
            service.SetConfigFile(ContainerRegistryController.configMountPath, "");
            fixture.Operator.Services.AddSingleton(service);
            fixture.Operator.AddController<ContainerRegistryController>();
            fixture.Start();
        }

        [Fact]
        public async void Test1()
        {
            fixture.ClearResources();
            fixture.RegisterType<V1NeonContainerRegistry>();

            var controller = fixture.Operator.GetController<ContainerRegistryController>();

            var containerRegistry = new V1NeonContainerRegistry()
            {
                Metadata = new V1ObjectMeta()
                {
                    Name = "test"
                },
                Spec = new V1NeonContainerRegistry.RegistrySpec()
                {
                    Username = "user",
                    Password = "password",
                    Location = "docker.io",
                    SearchOrder = -1
                }
            };

            fixture.Resources.Add(containerRegistry);

            await controller.IdleAsync();

            Assert.True(true);
        }
    }
}