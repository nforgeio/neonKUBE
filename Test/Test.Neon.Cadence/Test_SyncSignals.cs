//-----------------------------------------------------------------------------
// FILE:        Test_SyncSignal.cs
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
    public class Test_SyncSignals : IClassFixture<CadenceFixture>, IDisposable
    {
        private CadenceFixture  fixture;
        private CadenceClient   client;
        private HttpClient      proxyClient;

        public Test_SyncSignals(CadenceFixture fixture)
        {
            var settings = new CadenceSettings()
            {
                DefaultDomain          = CadenceFixture.DefaultDomain,
                LogLevel               = CadenceTestHelper.LogLevel,
                CreateDomain           = true,
                Debug                  = CadenceTestHelper.Debug,
                DebugPrelaunched       = CadenceTestHelper.DebugPrelaunched,
                DebugDisableHeartbeats = CadenceTestHelper.DebugDisableHeartbeats
            };

            if (fixture.Start(settings, keepConnection: true, keepOpen: CadenceTestHelper.KeepCadenceServerOpen) == TestFixtureStatus.Started)
            {
                this.fixture     = fixture;
                this.client      = fixture.Client;
                this.proxyClient = new HttpClient() { BaseAddress = client.ProxyUri };

                // Auto register the test workflow and activity implementations.

                client.RegisterAssemblyAsync(Assembly.GetExecutingAssembly()).Wait();

                // Start the worker.

                client.StartWorkerAsync(CadenceTestHelper.TaskList).Wait();
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
        public interface ISyncSignal : IWorkflow
        {
            [WorkflowMethod()]
            Task RunAsync(TimeSpan delay);

            [SignalMethod("signal-void", Synchronous = true)]
            Task SignalAsync(TimeSpan delay);

            [SignalMethod("signal-with-result", Synchronous = true)]
            Task<string> SignalAsync(TimeSpan delay, string input);
        }

        [Workflow(AutoRegister = true)]
        public class SyncSignal : WorkflowBase, ISyncSignal
        {
            public static readonly TimeSpan WorkflowDelay = TimeSpan.FromSeconds(10);
            public static readonly TimeSpan SignalDelay   = TimeSpan.FromSeconds(3);

            public static bool SignalBeforeDelay = false;
            public static bool SignalAfterDelay  = false;

            public static void Clear()
            {
                SignalBeforeDelay = false;
                SignalAfterDelay  = false;
            }

            public async Task RunAsync(TimeSpan delay)
            {
                await Workflow.SleepAsync(delay);
            }

            public async Task SignalAsync(TimeSpan delay)
            {
                SignalBeforeDelay = true;
                await Task.Delay(delay);
                SignalAfterDelay = true;
            }

            public async Task<string> SignalAsync(TimeSpan delay, string input)
            {
                SignalBeforeDelay = true;
                await Task.Delay(delay);
                SignalAfterDelay = true;

                return input;
            }
        }

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface ISyncChildSignal : IWorkflow
        {
            [WorkflowMethod()]
            Task<bool> RunAsync(TimeSpan signalDelay, bool signalWithResult);
        }

        [Workflow(AutoRegister = true)]
        public class SyncChildSignal : WorkflowBase, ISyncChildSignal
        {
            public async Task<bool> RunAsync(TimeSpan signalDelay, bool signalWithResult)
            {
                SyncSignal.Clear();

                // Start a child workflow and then send a synchronous
                // signal that returns void or a result depending on
                // the parameter.
                //
                // The method returns TRUE on success.

                var childStub = Workflow.NewChildWorkflowFutureStub<ISyncSignal>();
                var future    = await childStub.StartAsync(SyncSignal.WorkflowDelay);
                var pass      = true;

                if (signalWithResult)
                {
                    var result = await childStub.Stub.SignalAsync(signalDelay, "Hello World!");

                    pass = result == "Hello World!";
                }
                else
                {
                    await childStub.Stub.SignalAsync(signalDelay);
                }

                pass = pass && SyncSignal.SignalBeforeDelay;
                pass = pass && SyncSignal.SignalAfterDelay;

                await future.GetAsync();

                return pass;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task SyncSignal_WithoutResult()
        {
            // Verify that sending a synchronous signal returning void
            // works as expected when there's no delay executing the signal.

            SyncSignal.Clear();

            var stub = client.NewWorkflowStub<ISyncSignal>();
            var task = stub.RunAsync(SyncSignal.WorkflowDelay);

            await stub.SignalAsync(TimeSpan.Zero);
            Assert.True(SyncSignal.SignalBeforeDelay);
            Assert.True(SyncSignal.SignalAfterDelay);

            await task;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task SyncSignal_WithoutResult_AndDelay()
        {
            // Verify that sending a synchronous signal returning void
            // works as expected when we delay the signal execution long
            // enough to force query retries.

            SyncSignal.Clear();

            var stub = client.NewWorkflowStub<ISyncSignal>();
            var task = stub.RunAsync(SyncSignal.WorkflowDelay);

            await stub.SignalAsync(SyncSignal.SignalDelay);
            Assert.True(SyncSignal.SignalBeforeDelay);
            Assert.True(SyncSignal.SignalAfterDelay);

            await task;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task SyncSignal_WithResult()
        {
            // Verify that sending a synchronous signal returning a result
            // works as expected when there's no delay executing the signal.

            SyncSignal.Clear();

            var stub   = client.NewWorkflowStub<ISyncSignal>();
            var task   = stub.RunAsync(SyncSignal.WorkflowDelay);
            var result = await stub.SignalAsync(TimeSpan.Zero, "Hello World!");

            Assert.True(SyncSignal.SignalBeforeDelay);
            Assert.True(SyncSignal.SignalAfterDelay);
            Assert.Equal("Hello World!", result);

            await task;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task SyncSignal_WithResult_AndDelay()
        {
            // Verify that sending a synchronous signal returning a result
            // works as expected when we delay the signal execution long
            // enough to force query retries.

            SyncSignal.Clear();

            var stub   = client.NewWorkflowStub<ISyncSignal>();
            var task   = stub.RunAsync(SyncSignal.WorkflowDelay);
            var result = await stub.SignalAsync(SyncSignal.SignalDelay, "Hello World!");

            Assert.True(SyncSignal.SignalBeforeDelay);
            Assert.True(SyncSignal.SignalAfterDelay);
            Assert.Equal("Hello World!", result);

            await task;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task SyncSignalChild_WithoutResult()
        {
            // Verify that sending a synchronous child signal returning void
            // works as expected when there's no delay executing the signal.

            var stub = client.NewWorkflowStub<ISyncChildSignal>();
            var task = stub.RunAsync(TimeSpan.Zero, signalWithResult: false);

            Assert.True(await task);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task SyncSignalChild_WithoutResult_AndDelay()
        {
            // Verify that sending a synchronous child signal returning void
            // works as expected when we delay the signal execution long
            // enough to force query retries.

            var stub = client.NewWorkflowStub<ISyncChildSignal>();
            var task = stub.RunAsync(SyncSignal.SignalDelay, signalWithResult: false);

            Assert.True(await task);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task SyncSignalChild_WithResult()
        {
            // Verify that sending a synchronous child signal returning a result
            // works as expected when there's no delay executing the signal.

            var stub = client.NewWorkflowStub<ISyncChildSignal>();
            var task = stub.RunAsync(TimeSpan.Zero, signalWithResult: true);

            Assert.True(await task);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task SyncSignalChild_WithResult_AndDelay()
        {
            // Verify that sending a synchronous child signal returning a result
            // works as expected when we delay the signal execution long
            // enough to force query retries.

            var stub = client.NewWorkflowStub<ISyncChildSignal>();
            var task = stub.RunAsync(SyncSignal.SignalDelay, signalWithResult: true);

            Assert.True(await task);
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IInvokeSignal : IWorkflow
        {
            [WorkflowMethod(Name = "run-void")]
            Task RunVoidAsync();

            [WorkflowMethod(Name = "run-result")]
            Task RunResultAsync();

            [SignalMethod("signal-void", Synchronous = true)]
            Task SignalAsync();

            [SignalMethod("signal-with-result", Synchronous = true)]
            Task<string> SignalAsync(string input);
        }

        [Workflow(AutoRegister = true)]
        public class InvokeSignal : WorkflowBase, IInvokeSignal
        {
            private WorkflowQueue<SignalInvocation>         voidQueue;
            private WorkflowQueue<SignalInvocation<string>> resultQueue;

            public async Task RunVoidAsync()
            {
                voidQueue = await Workflow.NewQueueAsync<SignalInvocation>();

                var signalInfo = await voidQueue.DequeueAsync();

                await signalInfo.ReturnAsync();
            }

            public async Task RunResultAsync()
            {
                resultQueue = await Workflow.NewQueueAsync<SignalInvocation<string>>();

                var signalInfo = await resultQueue.DequeueAsync();
                var input      = (string)signalInfo["input"];

                await signalInfo.ReturnAsync($"Hello {input}!");
            }

            public async Task SignalAsync()
            {
                var signalInfo = new SignalInvocation();

                await voidQueue.EnqueueAsync(signalInfo);
                await signalInfo.WaitForReturnAsync();
            }

            public async Task<string> SignalAsync(string input)
            {
                var signalInfo = new SignalInvocation<string>();

                signalInfo.Add("input", input);
                await resultQueue.EnqueueAsync(signalInfo);

                return await signalInfo.WaitForReturnAsync();
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task SyncSignal_Invoke_WithoutResult()
        {
            // Verify that [SignalInvocation] works.

            var stub = client.NewWorkflowStub<IInvokeSignal>();
            var task = stub.RunVoidAsync();

            await stub.SignalAsync();
            await task;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task SyncSignal_Invoke_WithResult()
        {
            // Verify that [SignalInvocation] works.

            var stub = client.NewWorkflowStub<IInvokeSignal>();
            var task = stub.RunVoidAsync();

            await stub.SignalAsync();
            await task;
        }
    }
}
