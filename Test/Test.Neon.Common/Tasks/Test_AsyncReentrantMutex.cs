//-----------------------------------------------------------------------------
// FILE:	    Test_AsyncReentrantMutex.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
    [Trait(TestTrait.Category, TestArea.NeonCommon)]
    public class Test_AsyncReentrantMutex
    {
        private TimeSpan defaultTimeout = TimeSpan.FromSeconds(15);  // Maximum time to wait for a test operation to complete.

        [Fact]
        public async Task Nested_Action()
        {
            // Verify that action reentrancy actually works.

            var inner1 = false;
            var inner2 = false;
            var inner3 = false;

            using (var mutex = new AsyncReentrantMutex())
            {
                await mutex.ExecuteActionAsync(
                    async () =>
                    {
                        inner1 = true;

                        await mutex.ExecuteActionAsync(
                            async () =>
                            {
                                inner2 = true;

                                await mutex.ExecuteActionAsync(
                                    async () =>
                                    {
                                        inner3 = true;

                                        await Task.CompletedTask;
                                    });
                            });
                    });
            }

            Assert.True(inner1);
            Assert.True(inner2);
            Assert.True(inner3);
        }

        [Fact]
        public async Task Blocked_Action()
        {
            // Verify that non-nested action acquistions block.

            using (var mutex = new AsyncReentrantMutex())
            {
                var task1Time = DateTime.MinValue;
                var task2Time = DateTime.MinValue;

                var task1 = mutex.ExecuteActionAsync(
                    async () =>
                    {
                        task1Time = DateTime.UtcNow;

                        await Task.Delay(TimeSpan.FromSeconds(2));
                    });

                var task2 = mutex.ExecuteActionAsync(
                    async () =>
                    {
                        task2Time = DateTime.UtcNow;

                        await Task.Delay(TimeSpan.FromSeconds(2));
                    });

                await task1;
                await task2;

                // So the two tasks above could execute in any order, but only
                // one at a time.  With the delay, this means that the recorded
                // times should be at least 2 seconds apart.
                //
                // We'll verify at least a 1 second difference to mitigate any
                // clock skew.

                Assert.True(task1Time > DateTime.MinValue);
                Assert.True(task2Time > DateTime.MinValue);

                var delta = task1Time - task2Time;

                if (delta < TimeSpan.Zero)
                {
                    delta = -delta;
                }

                Assert.True(delta >= TimeSpan.FromSeconds(1));
            }
        }

        [Fact]
        public async Task Dispose_Action()
        {
            // Verify that [ObjectDisposedException] is thrown for action tasks waiting
            // to acquire the mutex.

            var mutex = new AsyncReentrantMutex();

            try
            {
                // Hold the mutex for 2 seconds so the tasks below will block.

                var task1Acquired = false;
                var task2Acquired = false;
                var task3Acquired = false;

                var task1 = mutex.ExecuteActionAsync(
                    async () =>
                    {
                        task1Acquired = true;
                        await Task.Delay(TimeSpan.FromSeconds(2));
                    });

                // Wait for [task1] to actually acquire to mutex.

                NeonHelper.WaitFor(() => task1Acquired, defaultTimeout);

                // Start two new tasks that will block.

                var task2 = mutex.ExecuteActionAsync(
                    async () =>
                    {
                        task2Acquired = true;
                        await Task.CompletedTask;
                    });

                var task3 = mutex.ExecuteActionAsync(
                    async () =>
                    {
                        task3Acquired = true;
                        await Task.CompletedTask;
                    });

                // Dispose the mutex.  We're expecting [task1] to complete normally and
                // [task2] and [task3] to fail with an [OperationCancelledException] with
                // their actions never being invoked.

                mutex.Dispose();

                await Assert.ThrowsAsync<ObjectDisposedException>(async () => await task2);
                Assert.False(task2Acquired);

                await Assert.ThrowsAsync<ObjectDisposedException>(async () => await task3);
                Assert.False(task3Acquired);

                await task1;
                Assert.True(task1Acquired);
            }
            finally
            {
                // Disposing this again shouldn't cause any trouble.

                mutex.Dispose();
            }
        }

        [Fact]
        public async Task Nested_Func()
        {
            // Verify that function reentrancy actually works.

            var inner1 = false;
            var inner2 = false;
            var inner3 = false;

            using (var mutex = new AsyncReentrantMutex())
            {
                var result = await mutex.ExecuteFuncAsync(
                    async () =>
                    {
                        inner1 = true;

                        return await mutex.ExecuteFuncAsync(
                            async () =>
                            {
                                inner2 = true;

                                return await mutex.ExecuteFuncAsync(
                                    async () =>
                                    {
                                        inner3 = true;

                                        return await Task.FromResult("HELLO WORLD!");
                                    });
                            });
                    });

                Assert.Equal("HELLO WORLD!", result);
            }

            Assert.True(inner1);
            Assert.True(inner2);
            Assert.True(inner3);
        }

        [Fact]
        public async Task Blocked_Func()
        {
            // Verify that non-nested function acquistions block.

            using (var mutex = new AsyncReentrantMutex())
            {
                var task1Time = DateTime.MinValue;
                var task2Time = DateTime.MinValue;

                var task1 = mutex.ExecuteFuncAsync(
                    async () =>
                    {
                        task1Time = DateTime.UtcNow;

                        await Task.Delay(TimeSpan.FromSeconds(2));
                        return "TASK1";
                    });

                var task2 = mutex.ExecuteFuncAsync(
                    async () =>
                    {
                        task2Time = DateTime.UtcNow;

                        await Task.Delay(TimeSpan.FromSeconds(2));
                        return "TASK2";
                    });

                var result1 = await task1;
                var result2 = await task2;

                Assert.Equal("TASK1", result1);
                Assert.Equal("TASK2", result2);

                // So the two tasks above could execute in any order, but only
                // one at a time.  With the delay, this means that the recorded
                // times should be at least 2 seconds apart.
                //
                // We'll verify at least a 1 second difference to mitigate any
                // clock skew.

                Assert.True(task1Time > DateTime.MinValue);
                Assert.True(task2Time > DateTime.MinValue);

                var delta = task1Time - task2Time;

                if (delta < TimeSpan.Zero)
                {
                    delta = -delta;
                }

                Assert.True(delta >= TimeSpan.FromSeconds(1));
            }
        }

        [Fact]
        public async Task Dispose_Func()
        {
            // Verify that [ObjectDisposedException] is thrown for function tasks waiting
            // to acquire the mutex.

            var mutex = new AsyncReentrantMutex();

            try
            {
                // Hold the mutex for 2 seconds so the tasks below will block.

                var task1Acquired = false;
                var task2Acquired = false;
                var task3Acquired = false;

                var task1 = mutex.ExecuteFuncAsync(
                    async () =>
                    {
                        task1Acquired = true;
                        await Task.Delay(TimeSpan.FromSeconds(2));
                        return "TASK1";
                    });

                // Wait for [task1] to actually acquire to mutex.

                NeonHelper.WaitFor(() => task1Acquired, defaultTimeout);

                // Start two new tasks that will block.

                var task2 = mutex.ExecuteFuncAsync(
                    async () =>
                    {
                        task2Acquired = true;
                        await Task.CompletedTask;
                        return "TASK2";
                    });

                var task3 = mutex.ExecuteFuncAsync(
                    async () =>
                    {
                        task3Acquired = true;
                        await Task.CompletedTask;
                        return "TASK1";
                    });

                // Dispose the mutex.  We're expecting [task1] to complete normally and
                // [task2] and [task3] to fail with an [OperationCancelledException] with
                // their actions never being invoked.

                mutex.Dispose();

                await Assert.ThrowsAsync<ObjectDisposedException>(async () => await task2);
                Assert.False(task2Acquired);

                await Assert.ThrowsAsync<ObjectDisposedException>(async () => await task3);
                Assert.False(task3Acquired);

                await task1;
                Assert.True(task1Acquired);
            }
            finally
            {
                // Disposing this again shouldn't cause any trouble.

                mutex.Dispose();
            }
        }
    }
}
