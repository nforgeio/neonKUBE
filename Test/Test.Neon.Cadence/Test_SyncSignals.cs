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

#pragma warning disable xUnit1026 // Theory methods should use all of their parameters

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
        private const int testIterations = 2;

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

        private void LogStart(int iteration)
        {
            //CadenceHelper.DebugLog("");
            //CadenceHelper.DebugLog("---------------------------------");
            //CadenceHelper.DebugLog("");
            //CadenceHelper.DebugLog($"ITERATION: {iteration}");
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
            public static readonly TimeSpan WorkflowDelay = TimeSpan.FromSeconds(8);
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

        [Theory]
        [Repeat(testIterations)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task SyncSignal_WithoutResult(int iteration)
        {
            LogStart(iteration);

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

        [Theory]
        [Repeat(testIterations)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task SyncSignal_WithoutResult_AndDelay(int iteration)
        {
            LogStart(iteration);

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

        [Theory]
        [Repeat(testIterations)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task SyncSignal_WithResult(int iteration)
        {
            LogStart(iteration);

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

        [Theory]
        [Repeat(testIterations)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task SyncSignal_WithResult_AndDelay(int iteration)
        {
            LogStart(iteration);

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

        [Theory]
        [Repeat(testIterations)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task SyncSignalChild_WithoutResult(int iteration)
        {
            LogStart(iteration);

            // Verify that sending a synchronous child signal returning void
            // works as expected when there's no delay executing the signal.

            var stub = client.NewWorkflowStub<ISyncChildSignal>();
            var task = stub.RunAsync(TimeSpan.Zero, signalWithResult: false);

            Assert.True(await task);
        }

        [Theory]
        [Repeat(testIterations)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task SyncSignalChild_WithoutResult_AndDelay(int iteration)
        {
            LogStart(iteration);

            // Verify that sending a synchronous child signal returning void
            // works as expected when we delay the signal execution long
            // enough to force query retries.

            var stub = client.NewWorkflowStub<ISyncChildSignal>();
            var task = stub.RunAsync(SyncSignal.SignalDelay, signalWithResult: false);

            Assert.True(await task);
        }

        [Theory]
        [Repeat(testIterations)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task SyncSignalChild_WithResult(int iteration)
        {
            LogStart(iteration);

            // Verify that sending a synchronous child signal returning a result
            // works as expected when there's no delay executing the signal.

            var stub = client.NewWorkflowStub<ISyncChildSignal>();
            var task = stub.RunAsync(TimeSpan.Zero, signalWithResult: true);

            Assert.True(await task);
        }

        [Theory]
        [Repeat(testIterations)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task SyncSignalChild_WithResult_AndDelay(int iteration)
        {
            LogStart(iteration);

            // Verify that sending a synchronous child signal returning a result
            // works as expected when we delay the signal execution long
            // enough to force query retries.

            var stub = client.NewWorkflowStub<ISyncChildSignal>();
            var task = stub.RunAsync(SyncSignal.SignalDelay, signalWithResult: true);

            Assert.True(await task);
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IQueuedSignal : IWorkflow
        {
            [WorkflowMethod(Name = "run-void")]
            Task RunVoidAsync();

            [SignalMethod("signal-void", Synchronous = true)]
            Task SignalVoidAsync(string name);

            [WorkflowMethod(Name = "run-with-result")]
            Task RunResultAsync();

            [SignalMethod("signal-with-result", Synchronous = true)]
            Task<string> SignalResultAsync(string name);
        }

        [Workflow(AutoRegister = true)]
        public class QueuedSignal : WorkflowBase, IQueuedSignal
        {
            public static bool SignalProcessed;
            public static string Name;

            public static void Clear()
            {
                SignalProcessed = false;
                Name = null;
            }

            private WorkflowQueue<SignalRequest>         voidQueue;
            private WorkflowQueue<SignalRequest<string>> resultQueue;

            public async Task RunVoidAsync()
            {
                voidQueue = await Workflow.NewQueueAsync<SignalRequest>();

                var signalRequest = await voidQueue.DequeueAsync();

                SignalProcessed = true;
                Name = signalRequest.Arg<string>("name");

                await signalRequest.ReplyAsync();
            }

            public async Task SignalVoidAsync(string name)
            {
                var signalRequest = new SignalRequest();

                await voidQueue.EnqueueAsync(signalRequest);
                throw new WaitForSignalReplyException();
            }

            public async Task RunResultAsync()
            {
                resultQueue = await Workflow.NewQueueAsync<SignalRequest<string>>();

                var signalRequest = await resultQueue.DequeueAsync();

                SignalProcessed = true;
                Name = signalRequest.Arg<string>("name");

                await signalRequest.ReplyAsync($"Hello {Name}!");
            }

            public async Task<string> SignalResultAsync(string name)
            {
                var signalRequest = new SignalRequest<string>();

                await resultQueue.EnqueueAsync(signalRequest);
                throw new WaitForSignalReplyException();
            }
        }

        [Theory]
        [Repeat(testIterations)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task SyncSignal_Queued_WithoutResult(int iteration)
        {
            LogStart(iteration);

            // Verify that [SignalRequest] works for void signals.
            //
            // This is a bit tricky.  The workflow waits for a signal,
            // processes it and then returns.  We'll know this happened
            // because the static [SignalProcessed] property will be set
            // and also because the signal and workflow methods returned.

            QueuedSignal.Clear();

            var stub = client.NewWorkflowStub<IQueuedSignal>();
            var task = stub.RunVoidAsync();

            await stub.SignalVoidAsync("Jack");
            await task;

            Assert.True(QueuedSignal.SignalProcessed);
            Assert.Equal("Jack", QueuedSignal.Name);
        }

        [Theory]
        [Repeat(testIterations)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task SyncSignal_Queued_WithResult(int iteration)
        {
            LogStart(iteration);

            // Verify that [SignalRequest] works for signals that return 
            // a result.
            //
            // This is a bit tricky.  The workflow waits for a signal,
            // processes it and then returns.  We'll know this happened
            // because the static [SignalProcessed] property will be set
            // and also because the signal and workflow methods returned.

            QueuedSignal.Clear();

            var stub = client.NewWorkflowStub<IQueuedSignal>();
            var task = stub.RunResultAsync();

            var result = await stub.SignalResultAsync("Jill");

            await task;

            Assert.True(QueuedSignal.SignalProcessed);
            Assert.True(result == "Hello Jill!");
            Assert.Equal("Jill", QueuedSignal.Name);
        }

        //---------------------------------------------------------------------

        [ActivityInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IDelayActivity : IActivity
        {
            [ActivityMethod]
            Task DelayAsync(TimeSpan delay);
        }

        [Activity(AutoRegister = true)]
        public class DelayActivity : ActivityBase, IDelayActivity
        {
            public async Task DelayAsync(TimeSpan delay)
            {
                await Task.Delay(delay);
            }
        }

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface ISignalWithActivity : IWorkflow
        {
            [WorkflowMethod(Name = "run")]
            Task RunAsync(TimeSpan delay);

            [SignalMethod("signal", Synchronous = true)]
            Task<string> SignalAsync(string name);
        }

        [Workflow(AutoRegister = true)]
        public class SignalWithActivity : WorkflowBase, ISignalWithActivity
        {
            private WorkflowQueue<SignalRequest<string>> signalQueue;

            public async Task RunAsync(TimeSpan delay)
            {
                signalQueue = await Workflow.NewQueueAsync<SignalRequest<string>>();

                var stub = Workflow.NewActivityStub<IDelayActivity>();

                await stub.DelayAsync(delay);

                var signalRequest = await signalQueue.DequeueAsync();
                var name          = signalRequest.Arg<string>("name");
                var reply         = (string)null;

                if (name != null)
                {
                    reply = $"Hello {name}!";
                }

                await signalRequest.ReplyAsync(reply);
            }

            public async Task<string> SignalAsync(string name)
            {
                await signalQueue.EnqueueAsync(new SignalRequest<string>());
                throw new WaitForSignalReplyException();
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task SyncSignal_WithActivity()
        {
            // Verify that synchronous signals work when the workflow also
            // executes an activity.

            var stub = client.NewWorkflowStub<ISignalWithActivity>();
            var task = stub.RunAsync(TimeSpan.FromSeconds(2));

            var result = await stub.SignalAsync("Jill");

            await task;

            Assert.Equal("Hello Jill!", result);
        }
    }
}
