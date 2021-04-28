//-----------------------------------------------------------------------------
// FILE:	    Test_AsyncAutoResetEvent.cs
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
    public class Test_AsyncAutoResetEvent
    {
        private TimeSpan defaultTimeout = TimeSpan.FromSeconds(15);

        private class TaskState
        {
            public bool IsRunning;
            public bool IsComplete;
            public bool IsFaulted;
        }

        private class TaskStateCollection : List<TaskState>
        {
            public const int TaskCount = 10;

            public TaskStateCollection()
                : base(TaskCount)
            {
                for (int i = 0; i < TaskCount; i++)
                {
                    Add(new TaskState());
                }
            }

            public bool AllRunning
            {
                get
                {
                    foreach (var state in this)
                    {
                        if (!state.IsRunning)
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }

            public bool AllComplete
            {
                get
                {
                    foreach (var state in this)
                    {
                        if (!state.IsComplete)
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }

            public bool AllFaulted
            {
                get
                {
                    foreach (var state in this)
                    {
                        if (!state.IsFaulted)
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }

            public bool AnyComplete
            {
                get
                {
                    foreach (var state in this)
                    {
                        if (state.IsComplete)
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public void Basic()
        {
            // Verify that an event that starts out unsignalled doesn't allow
            // any tasks to execute.

            using (var autoEvent = new AsyncAutoResetEvent(false))
            {
                var taskInfo = new TaskStateCollection();

                for (int i = 0; i < taskInfo.Count; i++)
                {
                    new Task(
                        async state =>
                        {
                            int taskIndex = (int)state;

                            taskInfo[taskIndex].IsRunning = true;

                            try
                            {
                                await autoEvent.WaitAsync();
                                taskInfo[taskIndex].IsComplete = true;
                            }
                            catch (ObjectDisposedException)
                            {
                                // Ignore these
                            }
                        },
                        i).Start();
                }

                NeonHelper.WaitFor(() => taskInfo.AllRunning, defaultTimeout);
                Thread.Sleep(TimeSpan.FromSeconds(5));
                Assert.False(taskInfo.AnyComplete);
            }

            // Verify that an event that starts out signalled but then
            // resetting it doesn't allow any tasks to execute.

            using (var autoEvent = new AsyncAutoResetEvent(true))
            {
                autoEvent.Reset();

                var taskInfo = new TaskStateCollection();

                for (int i = 0; i < taskInfo.Count; i++)
                {
                    new Task(
                        async state =>
                        {
                            int taskIndex = (int)state;

                            taskInfo[taskIndex].IsRunning = true;

                            try
                            {
                                await autoEvent.WaitAsync();
                                taskInfo[taskIndex].IsComplete = true;
                            }
                            catch (ObjectDisposedException)
                            {
                                // Ignore these
                            }
                        },
                        i).Start();
                }

                NeonHelper.WaitFor(() => taskInfo.AllRunning, defaultTimeout);
                Thread.Sleep(TimeSpan.FromSeconds(5));
                Assert.False(taskInfo.AnyComplete);
            }

            // Verify that an event that starts out unsignalled doesn't allow
            // any tasks to execute and then that every time the event is signalled,
            // a single task is unblocked.

            using (var autoEvent = new AsyncAutoResetEvent(false))
            {
                var taskInfo = new TaskStateCollection();

                for (int i = 0; i < taskInfo.Count; i++)
                {
                    new Task(
                        async state =>
                        {
                            int taskIndex = (int)state;

                            taskInfo[taskIndex].IsRunning = true;

                            try
                            {
                                await autoEvent.WaitAsync();
                                taskInfo[taskIndex].IsComplete = true;
                            }
                            catch (ObjectDisposedException)
                            {
                                // Ignore these
                            }
                        },
                        i).Start();
                }

                NeonHelper.WaitFor(() => taskInfo.AllRunning, defaultTimeout);
                Thread.Sleep(TimeSpan.FromSeconds(5));
                Assert.False(taskInfo.AnyComplete);

                for (int i = 0; i < taskInfo.Count; i++)
                {
                    autoEvent.Set();

                    NeonHelper.WaitFor(
                        () =>
                        {
                            return taskInfo.Where(ti => ti.IsComplete).Count() == i + 1;
                        },
                        defaultTimeout);
                }

                // Also verify that disposing the event multiple time isn't a problem.

                autoEvent.Dispose();
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public async Task Auto()
        {
            // Verify that auto reset events actually reset automatically.

            using (var autoEvent = new AsyncAutoResetEvent())
            {
                var count = 0;

                var task = Task.Run(
                    async () =>
                    {
                        while (true)
                        {
                            await autoEvent.WaitAsync();
                            count++;
                        }
                    });

                // Verify that the event starts out with the RESET state.

                await Task.Delay(2000);
                Assert.Equal(0, count);

                // Verify a single pulse.

                autoEvent.Set();
                await Task.Delay(2000);
                Assert.Equal(1, count);

                // Verify a second pulse.

                autoEvent.Set();
                await Task.Delay(2000);
                Assert.Equal(2, count);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public void Error()
        {
            AsyncAutoResetEvent autoEvent;

            // Verify that we get and [ObjectDisposedException] for [Set()] and [Reset()]
            // a disposed event.

            autoEvent = new AsyncAutoResetEvent();

            autoEvent.Dispose();
            Assert.Throws<ObjectDisposedException>(() => autoEvent.Set());
            Assert.Throws<ObjectDisposedException>(() => autoEvent.Reset());
            Task.Run(() => Assert.ThrowsAsync<ObjectDisposedException>(async () => await autoEvent.WaitAsync())).Wait();

            // Verify that disposing an event causes any waiting tasks
            // to unblock with an [ObjectDisposedException].

            autoEvent = new AsyncAutoResetEvent();

            var taskInfo = new TaskStateCollection();
            var badException = false;

            for (int i = 0; i < taskInfo.Count; i++)
            {
                new Task(
                    async state =>
                    {
                        int taskIndex = (int)state;

                        taskInfo[taskIndex].IsRunning = true;

                        try
                        {
                            await autoEvent.WaitAsync();
                        }
                        catch (ObjectDisposedException)
                        {
                            taskInfo[taskIndex].IsFaulted = true;
                        }
                        catch
                        {
                            badException = true;
                            taskInfo[taskIndex].IsFaulted = true;
                        }

                        taskInfo[taskIndex].IsComplete = true;
                    },
                    i).Start();
            }

            NeonHelper.WaitFor(() => taskInfo.AllRunning, defaultTimeout);
            Assert.False(taskInfo.AnyComplete);

            autoEvent.Dispose();

            NeonHelper.WaitFor(() => taskInfo.AllFaulted, defaultTimeout);
            Assert.False(badException);
        }
    }
}
