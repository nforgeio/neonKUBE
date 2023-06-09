// -----------------------------------------------------------------------------
// FILE:	    Test_ClusterDeployment.cs
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
using Neon.Kube.Xunit;
using Neon.XenServer;
using Neon.Xunit;

using Xunit;

namespace TestKube
{
    [Trait(TestTrait.Category, TestArea.NeonKube)]
    [Trait(TestTrait.Category, TestTrait.RequiresProfile)]
    [Trait(TestTrait.Category, TestTrait.Slow)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_ClusterDeployment
    {
        private const int repeatCount = 100;

        public Test_ClusterDeployment()
        {
            if (TestHelper.IsClusterTestingEnabled)
            {
                // Register a [ProfileClient] so tests will be able to pick
                // up secrets and profile information from [neon-assistant].

                NeonHelper.ServiceContainer.AddSingleton<IProfileClient>(new MaintainerProfile());
            }
        }

        [MaintainerTheory]
        [Repeat(repeatCount)]
        public async Task HyperV_Tiny(int runCount)
        {
            _ = runCount;

            using (var tempFile = new TempFile(".cluster.yaml"))
            {
                File.WriteAllText(tempFile.Path, HyperVClusters.Tiny);

                try
                {
                    await KubeHelper.NeonCliExecuteCaptureAsync(new object[] { "logout" });
                    (await KubeHelper.NeonCliExecuteCaptureAsync(new object[] { "cluster", "deploy", tempFile.Path }))
                        .EnsureSuccess();
                }
                finally
                {
                    await KubeHelper.NeonCliExecuteCaptureAsync(new object[] { "cluster", "delete" });
                }
            }
        }

        [MaintainerTheory]
        [Repeat(repeatCount)]
        public async Task XenServer_Tiny(int runCount)
        {
            _ = runCount;

            using (var tempFile = new TempFile(".cluster.yaml"))
            {
                var clusterDefinitionYaml = XenServerClusters.Tiny;
                var clusterDefinition     = ClusterDefinition.FromYaml(clusterDefinitionYaml);

                File.WriteAllText(tempFile.Path, clusterDefinitionYaml);

                try
                {
                    // We've seen intermittent problems uploading the node template to XenServers
                    // so we're going to remove any existing template first to exercise this.

                    var templateName = $"neonkube-{KubeVersions.NeonKubeWithBranchPart}";
                    var host         = clusterDefinition.Hosting.Hypervisor.Hosts.First();  // $hack(jefflill): assuming one XenServer host
                    var hostUsername = host.Username ?? clusterDefinition.Hosting.Hypervisor.HostUsername;
                    var hostPassword = host.Password ?? clusterDefinition.Hosting.Hypervisor.HostPassword;

                    using (var xenClient = new XenClient(host.Address, hostUsername, hostPassword))
                    {
                        var template = xenClient.Template.Find(templateName);

                        if (template != null)
                        {
                            xenClient.Template.Destroy(template);
                        }
                    }

                    // Logout out of the current cluster (if any) and then deploy a fresh one.

                    await KubeHelper.NeonCliExecuteCaptureAsync(new object[] { "logout" });
                    (await KubeHelper.NeonCliExecuteCaptureAsync(new object[] { "cluster", "deploy", tempFile.Path }))
                        .EnsureSuccess();
                }
                finally
                {
                    // Delete the deployed cluster.

                    await KubeHelper.NeonCliExecuteCaptureAsync(new object[] { "cluster", "delete" });
                }
            }
        }
    }
}
