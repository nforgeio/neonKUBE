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

            //[SignalMethod("signal-with-result", Synchronous = true)]
            //Task<string> SignalAsync(TimeSpan delay, string input);
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

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task SyncSignal_Void()
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
    }
}
