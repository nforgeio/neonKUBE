//-----------------------------------------------------------------------------
// FILE:	    Test_AsyncMutex.cs
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
    public class Test_AsyncMutex
    {
        private TimeSpan defaultTimeout = TimeSpan.FromSeconds(15);  // Maximum time to wait for a test operation to complete.
        private const int repeatCount = 4;

        [Fact]
        [Trait(TestTrait.Project, TestProject.NeonCommon)]
        public async Task Basic()
        {
            // Create a mutex and then several tasks that acquire the mutex for
            // a period of time, verifying that each obtains exclusive
            // access.

            var taskCount = 12;
            var refCount  = 0;
            var error     = false;
            var tasks     = new List<Task>();
            var stopwatch = new Stopwatch();
            var testTime  = defaultTimeout - TimeSpan.FromSeconds(2);

            stopwatch.Start();

            using (var mutex = new AsyncMutex())
            {
                for (int i = 0; i < taskCount; i++)
                {
                    tasks.Add(Task.Run(
                        async () =>
                        {
                            while (stopwatch.Elapsed < testTime)
                            {
                                using (await mutex.AcquireAsync())
                                {
                                    if (refCount > 0)
                                    {
                                        // This means that we don't have exclusive access indicating
                                        // that the mutex must be broken.

                                        error = true;
                                    }

                                    try
                                    {
                                        Interlocked.Increment(ref refCount);

                                        await Task.Delay(TimeSpan.FromMilliseconds(250));
                                    }
                                    finally
                                    {
                                        Interlocked.Decrement(ref refCount);
                                    }
                                }
                            }
                        }));

                    await NeonHelper.WaitAllAsync(tasks, defaultTimeout);
                }

                Assert.False(error);
            }
        }

        [Fact]
        [Trait(TestTrait.Project, TestProject.NeonCommon)]
        public async Task Dispose()
        {
            // Create a mutex, acquire it, and then create another task that will
            // attempt to acquire it as well (and will fail because the mutex has
            // already been acquired).  Then dispose the mutex and verify that the
            // waiting task saw the [ObjectDisposedException].

            var mutex    = new AsyncMutex();
            var inTask   = false;
            var acquired = false;
            var disposed = false;

            await mutex.AcquireAsync();

            var task = Task.Run(
                async () =>
                {
                    try
                    {
                        var acquireTask = mutex.AcquireAsync();

                        inTask = true;

                        await acquireTask;

                        acquired = true;
                    }
                    catch (ObjectDisposedException)
                    {
                        disposed = true;
                    }
                });

            // Wait for the task to have called [AcquireAsync()].

            NeonHelper.WaitFor(() => inTask, defaultTimeout);

            // Dispose the mutex, wait for the task to exit and then verify
            // that it caught the [ObjectDisposedException].

            mutex.Dispose();
            task.Wait(defaultTimeout);

            Assert.False(acquired);
            Assert.True(disposed);
        }
    }
}
