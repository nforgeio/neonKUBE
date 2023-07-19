// -----------------------------------------------------------------------------
// FILE:        Test_ClusterDeployment.XenServer.cs
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
        /// <summary>
        /// Constructs a <see cref="XenClient"/> for the XenServer host assigned to the test runner
        /// for its exclusive use.
        /// </summary>
        /// <param name="clusterDefinition">Specifies the cluster definitiion.</param>
        /// <returns>The new <see cref="XenClient"/></returns>
        private XenClient CreateXenClient(ClusterDefinition clusterDefinition)
        {
            var host         = clusterDefinition.Hosting.Hypervisor.Hosts.First();  // $hack(jefflill): assuming one dedicated XenServer host
            var hostUsername = host.Username ?? clusterDefinition.Hosting.Hypervisor.HostUsername;
            var hostPassword = host.Password ?? clusterDefinition.Hosting.Hypervisor.HostPassword;

            return new XenClient(host.Address, hostUsername, hostPassword);
        }

        [MaintainerTheory]
        [Repeat(repeatCount)]
        public async Task XenServer_Tiny(int runCount)
        {
            await DeployXenServerCluster(XenServerClustersDefinitions.Tiny, runCount);
        }

        [MaintainerTheory]
        [Repeat(repeatCount)]
        public async Task XenServer_Small(int runCount)
        {
            await DeployXenServerCluster(XenServerClustersDefinitions.Small, runCount);
        }

        [MaintainerTheory]
        [Repeat(repeatCount)]
        public async Task XenServer_Large(int runCount)
        {
            await DeployXenServerCluster(XenServerClustersDefinitions.Large, runCount);
        }

        private async Task DeployXenServerCluster(string clusterDefinitionYaml, int runCount)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(clusterDefinitionYaml), nameof(clusterDefinitionYaml));

            KubeTestHelper.CleanDeploymentLogs(typeof(Test_ClusterDeployment), nameof(DeployXenServerCluster));

            using (var tempFile = new TempFile(".cluster.yaml"))
            {
                var clusterDefinition = ClusterDefinition.FromYaml(clusterDefinitionYaml);
                var error             = false;

                File.WriteAllText(tempFile.Path, clusterDefinitionYaml);

                try
                {
                    // We've seen intermittent problems uploading the node template to XenServers
                    // so we're going to remove any existing templates first to exercise this along
                    // with removing all VMs from the dedicated XenServer host.

                    using (var xenClient = CreateXenClient(clusterDefinition))
                    {
                        xenClient.WipeHost(templateSelector: template => template.NameLabel.StartsWith("neon", StringComparison.InvariantCultureIgnoreCase));
                    }

                    // Logout out of the current cluster (if any), remove any existing cluster context that
                    // may conflict with the new cluster and then deploy a fresh cluster.

                    await KubeHelper.NeonCliExecuteCaptureAsync("logout");
                    KubeHelper.KubeConfig.RemoveCluster(clusterDefinition.Name);

                    (await KubeHelper.NeonCliExecuteCaptureAsync("cluster", "deploy", tempFile.Path, "--use-staged"))
                        .EnsureSuccess();
                }
                catch (Exception e)
                {
                    error = true;

                    KubeTestHelper.CaptureDeploymentLogsAndThrow(e, typeof(Test_ClusterDeployment), nameof(DeployXenServerCluster), runCount);
                }
                finally
                {
                    // Break for deployment errors and when the debugger is attached.  This is a
                    // good way to [revent the cluster from, being deleted for further investigation. 

                    if (error && Debugger.IsAttached)
                    {
                        Debugger.Break();
                    }

                    // Delete the deployed cluster.

                    await KubeHelper.NeonCliExecuteCaptureAsync("cluster", "delete", clusterDefinition.Name, "--force");
                }
            }
        }
    }
}
