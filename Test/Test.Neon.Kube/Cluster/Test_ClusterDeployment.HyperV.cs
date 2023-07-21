// -----------------------------------------------------------------------------
// FILE:        Test_ClusterDeployment.HyperV.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualBasic;

using Neon.Common;
using Neon.Deployment;
using Neon.HyperV;
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
        [Repeat(repeatCount)]
        public async Task HyperV_Tiny(int runCount)
        {
            await DeployHyperVCluster(HyperVClusterDefinitions.Tiny, runCount);
        }

        [MaintainerTheory]
        [Repeat(repeatCount)]
        public async Task HyperV_Small(int runCount)
        {
            await DeployHyperVCluster(HyperVClusterDefinitions.Small, runCount);
        }

        [MaintainerTheory]
        [Repeat(repeatCount)]
        public async Task HyperV_Large(int runCount)
        {
            await DeployHyperVCluster(HyperVClusterDefinitions.Large, runCount);
        }

        private async Task DeployHyperVCluster(string clusterDefinitionYaml, int runCount)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(clusterDefinitionYaml), nameof(clusterDefinitionYaml));

            KubeTestHelper.CleanDeploymentLogs(typeof(Test_ClusterDeployment), nameof(DeployHyperVCluster));

            using (var tempFile = new TempFile(".cluster.yaml"))
            {
                Exception error = null;

                File.WriteAllText(tempFile.Path, clusterDefinitionYaml);

                try
                {
                    // Remove any VMs that appear to belong to a previous test run.

                    using (var hyperv = new HyperVClient())
                    {
                        var clusterDefinition = ClusterDefinition.FromYaml(clusterDefinitionYaml);

                        hyperv.RemoveVmsWithPrefix(clusterDefinition.Hosting.Hypervisor.NamePrefix);
                    }

                    // Deploy the cluster.

                    await KubeHelper.NeonCliExecuteCaptureAsync("logout");
                    (await KubeHelper.NeonCliExecuteCaptureAsync("cluster", "deploy", tempFile.Path, "--use-staged"))
                        .EnsureSuccess();
                }
                catch (Exception e)
                {
                    error = e;

                    KubeTestHelper.CaptureDeploymentLogsAndThrow(e, typeof(Test_ClusterDeployment), nameof(DeployHyperVCluster), runCount);
                }
                finally
                {
                    // Break for deployment errors and when the debugger is attached.  This is a
                    // good way to [prevent the cluster from being deleted for further investigation. 

                    if (error != null && Debugger.IsAttached)
                    {
                        Debugger.Break();
                    }

                    await KubeHelper.NeonCliExecuteCaptureAsync("cluster", "delete", "--force");
                }
            }
        }
    }
}
