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

#if JUSTMOCK

using System;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Neon.Common;
using Neon.IO;
using Neon.Kube.Xunit.Operator;
using Neon.Kube.Resources.Cluster;
using NeonNodeAgent;

using k8s;
using k8s.Models;

using Telerik.JustMock;

using Tomlyn;
using System.Diagnostics;
using System.Linq;

namespace TestNeonNodeAgent
{
    public class Test_ContainerRegistries : IClassFixture<TestOperatorFixture>
    {
        private TestOperatorFixture fixture;

        public Test_ContainerRegistries(TestOperatorFixture fixture)
        {
            this.fixture = fixture;

            fixture.Operator.AddController<ContainerRegistryController>();
            fixture.Start();
        }

        [Fact]
        public async void Test1()
        {
            fixture.ClearResources();
            fixture.RegisterType<V1NeonContainerRegistry>();

            Mock.SetupStatic(typeof(Node), Behavior.CallOriginal, StaticConstructor.NonMocked);
            Mock.Arrange(() => Node.ExecuteCaptureAsync("pkill", new object[] { "-HUP", "crio" }, null, null, null, null, null, null)).ReturnsAsync(new ExecuteResponse(0));

            Mock.SetupStatic(typeof(NeonHelper), Behavior.CallOriginal, StaticConstructor.NonMocked);
            Mock.Arrange(() => NeonHelper.IsLinux).Returns(true);

            var tempFile = new TempFile();

            Mock.SetupStatic(typeof(ContainerRegistryController), Behavior.Loose);
            Mock.Arrange(() => ContainerRegistryController.configMountPath).Returns(tempFile.Path);

            var linux = NeonHelper.IsLinux;

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

            Mock.Arrange(() => Node.ExecuteCaptureAsync(
                "/usr/bin/podman",
                new object[]
                {
                    "login",
                    containerRegistry.Spec.Location,
                    "--username", containerRegistry.Spec.Username,
                    "--password", containerRegistry.Spec.Password
                },
                null, null, null, null, null, null)).ReturnsAsync(new ExecuteResponse(0));

            // This may hang if the debug window is enabled.
            Mock.SetupStatic(typeof(Task), Behavior.CallOriginal, StaticConstructor.NonMocked);
            Mock.Arrange(() => Task.Delay(TimeSpan.FromSeconds(15))).Returns(Task.CompletedTask);

            await controller.IdleAsync();

            var result = File.ReadAllText(tempFile.Path);
            var currentConfig = Toml.Parse(result);

            // todo(marcusbooyah): verify result
        }

        [Fact]
        public async void AddRegistryWithNoCrioConfig()
        {
            fixture.ClearResources();
            fixture.RegisterType<V1NeonContainerRegistry>();
            fixture.RegisterType<V1CrioConfiguration>();

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

            await controller.ReconcileAsync(containerRegistry);

            var crioConfigList = fixture.Resources.OfType<V1CrioConfiguration>();

            Assert.Single(crioConfigList);

            var crioConfig = crioConfigList.FirstOrDefault();

            Assert.Single(crioConfig.Spec.Registries);
            Assert.Equal(containerRegistry.Spec, crioConfig.Spec.Registries.FirstOrDefault().Value);
        }

        [Fact]
        public async void UpdateExistingRegistry()
        {
            fixture.ClearResources();
            fixture.RegisterType<V1NeonContainerRegistry>();
            fixture.RegisterType<V1CrioConfiguration>();

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

            await controller.ReconcileAsync(containerRegistry);

            var crioConfigList = fixture.Resources.OfType<V1CrioConfiguration>();

            Assert.Single(crioConfigList);

            var crioConfig = crioConfigList.FirstOrDefault();

            Assert.Single(crioConfig.Spec.Registries);

            var registry = crioConfig.Spec.Registries.FirstOrDefault();

            Assert.Equal(containerRegistry.Spec.Username, registry.Value.Username);
            Assert.Equal(containerRegistry.Spec.Password, registry.Value.Password);

            containerRegistry.Spec.Username = "bob";
            containerRegistry.Spec.Password = "drowssap";

            await controller.ReconcileAsync(containerRegistry);

            crioConfigList = fixture.Resources.OfType<V1CrioConfiguration>();

            Assert.Single(crioConfigList);

            crioConfig = crioConfigList.FirstOrDefault();

            Assert.Single(crioConfig.Spec.Registries);

            registry = crioConfig.Spec.Registries.FirstOrDefault();

            Assert.Equal(containerRegistry.Spec.Username, registry.Value.Username);
            Assert.Equal(containerRegistry.Spec.Password, registry.Value.Password);
        }
    }
}

#endif