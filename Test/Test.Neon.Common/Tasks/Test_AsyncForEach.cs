//-----------------------------------------------------------------------------
// FILE:	    Test_AsyncForEach.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Retry;
using Neon.Tasks;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    [Trait(TestTrait.Category, TestArea.NeonCommon)]
    public class Test_AsyncForEach
    {
        private TimeSpan delay = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Returns the maximum number of pool threads that should be able to
        /// run in parallel on the current machine.
        /// </summary>
        private int MaxPoolThreads
        {
            get
            {
                ThreadPool.GetAvailableThreads(out var workerThreads, out var completionPortThreads);
                Covenant.Assert(workerThreads > 2, "Insufficient pool threads available.");

                return Math.Min(10, workerThreads - 1);
            }
        }

        [Fact]
        public async Task Constrained()
        {
            // Verify that we can constrain the number of tasks that run in
            // parallel to something less than the number of available threads
            // in the pool.

            var syncLock       = new object();
            var maxParallel    = MaxPoolThreads - 2;
            var workItems      = new List<int>();
            var completed      = new HashSet<int>();
            var parallelCount  = 0;
            var parallelCounts = new List<int>();

            Covenant.Assert(maxParallel > 0, "Insufficient pool threads available.");

            for (int i = 0; i < maxParallel + 2; i++)
            {
                workItems.Add(i);
            }

            await Async.ForEachAsync(workItems,
                async item =>
                {
                    lock (syncLock)
                    {
                        parallelCount++;
                        parallelCounts.Add(parallelCount);
                    }

                    await Task.Delay(delay);
                    Interlocked.Decrement(ref parallelCount);

                    lock (syncLock)
                    {
                        parallelCount--;
                        completed.Add(item);
                    }
                },
                maxParallel: maxParallel);

            Assert.True(maxParallel >= parallelCounts.Max());

            // Ensure that all work items completed.

            var allCompleted = true;

            foreach (var item in workItems)
            {
                if (!completed.Contains(item))
                {
                    allCompleted = false;
                    break;
                }
            }

            Assert.True(allCompleted);
        }

        [Fact]
        public async Task Unconstrained()
        {
            // Verify that all tasks run in parallel when the number of
            // tasks is less than the number allowed to run simultaneously.

            var syncLock       = new object();
            var maxParallel    = MaxPoolThreads;
            var workItems      = new List<int>();
            var completed      = new HashSet<int>();
            var parallelCount  = 0;
            var parallelCounts = new List<int>();

            for (int i = 0; i < maxParallel; i++)
            {
                workItems.Add(i);
            }

            await Async.ForEachAsync(workItems,
                async item =>
                {
                    lock (syncLock)
                    {
                        parallelCount++;
                        parallelCounts.Add(parallelCount);
                    }

                    await Task.Delay(delay);
                    Interlocked.Decrement(ref parallelCount);

                    lock (syncLock)
                    {
                        parallelCount--;
                        completed.Add(item);
                    }
                },
                maxParallel: maxParallel);

            Assert.Equal(maxParallel, parallelCounts.Max());

            // Ensure that all work items completed.

            var allCompleted = true;

            foreach (var item in workItems)
            {
                if (!completed.Contains(item))
                {
                    allCompleted = false;
                    break;
                }
            }

            Assert.True(allCompleted);
        }

        [Fact]
        public async Task Cancellation()
        {
            // Verify that cancellation tokens work.

            var syncLock       = new object();
            var maxParallel    = MaxPoolThreads - 2;
            var workItems      = new List<int>();
            var cts            = new CancellationTokenSource();
            var completed      = new HashSet<int>();
            var parallelCount  = 0;
            var parallelCounts = new List<int>();

            Covenant.Assert(maxParallel > 0, "Insufficient pool threads available.");

            for (int i = 0; i < maxParallel + 2; i++)
            {
                workItems.Add(i);
            }

            var task = Async.ForEachAsync(workItems,
                async (item, cancellationToken) =>
                {
                    lock (syncLock)
                    {
                        parallelCount++;
                        parallelCounts.Add(parallelCount);
                    }

                    await Task.Delay(delay);
                    Interlocked.Decrement(ref parallelCount);

                    cancellationToken.ThrowIfCancellationRequested();

                    lock (syncLock)
                    {
                        parallelCount--;
                        completed.Add(item);
                    }
                },
                cancellationToken: cts.Token,
                maxParallel:       maxParallel);

            // Wait 1/2 of the execution time for each work item
            // and cancel the operation.  Then wait for the overall
            // operation to complete with an [OperationCanceledException].

            await Task.Delay(TimeSpan.FromSeconds(delay.TotalSeconds / 2));
            cts.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await task);

            // Verify

            Assert.True(maxParallel >= parallelCounts.Max());
            Assert.Empty(completed);
        }

        [Fact]
        public async Task Exception()
        {
            // Verify that exceptions thrown by work items work as expected.

            var syncLock       = new object();
            var maxParallel    = MaxPoolThreads - 2;
            var workItems      = new List<int>();
            var completed      = new HashSet<int>();
            var parallelCount  = 0;
            var parallelCounts = new List<int>();

            Covenant.Assert(maxParallel > 0, "Insufficient pool threads available.");

            for (int i = 0; i < maxParallel + 2; i++)
            {
                workItems.Add(i);
            }

            var task = Async.ForEachAsync(workItems,
                async item =>
                {
                    lock (syncLock)
                    {
                        parallelCount++;
                        parallelCounts.Add(parallelCount);
                    }

                    await Task.Delay(delay);
                    Interlocked.Decrement(ref parallelCount);

                    // Only the first item will throw an exception.

                    if (item == 0)
                    {
                        throw new NotSupportedException("test exception");
                    }

                    lock (syncLock)
                    {
                        parallelCount--;
                        completed.Add(item);
                    }
                },
                maxParallel: maxParallel);

            // Wait 1/2 of the execution time for each work item
            // and cancel the operation.  Then wait for the overall
            // operation to complete with an [OperationCanceledException].

            await Task.Delay(TimeSpan.FromSeconds(delay.TotalSeconds / 2));

            try
            {
                await task;
                Assert.True(false, $"Expected a [{nameof(NotSupportedException)}].");
            }
            catch (NotSupportedException)
            {
            }
            catch (Exception e)
            {
                Assert.True(false, $"Expected a [{nameof(NotSupportedException)}] but got a [{e.GetType().FullName}].");
            }

            // Verify

            Assert.True(maxParallel >= parallelCounts.Max());
            Assert.Equal(workItems.Count - 1, completed.Count);     // The first work item should not have completed
        }
    }
}
