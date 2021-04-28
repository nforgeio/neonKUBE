//-----------------------------------------------------------------------------
// FILE:        Test_SignalChecks.cs
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
using System.Diagnostics.Contracts;
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
using Neon.Xunit;
using Neon.Xunit.Cadence;

using Newtonsoft.Json;
using Xunit;

namespace TestCadence
{
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_SignalChecks : IClassFixture<CadenceFixture>, IDisposable
    {
        private const int maxWaitSeconds = 5;

        private static readonly TimeSpan allowedVariation = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan workflowTimeout  = TimeSpan.FromSeconds(20);

        private CadenceFixture  fixture;
        private CadenceClient   client;
        private HttpClient      proxyClient;

        public Test_SignalChecks(CadenceFixture fixture)
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

            if (fixture.Start(settings, reconnect: true, keepRunning: CadenceTestHelper.KeepCadenceServerOpen) == TestFixtureStatus.Started)
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

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
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
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
        public async Task SyncSignal_StringResultError()
        {
            // Verify that async signals must return a Task.  [ISyncSignalNotTask]
            // defines its signal as returning a string.  Registration should fail.

            await Assert.ThrowsAsync<WorkflowTypeException>(async () => await client.RegisterWorkflowAsync<SyncSignalString>());

            // Verify that we're not allowd to create a workflow stub either.

            Assert.Throws<WorkflowTypeException>(() => client.NewWorkflowStub<SyncSignalString>());
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
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
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
        public async Task SyncSignal_VoidResultError()
        {
            // Verify that async signals must return a Task.  [ISyncSignalNotTask]
            // defines its signal as returning void.  Registration should fail.

            await Assert.ThrowsAsync<WorkflowTypeException>(async () => await client.RegisterWorkflowAsync<SyncSignalVoid>());

            // Verify that we're not allowd to create a workflow stub either.

            Assert.Throws<WorkflowTypeException>(() => client.NewWorkflowStub<SyncSignalVoid>());
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
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
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
        public async Task AsyncSignal_StringResultError()
        {
            // Verify that synchronous signals must return a Task.  [IAsyncSignalNotTask]
            // defines its signal as returning a string.  Registration should fail.

            await Assert.ThrowsAsync<WorkflowTypeException>(async () => await client.RegisterWorkflowAsync<AsyncSignalString>());

            // Verify that we're not allowd to create a workflow stub either.

            Assert.Throws<WorkflowTypeException>(() => client.NewWorkflowStub<AsyncSignalString>());
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
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
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
        public async Task AsyncSignal_VoidResultError()
        {
            // Verify that synchronous signals must return a Task.  [IAsyncSignalNotTask]
            // defines its signal as returning a string.  Registration should fail.

            await Assert.ThrowsAsync<WorkflowTypeException>(async () => await client.RegisterWorkflowAsync<AsyncSignalVoid>());

            // Verify that we're not allowd to create a workflow stub either.

            Assert.Throws<WorkflowTypeException>(() => client.NewWorkflowStub<AsyncSignalVoid>());
        }
    }
}
