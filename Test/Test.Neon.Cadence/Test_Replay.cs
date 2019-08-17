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
using Neon.Cryptography;
using Neon.Data;
using Neon.IO;
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
    // This gist is that we need to disable sticky execution for the worker and
    // then the workflow will replay after sleeping.
    //
    // So we're going to implement a workflow that accepts a parameter that specifies
    // the operation to be tested and uses a static field to indicate whether the
    // workflow is being run for the first time or is replaying.  The test will
    // perform the specified operation on the first pass, trigger a replay, and
    // then ensure that the operation returned the same results on the second pass.

    public partial class Test_Replay : IClassFixture<CadenceFixture>, IDisposable
    {
        private const int maxWaitSeconds = 5;

        private static readonly TimeSpan allowedVariation = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan workflowTimeout  = TimeSpan.FromSeconds(20);

        private CadenceFixture  fixture;
        private CadenceClient   client;
        private HttpClient      proxyClient;

        public Test_Replay(CadenceFixture fixture)
        {
            var settings = new CadenceSettings()
            {
                DefaultDomain   = CadenceFixture.DefaultDomain,
                DefaultTaskList = CadenceFixture.DefaultTaskList,
                CreateDomain    = true,
                Debug           = true,

                //--------------------------------
                // $debug(jeff.lill): DELETE THIS!
                Emulate                = false,
                DebugPrelaunched       = false,
                DebugDisableHandshakes = false,
                DebugDisableHeartbeats = true,
                //--------------------------------
            };

            if (fixture.Start(settings, keepConnection: true, keepOpen: CadenceTestHelper.KeepCadenceServerOpen) == TestFixtureStatus.Started)
            {
                this.fixture     = fixture;
                this.client      = fixture.Client;
                this.proxyClient = new HttpClient() { BaseAddress = client.ProxyUri };

                // Auto register the test workflow and activity implementations.

                client.RegisterAssembly(Assembly.GetExecutingAssembly()).Wait();

                // Start the worker.

                client.SetCacheMaximumSizeAsync(0).Wait();

                var options = new WorkerOptions()
                {
                    DisableStickyExecution = true   // Replay tests require that this be disabled.
                };

                client.StartWorkerAsync(options: options).Wait();
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

        public enum ReplayTest
        {
            Nop,
            GetVersion,
            GetWorkflowExecution,
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

            public static void Reset()
            {
                firstPass = true;
            }

            public async Task<bool> RunAsync(ReplayTest test)
            {
                var     stub    = Workflow.NewLocalActivityStub<IReplayActivity, ReplayActivity>();
                bool    success = false;

                switch (test)
                {
                    case ReplayTest.Nop:

                        if (firstPass)
                        {
                            originalValue = await Workflow.GetVersionAsync("foo", Workflow.DefaultVersion, 1);
                            originalValue = await Workflow.GetVersionAsync("foo", Workflow.DefaultVersion, 1);

                            await ForceReplayAsync();
                        }
                        else
                        {
                            await Workflow.GetVersionAsync("foo", Workflow.DefaultVersion, 1);

                            success = Workflow.IsReplaying;
                        }
                        break;

                    case ReplayTest.GetVersion:
                    case ReplayTest.GetWorkflowExecution:
                    case ReplayTest.MutableSideEffect:
                    case ReplayTest.MutableSideEffectGeneric:
                    case ReplayTest.SideEffect:
                    case ReplayTest.SideEffectGeneric:
                    case ReplayTest.NewGuid:
                    case ReplayTest.NextRandomDouble:
                    case ReplayTest.NextRandom:
                    case ReplayTest.NextRandomMax:
                    case ReplayTest.NextRandomMinMax:
                    case ReplayTest.NextRandomBytes:
                    case ReplayTest.GetLastCompletionResult:
                    case ReplayTest.GetIsSetLastCompletionResult:
                    case ReplayTest.ChildWorkflow:
                    case ReplayTest.Activity:
                    case ReplayTest.LocalActivity:
                    default:

                        success = false;
                        break;
                }

                return await Task.FromResult(success);
            }

            private async Task ForceReplayAsync()
            {
                firstPass = false;

                await Workflow.SleepAsync(TimeSpan.Zero);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Nop()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.Nop));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task GetVersion()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.GetVersion));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task GetWorkflowExecution()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.GetWorkflowExecution));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task MutableSideEffect()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.MutableSideEffect));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task MutableSideEffectGeneric()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.MutableSideEffectGeneric));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task SideEffect()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.SideEffect));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task SideEffectGeneric()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.SideEffectGeneric));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task NewGuid()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.NewGuid));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task NextRandomDouble()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.NextRandomDouble));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task NextRandom()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.NextRandom));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task NextRandomMax()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.NextRandomMax));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task NextRandomMinMax()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.NextRandomMinMax));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task NextRandomBytes()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.NextRandomBytes));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task GetLastCompletionResult()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.GetLastCompletionResult));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task GetIsSetLastCompletionResult()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.GetIsSetLastCompletionResult));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task ChildWorkflow()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.ChildWorkflow));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Activity()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.Activity));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task LocalActivity()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.LocalActivity));
        }
    }
}
