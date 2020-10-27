//-----------------------------------------------------------------------------
// FILE:        Test_MultiClient.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Data;
using Neon.IO;
using Neon.Tasks;
using Neon.Xunit;
using Neon.Xunit.Cadence;

using Newtonsoft.Json;
using Xunit;

namespace TestCadence
{
    public class Test_MultiClient : IClassFixture<CadenceFixture>, IDisposable
    {
        private CadenceFixture  fixture;

        public Test_MultiClient(CadenceFixture fixture)
        {
            var settings = new CadenceSettings()
            {
                DefaultDomain          = CadenceFixture.DefaultDomain,
                LogLevel               = CadenceTestHelper.LogLevel,
                CreateDomain           = true,
                Debug                  = CadenceTestHelper.Debug,
                DebugPrelaunched       = CadenceTestHelper.DebugPrelaunched,
                DebugDisableHeartbeats = CadenceTestHelper.DebugDisableHeartbeats,
                ClientIdentity         = CadenceTestHelper.ClientIdentity
            };

            this.fixture = fixture;

            fixture.Start(settings, image: CadenceTestHelper.CadenceImage, reconnect: true, keepRunning: CadenceTestHelper.KeepCadenceServerOpen, noClient: true);
        }

        public void Dispose()
        {
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = "tasklist-1")]
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

        [WorkflowInterface(TaskList = "tasklist-2")]
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

        [WorkflowInterface(TaskList = "tasklist-3")]
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

        [WorkflowInterface(TaskList = "tasklist-4")]
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
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Simultaneous()
        {
            await SyncContext.ClearAsync;

            // We're going to establish two simultaneous client connections, 
            // register a workflow on each, and then verify that these workflows work.

            using (var client1 = await CadenceClient.ConnectAsync(fixture.Settings))
            {
                await client1.RegisterWorkflowAsync<WorkflowWithResult1>();
                await client1.StartWorkerAsync("tasklist-1");

                using (var client2 = await CadenceClient.ConnectAsync(fixture.Settings))
                {
                    await client2.RegisterWorkflowAsync<WorkflowWithResult2>();
                    await client2.StartWorkerAsync("tasklist-2");

                    var options1 = new WorkflowOptions()
                    {
                        TaskList = "tasklist-1"
                    };

                    var options2 = new WorkflowOptions()
                    {
                        TaskList = "tasklist-2"
                    };

                    var stub1 = client1.NewWorkflowStub<IWorkflowWithResult1>(options: options1);
                    var stub2 = client2.NewWorkflowStub<IWorkflowWithResult2>(options: options2);

                    Assert.Equal("WF1 says: Hello Jeff!", await stub1.HelloAsync("Jeff"));
                    Assert.Equal("WF2 says: Hello Jeff!", await stub2.HelloAsync("Jeff"));
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Connect_Twice()
        {
            await SyncContext.ClearAsync;

            // We're going to establish two successive client connections
            // and verify that these work.

            using (var client = await CadenceClient.ConnectAsync(fixture.Settings))
            {
                await client.RegisterWorkflowAsync<WorkflowWithResult3>();
                
                using (await client.StartWorkerAsync("tasklist-3"))
                {
                    var stub1 = client.NewWorkflowStub<IWorkflowWithResult3>();

                    Assert.Equal("WF3 says: Hello Jack!", await stub1.HelloAsync("Jack"));
                }
            }

            using (var client = await CadenceClient.ConnectAsync(fixture.Settings))
            {
                await client.RegisterWorkflowAsync<WorkflowWithResult4>();

                using (await client.StartWorkerAsync("tasklist-4"))
                {
                    var stub1 = client.NewWorkflowStub<IWorkflowWithResult4>();

                    Assert.Equal("WF4 says: Hello Jack!", await stub1.HelloAsync("Jack"));
                }
            }
        }

        //---------------------------------------------------------------------

        private static CadenceClient    workerClient1;
        private static CadenceClient    workerClient2;
        private static CadenceClient    workerClient3;

        //---------------------------------------

        [ActivityInterface(TaskList = "tasklist-1")]
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

        [WorkflowInterface(TaskList = "tasklist-1")]
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

        [ActivityInterface(TaskList = "tasklist-2")]
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

        [WorkflowInterface(TaskList = "tasklist-2")]
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

        [ActivityInterface(TaskList = "tasklist-3")]
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

        [WorkflowInterface(TaskList = "tasklist-3")]
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
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Multiple_TaskLists()
        {
            await SyncContext.ClearAsync;

            // Test the scenario where there multiple clients without
            // workers that will be used to simulate apps that make calls
            // on workflows and then create multiple clients that register
            // different workflows and activities and then verify that
            // each of the workerless clients are able to execute workflows
            // and activities and that these end up being executed on the
            // correct clients.

            var clients = new List<CadenceClient>();

            try
            {
                // Initialize the non-worker clients.

                CadenceClient   client1;
                CadenceClient   client2;
                CadenceClient   client3;

                clients.Add(client1 = await CadenceClient.ConnectAsync(fixture.Settings));
                clients.Add(client2 = await CadenceClient.ConnectAsync(fixture.Settings));
                clients.Add(client3 = await CadenceClient.ConnectAsync(fixture.Settings));

                // Initialize the worker clients.

                clients.Add(workerClient1 = await CadenceClient.ConnectAsync(fixture.Settings));
                clients.Add(workerClient2 = await CadenceClient.ConnectAsync(fixture.Settings));
                clients.Add(workerClient3 = await CadenceClient.ConnectAsync(fixture.Settings));

                // Start the workers.

                await workerClient1.RegisterActivityAsync<ActivityWorker1>();
                await workerClient1.RegisterWorkflowAsync<WorkflowWorker1>();
                await workerClient1.StartWorkerAsync("tasklist-1");

                await workerClient2.RegisterActivityAsync<ActivityWorker2>();
                await workerClient2.RegisterWorkflowAsync<WorkflowWorker2>();
                await workerClient2.StartWorkerAsync("tasklist-2");

                await workerClient3.RegisterActivityAsync<ActivityWorker3>();
                await workerClient3.RegisterWorkflowAsync<WorkflowWorker3>();
                await workerClient3.StartWorkerAsync("tasklist-3");

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
