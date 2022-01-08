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

namespace TestKube
{
    [Trait(TestTrait.Category, TestArea.NeonKube)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_ResourceManager
    {
        //---------------------------------------------------------------------
        // Private types

        [KubernetesEntity(Group = KubeConst.NeonResourceGroup, ApiVersion = "v1", Kind = "ContainerRegistry", PluralName = "containerregistries")]
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
        public void Reconciled()
        {
            var resourceManager = new ResourceManager<V1Test>();
            var resource0       = new V1Test();

            resource0.Metadata.Name = "zero";

            // Verify that we detect new resources.

            Assert.True(resourceManager.Reconciled(resource0));
            Assert.True(resourceManager.Contains("zero"));

            // Verify that we ignore existing resources.

            Assert.False(resourceManager.Reconciled(resource0));
            Assert.True(resourceManager.Contains("zero"));
        }

        [Fact]
        public void Reconciled_WithHandler()
        {
            var resourceManager = new ResourceManager<V1Test>();
            var resource0       = new V1Test();
            var handlerCalled   = false;

            resource0.Metadata.Name = "zero";

            // Verify that we detect new resources.

            Assert.True(resourceManager.Reconciled(resource0,
                resources =>
                {
                    handlerCalled = true;
                    Assert.Contains(resource0, resources);
                }));

            Assert.True(handlerCalled);
            Assert.True(resourceManager.Contains("zero"));

            // Verify that we ignore existing resources.

            handlerCalled = false;

            Assert.False(resourceManager.Reconciled(resource0,
                resources =>
                {
                    handlerCalled = true;
                    Assert.Contains(resource0, resources);
                }));

            Assert.False(handlerCalled);
            Assert.True(resourceManager.Contains("zero"));
        }

        [Fact]
        public void Deleted()
        {
            var resourceManager = new ResourceManager<V1Test>();
            var resource0       = new V1Test();

            resource0.Metadata.Name = "zero";

            Assert.True(resourceManager.Reconciled(resource0));

            // Verify that we detect resource deletion.

            Assert.True(resourceManager.Deleted(resource0));
            Assert.False(resourceManager.Contains("zero"));

            // Verify that we ignore deletion of missing resources.

            Assert.False(resourceManager.Deleted(resource0));
            Assert.False(resourceManager.Contains("zero"));
        }

        [Fact]
        public void Deleted_WithHandler()
        {
            var resourceManager = new ResourceManager<V1Test>();
            var resource0       = new V1Test();
            var handlerCalled   = false;

            resource0.Metadata.Name = "zero";

            Assert.True(resourceManager.Reconciled(resource0));

            // Verify that we detect resource deletion.

            Assert.True(resourceManager.Deleted(resource0,
                resources =>
                {
                    handlerCalled = true;
                    Assert.DoesNotContain(resource0, resources);
                }));

            Assert.True(handlerCalled);
            Assert.False(resourceManager.Contains("zero"));

            // Verify that we ignore deletion of missing resources.

            handlerCalled = false;

            Assert.False(resourceManager.Deleted(resource0,
                resources =>
                {
                    handlerCalled = true;
                    Assert.Contains(resource0, resources);
                }));

            Assert.False(handlerCalled);
            Assert.False(resourceManager.Contains("zero"));
        }

        [Fact]
        public void StatusModified()
        {
            var resourceManager = new ResourceManager<V1Test>();
            var resource0       = new V1Test();

            resource0.Metadata.Name = "zero";

            // Verify that this doesn't blow up when the resource isn't present.

            Assert.False(resourceManager.StatusModified(resource0));
            Assert.False(resourceManager.Contains("zero"));

            // Verify that this works when the resource is present.

            Assert.True(resourceManager.Reconciled(resource0));
            Assert.True(resourceManager.StatusModified(resource0));
            Assert.True(resourceManager.Contains("zero"));
        }

        [Fact]
        public void StatusModified_WithHandler()
        {
            var resourceManager = new ResourceManager<V1Test>();
            var resource0       = new V1Test();
            var handlerCalled   = false;

            resource0.Metadata.Name = "zero";

            // Verify that this doesn't blow up when the resource isn't present.

            Assert.False(resourceManager.StatusModified(resource0,
                resources =>
                {
                    handlerCalled = true;
                    Assert.Contains(resource0, resources);
                }));

            Assert.False(handlerCalled);
            Assert.False(resourceManager.Contains("zero"));

            // Verify that this works when the resource is present.

            handlerCalled = true;

            Assert.True(resourceManager.Reconciled(resource0));

            Assert.True(resourceManager.StatusModified(resource0,
                resources =>
                {
                    handlerCalled = true;
                    Assert.Contains(resource0, resources);
                }));

            Assert.True(handlerCalled);
            Assert.True(resourceManager.Contains("zero"));
        }

        [Fact]
        public void TryGetResource()
        {
            var resourceManager = new ResourceManager<V1Test>();
            var resource0       = new V1Test();

            resource0.Metadata.Name = "zero";

            // Verify when the resource isn't present.

            Assert.False(resourceManager.TryGetResource("zero", out var r));

            // Verify when the resource is present.

            Assert.True(resourceManager.Reconciled(resource0));
            Assert.True(resourceManager.TryGetResource("zero", out r));
            Assert.Equal("zero", r.Metadata.Name);
        }

        [Fact]
        public void GetResources()
        {
            var resourceManager = new ResourceManager<V1Test>();
            var resource0       = new V1Test();
            var resource1       = new V1Test();

            resource0.Metadata.Name = "zero";
            resource1.Metadata.Name = "one";

            // Verify with no resources;

            var resources = resourceManager.GetResources();

            Assert.Empty(resources);

            // Verify with one resource.

            Assert.True(resourceManager.Reconciled(resource0));
            
            resources = resourceManager.GetResources();

            Assert.Single(resources);
            Assert.Contains(resources, r => r.Metadata.Name == "zero");

            // Verify with two resources.

            Assert.True(resourceManager.Reconciled(resource1));
            
            resources = resourceManager.GetResources();

            Assert.Equal(2, resources.Count());
            Assert.Contains(resources, r => r.Metadata.Name == "zero");
            Assert.Contains(resources, r => r.Metadata.Name == "one");
        }
    }
}
