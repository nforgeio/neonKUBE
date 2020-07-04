//-----------------------------------------------------------------------------
// FILE:        Test_EndToEnd.Workflow.cs
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
using System.Diagnostics;
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
using Neon.Kube;
using Neon.Net;
using Neon.Retry;
using Neon.Tasks;
using Neon.Xunit;
using Neon.Xunit.Cadence;

using Test.Neon.Models.Cadence;

using Newtonsoft.Json;
using Xunit;

namespace TestCadence
{
    public partial class Test_EndToEnd
    {
        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowWithNoResult : IWorkflow
        {
            [WorkflowMethod]
            Task RunAsync();
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowWithNoResult : WorkflowBase, IWorkflowWithNoResult
        {
            //-------------------------------------------------------
            // Static members

            public static bool WorkflowWithNoResultCalled = false;

            public static new void Reset()
            {
                WorkflowWithNoResultCalled = false;
            }

            //-------------------------------------------------------
            // Instance members

            public async Task RunAsync()
            {
                WorkflowWithNoResultCalled = true;

                await Task.CompletedTask;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_WithNoResult()
        {
            await SyncContext.ClearAsync;

            // Verify that we can call a simple workflow that accepts a
            // parameter and returns a result.

            WorkflowWithNoResult.Reset();

            var stub = client.NewWorkflowStub<IWorkflowWithNoResult>();

            await stub.RunAsync();

            Assert.True(WorkflowWithNoResult.WorkflowWithNoResultCalled);
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowWithResult : IWorkflow
        {
            [WorkflowMethod]
            Task<string> HelloAsync(string name);

            [WorkflowMethod(Name = "run")]
            Task RunAsync();
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowWithResult : WorkflowBase, IWorkflowWithResult
        {
            public async Task<string> HelloAsync(string name)
            {
                return await Task.FromResult($"Hello {name}!");
            }

            public async Task RunAsync()
            {
                await Task.CompletedTask;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Workflow_GetWorkflowTypeName()
        {
            // Verify that [CadenceHelper.GetWorkflowTypeName()] works correctly
            // for default and named methods.

            const string baseTypeName = "TestCadence.Test_EndToEnd.WorkflowWithResult";

            Assert.Equal(baseTypeName, CadenceHelper.GetWorkflowTypeName<IWorkflowWithResult>());
            Assert.Equal(baseTypeName, CadenceHelper.GetWorkflowTypeName<IWorkflowWithResult>(""));
            Assert.Equal(baseTypeName + "::run", CadenceHelper.GetWorkflowTypeName<IWorkflowWithResult>("run"));

            // Verify that we see an exception if the targeted method doesn't exist.

            Assert.Throws<ArgumentException>(() => CadenceHelper.GetWorkflowTypeName<IWorkflowWithResult>("does-not-exist"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_WithResult()
        {
            await SyncContext.ClearAsync;

            // Verify that we can call a simple workflow that accepts a
            // parameter and results a result.

            var stub = client.NewWorkflowStub<IWorkflowWithResult>();

            Assert.Equal("Hello Jeff!", await stub.HelloAsync("Jeff"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_WithMemos()
        {
            await SyncContext.ClearAsync;

            // Verify that we can call a simple workflow that accepts a
            // parameter and results a result.

            var options = new WorkflowOptions()
            {
                Memo = new Dictionary<string, object>()
                {
                    { "int", 10 },
                    { "bool", true }
                }
            };

            var stub = client.NewWorkflowStub<IWorkflowWithResult>(options);

            Assert.Equal("Hello Jeff!", await stub.HelloAsync("Jeff"));
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowLogger : IWorkflow
        {
            [WorkflowMethod]
            Task RunAsync();
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowLogger : WorkflowBase, IWorkflowLogger
        {
            public async Task RunAsync()
            {
                Workflow.Logger.LogInfo("Hello World!");
                await Task.CompletedTask;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_Logger()
        {
            await SyncContext.ClearAsync;

            // Verify that logging within a workflow doesn't barf.

            // $todo(jefflill):
            //
            // It would be nice to add additional tests that actually
            // verify that something reasonable was logged, including
            // using the workflow run ID as the log context.
            //
            // I did verify this manually.

            var stub = client.NewWorkflowStub<IWorkflowLogger>();

            await stub.RunAsync();
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
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
            await SyncContext.ClearAsync;

            // Verify: Workflow.UtcNow(). 

            var stub           = client.NewWorkflowStub<IWorkflowUtcNow>();
            var workflowUtcNow = await stub.GetUtcNowAsync();
            var nowUtc         = DateTime.UtcNow;

            Assert.True(nowUtc - workflowUtcNow < allowedVariation);
            Assert.True(workflowUtcNow - nowUtc < allowedVariation);
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
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
            await SyncContext.ClearAsync;

            // Verify: Workflow.SleepAsync(). 

            var stub      = client.NewWorkflowStub<IWorkflowSleep>();
            var sleepTime = TimeSpan.FromSeconds(1);
            var times     = await stub.SleepAsync(sleepTime);

            Assert.True(times[1] - times[0] >= sleepTime);
            Assert.True(times[2] - times[1] >= sleepTime);
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
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
            await SyncContext.ClearAsync;

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

            Assert.True(DateTime.UtcNow - startUtcNow < TimeSpan.FromSeconds(2));
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
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
            await SyncContext.ClearAsync;

            // Verify that a single workflow stub instance may only be used
            // to start a workflow once.

            var stub = client.NewWorkflowStub<IWorkflowStubExecTwice>();

            await stub.RunAsync();
            await Assert.ThrowsAsync<WorkflowExecutionAlreadyStartedException>(async () => await stub.RunAsync());
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
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
            await SyncContext.ClearAsync;

            // Verify that we can call multiple entry points.

            var stub1 = client.NewWorkflowStub<IWorkflowMultiEntrypoints>();

            Assert.Equal("Hello Jeff!", await stub1.HelloAsync("Jeff"));

            var stub2 = client.NewWorkflowStub<IWorkflowMultiEntrypoints>();

            Assert.Equal("Goodbye Jeff!", await stub2.GoodbyeAsync("Jeff"));
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
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
        public async Task Workflow_MultipleStubs()
        {
            await SyncContext.ClearAsync;

            // Verify that we CANNOT reuse a workflow stub to make multiple calls.

            var stub = client.NewWorkflowStub<IWorkflowMultipleStubCalls>();

            await stub.RunAsync();                                                                                  // This call should work.
            await Assert.ThrowsAsync<WorkflowExecutionAlreadyStartedException>(async () => await stub.RunAsync());  // This call should fail.
        }

        //---------------------------------------------------------------------

        [ActivityInterface(TaskList = CadenceTestHelper.TaskList)]
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

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
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

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_Cron()
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

            // Start the CRON workflow and wait for the result from the first run.

            var stub = client.NewWorkflowFutureStub<ICronWorkflow>(string.Empty, options);

            await stub.StartAsync();

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

        //---------------------------------------------------------------------

        private const int RandomSampleCount         = 1000;
        private const int MaxDuplicateRandomSamples = RandomSampleCount / 10;

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IRandomWorkflow : IWorkflow
        {
            [WorkflowMethod(Name = "GetRandomDoubles")]
            Task<List<double>> GetRandomDoublesAsync(int count);

            [WorkflowMethod(Name = "GetRandomInts")]
            Task<List<int>> GetRandomIntsAsync(int count);

            [WorkflowMethod(Name = "GetRandomInts_Max")]
            Task<List<int>> GetRandomIntsAsync(int count, int maxValue);

            [WorkflowMethod(Name = "GetRandomInts_MinMax")]
            Task<List<int>> GetRandomIntsAsync(int count, int minValue, int maxValue);

            [WorkflowMethod(Name = "GetRandomBytes")]
            Task<List<byte[]>> GetRandomBytesAsync(int count, int size);
        }

        [Workflow(AutoRegister = true)]
        public class RandomWorkflow : WorkflowBase, IRandomWorkflow
        {
            public async Task<List<double>> GetRandomDoublesAsync(int count)
            {
                var list = new List<double>();

                for (int i = 0; i < count; i++)
                {
                    list.Add(await Workflow.NextRandomDoubleAsync());
                }

                return await Task.FromResult(list);
            }

            public async Task<List<int>> GetRandomIntsAsync(int count)
            {
                var list = new List<int>();

                for (int i = 0; i < count; i++)
                {
                    list.Add(await Workflow.NextRandomAsync());
                }

                return await Task.FromResult(list);
            }

            public async Task<List<int>> GetRandomIntsAsync(int count, int maxValue)
            {
                var list = new List<int>();

                for (int i = 0; i < count; i++)
                {
                    list.Add(await Workflow.NextRandomAsync(maxValue));
                }

                return await Task.FromResult(list);
            }

            public async Task<List<int>> GetRandomIntsAsync(int count, int minValue, int maxValue)
            {
                var list = new List<int>();

                for (int i = 0; i < count; i++)
                {
                    list.Add(await Workflow.NextRandomAsync(minValue, maxValue));
                }

                return await Task.FromResult(list);
            }

            public async Task<List<byte[]>> GetRandomBytesAsync(int count, int size)
            {
                var list = new List<byte[]>();

                for (int i = 0; i < count; i++)
                {
                    list.Add(await Workflow.NextRandomBytesAsync(size));
                }

                return await Task.FromResult(list);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_NextRandomDouble()
        {
            await SyncContext.ClearAsync;

            // Start a workflow that will return a set of random doubles (0.0 <= value < 1.0)
            // and verify that there are only a small number of duplicates.  Then do the same
            // with another workflow and verify that there are only a small number of duplicates
            // compared to the first set.
            //
            // There's a statistically small chance that this could fail because we just happened
            // to seed the random number generators the same or just got incrdeably lucky but
            // I'm going to generate enough samples so this should be very unlikely.

            var samples1   = await client.NewWorkflowStub<IRandomWorkflow>().GetRandomDoublesAsync(RandomSampleCount);
            var sampleSet  = new HashSet<double>();
            var duplicates = 0;

            Assert.Equal(RandomSampleCount, samples1.Count);

            foreach (var sample in samples1)
            {
                Assert.True(0.0 <= sample);
                Assert.True(sample < 1.0);

                if (sampleSet.Contains(sample))
                {
                    duplicates++;
                }
                else
                {
                    sampleSet.Add(sample);
                }
            }

            Assert.True(duplicates <= MaxDuplicateRandomSamples, $"NextRandomDoubleAsync() returned more than {MaxDuplicateRandomSamples} duplicate values out of {RandomSampleCount} generated samples.");

            // Compare against a new run.  Each wokflow should use a different seed
            // so we're expecting a different sequence.

            var samples2 = await client.NewWorkflowStub<IRandomWorkflow>().GetRandomDoublesAsync(RandomSampleCount);

            Assert.Equal(RandomSampleCount, samples1.Count);

            duplicates = 0;

            for (int i = 0; i < RandomSampleCount; i++)
            {
                Assert.True(0.0 <= samples2[i]);
                Assert.True(samples2[i] < 1.0);

                if (samples1[i] == samples1[2])
                {
                    duplicates++;
                }
            }

            Assert.True(duplicates <= MaxDuplicateRandomSamples, $"NextRandomDoubleAsync() returned more than {MaxDuplicateRandomSamples} duplicate values out of {RandomSampleCount} generated samples.");
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_NextRandomInt()
        {
            await SyncContext.ClearAsync;

            // Start a workflow that will return a set of random integers (unconstrained)
            // and verify that there are only a small number of duplicates.  Then do the same
            // with another workflow and verify that there are only a small number of duplicates
            // compared to the first set.
            //
            // There's a statistically small chance that this could fail because we just happened
            // to seed the random number generators the same or just got incrdeably lucky but
            // I'm going to generate enough samples so this should be very unlikely.

            var samples1   = await client.NewWorkflowStub<IRandomWorkflow>().GetRandomDoublesAsync(RandomSampleCount);
            var sampleSet  = new HashSet<double>();
            var duplicates = 0;

            Assert.Equal(RandomSampleCount, samples1.Count);

            foreach (var sample in samples1)
            {
                if (sampleSet.Contains(sample))
                {
                    duplicates++;
                }
                else
                {
                    sampleSet.Add(sample);
                }
            }

            Assert.True(duplicates <= MaxDuplicateRandomSamples, $"NextRandomAsync() returned more than {MaxDuplicateRandomSamples} duplicate values out of {RandomSampleCount} generated samples.");

            // Compare against a new run.  Each wokflow should use a different seed
            // so we're expecting a different sequence.

            var samples2 = await client.NewWorkflowStub<IRandomWorkflow>().GetRandomIntsAsync(RandomSampleCount);

            Assert.Equal(RandomSampleCount, samples1.Count);

            duplicates = 0;

            for (int i = 0; i < RandomSampleCount; i++)
            {
                if (samples1[i] == samples1[2])
                {
                    duplicates++;
                }
            }

            Assert.True(duplicates <= MaxDuplicateRandomSamples, $"NextRandomAsync() returned more than {MaxDuplicateRandomSamples} duplicate values out of {RandomSampleCount} generated samples.");
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_NextRandomInt_Max()
        {
            await SyncContext.ClearAsync;

            // Start a workflow that will return a set of random integers (<= 1 million)
            // and verify that there are only a small number of duplicates.  Then do the same
            // with another workflow and verify that there are only a small number of duplicates
            // compared to the first set.
            //
            // There's a statistically small chance that this could fail because we just happened
            // to seed the random number generators the same or just got incrdeably lucky but
            // I'm going to generate enough samples so this should be very unlikely.

            const int maxSample = 1000000;

            var samples1   = await client.NewWorkflowStub<IRandomWorkflow>().GetRandomIntsAsync(RandomSampleCount, maxSample);
            var sampleSet  = new HashSet<double>();
            var duplicates = 0;

            Assert.Equal(RandomSampleCount, samples1.Count);

            foreach (var sample in samples1)
            {
                Assert.True(sample <= maxSample);

                if (sampleSet.Contains(sample))
                {
                    duplicates++;
                }
                else
                {
                    sampleSet.Add(sample);
                }
            }

            Assert.True(duplicates <= MaxDuplicateRandomSamples, $"NextRandomAsync(max) returned more than {MaxDuplicateRandomSamples} duplicate values out of {RandomSampleCount} generated samples.");

            // Compare against a new run.  Each wokflow should use a different seed
            // so we're expecting a different sequence.

            var samples2 = await client.NewWorkflowStub<IRandomWorkflow>().GetRandomIntsAsync(RandomSampleCount, maxSample);

            Assert.Equal(RandomSampleCount, samples1.Count);

            duplicates = 0;

            for (int i = 0; i < RandomSampleCount; i++)
            {
                Assert.True(samples2[i] <= maxSample);

                if (samples1[i] == samples1[2])
                {
                    duplicates++;
                }
            }

            Assert.True(duplicates <= MaxDuplicateRandomSamples, $"NextRandomAsync(max) returned more than {MaxDuplicateRandomSamples} duplicate values out of {RandomSampleCount} generated samples.");
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_NextRandomInt_MinMax()
        {
            await SyncContext.ClearAsync;

            // Start a workflow that will return a set of random integers (1 million <= value <= 2 million)
            // and verify that there are only a small number of duplicates.  Then do the same
            // with another workflow and verify that there are only a small number of duplicates
            // compared to the first set.
            //
            // There's a statistically small chance that this could fail because we just happened
            // to seed the random number generators the same or just got incrdeably lucky but
            // I'm going to generate enough samples so this should be very unlikely.

            const int minSample = 1000000;
            const int maxSample = 2000000;

            var samples1   = await client.NewWorkflowStub<IRandomWorkflow>().GetRandomIntsAsync(RandomSampleCount, minSample, maxSample);
            var sampleSet  = new HashSet<double>();
            var duplicates = 0;

            Assert.Equal(RandomSampleCount, samples1.Count);

            foreach (var sample in samples1)
            {
                Assert.True(minSample <= sample);
                Assert.True(sample <= maxSample);

                if (sampleSet.Contains(sample))
                {
                    duplicates++;
                }
                else
                {
                    sampleSet.Add(sample);
                }
            }

            Assert.True(duplicates <= MaxDuplicateRandomSamples, $"NextRandomAsync(min, max) returned more than {MaxDuplicateRandomSamples} duplicate values out of {RandomSampleCount} generated samples.");

            // Compare against a new run.  Each wokflow should use a different seed
            // so we're expecting a different sequence.

            var samples2 = await client.NewWorkflowStub<IRandomWorkflow>().GetRandomIntsAsync(RandomSampleCount, minSample, maxSample);

            Assert.Equal(RandomSampleCount, samples1.Count);

            duplicates = 0;

            for (int i = 0; i < RandomSampleCount; i++)
            {
                Assert.True(minSample <= samples2[i]);
                Assert.True(samples2[i] <= maxSample);

                if (samples1[i] == samples1[2])
                {
                    duplicates++;
                }
            }

            Assert.True(duplicates <= MaxDuplicateRandomSamples, $"NextRandomAsync(min, max) returned more than {MaxDuplicateRandomSamples} duplicate values out of {RandomSampleCount} generated samples.");
        }

        private class BytesHolder
        {
            public BytesHolder(byte[] bytes)
            {
                Covenant.Requires<ArgumentNullException>(bytes != null, nameof(bytes));
                Covenant.Requires<ArgumentException>(bytes.Length > 4, nameof(bytes));

                this.Bytes = bytes;
            }

            public byte[] Bytes { get; private set; }

            public override int GetHashCode()
            {
                return (Bytes[0] << 24) | (Bytes[1] << 16) | (Bytes[2] << 8) | Bytes[3];
            }

            public override bool Equals(object obj)
            {
                var other = obj as BytesHolder;

                if (other == null)
                {
                    return false;
                }

                return NeonHelper.ArrayEquals(this.Bytes, other.Bytes);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_NextRandomBytes()
        {
            await SyncContext.ClearAsync;

            // Start a workflow that will return a set of random byte arrays and verify that there
            // are only a small number of duplicates.  Then do the same with another workflow and
            // verify that there are only a small number of duplicates compared to the first set.
            //
            // There's a statistically small chance that this could fail because we just happened
            // to seed the random number generators the same or just got incrdeably lucky but
            // I'm going to generate enough samples so this should be very unlikely.

            const int size = 32;

            var samples1   = await client.NewWorkflowStub<IRandomWorkflow>().GetRandomBytesAsync(RandomSampleCount, size);
            var sampleSet  = new HashSet<BytesHolder>();
            var duplicates = 0;

            Assert.Equal(RandomSampleCount, samples1.Count);

            foreach (var sample in samples1)
            {
                Assert.Equal(size, sample.Length);

                var holder = new BytesHolder(sample);

                if (sampleSet.Contains(holder))
                {
                    duplicates++;
                }
                else
                {
                    sampleSet.Add(holder);
                }
            }

            Assert.True(duplicates <= MaxDuplicateRandomSamples, $"NextRandomBytesAsync() returned more than {MaxDuplicateRandomSamples} duplicate values out of {RandomSampleCount} generated samples.");

            // Compare against a new run.  Each wokflow should use a different seed
            // so we're expecting a different sequence.

            var samples2 = await client.NewWorkflowStub<IRandomWorkflow>().GetRandomBytesAsync(RandomSampleCount, size);

            Assert.Equal(RandomSampleCount, samples1.Count);

            duplicates = 0;

            for (int i = 0; i < RandomSampleCount; i++)
            {
                Assert.Equal(size, samples2[i].Length);

                foreach (var item in samples1)
                {
                    if (NeonHelper.ArrayEquals(samples2[i], item))
                    {
                        duplicates++;
                        break;
                    }
                }
            }

            Assert.True(duplicates <= MaxDuplicateRandomSamples, $"NextRandomAsync() returned more than {MaxDuplicateRandomSamples} duplicate values out of {RandomSampleCount} generated samples.");
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowEcho : IWorkflow
        {
            [WorkflowMethod]
            Task<byte[]> EchoAsync(byte[] contents);
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowEcho : WorkflowBase, IWorkflowEcho
        {
            public async Task<byte[]> EchoAsync(byte[] contents)
            {
                return await Task.FromResult(contents);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_Echo()
        {
            await SyncContext.ClearAsync;

            // Verify that we send and receive varying sizes of content, from
            // small to pretty large (1MiB).

            var rand = new Random();

            Assert.Null(await client.NewWorkflowStub<IWorkflowEcho>().EchoAsync(null));

            for (int size = 1024; size <= 1 * 1024 * 1024; size *= 2)
            {
                var value = new byte[size];

                rand.NextBytes(value);
                Assert.Equal(value, await client.NewWorkflowStub<IWorkflowEcho>().EchoAsync(value));
            }
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowSideEffect : IWorkflow
        {
            [WorkflowMethod(Name = "SideEffect")]
            Task<string> SideEffectAsync(string input);

            [WorkflowMethod(Name = "GenericSideEffect")]
            Task<string> GenericSideEffectAsync(string input);
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowSideEffect : WorkflowBase, IWorkflowSideEffect
        {
            public async Task<string> SideEffectAsync(string input)
            {
                var output = (string)await Workflow.SideEffectAsync(typeof(string), () => input);

                return await Task.FromResult(output);
            }

            public async Task<string> GenericSideEffectAsync(string input)
            {
                var output = await Workflow.SideEffectAsync<string>(() => input);

                return await Task.FromResult(output);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_SideEffect()
        {
            await SyncContext.ClearAsync;

            // Verify that SideEffect() and SideEffect<T>() work.

            Assert.Equal("test1", await client.NewWorkflowStub<IWorkflowSideEffect>().SideEffectAsync("test1"));
            Assert.Equal("test2", await client.NewWorkflowStub<IWorkflowSideEffect>().GenericSideEffectAsync("test2"));
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowMutableSideEffect : IWorkflow
        {
            [WorkflowMethod(Name = "MutableSideEffect")]
            Task<string> MutableSideEffectAsync(string id, string input);

            [WorkflowMethod(Name = "GenericSideEffect")]
            Task<string> GenericMutableSideEffectAsync(string id, string input);
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowMutableSideEffect : WorkflowBase, IWorkflowMutableSideEffect
        {
            public async Task<string> MutableSideEffectAsync(string id, string input)
            {
                var output = (string)await Workflow.MutableSideEffectAsync(typeof(string), id, () => input);

                return await Task.FromResult(output);
            }

            public async Task<string> GenericMutableSideEffectAsync(string id, string input)
            {
                var output = await Workflow.MutableSideEffectAsync<string>(id, () => input);

                return await Task.FromResult(output);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_MutableSideEffect()
        {
            await SyncContext.ClearAsync;

            // Verify that MutableSideEffect() and MutableSideEffect<T>() work.

            Assert.Equal("test1", await client.NewWorkflowStub<IWorkflowMutableSideEffect>().MutableSideEffectAsync("id-1", "test1"));
            Assert.Equal("test2", await client.NewWorkflowStub<IWorkflowMutableSideEffect>().GenericMutableSideEffectAsync("id-2", "test2"));
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowSignal : IWorkflow
        {
            [WorkflowMethod]
            Task<List<string>> RunAsync(TimeSpan timeout, int expectedSignals);

            [SignalMethod("signal")]
            Task SignalAsync(string message);
        }

        /// <summary>
        /// This workflow tests basic signal reception by waiting for some number of signals
        /// and then returning the received signal messages.  The workflow will timeout
        /// if the signals aren't received in time.  Note that we've hacked workflow start
        /// detection using a static field.
        /// </summary>
        [Workflow(AutoRegister = true)]
        public class WorkflowSignal : WorkflowBase, IWorkflowSignal
        {
            //-----------------------------------------------------------------
            // Static members

            public static bool HasStarted { get; private set; } = false;

            public static new void Reset()
            {
                HasStarted = false;
            }

            //-----------------------------------------------------------------
            // Instance members

            private List<string> signalMessages = new List<string>();

            public async Task<List<string>> RunAsync(TimeSpan timeout, int expectedSignals)
            {
                HasStarted = true;

                var timeoutUtc = await Workflow.UtcNowAsync() + timeout;

                while (await Workflow.UtcNowAsync() < timeoutUtc)
                {
                    if (signalMessages.Count >= expectedSignals)
                    {
                        return signalMessages;
                    }

                    await Workflow.SleepAsync(TimeSpan.FromSeconds(1));
                }

                throw new TimeoutException("Timeout waiting for signal(s).");
            }

            public async Task SignalAsync(string message)
            {
                signalMessages.Add(message);

                await Task.CompletedTask;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_SignalOnce()
        {
            await SyncContext.ClearAsync;

            WorkflowSignal.Reset();

            var stub = client.NewWorkflowStub<IWorkflowSignal>();
            var task = stub.RunAsync(TimeSpan.FromSeconds(maxWaitSeconds), expectedSignals: 1);

            NeonHelper.WaitFor(() => WorkflowSignal.HasStarted, workflowTimeout);

            await stub.SignalAsync("my-signal-1");

            Assert.Equal(new List<string>() { "my-signal-1" }, await task);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_SignalTwice()
        {
            await SyncContext.ClearAsync;
            WorkflowSignal.Reset();

            var stub = client.NewWorkflowStub<IWorkflowSignal>();
            var task = stub.RunAsync(TimeSpan.FromSeconds(maxWaitSeconds), expectedSignals: 2);

            NeonHelper.WaitFor(() => WorkflowSignal.HasStarted, workflowTimeout);

            await stub.SignalAsync("my-signal-1");
            await stub.SignalAsync("my-signal-2");

            var results = await task;

            Assert.Equal(2, results.Count);
            Assert.Contains("my-signal-1", results);
            Assert.Contains("my-signal-2", results);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_SignalBeforeStart()
        {
            await SyncContext.ClearAsync;

            // Verify that we're not allowed to send a signal via a
            // stub before we started the workflow.

            WorkflowSignal.Reset();

            var stub = client.NewWorkflowStub<IWorkflowSignal>();

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await stub.SignalAsync("my-signal"));
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowQuery : IWorkflow
        {
            [WorkflowMethod]
            Task<List<string>> RunAsync(TimeSpan timeout, int expectedQueries);

            [QueryMethod("query")]
            Task<string> QueryAsync(string arg1, int arg2);

            [QueryMethod("query-no-result")]
            Task QueryNoResultAsync(string arg1, int arg2);
        }

        /// <summary>
        /// This workflow tests basic query reception by waiting for some number of queries
        /// and then returning the generated query results.  The queries will return the 
        /// parameters converted to strings and separated with a colon.  The workflow will
        /// timeout if the queries aren't received in time.  Note that we've hacked workflow
        /// start detection using a static field.
        /// </summary>
        [Workflow(AutoRegister = true)]
        public class WorkflowQuery : WorkflowBase, IWorkflowQuery
        {
            //-----------------------------------------------------------------
            // Static members

            public static bool HasStarted { get; private set; } = false;

            public static new void Reset()
            {
                HasStarted = false;
            }

            //-----------------------------------------------------------------
            // Instance members

            private List<string> queryResults = new List<string>();

            public async Task<List<string>> RunAsync(TimeSpan timeout, int expectedQueries)
            {
                HasStarted = true;

                var timeoutUtc = await Workflow.UtcNowAsync() + timeout;

                while (await Workflow.UtcNowAsync() < timeoutUtc)
                {
                    if (queryResults.Count >= expectedQueries)
                    {
                        return queryResults;
                    }

                    await Workflow.SleepAsync(TimeSpan.FromSeconds(1));
                }

                throw new CadenceTimeoutException("Timeout waiting for query(s).");
            }

            public async Task<string> QueryAsync(string arg1, int arg2)
            {
                var result = $"{arg1}:{arg2}";

                queryResults.Add(result);

                return await Task.FromResult(result);
            }

            public async Task QueryNoResultAsync(string arg1, int arg2)
            {
                var result = $"{arg1}:{arg2}";

                queryResults.Add(result);

                await Task.CompletedTask;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_QueryOnce()
        {
            await SyncContext.ClearAsync;
            WorkflowQuery.Reset();

            var stub = client.NewWorkflowStub<IWorkflowQuery>();
            var task = stub.RunAsync(TimeSpan.FromSeconds(maxWaitSeconds), expectedQueries: 1);

            NeonHelper.WaitFor(() => WorkflowQuery.HasStarted, workflowTimeout);

            Assert.Equal("my-query:1", await stub.QueryAsync("my-query", 1));
            Assert.Equal(new List<string>() { "my-query:1" }, await task);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_QueryTwice()
        {
            await SyncContext.ClearAsync;
            WorkflowQuery.Reset();

            var stub = client.NewWorkflowStub<IWorkflowQuery>();
            var task = stub.RunAsync(TimeSpan.FromSeconds(maxWaitSeconds), expectedQueries: 2);

            NeonHelper.WaitFor(() => WorkflowQuery.HasStarted, workflowTimeout);

            Assert.Equal("my-query:1", await stub.QueryAsync("my-query", 1));
            Assert.Equal("my-query:2", await stub.QueryAsync("my-query", 2));

            var results = await task;

            Assert.Equal(2, results.Count);
            Assert.Contains("my-query:1", results);
            Assert.Contains("my-query:2", results);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_QueryNoResult()
        {
            await SyncContext.ClearAsync;
            WorkflowQuery.Reset();

            // Verify that we can call a query method that doesn't
            // return a result.

            var stub = client.NewWorkflowStub<IWorkflowQuery>();
            var task = stub.RunAsync(TimeSpan.FromSeconds(maxWaitSeconds), expectedQueries: 1);

            NeonHelper.WaitFor(() => WorkflowQuery.HasStarted, workflowTimeout);

            await stub.QueryNoResultAsync("my-query", 1);
            Assert.Equal(new List<string>() { "my-query:1" }, await task);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_QueryBeforeStart()
        {
            await SyncContext.ClearAsync;
            WorkflowQuery.Reset();

            // Verify that we're not allowed to submit a query via a
            // stub before we started the workflow.

            var stub = client.NewWorkflowStub<IWorkflowQuery>();

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await stub.QueryAsync("my-query", 1));
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowGetVersion : IWorkflow
        {
            [WorkflowMethod]
            Task<int> RunAsync(string changeId, int minVersion, int maxVersion);
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowGetVersion : WorkflowBase, IWorkflowGetVersion
        {
            public async Task<int> RunAsync(string changeId, int minVersion, int maxVersion)
            {
                return await Workflow.GetVersionAsync(changeId, minVersion, maxVersion);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_GetVersion()
        {
            await SyncContext.ClearAsync;
            WorkflowQuery.Reset();

            // Minimally exercise the workflow GetVersion() API.

            var stub = client.NewWorkflowStub<IWorkflowGetVersion>();

            Assert.Equal(1, await stub.RunAsync("my-change-id", Workflow.DefaultVersion, 1));
        }

        //---------------------------------------------------------------------

        [ActivityInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IChildActivity : IActivity
        {
            [ActivityMethod]
            Task<string> HelloAsync(string name);
        }

        [Activity(AutoRegister = true)]
        public class ChildActivity : ActivityBase, IChildActivity
        {
            public async Task<string> HelloAsync(string name)
            {
                return await Task.FromResult($"Hello {name}!");
            }
        }

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowComplex : IWorkflow
        {
            [WorkflowMethod]
            Task<List<string>> WaitForQueriesAndSignalsAsync(TimeSpan timeout, int expectedOperations);

            [WorkflowMethod(Name = "hello")]
            Task<string> HelloAsync(string name);

            [WorkflowMethod(Name = "goodbye")]
            Task<string> GoodbyeAsync(string name);

            [QueryMethod("query-1")]
            Task<string> Query1Async(string arg1);

            [QueryMethod("query-2")]
            Task<string> Query2Async(string arg1);

            [SignalMethod("signal-1")]
            Task Signal1Async(string arg1);

            [SignalMethod("signal-2")]
            Task Signal2Async(string arg1);
        }

        /// <summary>
        /// This workflow is used to verify that complex workflows with multiple
        /// entry point, query, and signal methods works.
        /// </summary>
        [Workflow(AutoRegister = true)]
        public class WorkflowComplex : WorkflowBase, IWorkflowComplex
        {
            //-----------------------------------------------------------------
            // Static members

            public static bool HasStarted { get; private set; } = false;

            public static new void Reset()
            {
                HasStarted = false;
            }

            //-----------------------------------------------------------------
            // Instance members

            private List<string> operations = new List<string>();

            public async Task<List<string>> WaitForQueriesAndSignalsAsync(TimeSpan timeout, int expectedOperations)
            {
                HasStarted = true;

                var timeoutUtc = await Workflow.UtcNowAsync() + timeout;

                while (await Workflow.UtcNowAsync() < timeoutUtc)
                {
                    if (operations.Count >= expectedOperations)
                    {
                        return operations;
                    }

                    await Workflow.SleepAsync(TimeSpan.FromSeconds(1));
                }

                throw new CadenceTimeoutException("Timeout waiting for queries and/or signals.");
            }

            public async Task<string> HelloAsync(string name)
            {
                return await Task.FromResult($"Hello {name}!");
            }

            public async Task<string> GoodbyeAsync(string name)
            {
                return await Task.FromResult($"Goodbye {name}!");
            }

            public async Task<string> Query1Async(string arg1)
            {
                var operation = $"query-1:{arg1}";

                operations.Add(operation);

                return await Task.FromResult(operation);
            }

            public async Task<string> Query2Async(string arg1)
            {
                var operation = $"query-2:{arg1}";

                operations.Add(operation);

                return await Task.FromResult(operation);
            }

            public async Task Signal1Async(string arg1)
            {
                var operation = $"signal-1:{arg1}";

                operations.Add(operation);

                await Task.CompletedTask;
            }

            public async Task Signal2Async(string arg1)
            {
                var operation = $"signal-2:{arg1}";

                operations.Add(operation);

                await Task.CompletedTask;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_Complex()
        {
            await SyncContext.ClearAsync;

            // Verify that we can start a workflow via different entry point methods.

            Assert.Equal("Hello Jeff!", await client.NewWorkflowStub<IWorkflowComplex>().HelloAsync("Jeff"));
            Assert.Equal("Goodbye Jeff!", await client.NewWorkflowStub<IWorkflowComplex>().GoodbyeAsync("Jeff"));

            // Verify that we can send different queries and signals to a workflow.

            WorkflowComplex.Reset();

            var stub = client.NewWorkflowStub<IWorkflowComplex>();
            var task = stub.WaitForQueriesAndSignalsAsync(TimeSpan.FromSeconds(maxWaitSeconds), expectedOperations: 4);

            NeonHelper.WaitFor(() => WorkflowComplex.HasStarted, workflowTimeout);

            Assert.Equal("query-1:my-query-1", await stub.Query1Async("my-query-1"));
            await stub.Signal1Async("my-signal-1");
            Assert.Equal("query-2:my-query-2", await stub.Query2Async("my-query-2"));
            await stub.Signal2Async("my-signal-2");

            var results = await task;

            Assert.Equal(4, results.Count);
            Assert.Contains("query-1:my-query-1", results);
            Assert.Contains("signal-1:my-signal-1", results);
            Assert.Contains("query-2:my-query-2", results);
            Assert.Contains("signal-2:my-signal-2", results);
        }

        //---------------------------------------------------------------------

        [ActivityInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IParallelActivity : IActivity
        {
            [ActivityMethod]
            Task RunAsync();

            [ActivityMethod(Name = "hello")]
            Task<string> HelloAsync(string name);
        }

        [Activity(AutoRegister = true)]
        public class ParallelActivity : ActivityBase, IParallelActivity
        {
            public async Task RunAsync()
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            public async Task<string> HelloAsync(string name)
            {
                return await Task.FromResult($"Hello {name}!");
            }
        }

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowChild : IWorkflow
        {
            [WorkflowMethod]
            Task RunAsync();

            [WorkflowMethod(Name = "hello")]
            Task<string> HelloAsync(string name);

            [WorkflowMethod(Name = "hello-activity")]
            Task<string> HelloActivityAsync(string name);

            [WorkflowMethod(Name = "nested-hello")]
            Task<string> NestedHelloAsync(string name);

            [WorkflowMethod(Name = "wait-for-signal")]
            Task WaitForSignalAsync();

            [WorkflowMethod(Name = "wait-for-query")]
            Task WaitForQueryAsync();

            [QueryMethod("query")]
            Task<string> QueryAsync(string value);

            [SignalMethod("signal")]
            Task SignalAsync(string value);
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowChild : WorkflowBase, IWorkflowChild
        {
            //-----------------------------------------------------------------
            // Static members

            public static bool          WasExecuted     = false;
            public static bool          ExitNow         = false;
            public static List<string>  ReceivedQueries = new List<string>();
            public static List<string>  ReceivedSignals = new List<string>();

            public static new void Reset()
            {
                WasExecuted = false;
                ExitNow     = false;

                ReceivedQueries.Clear();
                ReceivedSignals.Clear();
            }

            //-----------------------------------------------------------------
            // Instance members

            public async Task RunAsync()
            {
                WasExecuted = true;

                await Task.CompletedTask;
            }

            public async Task<string> HelloAsync(string name)
            {
                WasExecuted = true;

                return await Task.FromResult($"Hello {name}!");
            }

            public async Task<string> HelloActivityAsync(string name)
            {
                WasExecuted = true;

                return await Workflow.NewActivityStub<IChildActivity>().HelloAsync("Jeff");
            }

            public async Task<string> NestedHelloAsync(string name)
            {
                WasExecuted = true;

                var childStub = Workflow.NewChildWorkflowStub<IWorkflowChild>();

                return await childStub.HelloAsync(name);
            }

            public async Task WaitForSignalAsync()
            {
                WasExecuted = true;

                var maxWaitTimeUtc = await Workflow.UtcNowAsync() + TimeSpan.FromSeconds(maxWaitSeconds);

                while (ReceivedSignals.Count == 0 && !ExitNow)
                {
                    if (await Workflow.UtcNowAsync() >= maxWaitTimeUtc)
                    {
                        throw new TimeoutException("Timeout waiting for signal.");
                    }

                    await Workflow.SleepAsync(TimeSpan.FromSeconds(1));
                }
            }

            public async Task WaitForQueryAsync()
            {
                WasExecuted = true;

                var maxWaitTimeUtc = await Workflow.UtcNowAsync() + TimeSpan.FromSeconds(maxWaitSeconds);

                while (ReceivedQueries.Count == 0 && !ExitNow)
                {
                    if (await Workflow.UtcNowAsync() >= maxWaitTimeUtc)
                    {
                        throw new TimeoutException("Timeout waiting for query.");
                    }

                    await Workflow.SleepAsync(TimeSpan.FromSeconds(1));
                }
            }

            public async Task<string> QueryAsync(string value)
            {
                ReceivedQueries.Add(value);

                return await Task.FromResult(value);
            }

            public async Task SignalAsync(string value)
            {
                ReceivedSignals.Add(value);

                await Task.CompletedTask;
            }
        }

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowParent : IWorkflow
        {
            [WorkflowMethod]
            Task RunChildAsync();

            [WorkflowMethod(Name = "start-child")]
            Task StartChildAsync();

            [WorkflowMethod(Name = "hello")]
            Task<string> HelloChildAsync(string name);

            [WorkflowMethod(Name = "future-hello")]
            Task<string> FutureHelloChildAsync(string name);

            [WorkflowMethod(Name = "hello-activity")]
            Task<string> HelloChildActivityAsync(string name);

            [WorkflowMethod(Name = "nested-hello")]
            Task<string> NestedHelloChildAsync(string name);

            [WorkflowMethod(Name = "signal-child")]
            Task SignalChildAsync(string signal);

            [WorkflowMethod(Name = "query-child")]
            Task<bool> QueryChildAsync();

            [WorkflowMethod(Name = "future-activity-noargsresult")]
            Task<bool> FutureActivity_NoArgsResult();

            [WorkflowMethod(Name = "future-local-activity-noargsresult")]
            Task<bool> FutureLocalActivity_NoArgsResult();

            [WorkflowMethod(Name = "future-activity-argsresult")]
            Task<bool> FutureActivity_ArgsResult();

            [WorkflowMethod(Name = "future-local-activity-argsresult")]
            Task<bool> FutureLocalActivity_ArgsResult();

            [WorkflowMethod(Name = "parallel-activity")]
            Task<bool> ParallelActivity();

            [WorkflowMethod(Name = "parallel-local-activity")]
            Task<bool> ParallelLocalActivity();
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowParent : WorkflowBase, IWorkflowParent
        {
            public async Task RunChildAsync()
            {
                var childStub = Workflow.NewChildWorkflowStub<IWorkflowChild>();

                await childStub.RunAsync();
            }

            public async Task StartChildAsync()
            {
                // We're not specifying a method name when we create the child stub below
                // which means we'll be calling the [RunAsync()] method which also has
                // no defined workflow method name.

                var childStub = Workflow.NewChildWorkflowFutureStub<IWorkflowChild>();
                var future    = await childStub.StartAsync();
                
                await future.GetAsync();
            }

            public async Task<string> HelloChildAsync(string name)
            {
                var childStub = Workflow.NewChildWorkflowStub<IWorkflowChild>();

                return await childStub.HelloAsync(name);
            }

            public async Task<string> FutureHelloChildAsync(string name)
            {
                var childStub = Workflow.NewChildWorkflowFutureStub<IWorkflowChild>("hello");
                var future    = await childStub.StartAsync<string>(name);
                
                return await future.GetAsync();
            }

            public async Task<string> HelloChildActivityAsync(string name)
            {
                var childStub = Workflow.NewChildWorkflowStub<IWorkflowChild>();

                return await childStub.HelloActivityAsync(name);
            }

            public async Task<string> NestedHelloChildAsync(string name)
            {
                var childStub = Workflow.NewChildWorkflowStub<IWorkflowChild>();

                return await childStub.NestedHelloAsync(name);
            }

            public async Task SignalChildAsync(string signal)
            {
                WorkflowChild.Reset();

                var childStub = Workflow.NewChildWorkflowFutureStub<IWorkflowChild>("wait-for-signal");

                Assert.Null(childStub.Stub);        // This will be NULL until we start the child.

                var future = await childStub.StartAsync();

                NeonHelper.WaitFor(() => WorkflowChild.WasExecuted, TimeSpan.FromSeconds(maxWaitSeconds));

                Assert.NotNull(childStub.Stub);     // This should be set after the workflow starts.

                await childStub.Stub.SignalAsync(signal);
                await future.GetAsync();
            }

            public async Task<bool> QueryChildAsync()
            {
                // Verify that we can query a child workflow.

                var childStub      = Workflow.NewChildWorkflowStub<IWorkflowChild>();
                var task           = childStub.WaitForQueryAsync();
                var maxWaitTimeUtc = DateTime.UtcNow + TimeSpan.FromSeconds(maxWaitSeconds);
                var pass           = false;

                while (!WorkflowChild.WasExecuted)
                {
                    if (DateTime.UtcNow >= maxWaitTimeUtc)
                    {
                        throw new TimeoutException("Timeout waiting for child execution.");
                    }

                    System.Threading.Thread.Sleep(1000);
                }

                pass = await childStub.QueryAsync("test") == "test";

                WorkflowChild.ExitNow = true;

                await task;

                return pass;
            }

            public async Task<bool> FutureActivity_NoArgsResult()
            {
                // Execute an activity with no parameters or a result using a future.

                var runActivityStub   = Workflow.NewActivityFutureStub<IParallelActivity>();
                var runActivityFuture = await runActivityStub.StartAsync();

                await runActivityFuture.GetAsync();

                return true;
            }

            public async Task<bool> FutureLocalActivity_NoArgsResult()
            {
                // Execute a local activity with no parameters or a result using a future.

                var runActivityStub   = Workflow.NewStartLocalActivityStub<IParallelActivity, ParallelActivity>();
                var runActivityFuture = await runActivityStub.StartAsync();

                await runActivityFuture.GetAsync();

                return true;
            }

            public async Task<bool> FutureActivity_ArgsResult()
            {
                // Execute an activity with parameters and a result using a future.

                var helloActivityStub   = Workflow.NewActivityFutureStub<IParallelActivity>("hello");
                var helloActivityFuture = await helloActivityStub.StartAsync<string>("Jeff");
                var greeting            = await helloActivityFuture.GetAsync();

                return greeting == "Hello Jeff!";
            }

            public async Task<bool> FutureLocalActivity_ArgsResult()
            {
                // Execute a local activity with parameters and a result using a future.

                var helloActivityStub   = Workflow.NewStartLocalActivityStub<IParallelActivity, ParallelActivity>("hello");
                var helloActivityFuture = await helloActivityStub.StartAsync<string>("Jeff");
                var greeting            = await helloActivityFuture.GetAsync();

                return greeting == "Hello Jeff!";
            }

            public async Task<bool> ParallelActivity()
            {
                // Execute two activities in parallel.  This exercises activities with
                // and without parameters or results.

                var runActivityStub     = Workflow.NewActivityFutureStub<IParallelActivity>();
                var helloActivityStub   = Workflow.NewActivityFutureStub<IParallelActivity>("hello");
                var runActivityFuture   = await runActivityStub.StartAsync();
                var helloActivityFuture = await helloActivityStub.StartAsync<string>("Jeff");

                var greeting = await helloActivityFuture.GetAsync();

                if (greeting != "Hello Jeff!")
                {
                    return false;
                }

                await runActivityFuture.GetAsync();

                return true;
            }

            public async Task<bool> ParallelLocalActivity()
            {
                // Execute two local activities in parallel.  This exercises activities with
                // and without parameters or results.

                var runActivityStub     = Workflow.NewStartLocalActivityStub<IParallelActivity, ParallelActivity>();
                var helloActivityStub   = Workflow.NewStartLocalActivityStub<IParallelActivity, ParallelActivity>("hello");
                var runActivityFuture   = await runActivityStub.StartAsync();
                var helloActivityFuture = await helloActivityStub.StartAsync<string>("Jeff");

                var greeting = await helloActivityFuture.GetAsync();

                if (greeting != "Hello Jeff!")
                {
                    return false;
                }

                await runActivityFuture.GetAsync();

                return true;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_Child()
        {
            await SyncContext.ClearAsync;

            // Verify that we can call a child workflow that doesn't return a result.

            WorkflowChild.Reset();

            var stub = client.NewWorkflowStub<IWorkflowParent>();

            await stub.RunChildAsync();
            Assert.True(WorkflowChild.WasExecuted);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_ChildHello()
        {
            await SyncContext.ClearAsync;

            // Verify that we can call a child workflow that accepts a
            // parameter and returns a result.

            WorkflowChild.Reset();

            var stub = client.NewWorkflowStub<IWorkflowParent>();

            Assert.Equal("Hello Jeff!", await stub.HelloChildAsync("Jeff"));
            Assert.True(WorkflowChild.WasExecuted);

            WorkflowChild.WasExecuted = false;

            var stub2 = client.NewWorkflowStub<IWorkflowParent>();

            Assert.Equal("Hello Jeff!", await stub2.HelloChildAsync("Jeff"));
            Assert.True(WorkflowChild.WasExecuted);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_FutureChild_NoArgsOrResult ()
        {
            await SyncContext.ClearAsync;

            // Verify that we can run a child workflow via a future that 
            // accepts no args and doesn't return a result.  This also tests
            // calling the the workflow entrypoint with the default entrypoint
            // method name (null).

            WorkflowChild.Reset();

            var stub = client.NewWorkflowStub<IWorkflowParent>();

            await stub.StartChildAsync();

            Assert.True(WorkflowChild.WasExecuted);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_FutureChild_ArgsAndResult()
        {
            await SyncContext.ClearAsync;

            // Verify that we can run a child workflow via a future that 
            // accepts a parameter and returns a result.

            WorkflowChild.Reset();

            var stub = client.NewWorkflowStub<IWorkflowParent>();

            Assert.Equal("Hello Jeff!", await stub.FutureHelloChildAsync("Jeff"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_ChildActivity()
        {
            await SyncContext.ClearAsync;

            // Verify that we can call a child workflow that calls an activity.

            WorkflowChild.Reset();

            var stub = client.NewWorkflowStub<IWorkflowParent>();

            Assert.Equal("Hello Jeff!", await stub.HelloChildActivityAsync("Jeff"));
            Assert.True(WorkflowChild.WasExecuted);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_ChildSignal()
        {
            await SyncContext.ClearAsync;

            // Verify that we can signal a child workflow.

            WorkflowChild.Reset();

            var stub = client.NewWorkflowStub<IWorkflowParent>();

            await stub.SignalChildAsync("my-signal");

            Assert.Single(WorkflowChild.ReceivedSignals);
            Assert.Contains("my-signal", WorkflowChild.ReceivedSignals);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_ChildQuery()
        {
            await SyncContext.ClearAsync;

            // Verify that querying a child workflow works.

            WorkflowChild.Reset();

            var stub = client.NewWorkflowStub<IWorkflowParent>();
            var pass = await stub.QueryChildAsync();

            Assert.True(pass);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_ChildNested()
        {
            await SyncContext.ClearAsync;

            // Test calling a workflow that calls a child which
            // calls another child.

            WorkflowChild.Reset();

            var stub = client.NewWorkflowStub<IWorkflowParent>();

            Assert.Equal("Hello Jeff!", await stub.NestedHelloChildAsync("Jeff"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_FutureActivity_NoArgsResult()
        {
            await SyncContext.ClearAsync;

            // Test calling a workflow that uses a future to call an
            // activity with no parameters or result.

            var stub = client.NewWorkflowStub<IWorkflowParent>();

            Assert.True(await stub.FutureActivity_NoArgsResult());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_FutureLocalActivity_NoArgsResult()
        {
            await SyncContext.ClearAsync;

            // Test calling a workflow that uses a future to call a
            // local activity with no parameters or result.

            var stub = client.NewWorkflowStub<IWorkflowParent>();

            Assert.True(await stub.FutureLocalActivity_NoArgsResult());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_FutureActivity_ArgsResult()
        {
            await SyncContext.ClearAsync;

            // Test calling a workflow that uses a future to call an
            // activity with parameters and a result.

            var stub = client.NewWorkflowStub<IWorkflowParent>();

            Assert.True(await stub.FutureActivity_ArgsResult());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_FutureLocalActivity_ArgsResult()
        {
            await SyncContext.ClearAsync;

            // Test calling a workflow that uses a future to call a
            // local activity with parameters and a result.

            var stub = client.NewWorkflowStub<IWorkflowParent>();

            Assert.True(await stub.FutureLocalActivity_ArgsResult());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_ParallelActivity()
        {
            await SyncContext.ClearAsync;

            // Test calling a workflow that runs two activities in parallel.

            var stub = client.NewWorkflowStub<IWorkflowParent>();

            Assert.True(await stub.ParallelActivity());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_ParallelLocalActivity()
        {
            await SyncContext.ClearAsync;

            // Test calling a workflow that runs two activities in parallel.

            var stub = client.NewWorkflowStub<IWorkflowParent>();

            Assert.True(await stub.ParallelLocalActivity());
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowDifferentNamesInterface : IWorkflow
        {
            [WorkflowMethod]
            Task<string> HelloAsync(string name);
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowDifferentNamesClass : WorkflowBase, IWorkflowDifferentNamesInterface
        {
            public async Task<string> HelloAsync(string name)
            {
                return await Task.FromResult($"Hello {name}!");
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_DifferentNames()
        {
            await SyncContext.ClearAsync;

            // Verify that a workflow whose class and interface names
            // don't match works.  This ensures that the Cadence client
            // doesn't make any assumptions about naming conventions.
            //
            // ...which was happening in earlier times.

            var stub = client.NewWorkflowStub<IWorkflowDifferentNamesInterface>();

            Assert.Equal($"Hello Jeff!", await stub.HelloAsync("Jeff"));
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowFail : IWorkflow
        {
            [WorkflowMethod]
            Task RunAsync();
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowFail : WorkflowBase, IWorkflowFail
        {
            public async Task RunAsync()
            {
                await Task.CompletedTask;
                throw new ArgumentException("forced-failure");
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_Fail()
        {
            await SyncContext.ClearAsync;

            // Verify that we see an exception thrown by a workflow.

            var options = new WorkflowOptions()
            {
                DecisionTaskTimeout = TimeSpan.FromSeconds(5)
            };

            var stub = client.NewWorkflowStub<IWorkflowFail>(options);

            try
            {
                await stub.RunAsync();
                Assert.True(false, $"[{nameof(CadenceCustomException)}] expected");
            }
            catch (CadenceCustomException e)
            {
                Assert.Contains("ArgumentException", e.Reason);
                Assert.Contains("forced-failure", e.Message);
            }
            catch (Exception e)
            {
                Assert.True(false, $"Expected [{nameof(CadenceCustomException)}] not [{e.GetType().Name}]");
            }
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowUnregistered : IWorkflow
        {
            [WorkflowMethod]
            Task<string> HelloAsync(string name);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_Unregistered()
        {
            await SyncContext.ClearAsync;

            // Verify that we see an error when attempting to execute an
            // unregistered workflow.  In this case, there is no class
            // defined that implements the workflow.

            var options = new WorkflowOptions()
            {
                StartToCloseTimeout = TimeSpan.FromSeconds(5)
            };

            var stub = client.NewWorkflowStub<IWorkflowUnregistered>(options);

            await Assert.ThrowsAsync<StartToCloseTimeoutException>(async () => await stub.HelloAsync("Jack"));
        }

        //---------------------------------------------------------------------

        public class ComplexData
        {
            public string Name { get; set; }
            public int Age { get; set; }
        }

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowComplexData : IWorkflow
        {
            [WorkflowMethod]
            Task<ComplexData> RunAsync(ComplexData data);
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowComplexDataClass : WorkflowBase, IWorkflowComplexData
        {
            public async Task<ComplexData> RunAsync(ComplexData data)
            {
                return await Task.FromResult(data);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_ComplexData()
        {
            await SyncContext.ClearAsync;

            // Verify that we can pass and return a complex object to/from
            // a workflow.

            var data = new ComplexData
            {
                Name = "Jeff",
                Age  = 58
            };

            var stub   = client.NewWorkflowStub<IWorkflowComplexData>();
            var result = await stub.RunAsync(data);

            Assert.Equal(data.Name, result.Name);
            Assert.Equal(data.Age, result.Age);
        }

        //---------------------------------------------------------------------

        /// <summary>
        /// We need to convert the <see cref="WorkflowInfo"/> to this type
        /// because the <see cref="WorkflowInfo"/> properties have <c>internal</c>
        /// setters which will cause deserialization to fail.
        /// </summary>
        public class WorkflowInfoTest
        {
            /// <summary>
            /// Default constructor.
            /// </summary>
            public WorkflowInfoTest()
            {
            }

            /// <summary>
            /// Constructs and instance from a <see cref="WorkflowInfo"/>.
            /// </summary>
            /// <param name="info"></param>
            public WorkflowInfoTest(WorkflowInfo info)
            {
                this.Domain       = info.Domain;
                this.WorkflowId   = info.WorkflowId;
                this.RunId        = info.RunId;
                this.WorkflowType = info.WorkflowType;
                this.TaskList     = info.TaskList;
            }

            public string Domain { get; set; }
            public string WorkflowId { get; set; }
            public string RunId { get; set; }
            public string WorkflowType { get; set; }
            public string TaskList { get; set; }
            public string ExecutionWorkflowId { get; set; }
            public string ExecutionRunId { get; set; }
        }

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowInfo : IWorkflow
        {
            [WorkflowMethod]
            Task<WorkflowInfoTest> GetWorkflowInfoAsync();
        }

        [Workflow(AutoRegister = true, Name = "my-workflow-info-type")]
        public class WorkflowInfoClass : WorkflowBase, IWorkflowInfo
        {
            public async Task<WorkflowInfoTest> GetWorkflowInfoAsync()
            {
                var info = new WorkflowInfoTest(Workflow.WorkflowInfo);

                info.ExecutionWorkflowId = Workflow.Execution.WorkflowId;
                info.ExecutionRunId      = Workflow.Execution.RunId;

                return await Task.FromResult(info);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_Info()
        {
            await SyncContext.ClearAsync;

            // Verify that the [Workflow.WorkflowInfo] properties are
            // set correctly for a workflow.

            var options = new WorkflowOptions()
            {
                Domain     = client.Settings.DefaultDomain,
                WorkflowId = "my-workflow-id"
            };

            var stub = client.NewWorkflowStub<IWorkflowInfo>(options: options, workflowTypeName: "my-workflow-info-type");
            var info = await stub.GetWorkflowInfoAsync();

            Assert.Equal(options.Domain, info.Domain);
            Assert.NotEmpty(info.RunId);
            Assert.Equal(CadenceTestHelper.TaskList, info.TaskList);
            Assert.Equal(options.WorkflowId, info.WorkflowId);
            Assert.Equal("my-workflow-info-type", info.WorkflowType);
            Assert.Equal(options.WorkflowId, info.ExecutionWorkflowId);
            Assert.NotEmpty(info.ExecutionRunId);

            // $todo(jefflill):
            //
            // These properties are not supported yet:
            //
            //      ExecutionStartToCloseTimeout
            //      ChildPolicy
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowContinueAsNew0 : IWorkflow
        {
            [WorkflowMethod(Name = "Hello")]
            Task<string> HelloAsync(string name, int callCount);

            [WorkflowMethod(Name = "HelloWithOptions")]
            Task<string> HelloNewOptionsAsync(string name, int callCount);

            [WorkflowMethod(Name = "HelloStub")]
            Task<string> HelloStubAsync(string name);

            [WorkflowMethod(Name = "HelloStubOptions")]
            Task<string> HelloStubOptionsAsync(string name);
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowContinueAsNew0 : WorkflowBase, IWorkflowContinueAsNew0
        {
            public async Task<string> HelloAsync(string name, int callCount)
            {
                // The first time this is called, we're going to continue the workflow
                // as new using the current options and passing the same name but
                // incrementing the call count.
                //
                // Otherwise, we'll simply return the result string.

                if (callCount == 1)
                {
                    await Workflow.ContinueAsNewAsync(name, callCount + 1);
                    throw new Exception("We should never reach this.");
                }

                return await Task.FromResult($"WF0 says: Hello {name}!");
            }

            public async Task<string> HelloNewOptionsAsync(string name, int callCount)
            {
                var options = new ContinueAsNewOptions();

                // The first time this is called, we're going to continue the workflow
                // as new using new options and passing the same name but incrementing
                // the call count.
                //
                // Otherwise, we'll simply return the result string.

                if (callCount == 1)
                {
                    await Workflow.ContinueAsNewAsync(options, name, callCount + 1);
                    throw new Exception("We should never reach this.");
                }

                return await Task.FromResult($"WF0 says: Hello {name}!");
            }

            public async Task<string> HelloStubAsync(string name)
            {
                // We're going to continue as IWorkflowContinueAsNew1 using a stub.

                var stub = Workflow.NewContinueAsNewStub<IWorkflowContinueAsNew1>();

                await stub.HelloAsync(name);
                throw new Exception("We should never reach this.");
            }

            public async Task<string> HelloStubOptionsAsync(string name)
            {
                // We're going to continue as IWorkflowContinueAsNew1 using a stub
                // and with new options.

                var options  = new ContinueAsNewOptions() 
                {
                    Workflow = "TestCadence.Test_EndToEnd.WorkflowContinueAsNew1"
                };
                var stub     = Workflow.NewContinueAsNewStub<IWorkflowContinueAsNew1>(options);
                await stub.HelloAsync(name);
                throw new Exception("We should never reach this.");
            }
        }

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowContinueAsNew1 : IWorkflow
        {
            [WorkflowMethod]
            Task<string> HelloAsync(string name);
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowContinueAsNew1 : WorkflowBase, IWorkflowContinueAsNew1
        {
            public async Task<string> HelloAsync(string name)
            {
                return await Task.FromResult($"WF1 says: Hello {name}!");
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_ContinueAsNew()
        {
            await SyncContext.ClearAsync;

            // Verify that we can continue a workflow as new without using a stub
            // and with the same options.

            var stub = client.NewWorkflowStub<IWorkflowContinueAsNew0>();

            Assert.Equal("WF0 says: Hello Jeff!", await stub.HelloAsync("Jeff", 1));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_ContinueAsNew_Options()
        {
            await SyncContext.ClearAsync;

            // Verify that we can continue a workflow as new without using a stub
            // and with new options.

            // $todo(jefflill):
            //
            // This test could be improved.  We're not actually verifying that
            // the new options actually had an effect.  For now, we're just
            // ensuring that the client doesn't barf.

            var stub = client.NewWorkflowStub<IWorkflowContinueAsNew0>();

            Assert.Equal("WF0 says: Hello Jeff!", await stub.HelloNewOptionsAsync("Jeff", 1));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_ContinueAsNew_Stub()
        {
            await SyncContext.ClearAsync;

            // Verify that we can continue a workflow as new using a stub
            // and with the same options.

            var stub = client.NewWorkflowStub<IWorkflowContinueAsNew0>();

            Assert.Equal("WF1 says: Hello Jeff!", await stub.HelloStubAsync("Jeff"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_ContinueAsNew_StubOptions()
        {
            await SyncContext.ClearAsync;

            // Verify that we can continue a workflow as new using a stub
            // and with new options.

            // $todo(jefflill):
            //
            // This test could be improved.  We're not actually verifying that
            // the new options actually had an effect.  For now, we're just
            // ensuring that the client doesn't barf.

            var stub = client.NewWorkflowStub<IWorkflowContinueAsNew0>();

            Assert.Equal("WF1 says: Hello Jeff!", await stub.HelloStubOptionsAsync("Jeff"));
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowExternalStub : IWorkflow
        {
            [WorkflowMethod(Name = "run")]
            Task RunAsync();

            [WorkflowMethod(Name = "hello")]
            Task<string> HelloAsync(string name);

            [WorkflowMethod(Name = "hello-test-byid-noresult")]
            Task<bool> HelloTestByIdNoResultAsync();

            [WorkflowMethod(Name = "hello-test-byid-withresult")]
            Task<bool> HelloTestByIdWithResultAsync();

            [WorkflowMethod(Name = "hello-test-byexecution-noresult")]
            Task<bool> HelloTestByExecutionNoResultAsync();

            [WorkflowMethod(Name = "hello-test-byexecution-withresult")]
            Task<bool> HelloTestByExecutionWithResultAsync();

            [WorkflowMethod(Name = "sleep")]
            Task<string> SleepAsync(int seconds, string message);

            [WorkflowMethod(Name = "wait-for-external")]
            Task<string> WaitForExternalAsync(WorkflowExecution execution);
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowExternalStub : WorkflowBase, IWorkflowExternalStub
        {
            public async Task RunAsync()
            {
                await Task.CompletedTask;
            }

            public async Task<string> HelloAsync(string name)
            {
                return await Task.FromResult($"Hello {name}!");
            }

            public async Task<bool> HelloTestByIdNoResultAsync()
            {
                // Start a child workflow using a typed stub and a specific workflow ID
                // and then create an external stub with the same ID and then verify
                // that we can wait for the workflow using the stub Task as well as
                // the external stub (without retrieving the result).

                var TestWorkflowId = "WorkflowExternalStub-HelloTestByIdNoResultAsync-" + Guid.NewGuid().ToString("d");
                var stub           = Workflow.NewChildWorkflowStub<IWorkflowExternalStub>(new ChildWorkflowOptions() { WorkflowId = TestWorkflowId });
                var task           = stub.RunAsync();
                var externalStub   = Workflow.NewExternalWorkflowStub(TestWorkflowId);

                // $hack(jefflill): 
                //
                // Wait a bit to allow the workflow to be recorded before
                // we wait for the result.

                await Task.Delay(TimeSpan.FromSeconds(5));

                await externalStub.GetResultAsync();
                await task;

                return true;
            }

            public async Task<bool> HelloTestByIdWithResultAsync()
            {
                // Start a child workflow using a typed stub and a specific workflow ID
                // and then create an external stub with the same ID and then verify
                // that we can wait for the workflow using the stub Task as well as
                // the external stub (retrieving the result).

                var TestWorkflowId = "WorkflowExternalStub-HelloTestByIdWithResultAsync-" + Guid.NewGuid().ToString("d");
                var stub           = Workflow.NewChildWorkflowStub<IWorkflowExternalStub>(new ChildWorkflowOptions() { WorkflowId = TestWorkflowId });
                var task           = stub.HelloAsync("Jeff");
                var externalStub   = Workflow.NewExternalWorkflowStub(TestWorkflowId);

                // $hack(jefflill): 
                //
                // Wait a bit to allow the workflow to be recorded before
                // we wait for the result.

                await Task.Delay(TimeSpan.FromSeconds(5));

                await externalStub.GetResultAsync();

                var result = await task;

                return result == "Hello Jeff!";
            }

            public async Task<bool> HelloTestByExecutionNoResultAsync()
            {
                // Start a child workflow using a typed stub and a specific workflow ID
                // and then create an external stub with the same ID and then verify
                // that we can wait for the workflow using the stub as well as the
                // external stub (without retrieving the result).

                var TestWorkflowId = "WorkflowExternalStub-HelloTestByIdNoResultAsync-" + Guid.NewGuid().ToString("d");
                var stub           = Workflow.NewChildWorkflowStub<IWorkflowExternalStub>(new ChildWorkflowOptions() { WorkflowId = TestWorkflowId });
                
                await stub.RunAsync();

                var externalStub = Workflow.NewExternalWorkflowStub((await WorkflowStub.FromTypedAsync(stub)).Execution);

                await externalStub.GetResultAsync();

                return true;
            }

            public async Task<bool> HelloTestByExecutionWithResultAsync()
            {
                // Start a child workflow using a typed stub and a specific workflow ID
                // and then create an external stub with the same ID and then verify
                // that we can wait for the workflow using the stub as well as the
                // external stub (retrieving the result).

                var TestWorkflowId = "WorkflowExternalStub-HelloTestByIdWithResultAsync-" + Guid.NewGuid().ToString("d");
                var stub           = Workflow.NewChildWorkflowStub<IWorkflowExternalStub>(new ChildWorkflowOptions() { WorkflowId = TestWorkflowId });
                var result1        = await stub.HelloAsync("Jeff");
                var externalStub   = Workflow.NewExternalWorkflowStub((await WorkflowStub.FromTypedAsync(stub)).Execution);
                var result2        = await externalStub.GetResultAsync<string>();

                return result1 == "Hello Jeff!" && result1 == result2;
            }

            public async Task<string> SleepAsync(int seconds, string message)
            {
                // This simply sleeps for the specified time and then returns
                // the message passed.

                await Workflow.SleepAsync(TimeSpan.FromSeconds(seconds));

                return message;
            }

            public async Task<string> WaitForExternalAsync(WorkflowExecution execution)
            {
                // We're going to wait for the workflow with the external ID 
                // and return its result.

                var externalStub = Workflow.NewExternalWorkflowStub(execution);

                return await externalStub.GetResultAsync<string>();
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_ExternalWorkflowStub_ById_NoResult()
        {
            await SyncContext.ClearAsync;

            // Call a workflow that executes a child workflow by ID using a typed 
            // stub and then creates an external stub and then waits for that as
            // well without retrieving the result.

            var stub = client.NewWorkflowStub<IWorkflowExternalStub>();

            Assert.True(await stub.HelloTestByIdNoResultAsync());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_ExternalWorkflowStub_ById_WithResult()
        {
            await SyncContext.ClearAsync;

            // Call a workflow that executes a child workflow by ID using a typed 
            // stub and then creates an external stub and then waits for that as
            // well without retrieving the result.

            var stub = client.NewWorkflowStub<IWorkflowExternalStub>();

            Assert.True(await stub.HelloTestByIdWithResultAsync());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_ExternalWorkflowStub_ByExecution_NoResult()
        {
            await SyncContext.ClearAsync;

            // Call a workflow that executes a child workflow by execution using a typed 
            // stub and then creates an external stub and then waits for that as
            // well without retrieving the result.

            var stub = client.NewWorkflowStub<IWorkflowExternalStub>();

            Assert.True(await stub.HelloTestByExecutionNoResultAsync());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_ExternalWorkflowStub_ByExecution_WithResult()
        {
            await SyncContext.ClearAsync;

            // Call a workflow that executes a child workflow by execution using a typed 
            // stub and then creates an external stub and then waits for that as
            // well without retrieving the result.

            var stub = client.NewWorkflowStub<IWorkflowExternalStub>();

            Assert.True(await stub.HelloTestByExecutionWithResultAsync());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_ExternalWaitForLongTime()
        {
            await SyncContext.ClearAsync;

            // Create an external workflow that will run for a relatively long
            // time and then pass the external ID to a child workflow that will
            // wait for it and return the result.
            //
            // The purpose is to test whether Cadence has any hard limits for
            // how long a local activity can run (because the external stub
            // will uses a local activity to wait for the result).
            //
            // The Cadence documentation states that local activities should
            // run for only a few seconds.  I'm hoping they recommend this
            // to encourage longer running activities to use heartbeats and
            // I hope Cadence doesn't enforce a limit.

            // I manually ran this for 5 minutes using the setting below to
            // confirm that Cadence doesn't enforce a time limit on local
            // activities.  We'll reset to 5 seconds for normal test runs.

            // const int sleepSeconds = 300;

            const int sleepSeconds = 5;

            var sleepStub      = client.NewWorkflowFutureStub<IWorkflowExternalStub>("sleep");
            var sleepFuture    = await sleepStub.StartAsync<string>(sleepSeconds, "It works!");
            var sleepExecution = sleepFuture.Execution;

            var waitStub = client.NewWorkflowStub<IWorkflowExternalStub>();

            Assert.Equal("It works!", await waitStub.WaitForExternalAsync(sleepExecution));
            Assert.Equal("It works!", await sleepFuture.GetAsync());
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowChildGetExecution : IWorkflow
        {
            [WorkflowMethod(Name = "run")]
            Task<bool> RunAsync();

            [WorkflowMethod(Name = "child")]
            Task<WorkflowExecution> ChildAsync();

            [WorkflowMethod(Name = "hello")]
            Task<string> HelloAsync(string name);

            [WorkflowMethod(Name = "child-stub")]
            Task<bool> ChildStubWithResultAsync();

            [WorkflowMethod(Name = "wait-for-signal")]
            Task<string> WaitForSignalAsync(string name);

            [WorkflowMethod(Name = "no-result")]
            Task NoResultAsync();

            [SignalMethod("signal")]
            Task SignalAsync(string signal);
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowChildGetExecution : WorkflowBase, IWorkflowChildGetExecution
        {
            private static string receivedSignal;

            public static new void Reset()
            {
                receivedSignal = null;
            }

            public async Task<bool> RunAsync()
            {
                // Create a child stub and then verify that we see an [InvalidOperationException]
                // when we call [Workflow.GetExecutionAsync()] because it hasn't been started yet.

                var stub = Workflow.NewChildWorkflowStub<IWorkflowChildGetExecution>();

                try
                {
                    await Workflow.GetWorkflowExecutionAsync(stub);
                    return false;   // We should never go here.
                }
                catch (InvalidOperationException)
                {
                    // Expecting this.
                }
                catch
                {
                    return false;
                }

                // Call the child workflow and then compare the [WorkflowExecution] 
                // returned by the child with that returned by [Workflow.GetExecutionAsync()].
                // These should match.

                var childExecution = await stub.ChildAsync();
                var stubExecution  = await Workflow.GetWorkflowExecutionAsync(stub);

                return childExecution.WorkflowId == stubExecution.WorkflowId &&
                       childExecution.RunId == stubExecution.RunId;
            }

            public async Task<WorkflowExecution> ChildAsync()
            {
                return await Task.FromResult(Workflow.Execution);
            }

            public async Task<string> HelloAsync(string name)
            {
                return await Task.FromResult($"Hello {name}!");
            }

            public async Task<bool> ChildStubWithResultAsync()
            {
                // We're going to execute [WaitForSignalAsync] as a child workflow,
                // send it a signal, and then verify that the signal was received.

                receivedSignal = null;

                var parentWorkflowType = Workflow.WorkflowInfo.WorkflowType;
                var posMethod          = parentWorkflowType.LastIndexOf("::");

                if (posMethod != -1)
                {
                    parentWorkflowType = parentWorkflowType.Substring(0, posMethod);
                }

                var stub   = Workflow.NewUntypedChildWorkflowFutureStub<string>(parentWorkflowType + "::wait-for-signal");
                var future = await stub.StartAsync("Jeff");

                await stub.SignalAsync("signal", "hello-signal");

                var result = await future.GetAsync();

                return result == "Hello Jeff:hello-signal";
            }

            public async Task<string> WaitForSignalAsync(string name)
            {
                // Wait for a signal from the parent workflow.

                var startTimeUtc = await Workflow.UtcNowAsync();
                var waitTime     = TimeSpan.FromSeconds(maxWaitSeconds);

                while (receivedSignal == null)
                {
                    var utcNow = await Workflow.UtcNowAsync();

                    if (utcNow - startTimeUtc > waitTime)
                    {
                        return "Timed out waiting for signal";
                    }

                    await Workflow.SleepAsync(TimeSpan.FromSeconds(1));
                }

                return await Task.FromResult($"Hello {name}:{receivedSignal}");
            }

            public async Task NoResultAsync()
            {
                await Task.CompletedTask;
            }

            public async Task SignalAsync(string signal)
            {
                receivedSignal = signal;

                await Task.CompletedTask;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_ChildGetExecution()
        {
            await SyncContext.ClearAsync;

            // Call a workflow that confirms that [Workflow.GetExecutionAsync()]
            // works correctly against a child workflow.

            WorkflowChildGetExecution.Reset();

            var stub = client.NewWorkflowStub<IWorkflowChildGetExecution>();

            Assert.True(await stub.RunAsync());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_ToUntyped()
        {
            await SyncContext.ClearAsync;

            // Verify that we can convert an external workflow stub into an
            // untyped [WorkflowStub].

            var stub = client.NewWorkflowStub<IWorkflowExternalStub>();

            // We should see an [InvalidOperationException] when we attempt
            // the conversion before the workflow has been started.

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await WorkflowStub.FromTypedAsync(stub));

            // Now start a workflow, convert the stub and verify that we can
            // obtain the correct result.

            Assert.Equal("Hello Jeff!", await stub.HelloAsync("Jeff"));

            var untypedStub = await WorkflowStub.FromTypedAsync(stub);

            Assert.Equal("Hello Jeff!", await untypedStub.GetResultAsync<string>());
            Assert.NotNull(untypedStub.Execution);
            Assert.NotEmpty(untypedStub.Execution.WorkflowId);
            Assert.NotEmpty(untypedStub.Execution.RunId);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_FutureChild_WithResult()
        {
            await SyncContext.ClearAsync;

            // Call a workflow that creates a [ChildWorkflowStub] and starts a child
            // workflow, passing it a parameter, signalling it, and then verifying
            // that it returns the correct result.

            var stub = client.NewWorkflowStub<IWorkflowChildGetExecution>();

            Assert.True(await stub.ChildStubWithResultAsync());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_Future_WithResult()
        {
            await SyncContext.ClearAsync;

            // Call a workflow that returns a result using the future stub.

            var stub = client.NewWorkflowFutureStub<IWorkflowChildGetExecution>("hello");
            var future = await stub.StartAsync<string>("Jeff");

            Assert.Equal("Hello Jeff!", await future.GetAsync());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_Future_WithoutResult()
        {
            await SyncContext.ClearAsync;

            // Call a workflow that returns no result using the future stub.

            var stub = client.NewWorkflowFutureStub<IWorkflowChildGetExecution>("no-result");
            var future = await stub.StartAsync();

            await future.GetAsync();
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowIdReuse : IWorkflow
        {
            [WorkflowMethod(Name = "hello")]
            Task<string> HelloAsync(string name);

            [WorkflowMethod(Name = "hello-via-attribute", WorkflowIdReusePolicy = WorkflowIdReusePolicy.AllowDuplicate)]
            Task<string> HelloWithAttributeAsync(string name);

            [WorkflowMethod(Name = "child-no-reuse")]
            Task<bool> ChildNoReuseAsync();

            [WorkflowMethod(Name = "child-reuse-via-options")]
            Task<bool> ChildReuseViaOptionsAsync();

            [WorkflowMethod(Name = "child-reuse-via-attribute")]
            Task<bool> ChildReuseViaAttributeAsync();
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowIdReuse : WorkflowBase, IWorkflowIdReuse
        {
            public async Task<string> HelloAsync(string name)
            {
                return await Task.FromResult($"Hello {name}!");
            }

            public async Task<string> HelloWithAttributeAsync(string name)
            {
                return await Task.FromResult($"Hello {name}!");
            }

            public async Task<bool> ChildNoReuseAsync()
            {
                // Verify that we can have Cadence reject duplicate workflow IDs.

                var options = new ChildWorkflowOptions()
                {
                    WorkflowId            = $"Child_IdNoReuse-{Guid.NewGuid().ToString("d")}",
                    WorkflowIdReusePolicy = WorkflowIdReusePolicy.RejectDuplicate
                };

                // Do the first run; this should succeed.

                var stub = Workflow.NewChildWorkflowStub<IWorkflowIdReuse>(options);

                if (await stub.HelloAsync("Jack") != "Hello Jack!")
                {
                    return false;
                }

                // Do the second run with the same ID.
                
                // Child workflows seem to work differently from external workflows 
                // in this situation.  Child workflows return a WorkflowExecutionAlreadyStartedError
                // when the workflow is already running whereas external workflows
                // will simply return the result from the previous run.

                stub = Workflow.NewChildWorkflowStub<IWorkflowIdReuse>(options);

                try
                {
                    await stub.HelloAsync("Jill");
                    return false;   // We're expecting an exception.
                }
                catch (WorkflowExecutionAlreadyStartedException)
                {
                    return true;    // Expecting this.
                }
            }

            public async Task<bool> ChildReuseViaOptionsAsync()
            {
                // Verify that we can have Cadence allow duplicate workflow IDs
                // using child options.

                var options = new ChildWorkflowOptions()
                {
                    WorkflowId            = $"Child_ReuseViaOptions-{Guid.NewGuid().ToString("d")}",
                    WorkflowIdReusePolicy = WorkflowIdReusePolicy.AllowDuplicate
                };

                // Do the first run; this should succeed.

                var stub = Workflow.NewChildWorkflowStub<IWorkflowIdReuse>(options);

                if (await stub.HelloAsync("Jack") != "Hello Jack!")
                {
                    return false;
                }

                // Do the second run with the same ID; this should also succeed.

                stub = Workflow.NewChildWorkflowStub<IWorkflowIdReuse>(options);

                return await stub.HelloAsync("Jill") == "Hello Jill!";
            }

            public async Task<bool> ChildReuseViaAttributeAsync()
            {
                // Verify that we can have Cadence allow duplicate workflow IDs
                // using the method attribute.

                var options = new ChildWorkflowOptions()
                {
                    WorkflowId = $"Child_ReuseViaAttribute-{Guid.NewGuid().ToString("d")}"
                };

                // Do the first run; this should succeed.

                var stub = Workflow.NewChildWorkflowStub<IWorkflowIdReuse>(options);

                if (await stub.HelloWithAttributeAsync("Jack") != "Hello Jack!")
                {
                    return false;
                }

                // Do the second run with the same ID; this should also succeed.

                stub = Workflow.NewChildWorkflowStub<IWorkflowIdReuse>(options);

                return await stub.HelloWithAttributeAsync("Jill") == "Hello Jill!";
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_ExternalIdNoReuse()
        {
            await SyncContext.ClearAsync;

            // Verify that we can have Cadence reject duplicate workflow IDs.

            var options = new WorkflowOptions()
            {
                WorkflowId            = $"Workflow_ExternalIdNoReuse-{Guid.NewGuid().ToString("d")}",
                WorkflowIdReusePolicy = WorkflowIdReusePolicy.RejectDuplicate
            };

            // Do the first run; this should succeed.

            var stub = client.NewWorkflowStub<IWorkflowIdReuse>(options);

            Assert.Equal("Hello Jack!", await stub.HelloAsync("Jack"));

            // Do the second run with the same ID.  This shouldn't actually start
            // another workflow and will return the result from the original
            // workflow instead.

            stub = client.NewWorkflowStub<IWorkflowIdReuse>(options);

            Assert.Equal("Hello Jack!", await stub.HelloAsync("Jill"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_ExternalIdReuseViaOptions()
        {
            await SyncContext.ClearAsync;

            // Verify that we can reuse a workflow ID for an external
            // workflow via options.

            var options = new WorkflowOptions()
            {
                WorkflowId            = $"Workflow_ExternalIdReuseViaOptions-{Guid.NewGuid().ToString("d")}",
                WorkflowIdReusePolicy = WorkflowIdReusePolicy.AllowDuplicate
            };

            // Do the first run.

            var stub = client.NewWorkflowStub<IWorkflowIdReuse>(options);

            Assert.Equal("Hello Jack!", await stub.HelloAsync("Jack"));

            // Do the second run.

            stub = client.NewWorkflowStub<IWorkflowIdReuse>(options);

            Assert.Equal("Hello Jill!", await stub.HelloAsync("Jill"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_ExternalIdReuseViaAttribute()
        {
            await SyncContext.ClearAsync;

            // Verify that we can reuse a workflow ID for an external
            // workflow via a [WorkflowMethod] attribute.

            var options = new WorkflowOptions()
            {
                WorkflowId            = $"Workflow_ExternalIdReuseViaAttribute-{Guid.NewGuid().ToString("d")}",
                WorkflowIdReusePolicy = WorkflowIdReusePolicy.AllowDuplicate
            };

            // Do the first run.

            var stub = client.NewWorkflowStub<IWorkflowIdReuse>(options);

            Assert.Equal("Hello Jack!", await stub.HelloWithAttributeAsync("Jack"));

            // Do the second run.

            stub = client.NewWorkflowStub<IWorkflowIdReuse>(options);

            Assert.Equal("Hello Jill!", await stub.HelloWithAttributeAsync("Jill"));
        }
        
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_ChildIdNoReuse()
        {
            await SyncContext.ClearAsync;

            // Verify that we can have Cadence reject duplicate child workflow IDs.

            var stub = client.NewWorkflowStub<IWorkflowIdReuse>();

            Assert.True(await stub.ChildNoReuseAsync());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_ChildIdReuseViaOptions()
        {
            await SyncContext.ClearAsync;

            // Verify that we can have Cadence use duplicate child workflow IDs.

            var stub = client.NewWorkflowStub<IWorkflowIdReuse>();

            Assert.True(await stub.ChildReuseViaOptionsAsync());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_ChildIdReuseViaAttribute()
        {
            // Verify that we can have Cadence use duplicate child workflow IDs.

            var stub = client.NewWorkflowStub<IWorkflowIdReuse>();

            Assert.True(await stub.ChildReuseViaAttributeAsync());
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowUntypedChildFuture : IWorkflow
        {
            [WorkflowMethod(Name = "run")]
            Task RunAsync();

            [WorkflowMethod(Name = "hello")]
            Task<string> HelloAsync(string name);

            [WorkflowMethod(Name = "with-result")]
            Task<bool> WithResult();

            [WorkflowMethod(Name = "with-no-result")]
            Task<bool> WithNoResult();

            [SignalMethod("signal")]
            Task SignalAsync(string signal);
        }

        [Workflow(AutoRegister = true, Name = "WorkflowUntypedChildFuture")]
        public class WorkflowUntypedChildFuture : WorkflowBase, IWorkflowUntypedChildFuture
        {
            public static bool      HasExecuted    = false;
            public static string    ReceivedSignal = null;
            public static bool      Error          = false;

            public static new void Reset()
            {
                HasExecuted    = false;
                ReceivedSignal = null;
                Error          = false;
            }

            private async Task<bool> WaitForSignal()
            {
                // Wait for a signal from the parent workflow.

                var startTimeUtc = await Workflow.UtcNowAsync();
                var waitTime     = TimeSpan.FromSeconds(maxWaitSeconds);

                while (ReceivedSignal == null)
                {
                    var utcNow = await Workflow.UtcNowAsync();

                    if (utcNow - startTimeUtc > waitTime)
                    {
                        return false;
                    }

                    await Workflow.SleepAsync(TimeSpan.FromSeconds(1));
                }

                return true;
            }

            public async Task RunAsync()
            {
                HasExecuted = true;

                if (!await WaitForSignal())
                {
                    Error = true;
                    return;
                }

                await Task.CompletedTask;
            }

            public async Task<string> HelloAsync(string name)
            {
                HasExecuted = true;

                if (!await WaitForSignal())
                {
                    return "ERROR: Timed out waiting for signal";
                }

                return await Task.FromResult($"Hello {name}!");
            }

            public async Task<bool> WithNoResult()
            {
                WorkflowUntypedChildFuture.Reset();

                // Create an untyped child stub for RunAsync() which doesn't take 
                // and parameters and returns no result, verify that we can start
                // and signal it and then verify that we can wait for it to
                // return and that it actually was executed.

                var stub   = Workflow.NewUntypedChildWorkflowFutureStub("WorkflowUntypedChildFuture::run");
                var future = await stub.StartAsync();

                await stub.SignalAsync("signal", "test");
                await future.GetAsync();

                return WorkflowUntypedChildFuture.HasExecuted;
            }

            public async Task<bool> WithResult()
            {
                WorkflowUntypedChildFuture.Reset();

                // Create an untyped child stub for HelloAsync() which take a
                // parameters and returns a result, verify that we can start
                // and signal it and then verify that we can wait for it to
                // return and that it actually was executed.

                var stub   = Workflow.NewUntypedChildWorkflowFutureStub<string>("WorkflowUntypedChildFuture::hello");
                var future = await stub.StartAsync("Jeff");

                await stub.SignalAsync("signal", "test");

                var result = await future.GetAsync();

                return WorkflowUntypedChildFuture.HasExecuted && result == "Hello Jeff!";
            }

            public async Task SignalAsync(string signal)
            {
                ReceivedSignal = signal;

                await Task.CompletedTask;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_UntypedChildFuture_WithNoResult()
        {
            await SyncContext.ClearAsync;

            // Verify that a child workflow with no arguments or result can be 
            // called and signalled via an untyped future stub.

            var stub = client.NewWorkflowStub<IWorkflowUntypedChildFuture>(workflowTypeName: "WorkflowUntypedChildFuture");

            Assert.True(await stub.WithNoResult() && !WorkflowUntypedChildFuture.Error);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_UntypedChildFuture_WithResult()
        {
            await SyncContext.ClearAsync;

            // Verify that a child workflow with an argument and result can be 
            // called and signalled via an untyped future stub.

            var stub = client.NewWorkflowStub<IWorkflowUntypedChildFuture>(workflowTypeName: "WorkflowUntypedChildFuture");

            Assert.True(await stub.WithResult() && !WorkflowUntypedChildFuture.Error);
        }

        //---------------------------------------------------------------------

        public class PersonItem
        {
            public string Name { get; set; }
            public int Age { get; set; }
        }

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowQueueTest : IWorkflow
        {
            [WorkflowMethod(Name = "QueueToSelf_Single")]
            Task<string> QueueToSelf_Single();

            [WorkflowMethod(Name = "QueueToSelf_Multiple")]
            Task<string> QueueToSelf_Multiple(int capacity);

            [WorkflowMethod(Name = "QueueToSelf_WithClose")]
            Task<string> QueueToSelf_WithClose();

            [WorkflowMethod(Name = "QueueToSelf_Timeout")]
            Task<string> QueueToSelf_Timeout();

            [WorkflowMethod(Name = "WaitForSignals")]
            Task<List<string>> WaitForSignals(int expectedSignals);

            [WorkflowMethod(Name = "WaitForSignalsAndClose")]
            Task<List<string>> WaitForSignalAndClose(int expectedSignals);

            [WorkflowMethod(Name = "WaitForSignal_TimeoutWithDequeue")]
            Task<string> WaitForSignal_TimeoutWithDequeue();

            [WorkflowMethod(Name = "QueueToSelf-Bytes")]
            Task<string> QueueToSelf_Bytes(int byteCount);

            [WorkflowMethod(Name = "QueueToSelf-Person")]
            Task<PersonItem> QueueToSelf_Person(PersonItem person);

            [SignalMethod("signal")]
            Task SignalAsync(string message);
        }

        /// <summary>
        /// This workflow tests basic signal reception by waiting for some number of signals
        /// and then returning the received signal messages.  The workflow will timeout
        /// if the signals aren't received in time.  Note that we've hacked workflow start
        /// detection using a static field.
        /// </summary>
        [Workflow(AutoRegister = true)]
        public class WorkflowQueueTest : WorkflowBase, IWorkflowQueueTest
        {
            private WorkflowQueue<string>   signalQueue;

            public async Task<string> QueueToSelf_Single()
            {
                // Tests basic queuing by creating a queue, enqueueing a string and then
                // dequeuing it locally.  This return NULL if the test passed otherwise
                // an error message.

                using (var queue = await Workflow.NewQueueAsync<string>())
                {
                    if (queue.Capacity != WorkflowQueue<TargetException>.DefaultCapacity)
                    {
                        return $"1: Expected: capacity == {WorkflowQueue<TargetException>.DefaultCapacity}";
                    }

                    await queue.EnqueueAsync("Hello World!");

                    var dequeued = await queue.DequeueAsync();

                    if (dequeued != "Hello World!")
                    {
                        return $"2: Unpexected item: {dequeued}";
                    }

                    return null;
                }
            }

            public async Task<string> QueueToSelf_Multiple(int capacity = 0)
            {
                // Tests basic queuing by creating a queue, enqueueing multiple strings
                // and then dequeuing them locally.  This return NULL if the test passed
                // otherwise an error message.

                if (capacity == 0)
                {
                    // Verify that we're able to process a few items with a default
                    // capacity queue.

                    using (var queue = await Workflow.NewQueueAsync<string>())
                    {
                        if (queue.Capacity != WorkflowQueue<TargetException>.DefaultCapacity)
                        {
                            return $"1: Expected: capacity == {WorkflowQueue<TargetException>.DefaultCapacity}";
                        }

                        await queue.EnqueueAsync("signal 1");
                        await queue.EnqueueAsync("signal 2");

                        var item = await queue.DequeueAsync();

                        if (item != "signal 1")
                        {
                            return $"2: Unpexected item: {item}";
                        }

                        item = await queue.DequeueAsync();

                        if (item != "signal 2")
                        {
                            return $"3: Unpexected item: {item}";
                        }

                        return null;
                    }
                }
                else
                {
                    // Verify that we can use a non default capacity and
                    // that we can fill the queue to capacity, read all
                    // of the items, and then fill and read again once more.
                    //
                    // The second pass ensures that nothing weird happens
                    // after we fill and then drain a queue.

                    using (var queue = await Workflow.NewQueueAsync<string>(capacity: capacity))
                    {
                        if (queue.Capacity != capacity)
                        {
                            return $"1: Expected: capacity == {capacity}";
                        }

                        for (int pass = 1; pass <= 2; pass++)
                        {
                            // Do the writes.

                            for (int i = 0; i < capacity; i++)
                            {
                                await queue.EnqueueAsync($"signal {i}");
                            }

                            // Do the reads.

                            for (int i = 0; i < capacity; i++)
                            {
                                var item = await queue.DequeueAsync();

                                if (item != $"signal {i}")
                                {
                                    return $"2: Unpexected item: {item}";
                                }
                            }
                        }

                        return null;
                    }
                }
            }

            public async Task<string> QueueToSelf_Timeout()
            {
                // Verifies that [cadence-proxy] honors dequeuing timeouts.

                using (var queue = await Workflow.NewQueueAsync<string>())
                {
                    try
                    {
                        await queue.DequeueAsync(TimeSpan.FromSeconds(1));
                        return "1: Expected dequeue to timeout";
                    }
                    catch (CadenceTimeoutException)
                    {
                        return null;    // Expecting this
                    }
                    catch (Exception e)
                    {
                        return $"2: Unexpected exception: {e.GetType().FullName}: {e.Message}";
                    }
                }
            }

            public async Task<string> WaitForSignal_TimeoutWithDequeue()
            {
                // Verifies that [cadence-proxy] honors dequeuing timeouts.

                using (signalQueue = await Workflow.NewQueueAsync<string>())
                {
                    try
                    {
                        // dequeue first value should come through

                        await signalQueue.DequeueAsync(TimeSpan.FromSeconds(5));

                        // next dequeue should timeout

                        await signalQueue.DequeueAsync(TimeSpan.FromSeconds(10));

                        return "1: Expected dequeue to timeout";
                    }
                    catch (CadenceTimeoutException)
                    {
                        return null;    // Expecting this
                    }
                    catch (Exception e)
                    {
                        return $"2: Unexpected exception: {e.GetType().FullName}: {e.Message}";
                    }
                }
            }

            public async Task<string> QueueToSelf_WithClose()
            {
                // Tests basic queuing by creating a queue, enqueueing a string and then closing
                // the queue locally and then verifying that the we can dequeue the string and
                // then see a [WorkflowQueueClosedExcetion] on the next read.
                //
                // This return NULL if the test passed otherwise an error message.

                using (var queue = await Workflow.NewQueueAsync<string>())
                {
                    if (queue.Capacity != WorkflowQueue<TargetException>.DefaultCapacity)
                    {
                        return $"1: Expected: capacity == {WorkflowQueue<TargetException>.DefaultCapacity}";
                    }

                    await queue.EnqueueAsync("Hello World!");
                    await queue.CloseAsync();

                    var dequeued = await queue.DequeueAsync();

                    if (dequeued != "Hello World!")
                    {
                        return $"2: Unpexected item: {dequeued}";
                    }

                    try
                    {
                        await queue.DequeueAsync(TimeSpan.FromSeconds(maxWaitSeconds));

                        return $"3: ERROR: {nameof(WorkflowQueueClosedException)} expected.";
                    }
                    catch (WorkflowQueueClosedException)
                    {
                    }
                    catch
                    {
                        return $"4: ERROR: {nameof(WorkflowQueueClosedException)} expected.";
                    }

                    return null;
                }
            }

            public async Task<string> QueueToSelf_Bytes(int byteCount)
            {
                // Tests the maximum queued message size limit by writing a message
                // of the specified sized to the queue and then returning NULL if
                // the operation worked or the full name of the exception thrown if
                // it failed.
                //
                // The unit test will call this twice, once with a byte count that's
                // just less than or equal to the limit and then again with a count
                // that's just over the limit.  The first call should succeed and 
                // the second should fail, with the expected exception.

                using (var queue = await Workflow.NewQueueAsync<byte[]>())
                {
                    var bytes = new byte[byteCount];

                    for (int i = 0; i < byteCount; i++)
                    {
                        bytes[i] = (byte)i;
                    }

                    try
                    {
                        await queue.EnqueueAsync(bytes);
                    }
                    catch (Exception e)
                    {
                        return e.GetType().FullName;
                    }

                    return null;
                }
            }

            public async Task<PersonItem> QueueToSelf_Person(PersonItem person)
            {
                // Verify that queues can handle arbitrary class instances.

                using (var queue = await Workflow.NewQueueAsync<PersonItem>())
                {
                    await queue.EnqueueAsync(person);

                    return await queue.DequeueAsync();
                }
            }

            public async Task<List<string>> WaitForSignals(int expectedSignals)
            {
                // Creates a queue and then waits for the requested number of signals
                // to be received and be delivered to the workflow via the queue.

                signalQueue = await Workflow.NewQueueAsync<string>();

                var signals = new List<string>();

                for (int i = 0; i < expectedSignals; i++)
                {
                    signals.Add(await signalQueue.DequeueAsync(TimeSpan.FromSeconds(maxWaitSeconds)));
                }

                return signals;
            }

            public async Task<List<string>> WaitForSignalAndClose(int expectedSignals)
            {
                // Creates a queue and then waits for the requested number of signals
                // to be received and be delivered to the workflow via the queue and
                // then performs one more read, expecting a [WorkflowQueueClosedExce[tion].

                signalQueue = await Workflow.NewQueueAsync<string>();

                var signals = new List<string>();

                for (int i = 0; i < expectedSignals; i++)
                {
                    signals.Add(await signalQueue.DequeueAsync(TimeSpan.FromSeconds(maxWaitSeconds)));
                }

                try
                {
                    await signalQueue.DequeueAsync(TimeSpan.FromSeconds(maxWaitSeconds));

                    signals.Add($"ERROR: {nameof(WorkflowQueueClosedException)} expected.");
                }
                catch (WorkflowQueueClosedException)
                {
                }
                catch
                {
                    signals.Add($"ERROR: {nameof(WorkflowQueueClosedException)} expected.");
                }

                return signals;
            }

            public async Task SignalAsync(string message)
            {
                Covenant.Assert(signalQueue != null);

                if (message == "close")
                {
                    await signalQueue.CloseAsync();
                }
                else
                {
                    await signalQueue.EnqueueAsync(message);
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_Queue_Single()
        {
            await SyncContext.ClearAsync;

            // Verify the simple case where a workflow creates a queue and then
            // can enqueue/dequeue a single item locally within the workflow method.

            var stub = client.NewWorkflowStub<IWorkflowQueueTest>();

            Assert.Null(await stub.QueueToSelf_Single());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_Queue_Multiple()
        {
            await SyncContext.ClearAsync;

            // Verify the simple case where a workflow creates a queue and then
            // can enqueue/dequeue multiple items locally within the workflow method.
            // This test creates a queue with the default capacity.

            var stub = client.NewWorkflowStub<IWorkflowQueueTest>();

            Assert.Null(await stub.QueueToSelf_Multiple(0));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_Queue_Multiple_200()
        {
            await SyncContext.ClearAsync;

            // Verify the simple case where a workflow creates a queue and then
            // can enqueue/dequeue multiple items locally within the workflow method.
            // This test creates a queue with a 200 item capacity.

            var stub = client.NewWorkflowStub<IWorkflowQueueTest>();

            Assert.Null(await stub.QueueToSelf_Multiple(200));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_Queue_Timeout()
        {
            await SyncContext.ClearAsync;

            // Verify that [cadence-proxy] honors dequeue timeouts.

            var stub = client.NewWorkflowStub<IWorkflowQueueTest>();

            Assert.Null(await stub.QueueToSelf_Timeout());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_Queue_TimeoutWithDequeue()
        {
            await SyncContext.ClearAsync;

            // Verify that [cadence-proxy] honors dequeue timeouts.

            var stub   = client.NewWorkflowFutureStub<IWorkflowQueueTest>("WaitForSignal_TimeoutWithDequeue");
            var future = await stub.StartAsync<string>();

            await stub.SignalAsync("signal", "signal: 0");

            Assert.Null(await future.GetAsync());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_Queue_Close()
        {
            await SyncContext.ClearAsync;

            // Verify the simple case where a workflow creates a queue and then
            // can enqueue/dequeue a single item locally within the workflow method.

            var stub = client.NewWorkflowStub<IWorkflowQueueTest>();

            Assert.Null(await stub.QueueToSelf_WithClose());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_Queue_FromSignal_Single()
        {
            await SyncContext.ClearAsync;

            // Verify that a workflow can process data received via a signal
            // when is then fed to the workflow via a queue.

            var stub   = client.NewWorkflowFutureStub<IWorkflowQueueTest>("WaitForSignals");
            var future = await stub.StartAsync<List<string>>(1);

            await stub.SignalAsync("signal", "signal: 0");

            var received = await future.GetAsync();

            Assert.Single(received);
            Assert.Contains(received, v => v == "signal: 0");
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_Queue_FromSignal_Multiple()
        {
            await SyncContext.ClearAsync;

            // Verify that a workflow can process data received via multiple signals
            // when are then fed to the workflow via a queue.

            const int signalCount = 5;

            var stub   = client.NewWorkflowFutureStub<IWorkflowQueueTest>("WaitForSignals");
            var future = await stub.StartAsync<List<string>>(signalCount);
            var sent   = new List<string>();

            for (int i = 0; i < signalCount; i++)
            {
                sent.Add($"signal: {i}");
            }

            foreach (var signal in sent)
            {
                await stub.SignalAsync("signal", signal);
            }

            var received = await future.GetAsync();

            Assert.Equal(signalCount, received.Count);

            foreach (var signal in sent)
            {
                Assert.Contains(received, v => v == signal);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_Queue_CloseViaSignal()
        {
            await SyncContext.ClearAsync;

            // Verify that a queue closed via a signal throws a [WorkflowQueueClosedException]
            // when dequeued in the workflow.

            const int signalCount = 5;

            var stub   = client.NewWorkflowFutureStub<IWorkflowQueueTest>("WaitForSignalsAndClose");
            var future = await stub.StartAsync<List<string>>(signalCount);
            var sent   = new List<string>();

            for (int i = 0; i < signalCount; i++)
            {
                sent.Add($"signal: {i}");
            }

            foreach (var signal in sent)
            {
                await stub.SignalAsync("signal", signal);
            }

            await stub.SignalAsync("signal", "close");

            var received = await future.GetAsync();

            Assert.Equal(signalCount, received.Count);

            foreach (var signal in sent)
            {
                Assert.Contains(received, v => v == signal);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_Queue_ItemMax()
        {
            await SyncContext.ClearAsync;

            // The maximim size allowed for an encoded item is 64MiB-1.  This
            // test verifies that we proactively check for this by creating 
            // an encoded item just under the limit and another just over the
            // limit and then verifying that the first item can be written
            // and the second can't.

            // Determine the limits:

            byte[]  item;
            int     maxGood = 0;
            int     minBad  = 0;

            for (int count = 0; count <= ushort.MaxValue; count++)
            {
                item = new byte[count];

                var encoded = client.DataConverter.ToData(item);

                if (encoded.Length <= ushort.MaxValue)
                {
                    maxGood = count;
                }
                else
                {
                    minBad = count;
                    break;
                }
            }

            // First call with an item under the limit.

            var stub = client.NewWorkflowStub<IWorkflowQueueTest>();

            Assert.Null(await stub.QueueToSelf_Bytes(maxGood));

            // Second call with an item over the limit.

            stub = client.NewWorkflowStub<IWorkflowQueueTest>();

            Assert.Equal("System.NotSupportedException", await stub.QueueToSelf_Bytes(minBad));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_Queue_Class()
        {
            await SyncContext.ClearAsync;

            // Verify that queues can handle arbetrary class instances.

            var stub   = client.NewWorkflowStub<IWorkflowQueueTest>();
            var person = new PersonItem()
            {
                Name = "Joe Bloe",
                Age  = 27
            };

            person = await stub.QueueToSelf_Person(person);

            Assert.NotNull(person);
            Assert.Equal("Joe Bloe", person.Name);
            Assert.Equal(27, person.Age);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_Queue_ViaExternalStub_ByExecution()
        {
            await SyncContext.ClearAsync;

            // Verify that we can create a typed stub using an execution for an existing workflow
            // and use that to send a signal.

            var stub      = client.NewWorkflowFutureStub<IWorkflowQueueTest>("WaitForSignals");
            var future    = await stub.StartAsync<List<string>>(1);
            var typedStub = client.NewWorkflowStub<IWorkflowQueueTest>(future.Execution);

            await typedStub.SignalAsync("signal: 0");

            var received = await future.GetAsync();

            Assert.Single(received);
            Assert.Contains(received, v => v == "signal: 0");
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_Queue_ViaExternalStub_ByIDs()
        {
            await SyncContext.ClearAsync;

            // Verify that we can create a typed stub using IDs for an existing workflow
            // and use that to send a signal.

            var stub      = client.NewWorkflowFutureStub<IWorkflowQueueTest>("WaitForSignals");
            var future    = await stub.StartAsync<List<string>>(1);
            var typedStub = client.NewWorkflowStub<IWorkflowQueueTest>(future.Execution.WorkflowId, future.Execution.RunId);

            await typedStub.SignalAsync("signal: 0");

            var received = await future.GetAsync();

            Assert.Single(received);
            Assert.Contains(received, v => v == "signal: 0");
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowTimeout : IWorkflow
        {
            [WorkflowMethod(Name = "sleep")]
            Task SleepAsync(TimeSpan sleepTime);

            [WorkflowMethod(Name = "activity-heartbeat-timeout")]
            Task<bool> ActivityHeartbeatTimeoutAsync();

            [WorkflowMethod(Name = "activity-timeout")]
            Task<bool> ActivityTimeout();

            [WorkflowMethod(Name = "activity-dotnetexception")]
            Task<bool> ActivityDotNetException();
        }

        [ActivityInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IActivityTimeout : IActivity
        {
            [ActivityMethod(Name = "sleep")]
            Task SleepAsync(TimeSpan sleepTime);

            [ActivityMethod(Name = "throw-transient")]
            Task ThrowTransientAsync();
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowTimeout : WorkflowBase, IWorkflowTimeout
        {
            public async Task SleepAsync(TimeSpan sleepTime)
            {
                await Workflow.SleepAsync(sleepTime);
            }

            public async Task<bool> ActivityHeartbeatTimeoutAsync()
            {
                // We're going to start an activity that will sleep for
                // longer than its heartbeat interval.  Cadence should
                // detect that the heartbeat time was exceeded and
                // throw an [ActivityHeartbeatTimeoutException].
                //
                // The method returns TRUE if we catch ther desired
                // exception.

                var sleepTime   = TimeSpan.FromSeconds(5);
                var timeoutTime = TimeSpan.FromTicks(sleepTime.Ticks / 2);
                var stub        = Workflow.NewActivityStub<IActivityTimeout>(new ActivityOptions() { HeartbeatTimeout = timeoutTime });

                try
                {
                    await stub.SleepAsync(sleepTime);
                }
                catch (ActivityHeartbeatTimeoutException)
                {
                    return true;
                }

                return false;
            }

            public async Task<bool> ActivityTimeout()
            {
                // We're going to start an activity that will run longer than it's
                // start to close timeout and verify that we see a
                // [StartToCloseTimeoutException].  The method returns TRUE
                // when we catch the expected exception.

                var sleepTime   = TimeSpan.FromSeconds(5);
                var timeoutTime = TimeSpan.FromTicks(sleepTime.Ticks / 2);
                var stub        = Workflow.NewActivityStub<IActivityTimeout>(
                    new ActivityOptions()
                    { 
                        StartToCloseTimeout = timeoutTime,
                        HeartbeatTimeout    = TimeSpan.FromSeconds(60),
                    });

                try
                {
                    await stub.SleepAsync(sleepTime);
                }
                catch (StartToCloseTimeoutException)
                {
                    return true;
                }

                return false;
            }

            public async Task<bool> ActivityDotNetException()
            {
                // Call an activity that throws a [TransientException] and
                // verify that we see a [CadenceGenericException] formatted
                // with the exception information.
                //
                // The method returns TRUE when the exception looks good.

                var stub = Workflow.NewActivityStub<IActivityTimeout>();

                try
                {
                    await stub.ThrowTransientAsync();
                }
                catch (CadenceGenericException e)
                {
                    if (e.Reason != typeof(TransientException).FullName)
                    {
                        return false;
                    }

                    if (e.Message != "This is a test!")
                    {
                        return false;
                    }

                    return true;
                }

                return false;
            }
        }

        [Activity(AutoRegister = true)]
        public class ActivityTimeout : ActivityBase, IActivityTimeout
        {
            public async Task SleepAsync(TimeSpan sleepTime)
            {
                await Task.Delay(sleepTime);
            }

            public async Task ThrowTransientAsync()
            {
                // Throw a [TransientException] so the calling workflow can verify
                // that the GOLANG error is generated properly and that it ends up
                // being wrapped in a [CadenceGenericException] as expected.

                await Task.CompletedTask;

                throw new TransientException("This is a test!");
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_StartToCloseTimeout()
        {
            await SyncContext.ClearAsync;

            // Verify that we get the expected exception when a workflow doesn't
            // complete within a START_TO_CLOSE_TIMEOUT.

            var timeout   = TimeSpan.FromSeconds(2);
            var sleepTime = TimeSpan.FromTicks(timeout.Ticks * 2);

            var stub = client.NewWorkflowStub<IWorkflowTimeout>(
                new WorkflowOptions()
                {
                    StartToCloseTimeout = timeout
                });

            await Assert.ThrowsAsync<StartToCloseTimeoutException>(async () => await stub.SleepAsync(sleepTime));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Activity_StartToCloseTimeout()
        {
            await SyncContext.ClearAsync;

            // Verify that we get the expected exception when an activity doesn't
            // complete within a START_TO_CLOSE_TIMEOUT.

            var stub = client.NewWorkflowStub<IWorkflowTimeout>();

            Assert.True(await stub.ActivityTimeout());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Activity_HeartbeatTimeout()
        {
            await SyncContext.ClearAsync;

            // Verify that we see an [ActivityHeartbeatTimeoutException] when
            // we run an activity that doesn't heartbeat in time.

            var stub = client.NewWorkflowStub<IWorkflowTimeout>();

            Assert.True(await stub.ActivityHeartbeatTimeoutAsync());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Activity_DotNetException()
        {
            await SyncContext.ClearAsync;

            // Call a workflow that calls an activity that throws a .NET
            // exception and verify that the exception caught by the
            // workflow looks reasonable.

            var stub = client.NewWorkflowStub<IWorkflowTimeout>();

            Assert.True(await stub.ActivityDotNetException());
        }

        //---------------------------------------------------------------------

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_Container()
        {
            const string taskList = "test-cadence-container";

            await SyncContext.ClearAsync;

            // Start the [nkubeio/test-cadence:latest] Docker image locally, having it
            // connect to the local Cadence cluster and then start a bunch of workflows that
            // will be executed by the container and verify that they completed.

            // We need a routable IP address for the current machine so we can use it to
            // generate the Cadence URI we'll pass to the [test-cadence] container so it
            // will be able to connect to the Cadence server running locally.

            var ipAddress = NetHelper.GetRoutableIpAddress();

            if (ipAddress == null)
            {
                Assert.True(false, "Cannot complete test without a routable IP address.");
                return;
            }

            // Start the [test-cadence] container and give it a chance to connect to Cadence
            // and register its workflows and activities.  We'll remove any existing container
            // first and then remove the container after we're done.

            var testCadenceImage = $"{KubeConst.NeonBranchRegistry}/test-cadence:latest";

            // $debug(jefflill): 
            //
            // It might be useful to uncomment/modify this line while
            // debugging changes to the [test-cadence] Docker image.

            // testCadenceImage = "nkubedev/test-cadence:cadence-latest";

            NeonHelper.Execute(NeonHelper.DockerCli,
                new object[]
                {
                    "rm", "--force", "test-cadence"
                });

            // Make sure we have the latest image first.

            var exitCode = NeonHelper.Execute(NeonHelper.DockerCli,
                new object[]
                {
                    "pull",
                    testCadenceImage
                });

            if (exitCode != 0)
            {
                Assert.True(false, $"Cannot pull the [{testCadenceImage}] Docker image.");
            }

            // Start the test workflow service.

            exitCode = NeonHelper.Execute(NeonHelper.DockerCli,
                new object[]
                {
                    "run",
                    "--detach", 
                    "--name", "test-cadence",
                    "--env", $"CADENCE_SERVERS=cadence://{ipAddress}:7933",
                    "--env", $"CADENCE_DOMAIN={CadenceFixture.DefaultDomain}",
                    "--env", $"CADENCE_TASKLIST={taskList}",
                    testCadenceImage
                });

            if (exitCode != 0)
            {
                Assert.True(false, $"Cannot run the [{testCadenceImage}] Docker image.");
            }

            try
            {
                // Start a decent number of workflows that will run in parallel for a while
                // and then verify that they all complete successfully.

                const int workflowCount      = 500;
                const int workflowIterations = 5;

                var sleepTime = TimeSpan.FromSeconds(1);
                var pending   = new List<Task<string>>();

                for (int i = 0; i < workflowCount; i++)
                {
                    var stub = client.NewWorkflowStub<IBusyworkWorkflow>(
                        new WorkflowOptions()
                        {
                            WorkflowId = $"busywork-{Guid.NewGuid().ToString("d")}",
                            TaskList   = taskList
                        });

                    pending.Add(stub.DoItAsync(workflowIterations, sleepTime, $"workflow-{i}"));
                }

                for (int i = 0; i < workflowCount; i++)
                {
                    Assert.Equal($"workflow-{i}", await pending[i]);
                }
            }
            finally
            {
                // Kill the [test-cadence] container.

                exitCode = NeonHelper.Execute(NeonHelper.DockerCli,
                    new object[]
                    {
                        "rm", "--force", "test-cadence",
                    });

                if (exitCode != 0)
                {
                    Assert.True(false, $"Cannot remove the [{testCadenceImage}] Docker container.");
                }
            }
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowAmbientState : IWorkflow
        {
            [WorkflowMethod]
            Task<bool> RunAsync();
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowAmbientState : WorkflowBase, IWorkflowAmbientState
        {
            public async Task<bool> RunAsync()
            {
                // Returns TRUE when the workflow property and the correspending ambient
                // reference the same instance.

                return await Task.FromResult(object.ReferenceEquals(this.Workflow, Workflow.Current));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_AmbientState()
        {
            // Verify that the ambient [Workflow.Current] property is being set properly.

            var stub = client.NewWorkflowStub<IWorkflowAmbientState>();

            Assert.Null(Workflow.Current);
            Assert.True(await stub.RunAsync());
            Assert.Null(Workflow.Current);
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowNullables : IWorkflow
        {
            [WorkflowMethod]
            Task<TimeSpan?> TestAsync(TimeSpan? value);
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowNullables : WorkflowBase, IWorkflowNullables
        {
            public async Task<TimeSpan?> TestAsync(TimeSpan? value)
            {
                return await Task.FromResult(value);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_Nullable()
        {
            // Verify that nullable workflow arguments and results are serialized properly.

            var stub = client.NewWorkflowStub<IWorkflowNullables>();

            Assert.Null(await stub.TestAsync(null));

            stub = client.NewWorkflowStub<IWorkflowNullables>();

            Assert.Equal(TimeSpan.FromSeconds(77), await stub.TestAsync(TimeSpan.FromSeconds(77)));
        }
    }
}
