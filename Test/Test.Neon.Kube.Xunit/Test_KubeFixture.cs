//-----------------------------------------------------------------------------
// FILE:	    Test_KubeFixture.cs
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

using Neon.Common;
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
    public class Test_KubeFixture : IClassFixture<ClusterFixture>
    {
        private ClusterFixture fixture;

        public Test_KubeFixture(ClusterFixture fixture, ITestOutputHelper testOutputHelper)
        {
            this.fixture = fixture;

            var fixtureOptions =
                new ClusterFixtureOptions()
                {
                    RemoveClusterOnStart   = false,
                    RemoveClusterOnDispose = false,
                    ResetOptions           = new ClusterResetOptions(),
                    TestOutputHelper       = testOutputHelper,
                    Unredacted             = true,
                };

            var status = fixture.StartCluster(string.Empty);

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
            // property defaults.  This test doesn't actually do anything with the cluster.

            var fixtureOptions = new ClusterFixtureOptions();

            Assert.NotNull(fixtureOptions.ResetOptions);
            Assert.False(fixtureOptions.Unredacted);
            Assert.False(fixtureOptions.RemoveClusterOnStart);
            Assert.False(fixtureOptions.RemoveClusterOnDispose);
            Assert.NotNull(fixtureOptions.TestOutputHelper);
            Assert.NotNull(fixtureOptions.ImageUriOrPath);
            Assert.NotNull(fixtureOptions.NeonCloudHeadendUri);
            Assert.False(fixtureOptions.CaptureDeploymentLogs);
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
