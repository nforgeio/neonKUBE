//-----------------------------------------------------------------------------
// FILE:        Test_EndToEnd.Workflow.cs
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
    public partial class Test_EndToEnd
    {
        //---------------------------------------------------------------------

        private static bool workflowTests_WorkflowWithNoResultCalled;

        public interface IWorkflowWithNoResult
        {
            [WorkflowMethod]
            Task RunAsync();
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowWithNoResult : WorkflowBase, IWorkflowWithNoResult
        {
            public async Task RunAsync()
            {
                workflowTests_WorkflowWithNoResultCalled = true;

                await Task.CompletedTask;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_WithNoResult()
        {
            // Verify that we can call a simple workflow that accepts a
            // parameter and results a result.

            workflowTests_WorkflowWithNoResultCalled = false;

            var stub = client.NewWorkflowStub<IWorkflowWithNoResult>();

            await stub.RunAsync();

            Assert.True(workflowTests_WorkflowWithNoResultCalled);
        }

        //---------------------------------------------------------------------

        public interface IWorkflowWithResult
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

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_WithResult()
        {
            // Verify that we can call a simple workflow that accepts a
            // parameter and results a result.

            var stub = client.NewWorkflowStub<IWorkflowWithResult>();

            Assert.Equal("Hello Jeff!", await stub.HelloAsync("Jeff"));
        }

        //---------------------------------------------------------------------

        public interface IWorkflowUtcNow
        {
            [WorkflowMethod]
            Task<DateTime> GetUtcNowAsync();
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowUtcNow : WorkflowBase, IWorkflowUtcNow
        {
            public async Task<DateTime> GetUtcNowAsync()
            {
                return await Workflow.UtcNowAsync();
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_UtcNow()
        {
            // Verify: Workflow.UtcNow(). 

            var stub           = client.NewWorkflowStub<IWorkflowUtcNow>();
            var workflowUtcNow = await stub.GetUtcNowAsync();
            var nowUtc         = DateTime.UtcNow;

            Assert.True(nowUtc - workflowUtcNow < allowedVariation);
            Assert.True(workflowUtcNow - nowUtc < allowedVariation);
        }

        //---------------------------------------------------------------------

        public interface IWorkflowSleep
        {
            [WorkflowMethod]
            Task<List<DateTime>> SleepAsync(TimeSpan time);
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowSleep : WorkflowBase, IWorkflowSleep
        {
            public async Task<List<DateTime>> SleepAsync(TimeSpan sleepTime)
            {
                var times = new List<DateTime>();

                times.Add(await Workflow.UtcNowAsync());
                await Workflow.SleepAsync(sleepTime);
                times.Add(await Workflow.UtcNowAsync());
                await Workflow.SleepAsync(sleepTime);

                return times;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_Sleep()
        {
            // Verify: Workflow.SleepAsync(). 

            var stub      = client.NewWorkflowStub<IWorkflowSleep>();
            var sleepTime = TimeSpan.FromMilliseconds(1000);
            var times     = await stub.SleepAsync(sleepTime);

            Assert.True(times[1] > times[0]);
            Assert.True(times[1] - times[0] - sleepTime < allowedVariation);
        }

        //---------------------------------------------------------------------

        public interface IWorkflowSleepUntil
        {
            [WorkflowMethod]
            Task SleepUntilUtcAsync(DateTime wakeTimeUtc);
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowSleepUntil : WorkflowBase, IWorkflowSleepUntil
        {
            public async Task SleepUntilUtcAsync(DateTime wakeTimeUtc)
            {
                await Workflow.SleepUntilUtcAsync(wakeTimeUtc);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_SleepUntilUtc()
        {
            var stub = client.NewWorkflowStub<IWorkflowSleepUntil>();

            // Verify that Workflow.SleepUntilAsync() can schedule a
            // wake time in the future.

            var startUtcNow = DateTime.UtcNow;
            var wakeTimeUtc = startUtcNow + TimeSpan.FromSeconds(10);

            await stub.SleepUntilUtcAsync(wakeTimeUtc);

            var endUtcNow = DateTime.UtcNow;

            Assert.True(endUtcNow >= wakeTimeUtc);
            Assert.True(endUtcNow - wakeTimeUtc < allowedVariation);

            // Verify that scheduling a sleep time in the past is
            // essentially a NOP.

            stub = client.NewWorkflowStub<IWorkflowSleepUntil>();

            await stub.SleepUntilUtcAsync(endUtcNow - TimeSpan.FromDays(1));

            Assert.True(DateTime.UtcNow - endUtcNow < TimeSpan.FromSeconds(2));
        }

        //---------------------------------------------------------------------

        public interface IWorkflowStubExecTwice
        {
            [WorkflowMethod]
            Task RunAsync();
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowStubExecTwice : WorkflowBase, IWorkflowStubExecTwice
        {
            public async Task RunAsync()
            {
                await Task.CompletedTask;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_StubExecTwice()
        {
            // Verify that a single workflow stub instance may only be used
            // to start a workflow once.

            var stub = client.NewWorkflowStub<IWorkflowStubExecTwice>();

            await stub.RunAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await stub.RunAsync());
        }

        //---------------------------------------------------------------------

        public interface IWorkflowMultiEntrypoints
        {
            [WorkflowMethod(Name = "hello")]
            Task<string> HelloAsync(string name);

            [WorkflowMethod(Name = "goodbye")]
            Task<string> GoodbyeAsync(string name);
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowMultiEntrypoints : WorkflowBase, IWorkflowMultiEntrypoints
        {
            public async Task<string> HelloAsync(string name)
            {
                return await Task.FromResult($"Hello {name}!"); 
            }

            public async Task<string> GoodbyeAsync(string name)
            {
                return await Task.FromResult($"Goodbye {name}!");
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_MultiEntrypoints()
        {
            // Verify that we can call multiple entry points.

            var stub1 = client.NewWorkflowStub<IWorkflowMultiEntrypoints>();

            Assert.Equal("Hello Jeff!", await stub1.HelloAsync("Jeff"));

            var stub2 = client.NewWorkflowStub<IWorkflowMultiEntrypoints>();

            Assert.Equal("Goodbye Jeff!", await stub2.GoodbyeAsync("Jeff"));
        }

        //---------------------------------------------------------------------

        public interface IWorkflowBlankEntrypointConflict
        {
            [WorkflowMethod]
            Task<string> HelloAsync(string name);

            [WorkflowMethod]
            Task<string> GoodbyeAsync(string name);
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowBlankEntrypointConflict : WorkflowBase, IWorkflowBlankEntrypointConflict
        {
            public async Task<string> HelloAsync(string name)
            {
                return await Task.FromResult($"Hello {name}!");
            }

            public async Task<string> GoodbyeAsync(string name)
            {
                return await Task.FromResult($"Goodbye {name}!");
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Workflow_BlankEntrypointConflict()
        {
            // Verify that the client detects workflows that have multiple
            // entrypoints that conflict because they have the same (blank)
            // name.

            var stub = client.NewWorkflowStub<IWorkflowBlankEntrypointConflict>();

            Assert.Throws<WorkflowTypeException>(() => client.NewWorkflowStub<IWorkflowBlankEntrypointConflict>());
        }
    }
}
