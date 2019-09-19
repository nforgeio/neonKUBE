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
    public partial class Test_EndToEnd
    {
        //---------------------------------------------------------------------

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

            public new static void Reset()
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
            // Verify that we can call a simple workflow that accepts a
            // parameter and results a result.

            WorkflowWithNoResult.Reset();

            var stub = client.NewWorkflowStub<IWorkflowWithNoResult>();

            await stub.RunAsync();

            Assert.True(WorkflowWithNoResult.WorkflowWithNoResultCalled);
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
            // Verify that logging within a workflow doesn't barf.

            // $todo(jeff.lill):
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
        public async Task Workflow_MultipleStubs()
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

            // Start the CRON workflow and wait for the result from the first run.  

            var stub = client.NewWorkflowStub<ICronWorkflow>(options);

            stub.RunAsync();

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
                Covenant.Requires<ArgumentNullException>(bytes != null);
                Covenant.Requires<ArgumentException>(bytes.Length > 4);

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
            // Verify that SideEffect() and SideEffect<T>() work.

            Assert.Equal("test1", await client.NewWorkflowStub<IWorkflowSideEffect>().SideEffectAsync("test1"));
            Assert.Equal("test2", await client.NewWorkflowStub<IWorkflowSideEffect>().GenericSideEffectAsync("test2"));
        }

        //---------------------------------------------------------------------

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
            // Verify that MutableSideEffect() and MutableSideEffect<T>() work.

            Assert.Equal("test1", await client.NewWorkflowStub<IWorkflowMutableSideEffect>().MutableSideEffectAsync("id-1", "test1"));
            Assert.Equal("test2", await client.NewWorkflowStub<IWorkflowMutableSideEffect>().GenericMutableSideEffectAsync("id-2", "test2"));
        }

        //---------------------------------------------------------------------

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

            public new static void Reset()
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

                throw new CadenceTimeoutException("Timeout waiting for signal(s).");
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
            // Verify that we're not allowed to send a signal via a
            // stub before we started the workflow.

            WorkflowSignal.Reset();

            var stub = client.NewWorkflowStub<IWorkflowSignal>();

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await stub.SignalAsync("my-signal"));
        }

        //---------------------------------------------------------------------

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

            public new static void Reset()
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
            // Verify that we can call a query method that doesn't
            // return a result.

            WorkflowQuery.Reset();

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
            // Verify that we're not allowed to submit a query via a
            // stub before we started the workflow.

            WorkflowQuery.Reset();

            var stub = client.NewWorkflowStub<IWorkflowQuery>();

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await stub.QueryAsync("my-query", 1));
        }

        //---------------------------------------------------------------------

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
            // Minimally exercise the workflow GetVersion() API.

            WorkflowQuery.Reset();

            var stub = client.NewWorkflowStub<IWorkflowGetVersion>();

            Assert.Equal(1, await stub.RunAsync("my-change-id", Workflow.DefaultVersion, 1));
        }

        //---------------------------------------------------------------------

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

            public new static void Reset()
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

            public new static void Reset()
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

        public interface IWorkflowParent : IWorkflow
        {
            [WorkflowMethod]
            Task RunChildAsync();

            [WorkflowMethod(Name = "hello")]
            Task<string> HelloChildAsync(string name);

            [WorkflowMethod(Name = "hello-activity")]
            Task<string> HelloChildActivityAsync(string name);

            [WorkflowMethod(Name = "nested-hello")]
            Task<string> NestedHelloChildAsync(string name);

            [WorkflowMethod(Name = "signal-child")]
            Task SignalChildAsync(string signal);

            [WorkflowMethod(Name = "query-child")]
            Task<bool> QueryChildAsync();
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowParent : WorkflowBase, IWorkflowParent
        {
            public async Task RunChildAsync()
            {
                var childStub = Workflow.NewChildWorkflowStub<IWorkflowChild>();

                await childStub.RunAsync();
            }

            public async Task<string> HelloChildAsync(string name)
            {
                var childStub = Workflow.NewChildWorkflowStub<IWorkflowChild>();

                return await childStub.HelloAsync(name);
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
                var childStub = Workflow.NewChildWorkflowStub<IWorkflowChild>();
                var childTask = childStub.WaitForSignalAsync();

                await childStub.SignalAsync(signal);
                await childTask;
            }

            public async Task<bool> QueryChildAsync()
            {
                // Direct querying of child workflows is not currently supported.
                // We're going to verify that we get an exception.  This method 
                // returns TRUE for the expected behavior.
                //
                // NOTE: We'll probably relax this constraint in the future:
                //
                //      https://github.com/nforgeio/neonKUBE/issues/617

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

                try
                {
                    await childStub.QueryAsync("test");
                }
                catch (NotSupportedException)
                {
                    pass = true;
                }

                WorkflowChild.ExitNow = true;

                await task;

                return pass;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_Child()
        {
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
            // Verify that we can call a child workflow that accepts a
            // parameter and returns a result.

            WorkflowChild.Reset();

            var stub = client.NewWorkflowStub<IWorkflowParent>();

            Assert.Equal("Hello Jeff!", await stub.HelloChildAsync("Jeff"));
            Assert.True(WorkflowChild.WasExecuted);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_ChildActivity()
        {
            // Verify that we can call a child workflow that calls an activity.

            WorkflowChild.Reset();

            var stub = client.NewWorkflowStub<IWorkflowParent>();

            Assert.Equal("Hello Jeff!", await stub.HelloChildActivityAsync("Jeff"));
            Assert.True(WorkflowChild.WasExecuted);
        }

        [Fact(Skip = "Hangs right now")]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_ChildSignal()
        {
            // Verify that signalling a child workflow.
            
            WorkflowChild.Reset();

            var stub = client.NewWorkflowStub<IWorkflowParent>();

            await stub.SignalChildAsync("my-signal");

            Assert.Single(WorkflowChild.ReceivedSignals);
            Assert.Contains("my-signal", WorkflowChild.ReceivedSignals);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_ChildQueryNotSupported()
        {
            // Verify that querying a child workflow is not supported.

            WorkflowChild.Reset();

            var stub = client.NewWorkflowStub<IWorkflowParent>();
            var pass = await stub.QueryChildAsync();

            Assert.True(pass);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_ChildNested()
        {
            // Test calling a workflow that calls a child which
            // calls another child.

            WorkflowChild.Reset();

            var stub = client.NewWorkflowStub<IWorkflowParent>();

            Assert.Equal("Hello Jeff!", await stub.NestedHelloChildAsync("Jeff"));
        }

        //---------------------------------------------------------------------

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
            // Verify that a workflow whose class and interface names
            // don't match works.  This ensures that the Cadence client
            // doesn't make any assumptions about naming conventions.
            //
            // ...which was happening in earlier times.

            var stub = client.NewWorkflowStub<IWorkflowDifferentNamesInterface>();

            Assert.Equal($"Hello Jeff!", await stub.HelloAsync("Jeff"));
        }

        //---------------------------------------------------------------------

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
            // Verify that we see an exception thrown by a workflow.

            var options = new WorkflowOptions()
            {
                TaskStartToCloseTimeout = TimeSpan.FromSeconds(5)
            };

            var stub = client.NewWorkflowStub<IWorkflowFail>(options);

            try
            {
                await stub.RunAsync();
            }
            catch (Exception e)
            {
                Assert.IsType<CadenceGenericException>(e);
                Assert.Contains("ArgumentException", e.Message);
                Assert.Contains("forced-failure", e.Message);
            }
        }

        //---------------------------------------------------------------------

        public interface IWorkflowUnregistered : IWorkflow
        {
            [WorkflowMethod]
            Task<string> HelloAsync(string name);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_Unregistered()
        {
            // Verify that we see an error when attempting to execute an
            // unregistered workflow.  In this case, there is no class
            // defined that implements the workflow.

            var options = new WorkflowOptions()
            {
                ScheduleToCloseTimeout = TimeSpan.FromSeconds(5)
            };

            var stub = client.NewWorkflowStub<IWorkflowUnregistered>(options);

            await Assert.ThrowsAsync<CadenceTimeoutException>(async () => await stub.HelloAsync("Jack"));
        }

        //---------------------------------------------------------------------

        public class ComplexData
        {
            public string Name { get; set; }
            public int Age { get; set; }
        }

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
        }

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
                return await Task.FromResult(new WorkflowInfoTest(Workflow.WorkflowInfo));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_Info()
        {
            // Verify that the [Workflow.WorkflowInfo] properties are
            // set correctly for a workflow.

            var options = new WorkflowOptions()
            {
                Domain     = client.Settings.DefaultDomain,
                TaskList   = client.Settings.DefaultTaskList,
                WorkflowId = "my-workflow-id"
            };

            var stub = client.NewWorkflowStub<IWorkflowInfo>(options: options, workflowTypeName: "my-workflow-info-type");
            var info = await stub.GetWorkflowInfoAsync();

            Assert.Equal(options.Domain, info.Domain);
            Assert.NotEmpty(info.RunId);
            Assert.Equal(options.TaskList, info.TaskList);
            Assert.Equal(options.WorkflowId, info.WorkflowId);
            Assert.Equal("my-workflow-info-type", info.WorkflowType);

            // $todo(jeff.lill):
            //
            // These properties are not supported yet:
            //
            //      ExecutionStartToCloseTimeout
            //      ChildPolicy
        }

        //---------------------------------------------------------------------

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

                var options = new ContinueAsNewOptions();
                var stub    = Workflow.NewContinueAsNewStub<IWorkflowContinueAsNew1>(options);

                await stub.HelloAsync(name);
                throw new Exception("We should never reach this.");
            }
        }

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
            // Verify that we can continue a workflow as new without using a stub
            // and with the same options.

            var stub = client.NewWorkflowStub<IWorkflowContinueAsNew0>();

            Assert.Equal("WF0 says: Hello Jeff!", await stub.HelloAsync("Jeff", 1));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_ContinueAsNew_Options()
        {
            // Verify that we can continue a workflow as new without using a stub
            // and with new options.

            // $todo(jeff.lill):
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
            // Verify that we can continue a workflow as new using a stub
            // and with the same options.

            var stub = client.NewWorkflowStub<IWorkflowContinueAsNew0>();

            Assert.Equal("WF1 says: Hello Jeff!", await stub.HelloStubAsync("Jeff"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_ContinueAsNew_StubOptions()
        {
            // Verify that we can continue a workflow as new using a stub
            // and with new options.

            // $todo(jeff.lill):
            //
            // This test could be improved.  We're not actually verifying that
            // the new options actually had an effect.  For now, we're just
            // ensuring that the client doesn't barf.

            var stub = client.NewWorkflowStub<IWorkflowContinueAsNew0>();

            Assert.Equal("WF1 says: Hello Jeff!", await stub.HelloStubOptionsAsync("Jeff"));
        }

#if TODO
        // $todo(jeff.lill):
        //
        // I'm not actually sure what the point of external child workflow stubs
        // are and there are some implementation gaps.  We're going to leave these
        // unimplemented for now and revisit later.
        //
        //      https://github.com/nforgeio/neonKUBE/issues/615
        //
        // Note that the one test by workflow ID below is coded and that we'd need
        // to implement another test to do the same by workflow execution.

        //---------------------------------------------------------------------

        public interface IWorkflowExternalChildStubById : IWorkflow
        {
            [WorkflowMethod]
            Task<string> RunAsync();

            [QueryMethod("query")]
            Task<string> QueryAsync(string name);

            [SignalMethod("signal")]
            Task SignalExit(string value);
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowExternalChildStubById : WorkflowBase, IWorkflowExternalChildStubById
        {
            private string  signalValue;
            private bool    signaled;

            public async Task<string> RunAsync()
            {
                // Spin for up to 20 seconds, waiting for SignalExit() to be called
                // and then throw an exception if there was no signal or else return
                // the signal value passed.

                for (int i = 0; i < 20; i++)
                {
                    if (signaled)
                    {
                        break;
                    }

                    await Workflow.SleepAsync(TimeSpan.FromSeconds(1));
                }

                if (!signaled)
                {
                    throw new Exception("Signal not received in time.");
                }

                return await Task.FromResult(signalValue);
            }

            public async Task<string> QueryAsync(string name)
            {
                return await Task.FromResult($"Hello {name}!");
            }

            public async Task SignalExit(string value)
            {
                signalValue = value;
                signaled    = true;

                await Task.CompletedTask;
            }
        }

        public interface IWorkflowExternalParentStubById : IWorkflow
        {
            [WorkflowMethod]
            Task<string> RunAsync();
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowExternalParentStubById : WorkflowBase, IWorkflowExternalParentStubById
        {
            public async Task<string> RunAsync()
            {
                // Start a child workflow normally and then create an external stub for it.
                // We'll use this stub to verify that:
                //
                //      1. We cannot use the stub to re-execute the workflow.
                //      2. We can query the workflow.
                //      3. We can signal the workflow, causing it to complete.
                //
                // NOTE: This code is a a somewhat fragile due to the general situation
                //       decribed by:
                //
                //       https://github.com/nforgeio/neonKUBE/issues/627
                //
                // We're going to temporarily introduce delays to mitigate this. 

                const string workflowId = "my-child-workflow-external-1";
                const string signalArg  = "Hello World!";

                var delay = TimeSpan.FromSeconds(0.5);

                var options      = new ChildWorkflowOptions() { WorkflowId = workflowId };
                var stub         = Workflow.NewChildWorkflowStub<IWorkflowExternalChildStubById>(options);
                var task         = stub.RunAsync();
                var externalStub = Workflow.NewExternalWorkflowStub<IWorkflowExternalChildStubById>(workflowId);

                await Task.Delay(delay);

                // Verify that we're not allowed to re-execute the workflow via the external stub.

                var executed = false;

                try
                {
                    await externalStub.RunAsync();
                    executed = true;
                }
                catch (InvalidOperationException)
                {
                }
                catch (Exception e)
                {
                    return $"Unexpected [{e.GetType().FullName}] exeception.";
                }

                if (executed)
                {
                    return "External stub allowed re-execution.";
                }

                // Ensure that the child exits after receiving a signal via the external stub.

                await externalStub.SignalExit(signalArg);

                try
                {
                    var result = await task;

                    if (result != signalArg)
                    {
                        return $"Invalid signal result: [{result}] instead of [{signalArg}]";
                    }
                    else
                    {
                        return result;
                    }
                }
                catch (Exception e)
                {
                    return NeonHelper.ExceptionError(e);
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_ExternalChildStubById()
        {
            // Verifies that stubs returned by Workflow.NewExternalWorkflowStub(workflowId)
            // work correctly.

            var stub   = client.NewWorkflowStub<IWorkflowExternalParentStubById>();
            var result = await stub.RunAsync();

            if (result != null)
            {
                Assert.True(false, $"Test Error: {result}");
            }
        }
#endif
    }
}
