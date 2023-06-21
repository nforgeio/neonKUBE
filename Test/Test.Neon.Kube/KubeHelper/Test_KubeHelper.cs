//-----------------------------------------------------------------------------
// FILE:        Test_KubeHelper.cs
// CONTRIBUTOR: Jeff Lill
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
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using k8s;
using k8s.Models;

using Neon.Common;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Xunit;
using Neon.Xunit;

using Xunit;

namespace TestKube
{
    [Trait(TestTrait.Category, TestArea.NeonKube)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_KubeHelper
    {
        public class V1Test : IKubernetesObject, IMetadata<V1ObjectMeta>
        {
            public const string KubeGroup      = "test";
            public const string KubeApiVersion = "v1";
            public const string KubeKind       = "testobject";

            public string ApiVersion { get; set; }
            public string Kind { get; set; }
            public V1ObjectMeta Metadata { get; set; }
        }

        public class V1TestWithoutGroup : IKubernetesObject, IMetadata<V1ObjectMeta>
        {
            public const string KubeApiVersion = "v1";
            public const string KubeKind       = "testobject";

            public string ApiVersion { get; set; }
            public string Kind { get; set; }
            public V1ObjectMeta Metadata { get; set; }
        }

        public class V1TestWithoutApiVersion : IKubernetesObject, IMetadata<V1ObjectMeta>
        {
            public const string KubeGroup = "test";
            public const string KubeKind  = "testobject";

            public string ApiVersion { get; set; }
            public string Kind { get; set; }
            public V1ObjectMeta Metadata { get; set; }
        }

        public class V1TestWithoutKind : IKubernetesObject, IMetadata<V1ObjectMeta>
        {
            public const string KubeGroup      = "test";
            public const string KubeApiVersion = "v1";

            public string ApiVersion { get; set; }
            public string Kind { get; set; }
            public V1ObjectMeta Metadata { get; set; }
        }

        [Fact]
        public void CreateKubernetesObject()
        {
            // Create a few global Kubernetes objects and verify that their ApiVersion
            // and Kind properties are initialized properly.

            var configmap = KubeHelper.CreateKubeObject<V1ConfigMap>("test");

            Assert.Equal(V1ConfigMap.KubeApiVersion, configmap.ApiVersion);
            Assert.Equal(V1ConfigMap.KubeKind, configmap.Kind);

            var deployment = KubeHelper.CreateKubeObject<V1Deployment>("test");

            Assert.Equal($"{V1Deployment.KubeGroup}/{V1Deployment.KubeApiVersion}", deployment.ApiVersion);
            Assert.Equal(V1Deployment.KubeKind, deployment.Kind);

            // Verify that a custom object can be created.

            var test = KubeHelper.CreateKubeObject<V1Test>("test");

            Assert.Equal($"{V1Test.KubeGroup}/{V1Test.KubeApiVersion}", test.ApiVersion);
            Assert.Equal(V1Test.KubeKind, test.Kind);
        }

        [Fact]
        public void CreateKubernetesObject_NoConsts()
        {
            // Verify that [NotSupportedException] is thrown when the required constants
            // are not defined by the Kubernetes object type.

            Assert.Throws<NotSupportedException>(() => KubeHelper.CreateKubeObject<V1TestWithoutGroup>("test"));
            Assert.Throws<NotSupportedException>(() => KubeHelper.CreateKubeObject<V1TestWithoutApiVersion>("test"));
            Assert.Throws<NotSupportedException>(() => KubeHelper.CreateKubeObject<V1TestWithoutKind>("test"));
        }
    }
}
