//-----------------------------------------------------------------------------
// FILE:        Test_ContainerRegistries.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Neon.Common;
using Neon.IO;
using Neon.Operator.Xunit;
using Neon.Kube.Resources.Cluster;
using Neon.Kube;
using Neon.Kube.K8s;
using NeonNodeAgent;

using k8s;
using k8s.Models;

using Telerik.JustMock;

using Tomlyn;

namespace TestNeonNodeAgent
{
    public class Test_ContainerRegistries : IClassFixture<OperatorFixture>
    {
        private OperatorFixture fixture;

        public Test_ContainerRegistries(OperatorFixture fixture)
        {
            this.fixture = fixture;

            fixture.Operator.AddController<CrioConfigController>();
            fixture.Start();
        }

        [Fact]
        public async void TestCrioConfigurationSingleRegistry()
        {
            fixture.ClearResources();
            fixture.RegisterType<V1CrioConfiguration>();

            Mock.SetupStatic(typeof(Node), Behavior.CallOriginal, StaticConstructor.NonMocked);
            Mock.Arrange(() => Node.ExecuteCaptureAsync("pkill", new object[] { "-HUP", "crio" }, null, null, null, null, null, null)).ReturnsAsync(new ExecuteResponse(0));

            Mock.SetupStatic(typeof(NeonHelper), Behavior.CallOriginal, StaticConstructor.NonMocked);
            Mock.Arrange(() => NeonHelper.IsLinux).Returns(true);

            var tempFile = new TempFile();

            Mock.SetupStatic(typeof(CrioConfigController), Behavior.Loose);
            Mock.Arrange(() => CrioConfigController.configMountPath).Returns(tempFile.Path);

            var linux = NeonHelper.IsLinux;

            var controller = fixture.Operator.GetController<CrioConfigController>();

            var crioConfig             = new V1CrioConfiguration().Initialize();
            crioConfig.Metadata.Name   = Neon.Kube.KubeConst.ClusterCrioConfigName;
            crioConfig.Spec            = new V1CrioConfiguration.CrioConfigurationSpec();
            crioConfig.Spec.Registries = new List<KeyValuePair<string, V1NeonContainerRegistry.RegistrySpec>>();

            var containerRegistry = new V1NeonContainerRegistry.RegistrySpec()
            {
                Username    = "user",
                Password    = "password",
                Location    = "docker.io",
                SearchOrder = -1
            };

            crioConfig.Spec.Registries.Add(new KeyValuePair<string, V1NeonContainerRegistry.RegistrySpec>(Guid.NewGuid().ToString(), containerRegistry));

            Mock.Arrange(() => Node.ExecuteCaptureAsync(
                "/usr/bin/podman",
                new object[]
                {
                    "login",
                    containerRegistry.Location,
                    "--username", containerRegistry.Username,
                    "--password", containerRegistry.Password
                },
                null, null, null, null, null, null)).ReturnsAsync(new ExecuteResponse(0));

            // This may hang if the debug window is enabled.
            Mock.SetupStatic(typeof(Task), Behavior.CallOriginal, StaticConstructor.NonMocked);
            Mock.Arrange(() => Task.Delay(TimeSpan.FromSeconds(15))).Returns(Task.CompletedTask);

            await controller.ReconcileAsync(crioConfig);

            var result = File.ReadAllText(tempFile.Path).Trim();

            Assert.Equal(@"unqualified-search-registries = []

[[registry]]
prefix   = """"
insecure = false
blocked  = false
location = ""docker.io""", 
result, 
ignoreLineEndingDifferences: true, 
ignoreWhiteSpaceDifferences: true);

        }
    }
}

#endif
