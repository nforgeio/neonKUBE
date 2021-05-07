//-----------------------------------------------------------------------------
// FILE:        Test_EndToEnd.Stubs.cs
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
    public partial class Test_EndToEnd
    {
        //---------------------------------------------------------------------

        [WorkflowInterface(TaskQueue = TemporalTestHelper.TaskQueue)]
        public interface ITestWorkflowStub_Execute : IWorkflow
        {
            [WorkflowMethod(Name = "TestWorkflowStub_Execute[hello]", IsFullName = true)]
            Task<string> HelloAsync(string name);

            [WorkflowMethod(Name = "TestWorkflowStub_Execute[nop]", IsFullName = true)]
            Task NopAsync();

            [WorkflowMethod(Name = "TestWorkflowStub_Execute[wait-for-signals]", IsFullName = true)]
            Task<List<string>> GetSignalsAsync();

            [WorkflowMethod(Name = "TestWorkflowStub_Execute[wait-for-queries]", IsFullName = true)]
            Task<List<string>> GetQueriesAsync();

            [SignalMethod(name: "signal")]
            Task SignalAsync(string signal);

            [QueryMethod(name: "query")]
            Task<string> QueryAsync(string query);
        }

        [Workflow(AutoRegister = true)]
        public class TestWorkflowStub_Execute : WorkflowBase, ITestWorkflowStub_Execute
        {
            //-----------------------------------------------------------------
            // Static members

            private static bool isRunning = false;

            public static void Reset()
            {
                isRunning = false;
            }

            public static async Task WaitUntilRunningAsync()
            {
                await NeonHelper.WaitForAsync(async () => await Task.FromResult(isRunning), TimeSpan.FromSeconds(maxWaitSeconds));
            }

            //-----------------------------------------------------------------
            // Instance members

            private List<string> signals = new List<string>();
            private List<string> queries = new List<string>();

            public async Task<string> HelloAsync(string name)
            {
                isRunning = true;
                return await Task.FromResult($"Hello {name}!");
            }

            public async Task NopAsync()
            {
                isRunning = true;

                await Task.CompletedTask;
            }

            public async Task<List<string>> GetQueriesAsync()
            {
                isRunning = true;

                // Wait to receive some queries.

                await Workflow.SleepAsync(TimeSpan.FromSeconds(maxWaitSeconds));

                return queries;
            }

            public async Task<List<string>> GetSignalsAsync()
            {
                isRunning = true;

                // Wait to receive some signals.

                await Workflow.SleepAsync(TimeSpan.FromSeconds(maxWaitSeconds));

                return signals;
            }

            public async Task SignalAsync(string signal)
            {
                lock (signals)
                {
                    signals.Add(signal);
                }

                await Task.CompletedTask;
            }

            public async Task<string> QueryAsync(string query)
            {
                lock (queries)
                {
                    queries.Add(query);
                }

                return await Task.FromResult($"Received: {query}");
            }
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        public async Task WorkflowStub_Start_Untyped()
        {
            await SyncContext.ClearAsync;

            // Use an untyped workflow stub to execute a workflow.

            TestWorkflowStub_Execute.Reset();

            var stub      = client.NewUntypedWorkflowStub("TestWorkflowStub_Execute[hello]", new StartWorkflowOptions() { TaskQueue = TemporalTestHelper.TaskQueue });
            var execution = await stub.StartAsync("Jeff");

            Assert.NotNull(execution);
            Assert.Equal("Hello Jeff!", await stub.GetResultAsync<string>());

            // Verify that we're not allowed to reuse the stub.

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await stub.StartAsync("Jeff"));
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Category, TestArea.NeonCadence)]
        public async Task WorkflowStub_Execute_Untyped()
        {
            await SyncContext.ClearAsync;

            // Use an untyped workflow stub to execute a workflow that
            // returns a result in one step.

            TestWorkflowStub_Execute.Reset();

            var stub = client.NewUntypedWorkflowStub("TestWorkflowStub_Execute[hello]", new StartWorkflowOptions() { TaskQueue = TemporalTestHelper.TaskQueue });

            Assert.Equal("Hello Jeff!", await stub.ExecuteAsync<string>("Jeff"));

            // Verify that we're not allowed to reuse the stub.

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await stub.StartAsync("Jeff"));

            // Try this again with a workflow method that doesn't return a result.

            stub = client.NewUntypedWorkflowStub("TestWorkflowStub_Execute[nop]", new StartWorkflowOptions() { TaskQueue = TemporalTestHelper.TaskQueue });

            await stub.ExecuteAsync();

            // Verify that we're not allowed to reuse that stub either stub.

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await stub.StartAsync("Jeff"));

        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        public async Task WorkflowStub_Attach_Untyped()
        {
            await SyncContext.ClearAsync;

            // Use an untyped workflow stub to execute a workflow.

            TestWorkflowStub_Execute.Reset();

            var stub      = client.NewUntypedWorkflowStub("TestWorkflowStub_Execute[hello]", new StartWorkflowOptions() { TaskQueue = TemporalTestHelper.TaskQueue });
            var execution = await stub.StartAsync("Jeff");

            Assert.NotNull(execution);
            Assert.Equal("Hello Jeff!", await stub.GetResultAsync<string>());

            // Now connect another stub to the workflow and verify that we
            // can use it to obtain the result.

            stub = client.NewUntypedWorkflowStub(execution.WorkflowId, execution.RunId);

            Assert.Equal("Hello Jeff!", await stub.GetResultAsync<string>());

            // There's one more method override for attaching to an existing workflow.

            stub = client.NewUntypedWorkflowStub(execution);

            Assert.Equal("Hello Jeff!", await stub.GetResultAsync<string>());
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        public async Task WorkflowStub_Signal_Untyped()
        {
            await SyncContext.ClearAsync;

            // Use an untyped workflow stub to execute a workflow and then
            // verify that we're able to send signals to it.

            TestWorkflowStub_Execute.Reset();

            var stub = client.NewUntypedWorkflowStub("TestWorkflowStub_Execute[wait-for-signals]", new StartWorkflowOptions() { TaskQueue = TemporalTestHelper.TaskQueue });

            await stub.StartAsync();
            await TestWorkflowStub_Execute.WaitUntilRunningAsync();

            await stub.SignalAsync("signal", "my-signal-1");
            await stub.SignalAsync("signal", "my-signal-2");

            var received = await stub.GetResultAsync<List<string>>();

            Assert.Equal(2, received.Count);
            Assert.Contains("my-signal-1", received);
            Assert.Contains("my-signal-2", received);
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        public async Task WorkflowStub_Query_Untyped()
        {
            await SyncContext.ClearAsync;

            // Use an untyped workflow stub to execute a workflow and then
            // verify that we're able to send signals to it.

            TestWorkflowStub_Execute.Reset();

            var stub = client.NewUntypedWorkflowStub("TestWorkflowStub_Execute[wait-for-queries]", new StartWorkflowOptions() { TaskQueue = TemporalTestHelper.TaskQueue });

            await stub.StartAsync();
            await TestWorkflowStub_Execute.WaitUntilRunningAsync();

            Assert.Equal("Received: my-query-1", await stub.QueryAsync<string>("query", "my-query-1"));
            Assert.Equal("Received: my-query-2", await stub.QueryAsync<string>("query", "my-query-2"));

            var received = await stub.GetResultAsync<List<string>>();

            Assert.Equal(2, received.Count);
            Assert.Contains("my-query-1", received);
            Assert.Contains("my-query-2", received);
        }
    }
}
