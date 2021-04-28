//-----------------------------------------------------------------------------
// FILE:        Test_MultiClient.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Data;
using Neon.IO;
using Neon.Tasks;
using Neon.Temporal;
using Neon.Temporal.Internal;
using Neon.Xunit;
using Neon.Xunit.Temporal;

using Newtonsoft.Json;
using Xunit;

namespace TestTemporal
{
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_MultiClient : IClassFixture<TemporalFixture>
    {
        private TemporalFixture  fixture;

        public Test_MultiClient(TemporalFixture fixture)
        {
            var settings = new TemporalSettings()
            {
                Namespace              = TemporalFixture.Namespace,
                ProxyLogLevel          = TemporalTestHelper.ProxyLogLevel,
                CreateNamespace        = true,
                Debug                  = TemporalTestHelper.Debug,
                DebugPrelaunched       = TemporalTestHelper.DebugPrelaunched,
                DebugDisableHeartbeats = TemporalTestHelper.DebugDisableHeartbeats,
                ClientIdentity         = TemporalTestHelper.ClientIdentity
            };

            this.fixture = fixture;

            fixture.Start(settings, composeFile: TemporalTestHelper.TemporalStackDefinition, reconnect: true, keepRunning: TemporalTestHelper.KeepTemporalServerOpen, noClient: true);
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskQueue = "taskqueue-1")]
        public interface IWorkflowWithResult1: IWorkflow
        {
            [WorkflowMethod]
            Task<string> HelloAsync(string name);
        }

        public class WorkflowWithResult1 : WorkflowBase, IWorkflowWithResult1
        {
            public async Task<string> HelloAsync(string name)
            {
                return await Task.FromResult($"WF1 says: Hello {name}!");
            }
        }

        [WorkflowInterface(TaskQueue = "taskqueue-2")]
        public interface IWorkflowWithResult2 : IWorkflow
        {
            [WorkflowMethod]
            Task<string> HelloAsync(string name);
        }

        public class WorkflowWithResult2 : WorkflowBase, IWorkflowWithResult2
        {
            public async Task<string> HelloAsync(string name)
            {
                return await Task.FromResult($"WF2 says: Hello {name}!");
            }
        }

        [WorkflowInterface(TaskQueue = "taskqueue-3")]
        public interface IWorkflowWithResult3 : IWorkflow
        {
            [WorkflowMethod]
            Task<string> HelloAsync(string name);
        }

        public class WorkflowWithResult3 : WorkflowBase, IWorkflowWithResult3
        {
            public async Task<string> HelloAsync(string name)
            {
                return await Task.FromResult($"WF3 says: Hello {name}!");
            }
        }

        [WorkflowInterface(TaskQueue = "taskqueue-4")]
        public interface IWorkflowWithResult4 : IWorkflow
        {
            [WorkflowMethod]
            Task<string> HelloAsync(string name);
        }

        public class WorkflowWithResult4 : WorkflowBase, IWorkflowWithResult4
        {
            public async Task<string> HelloAsync(string name)
            {
                return await Task.FromResult($"WF4 says: Hello {name}!");
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonTemporal)]
        public async Task Simultaneous()
        {
            await SyncContext.ClearAsync;

            // We're going to establish two simultaneous client connections, 
            // register a workflow on each, and then verify that these workflows work.

            using (var client1 = await TemporalClient.ConnectAsync(fixture.Settings))
            {
                var worker1 = await client1.NewWorkerAsync();

                await worker1.RegisterWorkflowAsync<WorkflowWithResult1>();
                await worker1.StartAsync();

                var client2Settings = fixture.Settings.Clone();

                client2Settings.TaskQueue = "taskqueue-2";

                using (var client2 = await TemporalClient.ConnectAsync(fixture.Settings))
                {
                    var worker2 = await client1.NewWorkerAsync();

                    await worker2.RegisterWorkflowAsync<WorkflowWithResult1>();
                    await worker2.StartAsync();

                    var options1 = new StartWorkflowOptions()
                    {
                        TaskQueue = "taskqueue-1"
                    };

                    var options2 = new StartWorkflowOptions()
                    {
                        TaskQueue = "taskqueue-2"
                    };

                    var stub1 = client1.NewWorkflowStub<IWorkflowWithResult1>(options: options1);
                    var stub2 = client2.NewWorkflowStub<IWorkflowWithResult2>(options: options2);

                    Assert.Equal("WF1 says: Hello Jeff!", await stub1.HelloAsync("Jeff"));
                    Assert.Equal("WF2 says: Hello Jeff!", await stub2.HelloAsync("Jeff"));
                }
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonTemporal)]
        public async Task Connect_Twice()
        {
            await SyncContext.ClearAsync;

            // We're going to establish two successive client connections
            // and verify that these work.

            using (var client = await TemporalClient.ConnectAsync(fixture.Settings))
            {
                using (var worker = await client.NewWorkerAsync())
                {
                    await worker.RegisterWorkflowAsync<WorkflowWithResult3>();
                    await worker.StartAsync();
                
                    var stub = client.NewWorkflowStub<IWorkflowWithResult3>();

                    Assert.Equal("WF3 says: Hello Jack!", await stub.HelloAsync("Jack"));
                }
            }

            using (var client = await TemporalClient.ConnectAsync(fixture.Settings))
            {
                using (var worker = await client.NewWorkerAsync())
                {
                    await worker.RegisterWorkflowAsync<WorkflowWithResult4>();
                    await worker.StartAsync();

                    var stub = client.NewWorkflowStub<IWorkflowWithResult4>();

                    Assert.Equal("WF4 says: Hello Jack!", await stub.HelloAsync("Jack"));
                }
            }
        }

        //---------------------------------------------------------------------

        private static TemporalClient    workerClient1;
        private static TemporalClient    workerClient2;
        private static TemporalClient    workerClient3;

        //---------------------------------------

        [ActivityInterface(TaskQueue = "taskqueue-1")]
        public interface IActivityWorker1 : IActivity
        {
            [ActivityMethod]
            Task<bool> RunAsync();
        }

        public class ActivityWorker1 : ActivityBase, IActivityWorker1
        {
            public async Task<bool> RunAsync()
            {
                // Verify that this is executing on the correct client/worker instance.

                return await Task.FromResult(Activity.Client.ClientId == workerClient1.ClientId);
            }
        }

        [WorkflowInterface(TaskQueue = "taskqueue-1")]
        public interface IWorkflowWorker1 : IWorkflow
        {
            [WorkflowMethod]
            Task<bool> RunAsync(bool testActivity);
        }

        public class WorkflowWorker1 : WorkflowBase, IWorkflowWorker1
        {
            public async Task<bool> RunAsync(bool testActivity)
            {
                // Verify that this is executing on the correct client/worker instance.

                if (Workflow.Client.ClientId != workerClient1.ClientId)
                {
                    return false;
                }

                if (testActivity)
                {
                    var stub = Workflow.NewActivityStub<IActivityWorker1>();

                    if (!await stub.RunAsync())
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        //---------------------------------------

        [ActivityInterface(TaskQueue = "taskqueue-2")]
        public interface IActivityWorker2 : IActivity
        {
            [ActivityMethod]
            Task<bool> RunAsync();
        }

        public class ActivityWorker2 : ActivityBase, IActivityWorker2
        {
            public async Task<bool> RunAsync()
            {
                // Verify that this is executing on the correct client/worker instance.

                return await Task.FromResult(Activity.Client.ClientId == workerClient2.ClientId);
            }
        }

        [WorkflowInterface(TaskQueue = "taskqueue-2")]
        public interface IWorkflowWorker2 : IWorkflow
        {
            [WorkflowMethod]
            Task<bool> RunAsync(bool testActivity);
        }

        public class WorkflowWorker2 : WorkflowBase, IWorkflowWorker2
        {
            public async Task<bool> RunAsync(bool testActivity)
            {
                // Verify that this is executing on the correct client/worker instance.

                if (Workflow.Client.ClientId != workerClient2.ClientId)
                {
                    return false;
                }

                if (testActivity)
                {
                    var stub = Workflow.NewActivityStub<IActivityWorker2>();

                    if (!await stub.RunAsync())
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        //---------------------------------------

        [ActivityInterface(TaskQueue = "taskqueue-3")]
        public interface IActivityWorker3 : IActivity
        {
            [ActivityMethod]
            Task<bool> RunAsync();
        }

        public class ActivityWorker3 : ActivityBase, IActivityWorker3
        {
            public async Task<bool> RunAsync()
            {
                // Verify that this is executing on the correct client/worker instance.

                return await Task.FromResult(Activity.Client.ClientId == workerClient3.ClientId);
            }
        }

        [WorkflowInterface(TaskQueue = "taskqueue-3")]
        public interface IWorkflowWorker3 : IWorkflow
        {
            [WorkflowMethod]
            Task<bool> RunAsync(bool testActivity);
        }

        public class WorkflowWorker3 : WorkflowBase, IWorkflowWorker3
        {
            public async Task<bool> RunAsync(bool testActivity)
            {
                // Verify that this is executing on the correct client/worker instance.

                if (Workflow.Client.ClientId != workerClient3.ClientId)
                {
                    return false;
                }

                if (testActivity)
                {
                    var stub = Workflow.NewActivityStub<IActivityWorker3>();

                    if (!await stub.RunAsync())
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonTemporal)]
        public async Task Multiple_TaskQueues()
        {
            await SyncContext.ClearAsync;

            // Test the scenario where there multiple clients without
            // workers that will be used to simulate apps that make calls
            // on workflows and then create multiple clients that register
            // different workflows and activities and then verify that
            // each of the workerless clients are able to execute workflows
            // and activities and that these end up being executed on the
            // correct clients.

            var clients = new List<TemporalClient>();

            try
            {
                // Initialize the non-worker clients.

                TemporalClient      client1;
                TemporalClient      client2;
                TemporalClient      client3;

                TemporalSettings    settings1 = fixture.Settings.Clone();
                TemporalSettings    settings2 = fixture.Settings.Clone();
                TemporalSettings    settings3 = fixture.Settings.Clone();

                settings1.TaskQueue = "taskqueue-1";
                settings2.TaskQueue = "taskqueue-2";
                settings3.TaskQueue = "taskqueue-3";

                clients.Add(client1 = await TemporalClient.ConnectAsync(fixture.Settings));
                clients.Add(client2 = await TemporalClient.ConnectAsync(fixture.Settings));
                clients.Add(client3 = await TemporalClient.ConnectAsync(fixture.Settings));

                // Initialize the worker clients.

                clients.Add(workerClient1 = await TemporalClient.ConnectAsync(fixture.Settings));
                clients.Add(workerClient2 = await TemporalClient.ConnectAsync(fixture.Settings));
                clients.Add(workerClient3 = await TemporalClient.ConnectAsync(fixture.Settings));

                // Initialize and start the workers.

                var worker1 = await workerClient1.NewWorkerAsync();

                await worker1.RegisterActivityAsync<ActivityWorker1>();
                await worker1.RegisterWorkflowAsync<WorkflowWorker1>();
                await worker1.StartAsync();

                var worker2 = await workerClient1.NewWorkerAsync();

                await worker2.RegisterActivityAsync<ActivityWorker2>();
                await worker2.RegisterWorkflowAsync<WorkflowWorker2>();
                await worker2.StartAsync();

                var worker3 = await workerClient1.NewWorkerAsync();

                await worker3.RegisterActivityAsync<ActivityWorker3>();
                await worker3.RegisterWorkflowAsync<WorkflowWorker3>();
                await worker3.StartAsync();

                // Execute each of the worker workflows WITHOUT the associated activities 
                // from each client (both the worker and non-worker clients).

                foreach (var client in clients)
                {
                    var stub = client.NewWorkflowStub<IWorkflowWorker1>();

                    Assert.True(await stub.RunAsync(testActivity: false));
                }

                foreach (var client in clients)
                {
                    var stub = client.NewWorkflowStub<IWorkflowWorker2>();

                    Assert.True(await stub.RunAsync(testActivity: false));
                }

                foreach (var client in clients)
                {
                    var stub = client.NewWorkflowStub<IWorkflowWorker3>();

                    Assert.True(await stub.RunAsync(testActivity: false));
                }

                // Re-run the workflows calling the activities this time.

                foreach (var client in clients)
                {
                    var stub = client.NewWorkflowStub<IWorkflowWorker1>();

                    Assert.True(await stub.RunAsync(testActivity: true));
                }

                foreach (var client in clients)
                {
                    var stub = client.NewWorkflowStub<IWorkflowWorker2>();

                    Assert.True(await stub.RunAsync(testActivity: true));
                }

                foreach (var client in clients)
                {
                    var stub = client.NewWorkflowStub<IWorkflowWorker3>();

                    Assert.True(await stub.RunAsync(testActivity: true));
                }
            }
            finally
            {
                foreach (var client in clients)
                {
                    client.Dispose();
                }

                workerClient1 = null;
                workerClient2 = null;
                workerClient3 = null;
            }
        }
    }
}
