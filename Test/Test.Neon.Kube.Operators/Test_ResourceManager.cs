//-----------------------------------------------------------------------------
// FILE:	    Test_ResourceManager.cs
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

using Neon.Common;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Operator;
using Neon.Xunit;

using DotnetKubernetesClient.Entities;
using k8s.Models;
using Xunit;

using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;
using KubeOps.Operator.Controller.Results;

namespace TestKube
{
    [Trait(TestTrait.Category, TestArea.NeonKube)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_ResourceManager
    {
        //---------------------------------------------------------------------
        // Private types

        [KubernetesEntity(Group = KubeConst.NeonKubeResourceGroup, ApiVersion = "v1", Kind = "ContainerRegistry", PluralName = "containerregistries")]
        [KubernetesEntityShortNames]
        [EntityScope(EntityScope.Cluster)]
        [Description("Describes a neonKUBE cluster upstream container registry.")]
        private class V1Test : CustomKubernetesEntity<V1Test.V1TestEntitySpec>
        {
            /// <summary>
            /// The container registry specification.
            /// </summary>
            public class V1TestEntitySpec
            {
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        [Fact]
        public async Task Reconciled()
        {
            var resourceManager = new ResourceManager<V1Test>(waitForAll: false);
            var resource0       = new V1Test();
            var handlerCalled   = false;

            resource0.Metadata.Name = "zero";

            // Verify that we detect new resources.

            await resourceManager.ReconciledAsync(resource0,
                async (name, resources) =>
                {
                    handlerCalled = true;
                    Assert.Contains(resource0, resources.Values);
                    return await Task.FromResult<ResourceControllerResult>(null);
                });

            Assert.True(handlerCalled);
            Assert.True(await resourceManager.ContainsAsync("zero"));

            // Verify that we ignore existing resources.

            handlerCalled = false;

            await resourceManager.ReconciledAsync(resource0,
                async (name, resources) =>
                {
                    handlerCalled = true;
                    Assert.Contains(resource0, resources.Values);
                    return await Task.FromResult<ResourceControllerResult>(null);
                });

            Assert.False(handlerCalled);
            Assert.True(await resourceManager.ContainsAsync("zero"));
        }


        [Fact]
        public async Task Reconciled_WaitForAll()
        {
            // Verify that the resource manager will wait until all resources are
            // loaded before calling the handler.

            var resourceManager = new ResourceManager<V1Test>(waitForAll: true);
            var resource0       = new V1Test();
            var resource1       = new V1Test();
            var resource2       = new V1Test();
            var resource3       = new V1Test();
            var handlerCalled   = false;

            resource0.Metadata.Name = "zero";
            resource1.Metadata.Name = "one";
            resource2.Metadata.Name = "two";
            resource3.Metadata.Name = "three";

            handlerCalled = false;
            await resourceManager.ReconciledAsync(resource0,
                async (name, resources) =>
                {
                    handlerCalled = true;
                    Assert.Contains(resource0, resources.Values);
                    return await Task.FromResult<ResourceControllerResult>(null);
                });

            Assert.False(handlerCalled);
            Assert.True(await resourceManager.ContainsAsync("zero"));

            handlerCalled = false;
            await resourceManager.ReconciledAsync(resource1,
                async (name, resources) =>
                {
                    handlerCalled = true;
                    Assert.Contains(resource0, resources.Values);
                    return await Task.FromResult<ResourceControllerResult>(null);
                });

            Assert.False(handlerCalled);
            Assert.True(await resourceManager.ContainsAsync("one"));

            // Verify that handlers are not called for deleted events while
            // we're still waiting for all resources.

            await resourceManager.ReconciledAsync(resource2, async (name, resources) => await Task.FromResult<ResourceControllerResult>(null));

            handlerCalled = false;
            await resourceManager.DeletedAsync(resource2,
                async (name, resources) =>
                {
                    handlerCalled = true;
                    return await Task.FromResult<ResourceControllerResult>(null);
                });

            Assert.False(handlerCalled);

            // Verify that handlers are not called for status modified events while
            // we're still waiting for all resources.

            await resourceManager.ReconciledAsync(resource3, async (name, resources) => await Task.FromResult<ResourceControllerResult>(null));

            handlerCalled = false;
            await resourceManager.StatusModifiedAsync(resource3,
                async (name, resources) =>
                {
                    handlerCalled = true;
                    return await Task.FromResult<ResourceControllerResult>(null);
                });

            Assert.False(handlerCalled);

            // Resend an existing resource so that the resource manager will
            // determine that it has all resources and call the handler with 
            // a non-null name.

            handlerCalled = false;
            await resourceManager.ReconciledAsync(resource0,
                async (name, resources) =>
                {
                    handlerCalled = true;
                    Assert.Null(name);
                    Assert.Contains(resource0, resources.Values);
                    return await Task.FromResult<ResourceControllerResult>(null);
                });

            Assert.True(handlerCalled);
            Assert.True(await resourceManager.ContainsAsync("zero"));

            // Resend an existing resource with an updated generation and
            // verify that the handler is called.

            var newResource0  = new V1Test();
            var newGeneration = 100L;

            newResource0.Metadata.Name       = "zero";
            newResource0.Metadata.Generation = newGeneration;

            handlerCalled = false;
            await resourceManager.ReconciledAsync(newResource0,
                async (name, resources) =>
                {
                    handlerCalled = true;
                    Assert.Equal("zero", name);
                    Assert.Contains(newResource0, resources.Values);
                    Assert.Equal(newGeneration, resources[name].Metadata.Generation);
                    return await Task.FromResult<ResourceControllerResult>(null);
                });

            Assert.True(handlerCalled);
            Assert.True(await resourceManager.ContainsAsync("zero"));
        }

        [Fact]
        public async Task Deleted()
        {
            var resourceManager = new ResourceManager<V1Test>(waitForAll: false);
            var resource0       = new V1Test();
            var handlerCalled   = false;

            resource0.Metadata.Name = "zero";

            await resourceManager.ReconciledAsync(resource0, async (name, resources) => await Task.FromResult<ResourceControllerResult>(null));

            // Verify that we detect resource deletion.

            await resourceManager.DeletedAsync(resource0,
                async (name, resources) =>
                {
                    handlerCalled = true;
                    Assert.DoesNotContain(resource0, resources.Values);
                    return await Task.FromResult<ResourceControllerResult>(null);
                });

            Assert.True(handlerCalled);
            Assert.False(await resourceManager.ContainsAsync("zero"));

            // Verify that we ignore deletion of missing resources.

            handlerCalled = false;

            await resourceManager.DeletedAsync(resource0,
                async (name, resources) =>
                {
                    handlerCalled = true;
                    Assert.Contains(resource0, resources.Values);
                    return await Task.FromResult<ResourceControllerResult>(null);
                });

            Assert.False(handlerCalled);
            Assert.False(await resourceManager.ContainsAsync("zero"));
        }

        [Fact]
        public async Task StatusModified()
        {
            var resourceManager = new ResourceManager<V1Test>(waitForAll: false);
            var resource0       = new V1Test();
            var handlerCalled   = false;

            resource0.Metadata.Name = "zero";

            // Verify that this doesn't blow up when the resource isn't present.

            await resourceManager.StatusModifiedAsync(resource0,
                async (name, resources) =>
                {
                    handlerCalled = true;
                    Assert.Contains(resource0, resources.Values);
                    return await Task.FromResult<ResourceControllerResult>(null);
                });

            Assert.False(handlerCalled);
            Assert.False(await resourceManager.ContainsAsync("zero"));

            // Verify that this works when the resource is present.

            handlerCalled = true;

            await resourceManager.ReconciledAsync(resource0, async (name, resources) => await Task.FromResult<ResourceControllerResult>(null));

            await resourceManager.StatusModifiedAsync(resource0,
                async (name, resources) =>
                {
                    handlerCalled = true;
                    Assert.Contains(resource0, resources.Values);
                    return await Task.FromResult<ResourceControllerResult>(null);
                });

            Assert.True(handlerCalled);
            Assert.True(await resourceManager.ContainsAsync("zero"));
        }

        [Fact]
        public async Task TryGetResource()
        {
            var resourceManager = new ResourceManager<V1Test>(waitForAll: false);
            var resource0       = new V1Test();

            resource0.Metadata.Name = "zero";

            // Verify when the resource isn't present.

            Assert.Null(await resourceManager.GetResourceAsync("zero"));

            // Verify when the resource is present.

            await resourceManager.ReconciledAsync(resource0, async (name, resources) => await Task.FromResult<ResourceControllerResult>(null));

            var found = await resourceManager.GetResourceAsync("zero");

            Assert.NotNull(found);
            Assert.Equal("zero", found.Metadata.Name);
        }

        [Fact]
        public async Task CloneResources()
        {
            var resourceManager = new ResourceManager<V1Test>(waitForAll: false);
            var resource0       = new V1Test();
            var resource1       = new V1Test();

            resource0.Metadata.Name = "zero";
            resource1.Metadata.Name = "one";

            // Verify with no resources;

            var resources = await resourceManager.CloneResourcesAsync();

            Assert.Empty(resources);

            // Verify with one resource.

            await resourceManager.ReconciledAsync(resource0, async (name, resources) => await Task.FromResult<ResourceControllerResult>(null));

            resources = await resourceManager.CloneResourcesAsync();

            Assert.Single(resources);
            Assert.Contains(resources.Values, r => r.Metadata.Name == "zero");

            // Verify with two resources.

            await resourceManager.ReconciledAsync(resource1, async (name, resources) => await Task.FromResult<ResourceControllerResult>(null));

            resources = await resourceManager.CloneResourcesAsync();

            Assert.Equal(2, resources.Count());
            Assert.Contains(resources.Values, r => r.Metadata.Name == "zero");
            Assert.Contains(resources.Values, r => r.Metadata.Name == "one");
        }
    }
}
