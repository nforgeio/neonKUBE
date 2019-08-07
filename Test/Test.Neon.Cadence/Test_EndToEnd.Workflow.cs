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

        public interface IWorkflowWithNoResult : IWorkflow
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

        public interface IWorkflowUtcNow : IWorkflow
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

        public interface IWorkflowSleep : IWorkflow
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
                times.Add(await Workflow.UtcNowAsync());

                return times;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_Sleep()
        {
            // Verify: Workflow.SleepAsync(). 

            var stub      = client.NewWorkflowStub<IWorkflowSleep>();
            var sleepTime = TimeSpan.FromSeconds(1);
            var times     = await stub.SleepAsync(sleepTime);

            Assert.True(times[1] - times[0] >= sleepTime);
            Assert.True(times[2] - times[1] >= sleepTime);
        }

        //---------------------------------------------------------------------

        public interface IWorkflowSleepUntil : IWorkflow
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
            var sleepTime   = TimeSpan.FromSeconds(5);
            var wakeTimeUtc = startUtcNow + sleepTime;

            await stub.SleepUntilUtcAsync(wakeTimeUtc);

            Assert.True(DateTime.UtcNow - startUtcNow >= sleepTime);

            // Verify that scheduling a sleep time in the past is
            // essentially a NOP.

            stub = client.NewWorkflowStub<IWorkflowSleepUntil>();

            startUtcNow = DateTime.UtcNow;

            await stub.SleepUntilUtcAsync(startUtcNow - TimeSpan.FromDays(1));

            Assert.True(DateTime.UtcNow - startUtcNow < TimeSpan.FromSeconds(1));
        }

        //---------------------------------------------------------------------

        public interface IWorkflowStubExecTwice : IWorkflow
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

        public interface IWorkflowMultiEntrypoints : IWorkflow
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

        public interface IWorkflowMultipleStubCalls : IWorkflow
        {
            [WorkflowMethod]
            Task RunAsync();
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowMultipleStubCalls : WorkflowBase, IWorkflowMultipleStubCalls
        {
            public async Task RunAsync()
            {
                await Task.CompletedTask;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_MultipleStubCalls()
        {
            // Verify that we CANNOT reuse a workflow stub to make multiple calls.

            var stub = client.NewWorkflowStub<IWorkflowMultipleStubCalls>();

            await stub.RunAsync();                                                                      // This call should work.
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await stub.RunAsync());     // This call should fail.
        }

        //---------------------------------------------------------------------

        public interface ICronActivity : IActivity
        {
            [ActivityMethod]
            Task RunAsync(int callNumber);
        }

        [Activity(AutoRegister = true)]
        public class CronActivity : ActivityBase, ICronActivity
        {
            public static List<int> CronCalls = new List<int>();

            [ActivityMethod]
            public async Task RunAsync(int callNumber)
            {
                CronCalls.Add(callNumber);
                await Task.CompletedTask;
            }
        }

        public interface ICronWorkflow : IWorkflow
        {
            [WorkflowMethod]
            Task<int> RunAsync();
        }

        /// <summary>
        /// This workflow is designed to be deployed as a CRON workflow and will call 
        /// the <see cref="CronActivity"/> to record test information about each CRON
        /// workflow run.
        /// </summary>
        [Workflow(AutoRegister = true)]
        public class CronWorkflow : WorkflowBase, ICronWorkflow
        {
            public async Task<int> RunAsync()
            {
                // We're going to exercise HasPreviousResult() and GetPreviousResult() by recording
                // and incrementing the current run number and then passing it CronActivity which
                // will add it to the [CronCalls] list which the unit test will verify.

                var activity   = Workflow.NewActivityStub<ICronActivity>();
                var callNumber = 0;

                if (await Workflow.IsSetLastCompletionResultAsync())
                {
                    callNumber = await Workflow.GetLastCompletionResultAsync<int>();
                }

                callNumber++;

                await activity.RunAsync(callNumber);

                return await Task.FromResult(callNumber);
            }
        }

        [SlowFact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Workflow_Cron()
        {
            var assembly = Assembly.GetExecutingAssembly();

            // Start a CRON workflow on a 1 minute interval that updates the [cronCalls] list 
            // every time the workflow is invoked.  We'll wait for the first invocation and then
            // wait to verify that we've see at least 3 invocations and that each invocation 
            // propertly incremented the call number.

            CronActivity.CronCalls.Clear();     // Clear this to reset any old test state.

            var options = new WorkflowOptions()
            {
                WorkflowId   = "cron-workflow",
                CronSchedule = "0/1 * * * *"
            };

            var stub = client.NewWorkflowStub<ICronWorkflow>(options);

            _ = stub.RunAsync();

            // Start the CRON workflow and wait for the result from the first run.  

            NeonHelper.WaitFor(() => CronActivity.CronCalls.Count >= 1, timeout: TimeSpan.FromMinutes(1.5));

            Assert.Equal(1, CronActivity.CronCalls[0]);

            // Wait up to 2.5 minutes more for at least two more runs.

            NeonHelper.WaitFor(() => CronActivity.CronCalls.Count >= 3, timeout: TimeSpan.FromMinutes(2.5));

            // Verify that the run numbers look good.

            for (int i = 1; i <= 3; i++)
            {
                Assert.Equal(CronActivity.CronCalls[i - 1], i);
            }
       }
    }
}
