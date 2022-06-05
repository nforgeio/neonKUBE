//-----------------------------------------------------------------------------
// FILE:	    Test_LeaseElector.cs
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
using Prometheus;

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
    public class Test_LeaderElector : IClassFixture<ClusterFixture>
    {
        //---------------------------------------------------------------------
        // Static members

        private static readonly TimeSpan MaxWaitTime = TimeSpan.FromSeconds(30); 

        /// <summary>
        /// Static constructor.
        /// </summary>
        static Test_LeaderElector()
        {
            if (TestHelper.IsClusterTestingEnabled)
            {
                // Register a [ProfileClient] so tests will be able to pick
                // up secrets and profile information from [neon-assistant].

                NeonHelper.ServiceContainer.AddSingleton<IProfileClient>(new ProfileClient());
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private ClusterFixture fixture;

        public Test_LeaderElector(ClusterFixture fixture, ITestOutputHelper testOutputHelper)
        {
            this.fixture = fixture;

            var options = new ClusterFixtureOptions();
            var status  = fixture.StartWithNeonAssistant(options: options);

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
        public async Task SingleInstance()
        {
            // Verify that we can create a single [LeaderElector] instance and that:
            //
            //      1. The [OnNewLeader] action is called
            //      2. The [OnStartedLeader] action is called
            //      3. The new leader matches the current instance
            //      4. IsLeader and GetLeader() work

            bool    isLeading = false;
            string  leader    = null;
            var     config    = new LeaderElectionConfig(fixture.K8s, KubeNamespace.Default, "test", "instance-0");
            Task    electorTask;

            var elector = new LeaderElector(
                config:           config,
                onStartedLeading: () => isLeading = true,
                onStoppedLeading: () => isLeading = false,
                onNewLeader:      identity => leader = identity);

            using (elector)
            {
                electorTask = elector.RunAsync();

                NeonHelper.WaitFor(() => isLeading, timeout: MaxWaitTime);

                Assert.True(isLeading);
                Assert.True(elector.IsLeader);
                Assert.Equal("instance-0", leader);
                Assert.Equal("instance-0", elector.Leader);
            }

            // Ensure that the elector task completes.

            await electorTask.WaitAsync(timeout: MaxWaitTime);
        }

        [ClusterFact]
        public async Task SingleInstance_WithoutActions()
        {
            // Verify that the elector works without callback actions.

            var  config = new LeaderElectionConfig(fixture.K8s, KubeNamespace.Default, "test", "instance-0");
            var  elector = new LeaderElector(config);
            Task electorTask;

            using (elector)
            {
                electorTask = elector.RunAsync();

                NeonHelper.WaitFor(() => elector.IsLeader, timeout: MaxWaitTime);

                Assert.True(elector.IsLeader);
                Assert.Equal("instance-0", elector.Leader);
            }

            // Ensure that the elector task completes.

            await electorTask.WaitAsync(timeout: MaxWaitTime);
        }

        [ClusterFact]
        public async Task SingleInstance_WithCounters()
        {
            // Verify that the elector can increment performance counters.

            var promotionCounter = Metrics.CreateCounter("test_promotions", string.Empty);
            var demotionCounter  = Metrics.CreateCounter("test_demotions", string.Empty);
            var newLeaderCounter = Metrics.CreateCounter("test_newleaders", string.Empty);

            var config = new LeaderElectionConfig(
                k8s:              fixture.K8s,
                @namespace:       KubeNamespace.Default,
                leaseName:       "test",
                identity:        "instance-0",
                promotionCounter: promotionCounter,
                demotionCounter:  demotionCounter,
                newLeaderCounter: newLeaderCounter);

            var  elector = new LeaderElector(config);
            Task electorTask;

            using (elector)
            {
                electorTask = elector.RunAsync();

                NeonHelper.WaitFor(() => elector.IsLeader, timeout: MaxWaitTime);

                Assert.True(elector.IsLeader);
                Assert.Equal("instance-0", elector.Leader);
            }

            // Ensure that the elector task completes.

            await electorTask.WaitAsync(timeout: MaxWaitTime);

            // Ensure that the counters are correct.

            Assert.Equal(1, promotionCounter.Value);
            Assert.Equal(0, demotionCounter.Value);     // We don't see demotions when the elector is disposed
            Assert.Equal(2, newLeaderCounter.Value);    // We do see a leadership change when the elector is disposed
        }
    }
}
