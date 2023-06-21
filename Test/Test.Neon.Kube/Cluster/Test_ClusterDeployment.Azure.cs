// -----------------------------------------------------------------------------
// FILE:        Test_ClusterDeployment.Azure.cs
// CONTRIBUTOR: NEONFORGE Team
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License").
// You may not use this file except in compliance with the License.
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
using Microsoft.VisualBasic;

using Neon.Common;
using Neon.Deployment;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.ClusterDef;
using Neon.Kube.Config;
using Neon.Kube.Xunit;
using Neon.XenServer;
using Neon.Xunit;

using Xunit;

namespace TestKube
{
    public partial class Test_ClusterDeployment
    {
        [MaintainerTheory]
        [Trait(TestTrait.Category, TestTrait.CloudExpense)]
        [Repeat(repeatCount)]
        public async Task Azure_Tiny(int runCount)
        {
            await DeployAzureCluster(AzureClusterDefinitions.Tiny, runCount);
        }

        [MaintainerTheory]
        [Trait(TestTrait.Category, TestTrait.CloudExpense)]
        [Repeat(repeatCount)]
        public async Task Azure_Small(int runCount)
        {

            await DeployAzureCluster(AzureClusterDefinitions.Small, runCount);
        }

        [MaintainerTheory]
        [Trait(TestTrait.Category, TestTrait.CloudExpense)]
        [Repeat(repeatCount)]
        public async Task AzureV_Large(int runCount)
        {
            await DeployAzureCluster(AzureClusterDefinitions.Large, runCount);
        }

        private async Task DeployAzureCluster(string clusterDefinitionYaml, int runCount)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(clusterDefinitionYaml), nameof(clusterDefinitionYaml));

            KubeTestHelper.CleanDeploymentLogs(typeof(Test_ClusterDeployment), nameof(DeployAzureCluster));

            using (var tempFile = new TempFile(".cluster.yaml"))
            {
                File.WriteAllText(tempFile.Path, clusterDefinitionYaml);

                try
                {
                    await KubeHelper.NeonCliExecuteCaptureAsync(new object[] { "logout" });
                    (await KubeHelper.NeonCliExecuteCaptureAsync(new object[] { "cluster", "deploy", tempFile.Path, "--use-staged" }))
                        .EnsureSuccess();
                }
                catch (Exception e)
                {
                    KubeTestHelper.CaptureDeploymentLogsAndThrow(e, typeof(Test_ClusterDeployment), nameof(DeployAzureCluster), runCount);
                }
                finally
                {
                    await KubeHelper.NeonCliExecuteCaptureAsync(new object[] { "cluster", "delete", "--force" });
                }
            }
        }
    }
}
