//-----------------------------------------------------------------------------
// FILE:        Test_ContainerRegistries.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright � 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using NeonClusterOperator;

using k8s;
using k8s.Models;

using Telerik.JustMock;

using Tomlyn;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;

namespace TestNeonNodeAgent
{
    public class Test_ContainerRegistries : IClassFixture<TestOperatorFixture>
    {
        private TestOperatorFixture fixture;

        public Test_ContainerRegistries(TestOperatorFixture fixture)
        {
            this.fixture = fixture;

            fixture.Operator.AddController<NeonContainerRegistryController>();
            fixture.Operator.AddFinalizer<NeonContainerRegistryFinalizer>();
            fixture.Start();
        }

        [Fact]
        public async void AddRegistryWithNoCrioConfig()
        {
            fixture.ClearResources();
            fixture.RegisterType<V1NeonContainerRegistry>();
            fixture.RegisterType<V1CrioConfiguration>();

            var controller = fixture.Operator.GetController<NeonContainerRegistryController>();

            var containerRegistry = new V1NeonContainerRegistry()
            {
                Metadata = new V1ObjectMeta()
                {
                    Name = "test"
                },
                Spec = new V1NeonContainerRegistry.RegistrySpec()
                {
                    Username    = "user",
                    Password    = "password",
                    Location    = "docker.io",
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

            var controller        = fixture.Operator.GetController<NeonContainerRegistryController>();
            var containerRegistry = new V1NeonContainerRegistry()
            {
                Metadata = new V1ObjectMeta()
                {
                    Name = "test"
                },
                Spec = new V1NeonContainerRegistry.RegistrySpec()
                {
                    Username    = "user",
                    Password    = "password",
                    Location    = "docker.io",
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

        [Fact]
        public async void RemoveRegistry()
        {
            fixture.ClearResources();
            fixture.RegisterType<V1NeonContainerRegistry>();
            fixture.RegisterType<V1CrioConfiguration>();

            var controller = fixture.Operator.GetController<NeonContainerRegistryController>();
            var finalizer  = fixture.Operator.GetFinalizer<NeonContainerRegistryFinalizer>();

            var crioConfig             = new V1CrioConfiguration().Initialize();
            crioConfig.Metadata.Name   = Neon.Kube.KubeConst.ClusterCrioConfigName;
            crioConfig.Spec            = new V1CrioConfiguration.CrioConfigurationSpec();
            crioConfig.Spec.Registries = new List<KeyValuePair<string, V1NeonContainerRegistry.RegistrySpec>>();

            var containerRegistry           = new V1NeonContainerRegistry().Initialize();
            containerRegistry.Metadata.Name = Guid.NewGuid().ToString();
            containerRegistry.Metadata.Uid  = Guid.NewGuid().ToString();
            containerRegistry.Spec          = new V1NeonContainerRegistry.RegistrySpec()
            {
                Username    = "user",
                Password    = "password",
                Location    = "docker.io",
                SearchOrder = -1
            };

            crioConfig.Spec.Registries.Add(new KeyValuePair<string, V1NeonContainerRegistry.RegistrySpec>(containerRegistry.Uid(), containerRegistry.Spec));

            fixture.Resources.Add(crioConfig);

            await finalizer.FinalizeAsync(containerRegistry);

            var crioConfigList = fixture.Resources.OfType<V1CrioConfiguration>();

            Assert.Single(crioConfigList);

            crioConfig = crioConfigList.FirstOrDefault();

            Assert.Empty(crioConfig.Spec.Registries);
        }
    }
}

#endif
