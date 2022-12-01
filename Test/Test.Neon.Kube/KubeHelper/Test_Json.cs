//-----------------------------------------------------------------------------
// FILE:	    Test_Json.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using k8s;
using k8s.Models;

using Neon.Common;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Resources;
using Neon.Kube.Xunit;
using Neon.Xunit;

using Xunit;

namespace TestKube
{
    [Trait(TestTrait.Category, TestArea.NeonKube)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_Json
    {
        [Fact]
        public void SerializeCstorPoolCluster()
        {
            var serializerOptions = new JsonSerializerOptions();

            serializerOptions.Converters.Add(new JsonV1ResourceConverter());
            var cStorPoolCluster = new V1CStorPoolCluster()
            {
                Metadata = new V1ObjectMeta()
                {
                    Name = "cspc-stripe",
                    NamespaceProperty = KubeNamespace.NeonStorage
                },
                Spec = new V1CStorPoolClusterSpec()
                {
                    Pools = new List<V1CStorPoolSpec>(),
                    Resources = new V1ResourceRequirements()
                    {
                        Limits = new Dictionary<string, ResourceQuantity>() { { "memory", new ResourceQuantity("1Gi") } },
                        Requests = new Dictionary<string, ResourceQuantity>() { { "memory", new ResourceQuantity("1Gi") } },
                    },
                    AuxResources = new V1ResourceRequirements()
                    {
                        Limits = new Dictionary<string, ResourceQuantity>() { { "memory", new ResourceQuantity("1Gi") } },
                        Requests = new Dictionary<string, ResourceQuantity>() { { "memory", new ResourceQuantity("1Gi") } },
                    }
                }
            };
            string jsonString = JsonSerializer.Serialize(cStorPoolCluster, serializerOptions);
        }

        [Fact]
        public void SerializeX509Usages()
        {
            new KubernetesWithRetry(KubernetesClientConfiguration.BuildConfigFromConfigFile(KubeHelper.KubeConfigPath));

            var usages = new List<X509Usages>() { X509Usages.ServerAuth, X509Usages.ClientAuth };
            string jsonString = KubernetesJson.Serialize(usages);

            Assert.Equal(@"[""server auth"",""client auth""]", jsonString);
        }
    }
}
