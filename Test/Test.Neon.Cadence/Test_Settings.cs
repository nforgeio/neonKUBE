//-----------------------------------------------------------------------------
// FILE:        Test_Settings.cs
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
    [Trait(TestTrait.Category, TestTrait.Buggy)]
    [Trait(TestTrait.Category, TestArea.NeonCadence)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_Settings : IClassFixture<CadenceFixture>, IDisposable
    {
        private CadenceFixture  fixture;

        public Test_Settings(CadenceFixture fixture)
        {
            TestHelper.ResetDocker(this.GetType());

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

            fixture.Start(settings, reconnect: true, keepRunning: CadenceTestHelper.KeepCadenceServerOpen, noClient: true);
        }

        public void Dispose()
        {
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowIdReuse : IWorkflow
        {
            [WorkflowMethod(Name = "hello")]
            Task<string> HelloAsync(string name);
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowIdReuse : WorkflowBase, IWorkflowIdReuse
        {
            public async Task<string> HelloAsync(string name)
            {
                return await Task.FromResult($"Hello {name}!");
            }
        }

        [Fact(Timeout = CadenceTestHelper.TestTimeout)]
        [Trait(TestTrait.Category, TestArea.NeonCadence)]
        public async Task Workflow_ExternalIdNoReuse()
        {
            await SyncContext.ClearAsync;

            // Verify that default Cadence settings allow duplicate workflow IDs
            // and then change this to prevent reuse.

            var settings = fixture.Settings.Clone();

            Assert.Equal(WorkflowIdReusePolicy.AllowDuplicate, settings.WorkflowIdReusePolicy);

            settings.WorkflowIdReusePolicy = WorkflowIdReusePolicy.RejectDuplicate;

            using (var client = await CadenceClient.ConnectAsync(settings))
            {
                await client.RegisterAssemblyAsync(Assembly.GetExecutingAssembly());
                await client.StartWorkerAsync(CadenceTestHelper.TaskList);

                var options = new WorkflowOptions()
                {
                    WorkflowId = $"Workflow_ExternalIdNoReuse-{Guid.NewGuid().ToString("d")}"
                };

                // Do the first run; this should succeed.

                var stub = client.NewWorkflowStub<IWorkflowIdReuse>(options);

                Assert.Equal("Hello Jack!", await stub.HelloAsync("Jack"));

                // Do the second run with the same ID.  This shouldn't actually start
                // another workflow and will return the result from the original
                // workflow instead.

                stub = client.NewWorkflowStub<IWorkflowIdReuse>(options);

                Assert.Equal("Hello Jack!", await stub.HelloAsync("Jill"));
            }
        }

        [Fact(Timeout = CadenceTestHelper.TestTimeout)]
        [Trait(TestTrait.Category, TestArea.NeonCadence)]
        public async Task Workflow_ExternalIdReuseViaSettings()
        {
            await SyncContext.ClearAsync;

            // Verify that default Cadence settings allow duplicate workflow IDs.

            var settings = fixture.Settings.Clone();

            Assert.Equal(WorkflowIdReusePolicy.AllowDuplicate, settings.WorkflowIdReusePolicy);

            using (var client = await CadenceClient.ConnectAsync(settings))
            {
                await client.RegisterAssemblyAsync(Assembly.GetExecutingAssembly());
                await client.StartWorkerAsync(CadenceTestHelper.TaskList);

                var options = new WorkflowOptions()
                {
                    WorkflowId = $"Workflow_ExternalIdReuseViaOptions-{Guid.NewGuid().ToString("d")}"
                };

                // Do the first run.

                var stub = client.NewWorkflowStub<IWorkflowIdReuse>(options);

                Assert.Equal("Hello Jack!", await stub.HelloAsync("Jack"));

                // Do the second run.

                stub = client.NewWorkflowStub<IWorkflowIdReuse>(options);

                Assert.Equal("Hello Jill!", await stub.HelloAsync("Jill"));
            }
        }
    }
}
