//-----------------------------------------------------------------------------
// FILE:	    Test_XenKubeFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
    [Trait(TestTrait.Category, TestArea.NeonKube)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_XenKubeFixture : IClassFixture<ClusterFixture>
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Static constructor.
        /// </summary>
        static Test_XenKubeFixture()
        {
            // Register a [ProfileClient] so commands will be able to pick
            // up secrets and profile information from [neon-assistant].

            NeonHelper.ServiceContainer.AddSingleton<IProfileClient>(new ProfileClient());
        }

        //---------------------------------------------------------------------
        // Instance members

        private ClusterFixture fixture;

        public Test_XenKubeFixture(ClusterFixture fixture, ITestOutputHelper testOutputHelper)
        {
            this.fixture = fixture;

            var options = new ClusterFixtureOptions() { TestOutputHelper = testOutputHelper };
            var status  = fixture.StartCluster(ClusterDefinitions.XenTiny, options: options);

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
        public void VerifyDefaultOptions()
        {
            // Verify that the [ClusterFixtureOptions] and [ClusterResetOptions] set the correct
            // property defaults.  This doesn't actually do anything with the cluster.

            var fixtureOptions = new ClusterFixtureOptions();

            Assert.NotNull(fixtureOptions.ResetOptions);
            Assert.False(fixtureOptions.Unredacted);
            Assert.False(fixtureOptions.RemoveClusterOnStart);
            Assert.False(fixtureOptions.RemoveClusterOnDispose);
            Assert.NotNull(fixtureOptions.TestOutputHelper);
            Assert.NotNull(fixtureOptions.ImageUriOrPath);
            Assert.NotNull(fixtureOptions.NeonCloudHeadendUri);
            Assert.True(fixtureOptions.CaptureDeploymentLogs);
            Assert.Equal(500, fixtureOptions.MaxParallel);

            var resetOptions = fixtureOptions.ResetOptions;

            Assert.True(resetOptions.ResetHarbor);
            Assert.True(resetOptions.ResetMinio);
            Assert.True(resetOptions.ResetCrio);
            Assert.True(resetOptions.ResetAuth);
            Assert.True(resetOptions.ResetMonitoring);
            Assert.Empty(resetOptions.KeepNamespaces);

            resetOptions = new ClusterResetOptions();

            Assert.True(resetOptions.ResetHarbor);
            Assert.True(resetOptions.ResetMinio);
            Assert.True(resetOptions.ResetCrio);
            Assert.True(resetOptions.ResetAuth);
            Assert.True(resetOptions.ResetMonitoring);
            Assert.Empty(resetOptions.KeepNamespaces);
        }
    }
}
