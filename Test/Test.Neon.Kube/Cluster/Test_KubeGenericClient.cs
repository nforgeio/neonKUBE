//-----------------------------------------------------------------------------
// FILE:	    Test_KubeGenericClient.cs
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

using Microsoft.Extensions.DependencyInjection;
using Prometheus;

using Neon.Common;
using Neon.Deployment;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Resources;
using Neon.Kube.Xunit;
using Neon.Xunit;

using Xunit;
using Xunit.Abstractions;

using k8s;
using k8s.Models;

namespace TestKube
{
    [Trait(TestTrait.Category, TestArea.NeonKube)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_KubeGenericClient
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Static constructor.
        /// </summary>
        static Test_KubeGenericClient()
        {
            if (TestHelper.IsClusterTestingEnabled)
            {
                // Register a [ProfileClient] so tests will be able to pick
                // up secrets and profile information from [neon-assistant].

                NeonHelper.ServiceContainer.AddSingleton<IProfileClient>(new ProfileClient());
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private ClusterFixture      fixture;

        public Test_KubeGenericClient(ClusterFixture fixture, ITestOutputHelper testOutputHelper)
        {
            this.fixture = fixture;

            var options = new ClusterFixtureOptions();

            //################################################################
            // $debug(jefflill): Restore this after manual testing is complete
            //var status  = fixture.StartWithNeonAssistant(options: options);
            //################################################################

            var status = fixture.StartWithCurrentCluster(options: options);

            if (status == TestFixtureStatus.Disabled)
            {
                return;
            }
            else if (status == TestFixtureStatus.AlreadyRunning)
            {
                fixture.ResetCluster();
            }

            // $todo(jefflill):
            //
            // We need to create all of the test objects with the [NeonLabel.RemoveOnClusterReset]
            // label so they will be removed when the cluster is reset.  For the time being,
            // I'm just going to manually remove any of these resources here.

            foreach (var resource in fixture.K8sGeneric.ListAsync<V1NeonTestObject>().Result.Items)
            {
                fixture.K8sGeneric.DeleteAsync<V1NeonTestObject>(resource.Metadata.Name).Wait();
            }

            foreach (var resource in fixture.K8sGeneric.ListNamespacedAsync<V1NeonTestObject>("").Result.Items)
            {
                fixture.K8sGeneric.DeleteNamespacedAsync<V1NeonTestObject>("default", resource.Metadata.Name).Wait();
            }
        }

        [ClusterFact]
        public async Task Basic_Cluster()
        {
            // Verify that we can create a cluster scoped test object and then read it.

            var test = new V1NeonTestObject();

            test.Spec.Message = "HELLO WORLD!";

            var testCreated = await fixture.K8sGeneric.CreateAsync(test, "test-1");

            Assert.Equal(test.Spec.Message, testCreated.Spec.Message);

            var testRead = await fixture.K8sGeneric.ReadAsync<V1NeonTestObject>("test-1");

            Assert.Equal(test.Spec.Message, testRead.Spec.Message);

            // List the objects and verify.

            var list = (await fixture.K8sGeneric.ListAsync<V1NeonTestObject>()).Items;

            Assert.Single(list);

            var testListed = list.Single();

            Assert.Equal(test.Spec.Message, testListed.Spec.Message);
        }

        [ClusterFact]
        public async Task Basic_Namespaced_Default()
        {
            // Verify that we can create a namespace="default" scoped test object and then read it.

            var @namespace = "default";
            var test       = new V1NeonTestObject();

            test.Spec.Message = "HELLO WORLD!";

            var testCreated = await fixture.K8sGeneric.CreateNamespacedAsync(test, @namespace, "test-1");

            Assert.Equal(test.Spec.Message, testCreated.Spec.Message);

            var testRead = await fixture.K8sGeneric.ReadNamespacedAsync<V1NeonTestObject>(@namespace, "test-1");

            Assert.Equal(test.Spec.Message, testRead.Spec.Message);

            // List the objects and verify.

            var list = (await fixture.K8sGeneric.ListNamespacedAsync<V1NeonTestObject>(@namespace)).Items;

            Assert.Single(list);

            var testListed = list.Single();

            Assert.Equal(test.Spec.Message, testListed.Spec.Message);
        }

        [ClusterFact]
        public async Task Basic_Namespaced_Empty()
        {
            // Verify that we can create a namespace="" scoped test object and then read it.

            var @namespace = string.Empty;
            var test       = new V1NeonTestObject();

            test.Spec.Message = "HELLO WORLD!";

            var testCreated = await fixture.K8sGeneric.CreateNamespacedAsync(test, @namespace, "test-1");

            Assert.Equal(test.Spec.Message, testCreated.Spec.Message);

            var testRead = await fixture.K8sGeneric.ReadNamespacedAsync<V1NeonTestObject>(@namespace, "test-1");

            Assert.Equal(test.Spec.Message, testRead.Spec.Message);

            // List the objects and verify.

            var list = (await fixture.K8sGeneric.ListNamespacedAsync<V1NeonTestObject>(@namespace)).Items;

            Assert.Single(list);

            var testListed = list.Single();

            Assert.Equal(test.Spec.Message, testListed.Spec.Message);
        }

        [ClusterFact]
        public async Task Basic_Namespaced_NULL()
        {
            // Verify that we can create a namespace=NULL scoped test object and then read it.

            var @namespace = (string)null;
            var test       = new V1NeonTestObject();

            test.Spec.Message = "HELLO WORLD!";

            var testCreated = await fixture.K8sGeneric.CreateNamespacedAsync(test, @namespace, "test-1");

            Assert.Equal(test.Spec.Message, testCreated.Spec.Message);

            var testRead = await fixture.K8sGeneric.ReadNamespacedAsync<V1NeonTestObject>(@namespace, "test-1");

            Assert.Equal(test.Spec.Message, testRead.Spec.Message);

            // List the objects and verify.

            var list = (await fixture.K8sGeneric.ListNamespacedAsync<V1NeonTestObject>(@namespace)).Items;

            Assert.Single(list);

            var testListed = list.Single();

            Assert.Equal(test.Spec.Message, testListed.Spec.Message);
        }

        [ClusterFact]
        public async Task Core_Clustered()
        {
            // Verify that we can create a cluster scoped test object and then read it.

            var test = new V1NeonTestObject();

            test.Spec.Message = "HELLO WORLD!";

            var testCreated = await fixture.K8sGeneric.CreateAsync(test, "test-1");

            Assert.Equal(test.Spec.Message, testCreated.Spec.Message);

            var testRead = await fixture.K8sGeneric.ReadAsync<V1NeonTestObject>("test-1");

            Assert.Equal(test.Spec.Message, testRead.Spec.Message);

            // List the objects and verify.

            var list = (await fixture.K8sGeneric.ListAsync<V1NeonTestObject>()).Items;

            Assert.Single(list);

            var testListed = list.Single();

            Assert.Equal(test.Spec.Message, testListed.Spec.Message);
        }
    }
}
