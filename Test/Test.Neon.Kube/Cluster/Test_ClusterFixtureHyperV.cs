//-----------------------------------------------------------------------------
// FILE:	    Test_ClusterFixtureHyperV.cs
// CONTRIBUTOR: Jeff Lill
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
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Neon.Common;
using Neon.Deployment;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Xunit;
using Neon.Xunit;

using Xunit;
using Xunit.Abstractions;

namespace TestKube
{
    /// <summary>
    /// This is a somewhat limited test of <see cref="ClusterFixture"/> for Hyper-V hosted
    /// clusters.  This isn't intended to be comprehensive but is intended to be temporarily 
    /// modified for manually testing corner cases.  We've decided not to make this comprehensive
    /// because that would require that we test removing clusters which would disrupt other
    /// cluster unit tests.
    /// </summary>
    [Trait(TestTrait.Category, TestArea.NeonKube)]
    [Trait(TestTrait.Category, TestTrait.RequiresProfile)]
    [Trait(TestTrait.Category, TestTrait.Slow)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_ClusterFixtureHyperV : IClassFixture<ClusterFixture>
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Static constructor.
        /// </summary>
        static Test_ClusterFixtureHyperV()
        {
            if (TestHelper.IsClusterTestingEnabled)
            {
                // Register a [ProfileClient] so tests will be able to pick
                // up secrets and profile information from [neon-assistant].

                NeonHelper.ServiceContainer.AddSingleton<IProfileClient>(new MaintainerProfile());
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private ClusterFixture fixture;

        public Test_ClusterFixtureHyperV(ClusterFixture fixture, ITestOutputHelper testOutputHelper)
        {
            this.fixture = fixture;

            // Register a [ProfileClient] so tests will be able to pick up
            // secrets and profile information from [neon-assistant].  We're
            // also going to ensure that [neon-assistant] is signed-in and
            // also extend the sign-in period by 120 minutes so that we'll
            // still be signed-in when there's a subsequent test run.

            var maintainerProfile = new MaintainerProfile();

            maintainerProfile.EnsureAuthenticated(TimeSpan.FromMinutes(120));

            NeonHelper.ServiceContainer.AddSingleton<IProfileClient>(maintainerProfile);

            // Start/Reset the cluster

            var options = new ClusterFixtureOptions() { TestOutputHelper = testOutputHelper };
            var status  = fixture.StartCluster(HyperVClusterDefinitions.Tiny, options: options);

            if (status == TestFixtureStatus.Disabled)
            {
                return;
            }
            else if (status == TestFixtureStatus.AlreadyRunning)
            {
                fixture.ResetCluster();
            }
        }

        [ClusterFact]
        public async Task NeonCli()
        {
            // Verify that we can execute a neon-cli command.

            var response = (await fixture.NeonExecuteCaptureAsync("get", "namespaces"))
                .EnsureSuccess();

            var namespaces = 
                new string[]
                {
                    "default",
                    "kube-node-lease",
                    "kube-public",
                    "kube-system"
                };

            foreach (var @namespace in namespaces)
            {
                Assert.Contains(@namespace, response.OutputText);
            }
        }

        [ClusterFact]
        public async Task Helm()
        {
            // Verify that we can deploy a simple test pod via the Helm method.

            using (var tempFolder = new TempFolder())
            {
                var chartPath      = Path.Combine(tempFolder.Path, "Chart.yaml");
                var templateFolder = Path.Combine(tempFolder.Path, "templates");
                var templatePath   = Path.Combine(templateFolder, "deployment.yaml");

                File.WriteAllText(Path.Combine(tempFolder.Path, ".helmignore"), string.Empty);
                Directory.CreateDirectory(templateFolder);

                const string deploymentYaml =
@"
apiVersion: apps/v1
kind: Deployment
metadata:
  labels:
    app: test
  name: test
spec:
  replicas: 1
  selector:
    matchLabels:
      app: test
  template:
    metadata:
      labels:
        app: test
    spec:
      containers:
      - name: test
        image: ghcr.io/neonrelease-dev/test:latest
";
                File.WriteAllText(templatePath, deploymentYaml);

                const string chartYaml =
@"
apiVersion: v2
name: neon-cluster-operator
description: Manages NEONKUBE clusters via multiple control loops
type: application
version: 0
appVersion: 0
";
                File.WriteAllText(chartPath, chartYaml);

                (await fixture.NeonExecuteCaptureAsync("helm", "install", "test-pod", "--namespace", "default", tempFolder.Path))
                    .EnsureSuccess();
            }
        }
    }
}
