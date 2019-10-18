//-----------------------------------------------------------------------------
// FILE:        Test_Replay.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
    // Implementation Notes:
    // ---------------------
    // This class implements replay tests on the essential workflow operations.
    // Maxim over at Uber told me how to cause a workflow to be replayed:
    //
    //      https://github.com/nforgeio/neonKUBE/issues/620
    //
    // This gist is that we need to panic within the workflow.  Note that workflows
    // with no recorded history that fail will be considered to be an initial run
    // even if it isn't.  This seems a bit odd, but Maxim says this is by design
    // (and probably very unlikely to happen in the wild).  So, we're going to
    // ensure that all tests (besides the NOP one) have a recorded decision task.
    //
    // So we're going to implement a workflow that accepts a parameter that specifies
    // the operation to be tested and uses a static field to indicate whether the
    // workflow is being run for the first time or is replaying.  The test will
    // perform the specified operation on the first pass, trigger a replay, and
    // then ensure that the operation returned the same results on the second pass.

    public class Test_Replay : IClassFixture<CadenceFixture>, IDisposable
    {
        private const int maxWaitSeconds = 5;

        private static readonly TimeSpan allowedVariation = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan workflowTimeout  = TimeSpan.FromSeconds(20);

        private CadenceFixture  fixture;
        private CadenceClient   client;

        public Test_Replay(CadenceFixture fixture)
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
                this.fixture = fixture;
                this.client  = fixture.Client;

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

        public enum ReplayTest
        {
            Nop,
            GetVersion,
            WorkflowExecution,
            MutableSideEffect,
            MutableSideEffectGeneric,
            SideEffect,
            SideEffectGeneric,
            NewGuid,
            NextRandomDouble,
            NextRandom,
            NextRandomMax,
            NextRandomMinMax,
            NextRandomBytes,
            GetLastCompletionResult,
            GetIsSetLastCompletionResult,
            ChildWorkflow,
            Activity,
            LocalActivity
        }

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowReplayHello : IWorkflow
        {
            [WorkflowMethod]
            Task<string> HelloAsync(string name);
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowReplayHello : WorkflowBase, IWorkflowReplayHello
        {
            public async Task<string> HelloAsync(string name)
            {
                return await Task.FromResult($"Hello {name}!");
            }
        }

        [ActivityInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IReplayActivity : IActivity
        {
            [ActivityMethod]
            Task<string> RunAsync(string value);
        }

        [Activity(AutoRegister = true)]
        public class ReplayActivity : ActivityBase, IReplayActivity
        {
            [ActivityMethod]
            public async Task<string> RunAsync(string value)
            {
                return await Task.FromResult(value);
            }
        }

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowReplay : IWorkflow
        {
            [WorkflowMethod]
            Task<bool> RunAsync(ReplayTest test);
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowReplay : WorkflowBase, IWorkflowReplay
        {
            private static bool     firstPass = true;
            private static object   originalValue;

            public new static void Reset()
            {
                firstPass = true;
            }

            /// <summary>
            /// Some workflow operations like <see cref="Workflow.SideEffectAsync{T}(Func{T})"/> don't
            /// actually indicate the end of a decision task by themselves.  We'll use this method in
            /// these cases to run a local activity which will do this.
            /// </summary>
            /// <returns>The tracking <see cref="Task"/>.</returns>
            private async Task DecisionAsync()
            {
                var stub = Workflow.NewActivityStub<IReplayActivity>();

                await stub.RunAsync("test");
            }

            public async Task<bool> RunAsync(ReplayTest test)
            {
                var success = false;

                if (test != ReplayTest.Nop)
                {
                    // This ensures that the workflow has some history so that when
                    // Cadence restarts the workflow it will be treated as a replay
                    // instead of an initial execution.

                    await DecisionAsync();
                }

                switch (test)
                {
                    case ReplayTest.Nop:

                        if (firstPass)
                        {
                            firstPass = false;

                            await Workflow.ForceReplayAsync();
                        }
                        else
                        {
                            // NOTE: 
                            //
                            // The other Cadence clients (GOLANG, Java,...) always report
                            // IsReplaying=FALSE when a workflow with no history is restarted,
                            // which is what's happening in this case.  This is a bit weird
                            // but is BY DESIGN but will probably be very rare in real life.
                            //
                            //      https://github.com/uber-go/cadence-client/issues/821

                            success = !Workflow.IsReplaying;
                        }
                        break;

                    case ReplayTest.GetVersion:

                        if (firstPass)
                        {
                            firstPass     = false;
                            originalValue = await Workflow.GetVersionAsync("change", Workflow.DefaultVersion, 1);

                            await DecisionAsync();
                            await Workflow.ForceReplayAsync();
                        }
                        else
                        {
                            success = originalValue.Equals(await Workflow.GetVersionAsync("change", Workflow.DefaultVersion, 1));
                            success = success && Workflow.IsReplaying;

                            await DecisionAsync();
                        }
                        break;

                    case ReplayTest.WorkflowExecution:

                        var helloStub = Workflow.Client.NewWorkflowStub<IWorkflowReplayHello>();

                        if (firstPass)
                        {
                            firstPass     = false;
                            originalValue = await helloStub.HelloAsync("Jeff");

                            await DecisionAsync();
                            await Workflow.ForceReplayAsync();
                        }
                        else
                        {
                            success = originalValue.Equals(await helloStub.HelloAsync("Jeff"));
                            success = success && Workflow.IsReplaying;

                            await DecisionAsync();
                        }
                        break;

                    case ReplayTest.MutableSideEffect:

                        if (firstPass)
                        {
                            firstPass     = false;
                            originalValue = await Workflow.MutableSideEffectAsync(typeof(string), "value", () => "my-value");

                            await DecisionAsync();
                            await Workflow.ForceReplayAsync();
                        }
                        else
                        {
                            success = originalValue.Equals(await Workflow.MutableSideEffectAsync(typeof(string), "value", () => "my-value"));
                            success = success && Workflow.IsReplaying;

                            await DecisionAsync();
                        }
                        break;

                    case ReplayTest.MutableSideEffectGeneric:

                        if (firstPass)
                        {
                            firstPass     = false;
                            originalValue = await Workflow.MutableSideEffectAsync<string>("value", () => "my-value");

                            await DecisionAsync();
                            await Workflow.ForceReplayAsync();
                        }
                        else
                        {
                            success = originalValue.Equals(await Workflow.MutableSideEffectAsync<string>("value", () => "my-value"));
                            success = success && Workflow.IsReplaying;

                            await DecisionAsync();
                        }
                        break;

                    case ReplayTest.SideEffect:

                        if (firstPass)
                        {
                            firstPass     = false;
                            originalValue = await Workflow.SideEffectAsync(typeof(string), () => "my-value");

                            await DecisionAsync();
                            await Workflow.ForceReplayAsync();
                        }
                        else
                        {
                            success = originalValue.Equals(await Workflow.SideEffectAsync(typeof(string), () => "my-value"));
                            success = success && Workflow.IsReplaying;

                            await DecisionAsync();
                        }
                        break;

                    case ReplayTest.SideEffectGeneric:

                        if (firstPass)
                        {
                            firstPass = false;
                            originalValue = await Workflow.SideEffectAsync<string>(() => "my-value");

                            await DecisionAsync();
                            await Workflow.ForceReplayAsync();
                        }
                        else
                        {
                            success = originalValue.Equals(await Workflow.SideEffectAsync<string>(() => "my-value"));
                            success = success && Workflow.IsReplaying;

                            await DecisionAsync();
                        }
                        break;

                    case ReplayTest.NewGuid:

                        if (firstPass)
                        {
                            firstPass     = false;
                            originalValue = await Workflow.NewGuidAsync();

                            await DecisionAsync();
                            await Workflow.ForceReplayAsync();
                        }
                        else
                        {
                            success = originalValue.Equals(await Workflow.NewGuidAsync());
                            success = success && Workflow.IsReplaying;

                            await DecisionAsync();
                        }
                        break;

                    case ReplayTest.NextRandomDouble:

                        if (firstPass)
                        {
                            firstPass     = false;
                            originalValue = await Workflow.NextRandomDoubleAsync();

                            await DecisionAsync();
                            await Workflow.ForceReplayAsync();
                        }
                        else
                        {
                            success = originalValue.Equals(await Workflow.NextRandomDoubleAsync());
                            success = success && Workflow.IsReplaying;

                            await DecisionAsync();
                        }
                        break;

                    case ReplayTest.NextRandom:

                        if (firstPass)
                        {
                            firstPass     = false;
                            originalValue = await Workflow.NextRandomAsync();

                            await DecisionAsync();
                            await Workflow.ForceReplayAsync();
                        }
                        else
                        {
                            success = originalValue.Equals(await Workflow.NextRandomAsync());
                            success = success && Workflow.IsReplaying;

                            await DecisionAsync();
                        }
                        break;

                    case ReplayTest.NextRandomMax:

                        if (firstPass)
                        {
                            firstPass     = false;
                            originalValue = await Workflow.NextRandomAsync(10000);

                            await DecisionAsync();
                            await Workflow.ForceReplayAsync();
                        }
                        else
                        {
                            success = originalValue.Equals(await Workflow.NextRandomAsync(10000));
                            success = success && Workflow.IsReplaying;

                            await DecisionAsync();
                        }
                        break;

                    case ReplayTest.NextRandomMinMax:

                        if (firstPass)
                        {
                            firstPass     = false;
                            originalValue = await Workflow.NextRandomAsync(100, 1000);

                            await DecisionAsync();
                            await Workflow.ForceReplayAsync();
                        }
                        else
                        {
                            success = originalValue.Equals(await Workflow.NextRandomAsync(100, 1000));
                            success = success && Workflow.IsReplaying;

                            await DecisionAsync();
                        }
                        break;

                    case ReplayTest.NextRandomBytes:

                        if (firstPass)
                        {
                            firstPass     = false;
                            originalValue = await Workflow.NextRandomBytesAsync(32);

                            await DecisionAsync();
                            await Workflow.ForceReplayAsync();
                        }
                        else
                        {
                            success = NeonHelper.ArrayEquals((byte[])originalValue, await Workflow.NextRandomBytesAsync(32));
                            success = success && Workflow.IsReplaying;

                            await DecisionAsync();
                        }
                        break;

                    case ReplayTest.GetLastCompletionResult:

                        // $todo(jefflill):
                        //
                        // This case is a bit tricker to test.  We'd need to schedule the
                        // workflow with a CRON schedule, let it run once and then perform
                        // this test for the second run.
                        //
                        // I'm going just fail the test here and skip the actual test case.
                        // Testing this case is not worth the trouble right now.
#if TODO
                        if (firstPass)
                        {
                            firstPass     = false;
                            originalValue = await Workflow.GetLastCompletionResultAsync<object>();

                            await DecisionAsync();
                            await Workflow.ForceReplayAsync();
                        }
                        else
                        {
                            success = originalValue.Equals(await Workflow.GetLastCompletionResultAsync<object>());
                            success = success && Workflow.IsReplaying;

                            await DecisionAsync();
                        }
#else
                        success = false;
#endif
                        break;

                    case ReplayTest.GetIsSetLastCompletionResult:

                        if (firstPass)
                        {
                            firstPass     = false;
                            originalValue = await Workflow.IsSetLastCompletionResultAsync();

                            await DecisionAsync();
                            await Workflow.ForceReplayAsync();
                        }
                        else
                        {
                            success = originalValue.Equals(await Workflow.IsSetLastCompletionResultAsync());
                            success = success && Workflow.IsReplaying;

                            await DecisionAsync();
                        }
                        break;

                    case ReplayTest.ChildWorkflow:

                        var childStub = Workflow.NewChildWorkflowStub<IWorkflowReplayHello>();

                        if (firstPass)
                        {
                            firstPass     = false;
                            originalValue = await childStub.HelloAsync("Jeff");

                            await DecisionAsync();
                            await Workflow.ForceReplayAsync();
                        }
                        else
                        {
                            success = originalValue.Equals(await childStub.HelloAsync("Jeff"));
                            success = success && Workflow.IsReplaying;

                            await DecisionAsync();
                        }
                        break;

                    case ReplayTest.Activity:

                        var localActivityStub = Workflow.NewLocalActivityStub<IReplayActivity, ReplayActivity>();

                        if (firstPass)
                        {
                            firstPass     = false;
                            originalValue = await localActivityStub.RunAsync("Hello World!");

                            await DecisionAsync();
                            await Workflow.ForceReplayAsync();
                        }
                        else
                        {
                            success = originalValue.Equals(await localActivityStub.RunAsync("Hello World!"));
                            success = success && Workflow.IsReplaying;

                            await DecisionAsync();
                        }
                        break;

                    case ReplayTest.LocalActivity:

                        var activityStub = Workflow.NewActivityStub<IReplayActivity>();

                        if (firstPass)
                        {
                            firstPass = false;
                            originalValue = await activityStub.RunAsync("Hello World!");

                            await DecisionAsync();
                            await Workflow.ForceReplayAsync();
                        }
                        else
                        {
                            success = originalValue.Equals(await activityStub.RunAsync("Hello World!"));
                            success = success && Workflow.IsReplaying;

                            await DecisionAsync();
                        }
                        break;
                }

                return await Task.FromResult(success);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Nop()
        {
            await TaskContext.ResetAsync;
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.Nop));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task GetVersion()
        {
            await TaskContext.ResetAsync;
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.GetVersion));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task WorkflowExecution()
        {
            await TaskContext.ResetAsync;
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.WorkflowExecution));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task MutableSideEffect()
        {
            await TaskContext.ResetAsync;
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.MutableSideEffect));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task MutableSideEffectGeneric()
        {
            await TaskContext.ResetAsync;
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.MutableSideEffectGeneric));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task SideEffect()
        {
            await TaskContext.ResetAsync;
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.SideEffect));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task SideEffectGeneric()
        {
            await TaskContext.ResetAsync;
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.SideEffectGeneric));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task NewGuid()
        {
            await TaskContext.ResetAsync;
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.NewGuid));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task NextRandomDouble()
        {
            await TaskContext.ResetAsync;
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.NextRandomDouble));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task NextRandom()
        {
            await TaskContext.ResetAsync;
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.NextRandom));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task NextRandomMax()
        {
            await TaskContext.ResetAsync;
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.NextRandomMax));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task NextRandomMinMax()
        {
            await TaskContext.ResetAsync;
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.NextRandomMinMax));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task NextRandomBytes()
        {
            await TaskContext.ResetAsync;
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.NextRandomBytes));
        }

        [Fact(Skip = "Not Implemented")]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task GetLastCompletionResult()
        {
            await TaskContext.ResetAsync;
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.GetLastCompletionResult));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task GetIsSetLastCompletionResult()
        {
            await TaskContext.ResetAsync;
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.GetIsSetLastCompletionResult));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task ChildWorkflow()
        {
            await TaskContext.ResetAsync;
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.ChildWorkflow));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Activity()
        {
            await TaskContext.ResetAsync;
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.Activity));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task LocalActivity()
        {
            await TaskContext.ResetAsync;
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.LocalActivity));
        }
    }
}
