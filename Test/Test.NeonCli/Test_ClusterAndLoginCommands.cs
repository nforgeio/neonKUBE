// -----------------------------------------------------------------------------
// FILE:        Test_ClusterAndLoginCommands.cs
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

using Microsoft.Extensions.DependencyInjection;

using Neon;
using Neon.Common;
using Neon.Cryptography;
using Neon.Kube;
using Neon.Kube.ClusterDef;
using Neon.Kube.Config;
using Neon.Deployment;
using Neon.Kube.Hosting;
using Neon.Kube.Proxy;
using Neon.Kube.Xunit;
using Neon.IO;
using Neon.Xunit;

using Xunit;
using System.Dynamic;
using Xunit.Abstractions;
using Neon.Kube.ClusterMetadata;

namespace Test.NeonCli
{
    [Trait(TestTrait.Category, TestArea.NeonCli)]
    [Trait(TestTrait.Category, TestTrait.RequiresProfile)]
    [Trait(TestTrait.Category, TestTrait.Slow)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_ClusterAndLoginCommands
    {
        //---------------------------------------------------------------------
        // Private types

        private class Context
        {
            public string context { get; set; }
            public string @namespace { get; set; }
        }

        private class ListedContext
        {
            public string context { get; set; }
            public string @namespace { get; set; }
            public bool current { get; set; }
        }

        private class ContextList : List<ListedContext>
        {
        }

        //---------------------------------------------------------------------
        // Implementation

        /// <summary>
        /// This can be temporarily set to TRUE while debugging the tests.
        /// </summary>
        private bool debugMode = false;

        private const string clusterName  = "test-neoncli";
        private const string namePrefix   = "test-neoncli";
        private const string clusterLogin = $"root@{clusterName}";

        private const string awsClusterDefinition =
$@"
name: {clusterName}
purpose: test
timeSources:
- pool.ntp.org
kubernetes:
  allowPodsOnControlPlane: true
hosting:
  environment: aws
  aws:
    accessKeyId: $<secret:AWS_NEONFORGE[ACCESS_KEY_ID]>
    secretAccessKey: $<secret:AWS_NEONFORGE[SECRET_ACCESS_KEY]>
    availabilityZone: us-west-2a
    defaultEbsOptimized: true
nodes:
   node:
     role: control-plane
";

        private const string azureClusterDefinition =
$@"
name: {clusterName}
datacenter: westus2
purpose: test
timeSources:
- pool.ntp.org
kubernetes:
  allowPodsOnControlPlane: true
hosting:
  environment: azure
  azure:
    subscriptionId: $<secret:AZURE_NEONFORGE[SUBSCRIPTION_ID]>
    tenantId: $<secret:AZURE_NEONFORGE[TENANT_ID]>
    clientId: $<secret:AZURE_NEONFORGE[CLIENT_ID]>
    clientSecret: $<secret:AZURE_NEONFORGE[CLIENT_SECRET]>
    region: westus2
    disableProximityPlacement: false
    defaultVmSize: Standard_D4d_v5
nodes:
   node:
     role: control-plane
";

        private const string hypervClusterDefinition =
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
    namePrefix: {namePrefix}
    vcpus: 4
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

        private const string xenServerClusterDefinition =
$@"
name: {clusterName}
datacenter: $<profile:datacenter>
purpose: test
timeSources:
- pool.ntp.org
kubernetes:
  allowPodsOnControlPlane: true
hosting:
  environment: xenserver
  hypervisor:
    hostUsername: $<secret:XENSERVER_LOGIN[username]>
    hostPassword: $<secret:XENSERVER_LOGIN[password]>
    namePrefix: {namePrefix}
    vcpus: 4
    memory: 18 GiB
    osDisk: 64 GiB
    hosts:
    - name: XENHOST
      address: $<profile:xen-test.host>
  xenServer:
     snapshot: true
network:
  premiseSubnet: $<profile:lan.subnet>
  gateway: $<profile:lan.gateway>
  nameservers:
  - $<profile:lan.dns0>
  - $<profile:lan.dns1>
nodes:
  node:
    role: control-plane
    address: $<profile:xenserver.node0.ip>
    hypervisor:
      host: XENHOST
";

        private readonly string                                 neonCliPath;
        private readonly Dictionary<HostingEnvironment, string> envToDefinition =
            new Dictionary<HostingEnvironment, string>()
            {
                { HostingEnvironment.Aws, awsClusterDefinition },
                { HostingEnvironment.Azure, azureClusterDefinition },
                { HostingEnvironment.HyperV, hypervClusterDefinition },
                { HostingEnvironment.XenServer, xenServerClusterDefinition }
            };

        // Controls whether output checks YAML or JSON.

        private bool checkYaml;

        /// <summary>
        /// Constructor,
        /// </summary>
        public Test_ClusterAndLoginCommands()
        {
            // Locate the neon-cli binary.

            var thisAssembly            = Assembly.GetExecutingAssembly();
            var assemblyConfigAttribute = thisAssembly.GetCustomAttribute<AssemblyConfigurationAttribute>();
            var buildConfig             = assemblyConfigAttribute.Configuration;

            Covenant.Assert(assemblyConfigAttribute != null, () => $"Test assembly [{thisAssembly.FullName}] does not include [{nameof(AssemblyConfigurationAttribute)}].");

            neonCliPath = KubeHelper.NeonCliPath;

            Covenant.Assert(File.Exists(neonCliPath), () => $"[neon-cli] executable does not exist at: {neonCliPath}");
        }

        private const int repeatCount = 1;

        [MaintainerTheory]
        [Repeat(repeatCount)]
        [Trait(TestTrait.Category, TestTrait.CloudExpense)]
        public async Task Aws(int runCount)
        {
            _ = runCount;

            await Test(HostingEnvironment.Aws);
        }

        [MaintainerTheory]
        [Repeat(repeatCount)]
        [Trait(TestTrait.Category, TestTrait.CloudExpense)]
        public async Task Azure(int runCount)
        {
            _ = runCount;

            await Test(HostingEnvironment.Azure);
        }

        [MaintainerTheory]
        [Repeat(repeatCount)]
        public async Task HyperV(int runCount)
        {
            _ = runCount;

            await Test(HostingEnvironment.HyperV);
        }

        [MaintainerTheory]
        [Repeat(repeatCount)]
        public async Task XenServer(int runCount)
        {
            _ = runCount;

            await Test(HostingEnvironment.XenServer);
        }

        private async Task Test(HostingEnvironment environment)
        {
            // Use [neon-cli] to deploy a single-node Hyper-V test cluster and the verify that
            // common [neon-cli] cluster commands work as expected.  We're doing this all in a
            // single test method instead of using [ClusterFixture] because we want to test
            // cluster prepare/setup as well as cluster delete commands here.

            bool            clusterExists = false;
            ExecuteResponse response;

            // We need to inject an implementation for [PreprocessReader] so we'll
            // be able to resolve profile references within the cluster definition.

            NeonHelper.ServiceContainer.AddSingleton<IProfileClient>(new MaintainerProfile());

            // We're going to exercise the neon-cli output format option by using
            // "-o=json" for AWS and XenServer hosting environments and "-o=yaml"
            // for Azure and Hyper-V Azure hosting environments.

            string outputOption;

            switch (environment)
            {
                case HostingEnvironment.Aws:
                case HostingEnvironment.XenServer:

                    outputOption = "-o=json";
                    checkYaml    = false;
                    break;

                default:
                case HostingEnvironment.Azure:
                case HostingEnvironment.HyperV:

                    outputOption = "--output=yaml";
                    checkYaml    = true;
                    break;
            }

            //-----------------------------------------------------------------
            // Implement the tests.

            var clusterDefinitionYaml = envToDefinition[environment];
            var clusterDefinition     = ClusterDefinition.FromYaml(clusterDefinitionYaml);

            using (var tempFolder = new TempFolder())
            {
                try
                {
                    var clusterDefinitionPath = Path.Combine(tempFolder.Path, "cluster-definition.yaml");

                    File.WriteAllText(clusterDefinitionPath, clusterDefinitionYaml);

                    //-------------------------------------------------------------
                    // Remove the test cluster and login if they already exist
                    // (and we're not in debug mode).

                    response = (await NeonCliAsync("login", "list", outputOption))
                        .EnsureSuccess();

                    clusterExists = CheckOutput<ContextList>(response.OutputText,
                        items =>
                        {
                            return items.Any(item => item.context == clusterLogin);
                        });

                    if (clusterExists && !debugMode)
                    {
                        (await NeonCliAsync("cluster", "delete", "--force", clusterName)).EnsureSuccess();

                        response = (await NeonCliAsync("login", "list", outputOption))
                            .EnsureSuccess();

                        if (response.OutputText.Contains(clusterLogin))
                        {
                            Covenant.Assert(false, $"Cluster [{clusterName}] delete failed.");
                        }

                        clusterExists = false;
                    }

                    //-------------------------------------------------------------
                    // Validate the cluster definition.

                    response = (await NeonCliAsync("cluster", "validate", clusterDefinitionPath))
                        .EnsureSuccess();

                    //-------------------------------------------------------------
                    // Deploy the test cluster.  We're going to exercise the two step
                    // cluster prepare/setup command for some environments and the single
                    // step deploy command for others.

                    if (!clusterExists)
                    {
                        (await NeonCliAsync("logout"))
                            .EnsureSuccess();

                        switch (environment)
                        {
                            case HostingEnvironment.HyperV:
                            case HostingEnvironment.Aws:

                                (await NeonCliAsync("cluster", "prepare", clusterDefinitionPath, "--use-staged"))
                                    .EnsureSuccess();

                                (await NeonCliAsync("cluster", "setup", clusterLogin, "--use-staged"))
                                    .EnsureSuccess();

                                break;

                            default:
                            case HostingEnvironment.XenServer:
                            case HostingEnvironment.Azure:

                                (await NeonCliAsync("cluster", "deploy", clusterDefinitionPath, "--use-staged"))
                                    .EnsureSuccess();

                                break;
                        }
                    }

                    KubeHelper.KubeConfig.Reload();

                    if (debugMode && !clusterExists)
                    {
                        Assert.NotNull(KubeHelper.KubeConfig.Cluster);
                        Assert.NotNull(KubeHelper.KubeConfig.CurrentContext);
                        Assert.NotNull(KubeHelper.KubeConfig.Context);
                        Assert.NotNull(KubeHelper.KubeConfig.Cluster);
                        Assert.NotNull(KubeHelper.KubeConfig.User);
                    }

                    //-------------------------------------------------------------
                    // Verify PLAIN-TEXT: cluster login

                    // Logout to ready the tests below.

                    response = (await NeonCliAsync("logout"))
                        .EnsureSuccess();

                    KubeHelper.KubeConfig.Reload();
                    Assert.Null(KubeHelper.KubeConfig.CurrentContext);
                    Assert.Null(KubeHelper.KubeConfig.Context);
                    Assert.Null(KubeHelper.KubeConfig.Cluster);
                    Assert.Null(KubeHelper.KubeConfig.User);

                    // Ensure that [login list] works when there's no current cluster context.
                    // We've seen a [NullReferenceException] in the past for this scenario.

                    response = (await NeonCliAsync("login", "list", outputOption))
                        .EnsureSuccess();

                    Assert.True(CheckOutput<ContextList>(response.OutputText, items => !items.Any(item => item.current)));

                    // Ensure that [login] indicates that we're not logged to a cluster.

                    response = (await NeonCliAsync("login", outputOption))
                        .EnsureSuccess();

                    Assert.True(CheckOutput<Context>(response.OutputText, item => item == null));

                    // Login

                    KubeHelper.KubeConfig.Reload();

                    response = (await NeonCliAsync("login", clusterLogin, "--namespace=default", outputOption))
                        .EnsureSuccess();

                    KubeHelper.KubeConfig.Reload();
                    Assert.NotNull(KubeHelper.KubeConfig.CurrentContext);
                    Assert.Equal(clusterLogin, KubeHelper.KubeConfig.CurrentContext);

                    Assert.True(CheckOutput<Context>(response.OutputText,
                        item =>
                        {
                            if (item == null)
                            {
                                return false;
                            }

                            return item.context == KubeHelper.KubeConfig.CurrentContext &&
                                   item.@namespace == "default";
                        }));

                    response = (await NeonCliAsync("logout"))
                        .EnsureSuccess();

                    KubeHelper.KubeConfig.Reload();
                    Assert.Null(KubeHelper.KubeConfig.CurrentContext);
                    Assert.Null(KubeHelper.KubeConfig.Context);
                    Assert.Null(KubeHelper.KubeConfig.Cluster);
                    Assert.Null(KubeHelper.KubeConfig.User);

                    // Ensure that [login list] works when there's no current cluster context.
                    // We've seen a [NullReferenceException] in the past for this scenario.

                    response = (await NeonCliAsync("login", "list", outputOption))
                        .EnsureSuccess();

                    Assert.True(CheckOutput<ContextList>(response.OutputText,
                        items =>
                        {
                            var currentContext = items.SingleOrDefault(item => item.current);

                            return currentContext == null;
                        }));

                    // Ensure that [login] indicates that we're not logged to a cluster.

                    response = (await NeonCliAsync("login", outputOption))
                        .EnsureSuccess();

                    Assert.True(CheckOutput<Context>(response.OutputText, item => item == null));

                    // Login

                    KubeHelper.KubeConfig.Reload();

                    response = (await NeonCliAsync("login", clusterLogin, outputOption))
                        .EnsureSuccess();

                    KubeHelper.KubeConfig.Reload();
                    Assert.NotNull(KubeHelper.KubeConfig.CurrentContext);
                    Assert.Equal(clusterLogin, KubeHelper.KubeConfig.CurrentContext);

                    Assert.True(CheckOutput<Context>(response.OutputText,
                        item =>
                        {
                            if (item == null)
                            {
                                return false;
                            }

                            return item.context == KubeHelper.KubeConfig.CurrentContext &&
                                   item.@namespace == "default";
                        }));

                    //-------------------------------------------------------------
                    // Verify JSON: cluster login

                    // Logout to ready the tests below.

                    response = (await NeonCliAsync("logout"))
                        .EnsureSuccess();

                    KubeHelper.KubeConfig.Reload();
                    Assert.Null(KubeHelper.KubeConfig.CurrentContext);
                    Assert.Null(KubeHelper.KubeConfig.Context);
                    Assert.Null(KubeHelper.KubeConfig.Cluster);
                    Assert.Null(KubeHelper.KubeConfig.User);

                    // Ensure that [login list] works when there's no current cluster context.
                    // We've seen a [NullReferenceException] in the past for this scenario.

                    response = (await NeonCliAsync("login", "list", outputOption))
                        .EnsureSuccess();

                    Assert.True(CheckOutput<ContextList>(response.OutputText,
                        items =>
                        {
                            if (items.Count == 0)
                            {
                                return false;
                            }

                            return items.SingleOrDefault(item => item.context == clusterLogin) != null;
                        }));

                    // Ensure that [login] indicates that we're not logged into a cluster.

                    response = (await NeonCliAsync("login", outputOption))
                        .EnsureSuccess();

                    Assert.True(CheckOutput<Context>(response.OutputText, item => item == null));

                    // Login

                    KubeHelper.KubeConfig.Reload();

                    response = (await NeonCliAsync("login", "--namespace=default", outputOption, clusterLogin))
                        .EnsureSuccess();

                    KubeHelper.KubeConfig.Reload();
                    Assert.NotNull(KubeHelper.KubeConfig.CurrentContext);
                    Assert.Equal(clusterLogin, KubeHelper.KubeConfig.CurrentContext);

                    Assert.True(CheckOutput<Context>(response.OutputText,
                        item =>
                        {
                            if (item == null)
                            {
                                return false;
                            }

                            return item.context == clusterLogin && item.@namespace == "default";
                        }));

                    // Login List

                    KubeHelper.KubeConfig.Reload();

                    response = (await NeonCliAsync("login", "list", outputOption, clusterLogin))
                        .EnsureSuccess();

                    KubeHelper.KubeConfig.Reload();
                    Assert.NotNull(KubeHelper.KubeConfig.CurrentContext);
                    Assert.Equal(clusterLogin, KubeHelper.KubeConfig.CurrentContext);

                    Assert.True(CheckOutput<ContextList>(response.OutputText,
                        items =>
                        {
                            var currentItem = items.Single(item => item.current);

                            if (currentItem == null)
                            {
                                return false;
                            }

                            return currentItem.context == clusterLogin && currentItem.@namespace == "default";
                        }));

                    response = (await NeonCliAsync("login", "-n=neon-system", outputOption, clusterLogin))
                        .EnsureSuccess();

                    KubeHelper.KubeConfig.Reload();
                    Assert.NotNull(KubeHelper.KubeConfig.CurrentContext);
                    Assert.Equal(clusterLogin, KubeHelper.KubeConfig.CurrentContext);

                    Assert.True(CheckOutput<Context>(response.OutputText,
                        item =>
                        {
                            return item.context == clusterLogin && item.@namespace == "neon-system";
                        }));

                    // Logout

                    response = (await NeonCliAsync("logout"))
                        .EnsureSuccess();

                    KubeHelper.KubeConfig.Reload();
                    Assert.Null(KubeHelper.KubeConfig.CurrentContext);
                    Assert.Null(KubeHelper.KubeConfig.Context);
                    Assert.Null(KubeHelper.KubeConfig.Cluster);
                    Assert.Null(KubeHelper.KubeConfig.User);

                    // Ensure that [login list] works when there's no current cluster context.
                    // We've seen a [NullReferenceException] in the past for this scenario.

                    response = (await NeonCliAsync("login", "list", outputOption))
                        .EnsureSuccess();

                    Assert.True(CheckOutput<ContextList>(response.OutputText,
                        items =>
                        {
                            return !items.Any(item => item.current);
                        }));

                    // Ensure that [login] indicates that we're not logged to a cluster.

                    response = (await NeonCliAsync("login", outputOption))
                        .EnsureSuccess();

                    Assert.True(CheckOutput<ContextList>(response.OutputText,
                        item =>
                        {
                            return item == null;
                        }));

                    // Login

                    KubeHelper.KubeConfig.Reload();

                    response = (await NeonCliAsync("login", clusterLogin, outputOption))
                        .EnsureSuccess();

                    KubeHelper.KubeConfig.Reload();
                    Assert.NotNull(KubeHelper.KubeConfig.CurrentContext);
                    Assert.Equal(clusterLogin, KubeHelper.KubeConfig.CurrentContext);

                    Assert.True(CheckOutput<Context>(response.OutputText,
                        item =>
                        {
                            return item != null && item.context == clusterLogin;
                        }));

                    //-------------------------------------------------------------
                    // Verify that we can change the default namespace with [--namespace] and [-n].
                    // This also verifies that [neon login] lists cluster contexts.

                    response = (await NeonCliAsync("login", $"--namespace=default", outputOption))
                        .EnsureSuccess();

                    KubeHelper.KubeConfig.Reload();
                    Assert.NotNull(KubeHelper.KubeConfig.CurrentContext);
                    Assert.Equal(clusterLogin, KubeHelper.KubeConfig.CurrentContext);

                    Assert.True(CheckOutput<Context>(response.OutputText,
                        item =>
                        {
                            return item != null && item.context == clusterLogin && item.@namespace == "default";
                        }));

                    response = (await NeonCliAsync("login", $"-n=neon-system", outputOption))
                        .EnsureSuccess();

                    KubeHelper.KubeConfig.Reload();
                    Assert.NotNull(KubeHelper.KubeConfig.CurrentContext);
                    Assert.Equal(clusterLogin, KubeHelper.KubeConfig.CurrentContext);

                    Assert.True(CheckOutput<Context>(response.OutputText,
                        item =>
                        {
                            return item != null && item.context == clusterLogin && item.@namespace == "neon-system";
                        }));

                    //-------------------------------------------------------------
                    // Create a cluster proxy for use in the tests below.

                    HostingLoader.Initialize();

                    using var clusterProxy = ClusterProxy.Create(
                    kubeConfig:            KubeHelper.KubeConfig,
                    hostingManagerFactory: new HostingManagerFactory(),
                    operation:             ClusterProxy.Operation.LifeCycle);

                    //-------------------------------------------------------------
                    // Verify: cluster health

                    response = (await NeonCliAsync("cluster", "health")) // Output format defaults to YAML
                        .EnsureSuccess();

                    var clusterHealth = NeonHelper.YamlDeserialize<ClusterHealth>(response.OutputText);

                    Assert.Equal(ClusterState.Healthy, clusterHealth.State);
                    Assert.Equal("Cluster is healthy", clusterHealth.Summary);
                    Assert.Single(clusterHealth.Nodes);
                    Assert.Equal("node", clusterHealth.Nodes.First().Key);
                    Assert.Equal(ClusterNodeState.Running, clusterHealth.Nodes.First().Value);

                    response = (await NeonCliAsync("cluster", "health", "-o=yaml"))
                        .EnsureSuccess();

                    clusterHealth = NeonHelper.YamlDeserialize<ClusterHealth>(response.OutputText);

                    Assert.Equal(ClusterState.Healthy, clusterHealth.State);
                    Assert.Equal("Cluster is healthy", clusterHealth.Summary);
                    Assert.Single(clusterHealth.Nodes);
                    Assert.Equal("node", clusterHealth.Nodes.First().Key);
                    Assert.Equal(ClusterNodeState.Running, clusterHealth.Nodes.First().Value);

                    response = (await NeonCliAsync("cluster", "health", "--output=json"))
                        .EnsureSuccess();

                    clusterHealth = NeonHelper.JsonDeserialize<ClusterHealth>(response.OutputText);

                    Assert.Equal(ClusterState.Healthy, clusterHealth.State);
                    Assert.Equal("Cluster is healthy", clusterHealth.Summary);
                    Assert.Single(clusterHealth.Nodes);
                    Assert.Equal("node", clusterHealth.Nodes.First().Key);
                    Assert.Equal(ClusterNodeState.Running, clusterHealth.Nodes.First().Value);

                    //-------------------------------------------------------------
                    // Verify: cluster check

                    // $todo(jefflill):
                    //
                    // We're seeing some errors around priority classes which should be
                    // fixed:
                    //
                    //      https://github.com/nforgeio/neonKUBE/issues/1775
                    //
                    // as well as some missing resource limits, which may end up being
                    // by design.  I'm going to disable the success check here for now.

                    response = (await NeonCliAsync("cluster", "check"));

                    //-------------------------------------------------------------
                    // Verify: cluster info

                    response = (await NeonCliAsync("cluster", "info")) // Output format defaults to YAML
                        .EnsureSuccess();

                    var clusterInfo = NeonHelper.YamlDeserialize<ClusterInfo>(response.OutputText);

                    Assert.Equal(KubeHelper.KubeConfig.Cluster.ClusterInfo.ClusterId, clusterInfo.ClusterId);
                    Assert.Equal(KubeHelper.KubeConfig.Cluster.ClusterInfo.ClusterVersion, clusterInfo.ClusterVersion);
                    Assert.Equal(clusterName, clusterInfo.Name);

                    response = (await NeonCliAsync("cluster", "info", "-o=yaml"))
                        .EnsureSuccess();

                    clusterInfo = NeonHelper.YamlDeserialize<ClusterInfo>(response.OutputText);

                    Assert.Equal(KubeHelper.KubeConfig.Cluster.ClusterInfo.ClusterId, clusterInfo.ClusterId);
                    Assert.Equal(KubeHelper.KubeConfig.Cluster.ClusterInfo.ClusterVersion, clusterInfo.ClusterVersion);
                    Assert.Equal(clusterName, clusterInfo.Name);

                    response = (await NeonCliAsync("cluster", "info", "--output=json"))
                        .EnsureSuccess();

                    clusterInfo = NeonHelper.JsonDeserialize<ClusterInfo>(response.OutputText);

                    Assert.Equal(KubeHelper.KubeConfig.Cluster.ClusterInfo.ClusterId, clusterInfo.ClusterId);
                    Assert.Equal(KubeHelper.KubeConfig.Cluster.ClusterInfo.ClusterVersion, clusterInfo.ClusterVersion);
                    Assert.Equal(clusterName, clusterInfo.Name);

                    //-------------------------------------------------------------
                    // Verify: cluster purpose

                    response = (await NeonCliAsync("cluster", "purpose"))
                        .EnsureSuccess();

                    Assert.Contains("test", response.OutputText);

                    response = (await NeonCliAsync("cluster", "purpose", "production"))
                        .EnsureSuccess();

                    response = (await NeonCliAsync("cluster", "purpose"))
                        .EnsureSuccess();

                    Assert.Contains("production", response.OutputText);

                    // Revert back to "test" in case we need to re-run the test
                    // on the same cluster.

                    await NeonCliAsync("cluster", "purpose", "test");

                    //-------------------------------------------------------------
                    // Verify: login import/export/delete

                    using (var exportFolder = new TempFolder())
                    {
                        // Verify export of the current context to STDOUT.

                        response = (await NeonCliAsync("login", "export"))
                            .EnsureSuccess();

                        var export = NeonHelper.YamlDeserialize<ClusterLoginExport>(response.OutputText);

                        Assert.Equal(KubeHelper.KubeConfig.CurrentContext, export.Context.Name);
                        Assert.Equal(KubeHelper.KubeConfig.Cluster.Name, export.Context.Context.Cluster);
                        Assert.Equal(KubeHelper.KubeConfig.User.Name, export.Context.Context.User);

                        // Verify export of the current context by name to STDOUT.

                        response = (await NeonCliAsync("login", "export", $"--context={KubeHelper.KubeConfig.CurrentContext}"))
                            .EnsureSuccess();

                        export = NeonHelper.YamlDeserialize<ClusterLoginExport>(response.OutputText);

                        Assert.Equal(KubeHelper.KubeConfig.CurrentContext, export.Context.Name);
                        Assert.Equal(KubeHelper.KubeConfig.Cluster.Name, export.Context.Context.Cluster);
                        Assert.Equal(KubeHelper.KubeConfig.User.Name, export.Context.Context.User);

                        // Verify export of the current context to a temporary file.

                        var exportPath = Path.Combine(tempFolder.Path, "export.yaml");

                        response = (await NeonCliAsync("login", "export", exportPath))
                            .EnsureSuccess();

                        export = NeonHelper.YamlDeserialize<ClusterLoginExport>(File.ReadAllText(exportPath));

                        Assert.Equal(KubeHelper.KubeConfig.CurrentContext, export.Context.Name);
                        Assert.Equal(KubeHelper.KubeConfig.Cluster.Name, export.Context.Context.Cluster);
                        Assert.Equal(KubeHelper.KubeConfig.User.Name, export.Context.Context.User);

                        // Delete the current context and verify.

                        var testContextName = KubeHelper.KubeConfig.CurrentContext;
                        var testClusterName = KubeHelper.KubeConfig.Context.Context.Cluster;
                        var testUserName    = KubeHelper.KubeConfig.Context.Context.User;

                        (await NeonCliAsync("login", "delete", "--force", KubeHelper.KubeConfig.CurrentContext))
                            .EnsureSuccess();

                        KubeHelper.KubeConfig.Reload();
                        Assert.Null(KubeHelper.KubeConfig.CurrentContext);
                        Assert.Null(KubeHelper.KubeConfig.GetContext(testContextName));
                        Assert.Null(KubeHelper.KubeConfig.GetCluster(testClusterName));
                        Assert.Null(KubeHelper.KubeConfig.GetUser(testUserName));

                        // Verify import when the there are no conflicts with existing config items.

                        (await NeonCliAsync("login", "import", exportPath))
                            .EnsureSuccess();

                        KubeHelper.KubeConfig.Reload();
                        Assert.Equal(testContextName, KubeHelper.KubeConfig.CurrentContext);
                        Assert.Equal(testClusterName, KubeHelper.KubeConfig.Cluster.Name);
                        Assert.Equal(testUserName, KubeHelper.KubeConfig.User.Name);

                        // Import again with [--force] to overwrite existing config items.

                        (await NeonCliAsync("login", "import", "--force", exportPath))
                            .EnsureSuccess();

                        KubeHelper.KubeConfig.Reload();
                        Assert.Equal(testContextName, KubeHelper.KubeConfig.CurrentContext);
                        Assert.Equal(testClusterName, KubeHelper.KubeConfig.Cluster.Name);
                        Assert.Equal(testUserName, KubeHelper.KubeConfig.User.Name);
                    }

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
                        response = (await NeonCliAsync("cluster", "dashboard", "--url", dashboard.Key))
                            .EnsureSuccess();

                        Assert.Contains(dashboard.Value.Spec.Url, response.OutputText);
                    }

                    //-------------------------------------------------------------
                    // Verify: cluster lock, unlock, islocked commands.

                    response = (await NeonCliAsync("cluster", "lock"))
                        .EnsureSuccess();

                    response = (await NeonCliAsync("cluster", "islocked"));

                    Assert.Equal(0, response.ExitCode);     // exitcode=0: LOCKED

                    response = (await NeonCliAsync("cluster", "islocked", outputOption));

                    Assert.Equal(0, response.ExitCode);     // exitcode=0: LOCKED
                    Assert.True(CheckOutput<ClusterLockStatus>(response.OutputText,
                        item =>
                        {
                            return item != null &&
                                   item.Cluster == clusterDefinition.Name &&
                                   item.State == ClusterLockState.Locked;
                        }));

                    response = (await NeonCliAsync("cluster", "unlock"))
                        .EnsureSuccess();

                    response = (await NeonCliAsync("cluster", "islocked"));

                    Assert.Equal(2, response.ExitCode);     // exitcode=2: UNLOCKED

                    response = (await NeonCliAsync("cluster", "islocked", outputOption));
                    Assert.True(CheckOutput<ClusterLockStatus>(response.OutputText,
                        item =>
                        {
                            return item != null &&
                                   item.Cluster == clusterDefinition.Name &&
                                   item.State == ClusterLockState.Unlocked;
                        }));

                    response = (await NeonCliAsync("cluster", "lock"))
                        .EnsureSuccess();

                    response = (await NeonCliAsync("cluster", "islocked"));

                    Assert.Equal(0, response.ExitCode);     // exitcode=0: LOCKED

                    response = (await NeonCliAsync("cluster", "islocked", outputOption));
                    Assert.True(CheckOutput<ClusterLockStatus>(response.OutputText,
                        item =>
                        {
                            return item != null &&
                                   item.Cluster == clusterDefinition.Name &&
                                   item.State == ClusterLockState.Locked;
                        }));

                    //-------------------------------------------------------------
                    // Unlock the cluster so we can test dangerous commands below.

                    response = (await NeonCliAsync("cluster", "unlock"))
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

                        clusterHealth = NeonHelper.YamlDeserialize<ClusterHealth>(response.OutputText);

                        Assert.Equal(ClusterState.Paused, clusterHealth.State);

                        // Resume the cluster.

                        response = (await NeonCliAsync("cluster", "start"))
                            .EnsureSuccess();

                        response = (await NeonCliAsync("cluster", "health"))
                            .EnsureSuccess();

                        clusterHealth = NeonHelper.YamlDeserialize<ClusterHealth>(response.OutputText);

                        Assert.Equal(ClusterState.Healthy, clusterHealth.State);
                    }

                    //-------------------------------------------------------------
                    // Verify: cluster stop/start

                    if ((clusterProxy.Capabilities & HostingCapabilities.Stoppable) != 0)
                    {
                        // Stop the cluster.

                        response = (await NeonCliAsync("cluster", "stop", "--force"))
                            .EnsureSuccess();

                        response = (await NeonCliAsync("cluster", "health"))
                            .EnsureSuccess();

                        clusterHealth = NeonHelper.YamlDeserialize<ClusterHealth>(response.OutputText);

                        Assert.Equal(ClusterState.Off, clusterHealth.State);

                        // Start the cluster.

                        response = (await NeonCliAsync("cluster", "start"))
                            .EnsureSuccess();

                        response = (await NeonCliAsync("cluster", "health"))
                            .EnsureSuccess();

                        clusterHealth = NeonHelper.YamlDeserialize<ClusterHealth>(response.OutputText);

                        Assert.Equal(ClusterState.Healthy, clusterHealth.State);
                    }

                    //-------------------------------------------------------------
                    // Verify: cluster delete

                    response = (await NeonCliAsync("cluster", "delete", "--force"))
                        .EnsureSuccess();

                    KubeHelper.KubeConfig.Reload();
                    Assert.Empty(KubeHelper.KubeConfig.Clusters.Where(cluster => cluster.Name == clusterName));
                }
                finally
                {
                    //-------------------------------------------------------------
                    // Remove the test cluster in case it wasn't removed above

                    KubeHelper.KubeConfig.Reload();

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

        /// <summary>
        /// Used to verify JSON output by deserializing <paramref name="jsonText"/> into
        /// a <typeparamref name="T"/> and then passing that to the <paramref name="checker"/>
        /// function which will return <c>true</c> when the parsed output looks good.
        /// </summary>
        /// <param name="jsonText">Specifies the output to be parsed.</param>
        /// <param name="checker">Specifies the output checker method.</param>
        /// <returns><c>true</c> when the parsed JSON looks OK.</returns>
        private bool CheckJsonOutput<T>(string jsonText, Func<T, bool> checker)
        {
            Covenant.Requires<ArgumentNullException>(jsonText != null, nameof(jsonText));
            Covenant.Requires<ArgumentNullException>(checker != null, nameof(checker));

            return checker.Invoke(NeonHelper.JsonDeserialize<T>(jsonText));
        }

        /// <summary>
        /// Used to verify YAML output by deserializing <paramref name="yamlText"/> into
        /// a <typeparamref name="T"/> and then passing that to the <paramref name="checker"/>
        /// function which will return <c>true</c> when the parsed output looks good.
        /// </summary>
        /// <param name="yamlText">Specifies the output to be parsed.</param>
        /// <param name="checker">Specifies the output checker method.</param>
        /// <returns><c>true</c> when the parsed YAML looks OK.</returns>
        private bool CheckYamlOutput<T>(string yamlText, Func<T, bool> checker)
        {
            Covenant.Requires<ArgumentNullException>(yamlText != null, nameof(yamlText));
            Covenant.Requires<ArgumentNullException>(checker != null, nameof(checker));

            return checker.Invoke(NeonHelper.YamlDeserialize<T>(yamlText));
        }

        /// <summary>
        /// <para>
        /// Used to verify comand output by deserializing <paramref name="text"/> into
        /// a <typeparamref name="T"/> and then passing that to the <paramref name="checker"/>
        /// function which will return <c>true</c> when the parsed output looks good.
        /// </para>
        /// <para>
        /// This method parses the input text as YAML when <see cref="checkYaml"/> is
        /// <c>true</c> or as JSON otherwise.
        /// </para>
        /// </summary>
        /// <param name="text">Specifies the output to be parsed.</param>
        /// <param name="checker">Specifies the output checker method.</param>
        /// <returns><c>true</c> when the parsed text looks OK.</returns>
        private bool CheckOutput<T>(string text, Func<T, bool> checker)
        {
            Covenant.Requires<ArgumentNullException>(text != null, nameof(text));
            Covenant.Requires<ArgumentNullException>(checker != null, nameof(checker));

            if (checkYaml)
            {
                return CheckYamlOutput<T>(text, checker);
            }
            else
            {
                return CheckJsonOutput<T>(text, checker);
            }
        }
    }
}
