// -----------------------------------------------------------------------------
// FILE:	    Test_ClusterCommands.cs
// CONTRIBUTOR: Jeff Lill
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
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Neon;
using Neon.Common;
using Neon.Cryptography;
using Neon.Kube;
using Neon.Kube.Xunit;
using Neon.IO;
using Neon.Xunit;

using NeonCli;

using Xunit;
using System.Runtime.InteropServices;
using Neon.Kube.Proxy;
using Neon.Kube.Hosting;

namespace Test.NeonCli
{
    [Trait(TestTrait.Category, TestArea.NeonCli)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_ClusterCommands
    {
        /// <summary>
        /// This can be temporarily set to TRUE while debugging the tests.
        /// </summary>
        private bool debugMode = true;

        private const string clusterName  = "test-neoncli";
        private const string clusterLogin = $"root@{clusterName}";

        private const string clusterDefinition =
$@"
name: {clusterName}
datacenter: $<profile:datacenter>
purpose: test
isLocked: false
timeSources:
- pool.ntp.org
kubernetes:
  allowPodsOnControlPlane: true
hosting:
  environment: hyperv
  hypervisor:
    namePrefix: test-neoncli
    cores: 4
    memory: 16 GiB
    osDisk: 64 GiB
    diskLocation: $<profile:hyperv.diskfolder>
network:
  premiseSubnet: $<profile:lan.subnet>
  gateway: $<profile:lan.gateway>
  nameservers:
  - $<profile:lan.dns0>
  - $<profile:lan.dns1>
nodes:
  node:
    role: control-plane
    address: $<profile:hyperv.tiny0.ip>
";

        private readonly string neonCliPath;

        /// <summary>
        /// Constructor,
        /// </summary>
        public Test_ClusterCommands()
        {
            // Locate the neon-cli binary.

            var thisAssembly            = Assembly.GetExecutingAssembly();
            var assemblyConfigAttribute = thisAssembly.GetCustomAttribute<AssemblyConfigurationAttribute>();
            var buildConfig             = assemblyConfigAttribute.Configuration;

            Covenant.Assert(assemblyConfigAttribute != null, $"Test assembly [{thisAssembly.FullName}] does not include [{nameof(AssemblyConfigurationAttribute)}].");

            // $todo(jefflill):
            //
            // I'm hardcoding the .NET framework moniker and arcitecture parts of the subpath.

            neonCliPath = Path.Combine(Environment.GetEnvironmentVariable("NK_ROOT"), "Tools", "neon-cli", "bin", buildConfig, "net7.0", "win10-x64", "neoncli.exe");

            Covenant.Assert(File.Exists(neonCliPath), $"[neon-cli] executable does not exist at: {neonCliPath}");
        }

        [Fact]
        [Trait(TestTrait.Category, TestTrait.Slow)]
        public async Task Verify()
        {
            // Use [neon-cli] to deploy a single-node Hyper-V test cluster and the verify that
            // common [neon-cli] cluster commands work as expected.  We're doing this all in a
            // single test method instead of using [ClusterFixture] because we want to test
            // cluster prepare/setup as well as cluster delete commands here.

            bool            clusterExists = false;
            ExecuteResponse response;

            using (var tempFolder = new TempFolder())
            {
                try
                {
                    var clusterDefinitionPath = Path.Combine(tempFolder.Path, "cluster-definition.yaml");

                    File.WriteAllText(clusterDefinitionPath, clusterDefinition);

                    //-------------------------------------------------------------
                    // Remove the test cluster and login if they already exist.

                    response = (await NeonCliAsync("login", "list"))
                        .EnsureSuccess();

                    if (response.OutputText.Contains(clusterLogin))
                    {
                        if (debugMode)
                        {
                            clusterExists = true;
                        }
                        else
                        {
                            (await NeonCliAsync("cluster", "delete", "--force", clusterName)).EnsureSuccess();

                            response = (await NeonCliAsync("login", "list"))
                                .EnsureSuccess();

                            if (response.OutputText.Contains($"root@{clusterName}"))
                            {
                                Covenant.Assert(false, $"Cluster [{clusterName}] delete failed.");
                            }
                        }
                    }

                    //-------------------------------------------------------------
                    // Validate the cluster definition.

                    response = (await NeonCliAsync("cluster", "validate", clusterDefinitionPath))
                        .EnsureSuccess();

                    //-------------------------------------------------------------
                    // Deploy the test cluster.

                    if (!clusterExists)
                    {
                        (await NeonCliAsync("logout"))
                            .EnsureSuccess();

                        (await NeonCliAsync("cluster", "prepare", clusterDefinitionPath, "--use-staged"))
                            .EnsureSuccess();

                        (await NeonCliAsync("cluster", "setup", clusterLogin, "--use-staged"))
                            .EnsureSuccess();
                    }

                    var configCluster = KubeHelper.KubeConfig.Cluster;

                    Assert.NotNull(configCluster);

                    //-------------------------------------------------------------
                    // Create a cluster proxy for use in the tests below.

                    using var clusterProxy = await ClusterProxy.CreateAsync(
                        kubeConfig:            KubeHelper.KubeConfig,
                        hostingManagerFactory: new HostingManagerFactory(),
                        cloudMarketplace:      false,
                        operation:             ClusterProxy.Operation.LifeCycle);

                    //-------------------------------------------------------------
                    // Verify: cluster health

                    response = (await NeonCliAsync("cluster", "health"))
                        .EnsureSuccess();

                    var clusterHealth = NeonHelper.JsonDeserialize<ClusterHealth>(response.OutputText);

                    Assert.Equal(ClusterState.Healthy, clusterHealth.State);
                    Assert.Equal("Cluster is healthy", clusterHealth.Summary);
                    Assert.Single(clusterHealth.Nodes);
                    Assert.Equal("node", clusterHealth.Nodes.First().Key);
                    Assert.Equal(ClusterNodeState.Running, clusterHealth.Nodes.First().Value);

                    //-------------------------------------------------------------
                    // Verify: cluster check

                    response = (await NeonCliAsync("cluster", "check"))
                        .EnsureSuccess();

                    //-------------------------------------------------------------
                    // Verify: cluster logout/login

                    Assert.NotNull(KubeHelper.KubeConfig.CurrentContext);

                    var contextName = KubeHelper.KubeConfig.CurrentContext;

                    // Logout

                    response = (await NeonCliAsync("cluster", "logout"))
                        .EnsureSuccess();

                    Assert.Null(KubeHelper.KubeConfig.CurrentContext);

                    // Login

                    response = (await NeonCliAsync("cluster", "login", contextName))
                        .EnsureSuccess();

                    Assert.NotNull(KubeHelper.KubeConfig.CurrentContext);
                    Assert.Equal(contextName, KubeHelper.KubeConfig.CurrentContext);

                    //-------------------------------------------------------------
                    // Verify: cluster info

                    response = (await NeonCliAsync("cluster", "info"))
                        .EnsureSuccess();

                    var clusterInfo = NeonHelper.JsonDeserialize<ClusterInfo>(response.OutputText);

                    Assert.Equal(configCluster.ClusterInfo.ClusterId, clusterInfo.ClusterId);
                    Assert.Equal(configCluster.ClusterInfo.ClusterVersion, clusterInfo.ClusterVersion);
                    Assert.Equal(clusterName, clusterInfo.Name);

                    //-------------------------------------------------------------
                    // Verify: cluster purpose

                    response = (await NeonCliAsync("cluster", "purpose"))
                        .EnsureSuccess();

                    Assert.Contains("test", response.OutputText);

                    response = (await NeonCliAsync("cluster", "purpose", "production"))
                        .EnsureSuccess();

                    Assert.Contains("production", response.OutputText);

                    //-------------------------------------------------------------
                    // Verify: cluster dashboard

                    // Verify the available dashboards.

                    var dashboards = await clusterProxy.ListClusterDashboardsAsync();

                    response = (await NeonCliAsync("cluster", "dashboard"))
                        .EnsureSuccess();

                    foreach (var dashboard in dashboards)
                    {
                        Assert.Contains(dashboard.Key, response.OutputText);
                    }

                    // Verify the dashboard URIs.

                    foreach (var dashboard in dashboards)
                    {
                        response = (await NeonCliAsync("cluster", "--url", "dashboard", dashboard.Key))
                            .EnsureSuccess();

                        Assert.Contains(dashboard.Value.Spec.Url, response.OutputText);
                    }

                    //-------------------------------------------------------------
                    // Verify: cluster lock/unlock/islocked commands.

                    response = (await NeonCliAsync("cluster", "islocked"));

                    Assert.Equal(0, response.ExitCode);     // exitcode=0: LOCKED

                    response = (await NeonCliAsync("cluster", "unlock", "--force"))
                        .EnsureSuccess();

                    response = (await NeonCliAsync("cluster", "islocked"));

                    Assert.Equal(2, response.ExitCode);     // exitcode=2: UNLOCKED

                    response = (await NeonCliAsync("cluster", "lock"))
                        .EnsureSuccess();

                    response = (await NeonCliAsync("cluster", "islocked"));

                    Assert.Equal(0, response.ExitCode);     // exitcode=0: LOCKED

                    //-------------------------------------------------------------
                    // Unlock the cluster so we can test dangerous commands.

                    response = (await NeonCliAsync("cluster", "unlock", "--force"))
                        .EnsureSuccess();

                    response = (await NeonCliAsync("cluster", "islocked"));

                    Assert.Equal(2, response.ExitCode);     // exitcode=2: UNLOCKED

                    //-------------------------------------------------------------
                    // Verify: cluster pause/resume

                    if ((clusterProxy.Capabilities & HostingCapabilities.Pausable) != 0)
                    {
                        // Pause the cluster.

                        response = (await NeonCliAsync("cluster", "pause", "--force"))
                            .EnsureSuccess();

                        response = (await NeonCliAsync("cluster", "health"))
                            .EnsureSuccess();

                        clusterHealth = NeonHelper.JsonDeserialize<ClusterHealth>(response.OutputText);

                        Assert.Equal(ClusterState.Paused, clusterHealth.State);

                        // Resume the cluster.

                        response = (await NeonCliAsync("cluster", "resume"))
                            .EnsureSuccess();

                        response = (await NeonCliAsync("cluster", "health"))
                            .EnsureSuccess();

                        clusterHealth = NeonHelper.JsonDeserialize<ClusterHealth>(response.OutputText);

                        Assert.Equal(ClusterState.Healthy, clusterHealth.State);
                    }

                    //-------------------------------------------------------------
                    // Verify: cluster stop/start

                    if ((clusterProxy.Capabilities & HostingCapabilities.Stoppable) != 0)
                    {
                        // Pause the cluster.

                        response = (await NeonCliAsync("cluster", "stop", "--force"))
                            .EnsureSuccess();

                        response = (await NeonCliAsync("cluster", "health"))
                            .EnsureSuccess();

                        clusterHealth = NeonHelper.JsonDeserialize<ClusterHealth>(response.OutputText);

                        Assert.Equal(ClusterState.Unhealthy, clusterHealth.State);

                        // Resume the cluster.

                        response = (await NeonCliAsync("cluster", "start"))
                            .EnsureSuccess();

                        response = (await NeonCliAsync("cluster", "health"))
                            .EnsureSuccess();

                        clusterHealth = NeonHelper.JsonDeserialize<ClusterHealth>(response.OutputText);

                        Assert.Equal(ClusterState.Healthy, clusterHealth.State);
                    }

                    //-------------------------------------------------------------
                    // Verify: cluster delete

                    response = (await NeonCliAsync("cluster", "delete", "--force"))
                        .EnsureSuccess();

                    Assert.Empty(KubeHelper.KubeConfig.Clusters.Where(cluster => cluster.Name == clusterName));
                }
                finally
                {
                    //-------------------------------------------------------------
                    // Remove the test cluster in case it wasn't removed above

                    if (!debugMode && KubeHelper.KubeConfig.Clusters.Any(cluster => cluster.Name == clusterName))
                    {
                        (await NeonCliAsync("cluster", "delete", "--force", clusterName)).EnsureSuccess();
                    }
                }
            }
        }

        /// <summary>
        /// Executes a <b>neon-cli</b> command/
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <returns>The command response.</returns>
        private async Task<ExecuteResponse> NeonCliAsync(params string[] args)
        {
            return await NeonHelper.ExecuteCaptureAsync(neonCliPath, args);
        }
    }
}