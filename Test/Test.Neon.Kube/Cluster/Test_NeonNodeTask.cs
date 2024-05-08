//-----------------------------------------------------------------------------
// FILE:        Test_NeonNodeTask.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
using System.Net;
using System.Text;
using System.Threading.Tasks;

using k8s.Autorest;
using Microsoft.Extensions.DependencyInjection;

using Neon.Common;
using Neon.Deployment;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.K8s;
using Neon.Kube.Resources;
using Neon.Kube.Resources.Cluster;
using Neon.Kube.Xunit;
using Neon.Xunit;

using Xunit;
using Xunit.Abstractions;

namespace TestKube
{
    [Trait(TestTrait.Category, TestArea.NeonKube)]
    [Trait(TestTrait.Category, TestTrait.Slow)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_NeonNodeTask : IClassFixture<ClusterFixture>
    {
        //---------------------------------------------------------------------
        // Static members

        private static readonly TimeSpan timeout      = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan pollInterval = TimeSpan.FromSeconds(1);

        private const string testFolderPath = $"/tmp/{nameof(Test_NeonNodeTask)}";

        /// <summary>
        /// Static constructor.
        /// </summary>
        static Test_NeonNodeTask()
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

        public Test_NeonNodeTask(ClusterFixture fixture, ITestOutputHelper testOutputHelper)
        {
            this.fixture = fixture;

            var options = new ClusterFixtureOptions();
            var status  = fixture.StartWithCurrentCluster(options: options);

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

        /// <summary>
        /// Removes all existing node tasks.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task DeleteExistingTasksAsync()
        {
            var existingTasks = (await fixture.K8s.CustomObjects.ListClusterCustomObjectAsync<V1NeonNodeTask>()).Items;

            foreach (var resource in existingTasks)
            {
                await fixture.K8s.CustomObjects.DeleteClusterCustomObjectAsync(resource);
            }
        }

        /// <summary>
        /// Returns a 5-digit base-36 UUID string.
        /// </summary>
        /// <returns>The UUID.</returns>
        private string CreateUuidString()
        {
            return NeonHelper.CreateBase36Uuid().Substring(0, 5);
        }

        [ClusterFact]
        public async Task NodeTask_Basic()
        {
            try
            {
                //-----------------------------------------------------------------
                // We're going to schedule simple node tasks for all cluster nodes that 
                // touch a temporary file and then verify that the file was written
                // to the nodes and that the node task status indicates the the operation
                // succeeded.

                await DeleteExistingTasksAsync();

                // Create a string dictionary that maps cluster node names to the unique
                // name to use for the test tasks targeting each node.

                var nodeToTaskName = new Dictionary<string, string>();

                foreach (var node in fixture.Cluster.Nodes)
                {
                    nodeToTaskName.Add(node.Name, $"test-basic-{node.Name}-{CreateUuidString()}");
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

                    var nodeTask = new V1NeonNodeTask();
                    var metadata = nodeTask.Metadata;
                    var spec     = nodeTask.Spec;

                    metadata.SetLabel(NeonLabel.RemoveOnClusterReset);

                    var filePath = GetTestFilePath(node.Name);
                    var folderPath = LinuxPath.GetDirectoryName(filePath);

                    spec.Node             = node.Name;
                    spec.RetentionSeconds = 30;
                    spec.BashScript       =
 $@"
set -euo pipefail

mkdir -p $NODE_ROOT{folderPath}
touch $NODE_ROOT{filePath}
";
                    await fixture.K8s.CustomObjects.CreateClusterCustomObjectAsync<V1NeonNodeTask>(nodeTask, name: nodeToTaskName[node.Name]);
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
                        foreach (var task in (await fixture.K8s.CustomObjects.ListClusterCustomObjectAsync<V1NeonNodeTask>()).Items.Where(task => taskNames.Contains(task.Metadata.Name)))
                        {
                            switch (task.Status.Phase)
                            {
                                case V1NeonNodeTask.Phase.New:
                                case V1NeonNodeTask.Phase.Pending:
                                case V1NeonNodeTask.Phase.Running:

                                    return false;
                            }
                        }

                        return true;
                    },
                    timeout:      timeout,
                    pollInterval: pollInterval);

                //-----------------------------------------------------------------
                // Verify that the node tasks completeted successfully and are being
                // retained for a while.

                var nodeTasks = await fixture.K8s.CustomObjects.ListClusterCustomObjectAsync<V1NeonNodeTask>();

                foreach (var task in nodeTasks.Items)
                {
                    if (taskNames.Contains(task.Metadata.Name))
                    {
                        Assert.Equal(V1NeonNodeTask.Phase.Success, task.Status.Phase);
                        Assert.Equal(0, task.Status.ExitCode);
                        Assert.Equal(string.Empty, task.Status.Output);
                        Assert.Equal(string.Empty, task.Status.Error);
                    }
                }

                //-----------------------------------------------------------------
                // Connect to each of the nodes and verify that the files touched by
                // the scripts actually exist.

                foreach (var node in fixture.Cluster.Nodes)
                {
                    var filePath = GetTestFilePath(node.Name);

                    // Clear and recreate the node test folder.

                    node.Connect();
                    Assert.True(node.FileExists(filePath));
                }
            }
            finally
            {
                // Remove the test folders on the nodes.

                foreach (var node in fixture.Cluster.Nodes)
                {
                    var filePath = GetTestFilePath(node.Name);

                    // Clear and recreate the node test folder.

                    node.Connect();
                    node.SudoCommand($"rm -rf {testFolderPath}");
                }
            }
        }

        [ClusterFact]
        public async Task NodeTask_ExitCodeAndStreams()
        {
            //-----------------------------------------------------------------
            // Submit a task to the first control-plane node that returns a non-zero
            // exit code as well as writes to the standard output and error
            // streams.
            //
            // Then we'll verify that the task [Phase==Failed] and confirm that
            // the exitcode and streams are present in the task status.

            await DeleteExistingTasksAsync();

            var taskName = $"test-exitcode-{CreateUuidString()}";
            var nodeTask = new V1NeonNodeTask();
            var metadata = nodeTask.Metadata;
            var spec     = nodeTask.Spec;

            spec.Node             = fixture.Cluster.DeploymentControlNode.Name;
            spec.RetentionSeconds = 30;
            spec.BashScript       =
@"
echo 'HELLO WORLD!'   >&1
echo 'GOODBYE WORLD!' >&2

exit 123
";
            metadata.SetLabel(NeonLabel.RemoveOnClusterReset);

            await fixture.K8s.CustomObjects.CreateClusterCustomObjectAsync<V1NeonNodeTask>(nodeTask, name: taskName);

            //-----------------------------------------------------------------
            // Wait the node task to report completion.

            await NeonHelper.WaitForAsync(
                async () =>
                {
                    var task = await fixture.K8s.CustomObjects.ReadClusterCustomObjectAsync<V1NeonNodeTask>(taskName);

                    switch (task.Status.Phase)
                    {
                        case V1NeonNodeTask.Phase.New:
                        case V1NeonNodeTask.Phase.Pending:
                        case V1NeonNodeTask.Phase.Running:

                            return false;

                        default:

                            return true;
                    }
                },
                timeout:      timeout,
                pollInterval: pollInterval);

            //-----------------------------------------------------------------
            // Verify that the node task failed (due to the non-zero exit code)
            // and that the exit code as well as the output/error streams were
            // captured.

            var task = await fixture.K8s.CustomObjects.ReadClusterCustomObjectAsync<V1NeonNodeTask>(taskName);

            Assert.Equal(V1NeonNodeTask.Phase.Failed, task.Status.Phase);
            Assert.Equal(123, task.Status.ExitCode);
            Assert.StartsWith("HELLO WORLD!", task.Status.Output);
            Assert.StartsWith("GOODBYE WORLD!", task.Status.Error);
        }

        [Fact]
        public async Task NeonTask_Timeout()
        {
            //-----------------------------------------------------------------
            // Verify that task timeouts are honored.  

            await DeleteExistingTasksAsync();

            var taskName = $"test-timeout-{CreateUuidString()}";
            var nodeTask = new V1NeonNodeTask();
            var metadata = nodeTask.Metadata;
            var spec     = nodeTask.Spec;

            spec.Node             = fixture.Cluster.DeploymentControlNode.Name;
            spec.TimeoutSeconds   = 15;
            spec.RetentionSeconds = 30;
            spec.BashScript       =
@"
sleep 30
";
            metadata.SetLabel(NeonLabel.RemoveOnClusterReset);

            await fixture.K8s.CustomObjects.CreateClusterCustomObjectAsync<V1NeonNodeTask>(nodeTask, name: taskName);

            //-----------------------------------------------------------------
            // Wait the node task to report completion.

            var phase    = V1NeonNodeTask.Phase.New;
            var exitCode = int.MaxValue;

            await NeonHelper.WaitForAsync(
                async () =>
                {
                    var task = await fixture.K8s.CustomObjects.ReadClusterCustomObjectAsync<V1NeonNodeTask>(taskName);

                    switch (task.Status.Phase)
                    {
                        case V1NeonNodeTask.Phase.New:
                        case V1NeonNodeTask.Phase.Pending:
                        case V1NeonNodeTask.Phase.Running:

                            return false;

                        default:

                            phase    = task.Status.Phase;
                            exitCode = task.Status.ExitCode;
                            return true;
                    }
                },
                timeout:      timeout,
                pollInterval: pollInterval);

            //-----------------------------------------------------------------
            // Verify that the node task timed out.

            Assert.Equal(V1NeonNodeTask.Phase.Timeout, phase);
            Assert.Equal(-1, exitCode);
        }

        [Fact]
        public async Task NeonTask_StartBefore()
        {
            //-----------------------------------------------------------------
            // Verify that a task scheduled with a StartBeforeTimestamp that is
            // already too late is detected and its status is set to TARDY.

            await DeleteExistingTasksAsync();

            var taskName = $"test-tardy-{CreateUuidString()}";
            var nodeTask = new V1NeonNodeTask();
            var metadata = nodeTask.Metadata;
            var spec     = nodeTask.Spec;

            spec.Node                 = fixture.Cluster.DeploymentControlNode.Name;
            spec.StartBeforeTimestamp = DateTime.UtcNow - TimeSpan.FromHours(1);
            spec.TimeoutSeconds       = 15;
            spec.RetentionSeconds     = 30;
            spec.BashScript           =
@"
sleep 5
";
            metadata.SetLabel(NeonLabel.RemoveOnClusterReset);

            await fixture.K8s.CustomObjects.CreateClusterCustomObjectAsync<V1NeonNodeTask>(nodeTask, name: taskName);

            //-----------------------------------------------------------------
            // Wait the node task to reported as TARDY.

            await NeonHelper.WaitForAsync(
                async () =>
                {
                    var task = await fixture.K8s.CustomObjects.ReadClusterCustomObjectAsync<V1NeonNodeTask>(taskName);

                    return task.Status.Phase == V1NeonNodeTask.Phase.Tardy;
                },
                timeout:      timeout,
                pollInterval: pollInterval);
        }

        [Fact]
        public async Task NeonTask_StartAfter()
        {
            //-----------------------------------------------------------------
            // Verify that a task scheduled in the future with a StartAfterTimestamp
            // is actually executed in the future.

            await DeleteExistingTasksAsync();

            var taskName     = $"test-scheduled-{CreateUuidString()}";
            var nodeTask     = new V1NeonNodeTask();
            var metadata     = nodeTask.Metadata;
            var spec         = nodeTask.Spec;
            var scheduledUtc = DateTime.UtcNow + TimeSpan.FromSeconds(90);

            spec.Node                = fixture.Cluster.DeploymentControlNode.Name;
            spec.StartAfterTimestamp = scheduledUtc;
            spec.TimeoutSeconds      = 15;
            spec.RetentionSeconds    = 30;
            spec.BashScript          =
@"
sleep 5
";
            metadata.SetLabel(NeonLabel.RemoveOnClusterReset);

            await fixture.K8s.CustomObjects.CreateClusterCustomObjectAsync<V1NeonNodeTask>(nodeTask, name: taskName);

            //-----------------------------------------------------------------
            // Wait the node task to reported as SUCCESS.

            var actualUtc = DateTime.MinValue;

            await NeonHelper.WaitForAsync(
                async () =>
                {
                    var task = await fixture.K8s.CustomObjects.ReadClusterCustomObjectAsync<V1NeonNodeTask>(taskName);

                    if (task.Status.Phase == V1NeonNodeTask.Phase.Success)
                    {
                        actualUtc = task.Status.StartTimestamp.Value;

                        return true;
                    }
                    else
                    {
                        return false;
                    }
                },
                timeout:      timeout,
                pollInterval: pollInterval);

            //-----------------------------------------------------------------
            // Verify that the task actually started at or after the scheduled time.

            Assert.True(actualUtc >= scheduledUtc);
        }

        [Fact]
        public async Task NeonTask_MissingNode()
        {
            //-----------------------------------------------------------------
            // Verify that the [V1NodeTask] controller in [neon-cluster-operator] deletes
            // tasks assigned to nodes that don't exist.

            await DeleteExistingTasksAsync();

            var taskName     = $"test-badnode-{CreateUuidString()}";
            var nodeTask     = new V1NeonNodeTask();
            var metadata     = nodeTask.Metadata;
            var spec         = nodeTask.Spec;
            var scheduledUtc = DateTime.UtcNow + TimeSpan.FromHours(1);

            spec.Node                = $"missing-node-{CreateUuidString()}";
            spec.StartAfterTimestamp = scheduledUtc;
            spec.TimeoutSeconds      = 15;
            spec.RetentionSeconds    = 30;
            spec.BashScript          =
@"
sleep 5
";
            metadata.SetLabel(NeonLabel.RemoveOnClusterReset);

            await fixture.K8s.CustomObjects.CreateClusterCustomObjectAsync<V1NeonNodeTask>(nodeTask, name: taskName);

            //-----------------------------------------------------------------
            // Wait the node task to be deleted.

            await NeonHelper.WaitForAsync(
                async () =>
                {
                    try
                    {
                        await fixture.K8s.CustomObjects.ReadClusterCustomObjectAsync<V1NeonNodeTask>(taskName);

                        return false;
                    }
                    catch (HttpOperationException e)
                    {
                        return e.Response.StatusCode == HttpStatusCode.NotFound;
                    }
                },
                timeout:      timeout,
                pollInterval: pollInterval);
        }
    }
}
