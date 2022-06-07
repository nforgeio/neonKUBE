//-----------------------------------------------------------------------------
// FILE:	    Test_NeonNodeAgent.cs
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
using Neon.Kube.Resources;
using Neon.Kube.Xunit;
using Neon.Xunit;

using Xunit;
using Xunit.Abstractions;

namespace TestKube
{
    [Trait(TestTrait.Category, TestArea.NeonKube)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_NeonNodeAgent
    {
        //---------------------------------------------------------------------
        // Static members

        private static readonly TimeSpan timeout      = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan pollInterval = TimeSpan.FromSeconds(1);

        private const string testFolderPath = $"/tmp/{nameof(Test_NeonNodeAgent)}";

        /// <summary>
        /// Static constructor.
        /// </summary>
        static Test_NeonNodeAgent()
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

        private ClusterFixture      fixture;

        public Test_NeonNodeAgent(ClusterFixture fixture, ITestOutputHelper testOutputHelper)
        {
            this.fixture = fixture;

            var options = new ClusterFixtureOptions();

            //################################################################
            // $debug(jefflill): Restore this after manual testing is complete
            //var status  = fixture.StartWithNeonAssistant(options: options);
            //################################################################

            var status = fixture.StartWithCurrentCluster(options: options);

            if (status == TestFixtureStatus.Disabled)
            {
                return;
            }
            else if (status == TestFixtureStatus.AlreadyRunning)
            {
                fixture.ResetCluster();
            }
        }

        /// <summary>
        /// Returns the absolute path to a file located within the unit test's temporary
        /// folder on one or more of the cluster nodes.
        /// </summary>
        /// <param name="fileName">The file name.</param>
        /// <returns>The absolute file path.</returns>
        private string GetTestFilePath(string fileName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(fileName), nameof(fileName));

            return LinuxPath.Combine(testFolderPath, fileName);
        }

        [ClusterFact]
        public async Task NodeTask_Basic()
        {
            // We're going to schedule simple node tasks for all cluster nodes that 
            // touches a temporary file and then verify that the file was written
            // to the nodes and that the node task status indicates the the operation
            // succeeded.

            // Create a string dictionary that maps cluster node names to the unique
            // name to use for the test tasks targeting each node.

            var nodeToTaskName = new Dictionary<string, string>();

            foreach (var node in fixture.Cluster.Nodes)
            {
                nodeToTaskName.Add(node.Name, $"test-basic-{node.Name}-{NeonHelper.CreateBase36Guid()}");
            }

            // Initalize a test folder on each node where the task will update a file
            // indicating that it ran and then submit a task for each node.

            foreach (var node in fixture.Cluster.Nodes)
            {
                // Clear and recreate the node test folder.

                node.Connect();
                node.SudoCommand($"rm -rf {testFolderPath}");
                node.SudoCommand($"mkdir -p {testFolderPath}");

                // Create the node task for the target node.

                var nodeTask = new V1NodeTask();
                var metadata = nodeTask.Metadata;
                var spec     = nodeTask.Spec;

                metadata.Name = nodeToTaskName[node.Name];
                metadata.SetLabel(NeonLabel.RemoveOnClusterReset);

                spec.Node       = node.Name;
                spec.BashScript = $"touch $NODE_ROOT/{GetTestFilePath(node.Name)}";

                await fixture.K8s.JNET_CreateClusterCustomObjectAsync(nodeTask);
            }

            // Wait for all of the node tasks to report completion.

            var taskNames = new HashSet<string>();

            foreach (var taskName in nodeToTaskName.Values)
            {
                taskNames.Add(taskName);
            }

            await NeonHelper.WaitForAsync(
                async () =>
                {
                    foreach (var nodeTask in (await fixture.K8s.JNET_ListClusterCustomObjectAsync<V1NodeTask>()).Items.Where(task => taskNames.Contains(task.Metadata.Name)))
                    {
                        switch (nodeTask.Status.Phase)
                        {
                            case V1NodeTask.NodeTaskPhase.New:
                            case V1NodeTask.NodeTaskPhase.Pending:
                            case V1NodeTask.NodeTaskPhase.Running:

                                return false;
                        }
                    }

                    return true;
                },
                timeout:      timeout,
                pollInterval: pollInterval);
        }
    }
}
