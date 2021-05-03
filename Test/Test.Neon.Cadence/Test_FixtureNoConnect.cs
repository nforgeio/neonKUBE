//-----------------------------------------------------------------------------
// FILE:        Test_FixtureNoConnect.cs
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
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
    /// <summary>
    /// These tests prevent the <see cref="CadenceFixture"/> from establishing a client
    /// connection and then explicitly establishes a connection to run some tests.
    /// </summary>
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public partial class Test_FixtureNoConnect : IClassFixture<CadenceFixture>, IDisposable
    {
        private CadenceFixture  fixture;
        private CadenceClient   client;

        public Test_FixtureNoConnect(CadenceFixture fixture)
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

            if (fixture.Start(settings, reconnect: true, keepRunning: CadenceTestHelper.KeepCadenceServerOpen, noClient: true) == TestFixtureStatus.Started)
            {
                this.fixture = fixture;
                this.client  = fixture.Client = CadenceClient.ConnectAsync(fixture.Settings).Result;

                // Auto register the test workflow and activity implementations.

                client.RegisterAssemblyAsync(Assembly.GetExecutingAssembly()).Wait();

                // Start the worker.

                client.StartWorkerAsync(CadenceTestHelper.TaskList).Wait();
            }
            else
            {
                this.fixture = fixture;
                this.client  = fixture.Client;
            }
        }

        public void Dispose()
        {
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowWithResult : IWorkflow
        {
            [WorkflowMethod]
            Task<string> HelloAsync(string name);
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowWithResult : WorkflowBase, IWorkflowWithResult
        {
            public async Task<string> HelloAsync(string name)
            {
                return await Task.FromResult($"Hello {name}!");
            }
        }

        [Fact(Timeout = CadenceTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonCadence)]
        public async Task Workflow_WithResult1()
        {
            await SyncContext.ClearAsync;
            
            // Verify that we can call a simple workflow that accepts a
            // parameter and returns a result.

            var stub = client.NewWorkflowStub<IWorkflowWithResult>();

            Assert.Equal("Hello Jeff!", await stub.HelloAsync("Jeff"));
        }

        [Fact(Timeout = CadenceTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonCadence)]
        public async Task Workflow_WithResult2()
        {
            await SyncContext.ClearAsync;

            // Verify that we can call a simple workflow that accepts a
            // parameter and returns a result.

            var stub = client.NewWorkflowStub<IWorkflowWithResult>();

            Assert.Equal("Hello Jack!", await stub.HelloAsync("Jack"));
        }
    }
}
