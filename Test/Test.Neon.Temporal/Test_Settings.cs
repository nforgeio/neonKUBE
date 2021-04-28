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
    public class Test_Settings : IClassFixture<TemporalFixture>, IDisposable
    {
        private TemporalFixture  fixture;

        public Test_Settings(TemporalFixture fixture)
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

        public void Dispose()
        {
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskQueue = TemporalTestHelper.TaskQueue)]
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

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonTemporal)]
        public async Task Workflow_ExternalIdNoReuse()
        {
            await SyncContext.ClearAsync;

            // Verify that default Cadence settings allow duplicate workflow IDs
            // and then change this to prevent reuse.

            var settings = fixture.Settings.Clone();

            Assert.Equal(WorkflowIdReusePolicy.AllowDuplicate, settings.WorkflowIdReusePolicy);

            settings.WorkflowIdReusePolicy = WorkflowIdReusePolicy.RejectDuplicate;

            using (var client = await TemporalClient.ConnectAsync(settings))
            {
                // Create a worker and register the workflow and activity 
                // implementations to let Temporal know we're open for business.

                var worker = client.NewWorkerAsync().Result;

                worker.RegisterAssemblyAsync(Assembly.GetExecutingAssembly()).Wait();
                worker.StartAsync().Wait();

                // Do the first run; this should succeed.

                var options = new StartWorkflowOptions()
                {
                    Id = $"Workflow_ExternalIdNoReuse-{Guid.NewGuid().ToString("d")}"
                };

                var stub = client.NewWorkflowStub<IWorkflowIdReuse>(options);

                Assert.Equal("Hello Jack!", await stub.HelloAsync("Jack"));

                // Do the second run with the same ID.  This shouldn't actually start
                // another workflow and will return the result from the original
                // workflow instead.

                stub = client.NewWorkflowStub<IWorkflowIdReuse>(options);

                Assert.Equal("Hello Jack!", await stub.HelloAsync("Jill"));
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonTemporal)]
        public async Task Workflow_ExternalIdReuseViaSettings()
        {
            await SyncContext.ClearAsync;

            // Verify that default Cadence settings allow duplicate workflow IDs.

            var settings = fixture.Settings.Clone();

            Assert.Equal(WorkflowIdReusePolicy.AllowDuplicate, settings.WorkflowIdReusePolicy);

            using (var client = await TemporalClient.ConnectAsync(settings))
            {
                // Create a worker and register the workflow and activity 
                // implementations to let Temporal know we're open for business.

                var worker = client.NewWorkerAsync().Result;

                worker.RegisterAssemblyAsync(Assembly.GetExecutingAssembly()).Wait();
                worker.StartAsync().Wait();

                // Do the first run.

                var options = new StartWorkflowOptions()
                {
                    Id = $"Workflow_ExternalIdReuseViaOptions-{Guid.NewGuid().ToString("d")}"
                };

                var stub = client.NewWorkflowStub<IWorkflowIdReuse>(options);

                Assert.Equal("Hello Jack!", await stub.HelloAsync("Jack"));

                // Do the second run.

                stub = client.NewWorkflowStub<IWorkflowIdReuse>(options);

                Assert.Equal("Hello Jill!", await stub.HelloAsync("Jill"));
            }
        }
    }
}
