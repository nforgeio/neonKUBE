//-----------------------------------------------------------------------------
// FILE:	    Test_CustomResources.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Reactive;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using k8s;
using k8s.Models;

using Neon.Common;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Operator;
using Neon.Kube.Operator.Entities;
using Neon.Kube.Resources;
using Neon.Kube.Resources.Cluster;
using Neon.Kube.Resources.Dex;
using Neon.Kube.Xunit;
using Neon.Xunit;

using Xunit;

namespace Test.Neon.Kube.Operator
{
    /// <summary>
    /// Custom Resource tests.
    /// </summary>
    [Trait(TestTrait.Category, TestArea.NeonKube)]
    public class Test_CustomResources
    {
        /// <summary>
        /// Ensures that the CRD can be written to a Yaml file.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        [Fact]
        public async Task CanWriteYamlFile()
        {
            var generator = new CustomResourceGenerator();

            var crd = await generator.GenerateCustomResourceDefinitionAsync(typeof(V1NeonDashboard));

            using (var tempFile = new TempFile(".yaml"))
            {
                await generator.WriteToFile(crd, tempFile.Path);
            }
        }

        /// <summary>
        /// Ensures that the CRD can be written to a Yaml file.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        [Fact]
        public async Task CanSerializeCustomResources()
        {
            var generator = new CustomResourceGenerator();

            var crd = await generator.GenerateCustomResourceDefinitionAsync(typeof(V1NeonSsoClient));
            var str  = KubernetesJson.Serialize(crd);
            var yaml = KubernetesYaml.Serialize(crd);
        }
    }
}
