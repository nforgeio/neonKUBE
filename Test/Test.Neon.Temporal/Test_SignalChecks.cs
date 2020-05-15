//-----------------------------------------------------------------------------
// FILE:        Test_SignalChecks.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Data;
using Neon.IO;
using Neon.Temporal;
using Neon.Temporal.Internal;
using Neon.Xunit;
using Neon.Xunit.Temporal;

using Newtonsoft.Json;
using Xunit;

namespace TestTemporal
{
    public class Test_SignalChecks : IClassFixture<TemporalFixture>, IDisposable
    {
        private const int maxWaitSeconds = 5;

        private static readonly TimeSpan allowedVariation = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan workflowTimeout  = TimeSpan.FromSeconds(20);

        private TemporalFixture     fixture;
        private TemporalClient      client;
        private HttpClient          proxyClient;

        public Test_SignalChecks(TemporalFixture fixture)
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

            if (fixture.Start(settings, stackDefinition: TemporalTestHelper.TemporalStackDefinition, reconnect: true, keepRunning: TemporalTestHelper.KeepTemporalServerOpen) == TestFixtureStatus.Started)
            {
                this.fixture     = fixture;
                this.client      = fixture.Client;
                this.proxyClient = new HttpClient() { BaseAddress = client.ProxyUri };
            }
            else
            {
                this.fixture     = fixture;
                this.client      = fixture.Client;
                this.proxyClient = new HttpClient() { BaseAddress = client.ProxyUri };
            }
        }

        public void Dispose()
        {
            if (proxyClient != null)
            {
                proxyClient.Dispose();
                proxyClient = null;
            }
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = TemporalTestHelper.TaskList)]
        public interface ISyncSignalString : IWorkflow
        {
            [WorkflowMethod]
            Task RunAsync();

            [SignalMethod("signal")]
            string SignalAsync();
        }

        [Workflow(AutoRegister = false)]
        public class SyncSignalString : WorkflowBase, ISyncSignalString
        {
            public async Task RunAsync()
            {
                await Task.CompletedTask;
            }

            public string SignalAsync()
            {
                return null;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public async Task SyncSignal_StringResultError()
        {
            // Verify that async signals must return a Task.  [ISyncSignalNotTask]
            // defines its signal as returning a string.  Registration should fail.

            var worker = await client.NewWorkerAsync();

            await Assert.ThrowsAsync<WorkflowTypeException>(async () => await worker.RegisterWorkflowAsync<SyncSignalString>());

            // Verify that we're not allowd to create a workflow stub either.

            Assert.Throws<WorkflowTypeException>(() => client.NewWorkflowStub<SyncSignalString>());
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = TemporalTestHelper.TaskList)]
        public interface ISyncSignalVoid : IWorkflow
        {
            [WorkflowMethod]
            Task RunAsync();

            [SignalMethod("signal")]
            void SignalAsync();
        }

        [Workflow(AutoRegister = false)]
        public class SyncSignalVoid : WorkflowBase, ISyncSignalVoid
        {
            public async Task RunAsync()
            {
                await Task.CompletedTask;
            }

            public void SignalAsync()
            {
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public async Task SyncSignal_VoidResultError()
        {
            // Verify that async signals must return a Task.  [ISyncSignalNotTask]
            // defines its signal as returning void.  Registration should fail.

            var worker = await client.NewWorkerAsync();

            await Assert.ThrowsAsync<WorkflowTypeException>(async () => await worker.RegisterWorkflowAsync<SyncSignalVoid>());

            // Verify that we're not allowd to create a workflow stub either.

            Assert.Throws<WorkflowTypeException>(() => client.NewWorkflowStub<SyncSignalVoid>());
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = TemporalTestHelper.TaskList)]
        public interface IAsyncSignalString : IWorkflow
        {
            [WorkflowMethod]
            Task RunAsync();

            [SignalMethod("signal", Synchronous = true)]
            string SignalAsync();
        }

        [Workflow(AutoRegister = false)]
        public class AsyncSignalString : WorkflowBase, IAsyncSignalString
        {
            public async Task RunAsync()
            {
                await Task.CompletedTask;
            }

            public string SignalAsync()
            {
                return null;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public async Task AsyncSignal_StringResultError()
        {
            // Verify that synchronous signals must return a Task.  [IAsyncSignalNotTask]
            // defines its signal as returning a string.  Registration should fail.

            var worker = await client.NewWorkerAsync();

            await Assert.ThrowsAsync<WorkflowTypeException>(async () => await worker.RegisterWorkflowAsync<AsyncSignalString>());

            // Verify that we're not allowd to create a workflow stub either.

            Assert.Throws<WorkflowTypeException>(() => client.NewWorkflowStub<AsyncSignalString>());
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = TemporalTestHelper.TaskList)]
        public interface IAsyncSignalVoid : IWorkflow
        {
            [WorkflowMethod]
            Task RunAsync();

            [SignalMethod("signal", Synchronous = true)]
            void SignalAsync();
        }

        [Workflow(AutoRegister = false)]
        public class AsyncSignalVoid : WorkflowBase, IAsyncSignalVoid
        {
            public async Task RunAsync()
            {
                await Task.CompletedTask;
            }

            public void SignalAsync()
            {
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public async Task AsyncSignal_VoidResultError()
        {
            // Verify that synchronous signals must return a Task.  [IAsyncSignalNotTask]
            // defines its signal as returning a string.  Registration should fail.

            var worker = await client.NewWorkerAsync();

            await Assert.ThrowsAsync<WorkflowTypeException>(async () => await worker.RegisterWorkflowAsync<AsyncSignalVoid>());

            // Verify that we're not allowd to create a workflow stub either.

            Assert.Throws<WorkflowTypeException>(() => client.NewWorkflowStub<AsyncSignalVoid>());
        }
    }
}
