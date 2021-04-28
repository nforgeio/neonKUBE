//-----------------------------------------------------------------------------
// FILE:	    Test_AsyncManualResetEvent.cs
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
    public class Test_AsyncManualResetEvent
    {
        private TimeSpan defaultTimeout = TimeSpan.FromSeconds(15);

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public void Basic()
        {
            bool taskRunning;
            bool taskCompleted;

            // Verify that an event that starts out unsignalled doesn't allow
            // a task to execute.

            using (var manualEvent = new AsyncManualResetEvent())
            {
                taskRunning = false;
                taskCompleted = false;

                Task.Run(async () =>
                {
                    taskRunning = true;
                    await manualEvent.WaitAsync();
                    taskCompleted = true;
                });

                NeonHelper.WaitFor(() => taskRunning, defaultTimeout);
                Thread.Sleep(TimeSpan.FromSeconds(5));
                Assert.False(taskCompleted);
            }

            // Verify that an event that starts out signalled does allow
            // a task to execute.

            using (var manualEvent = new AsyncManualResetEvent(true))
            {
                taskRunning = false;
                taskCompleted = false;

                Task.Run(async () =>
                {
                    taskRunning = true;
                    await manualEvent.WaitAsync();
                    taskCompleted = true;
                });

                NeonHelper.WaitFor(() => taskCompleted, defaultTimeout);
            }

            // Verify that an event that starts out unsignalled and is subsequently
            // signalled allows a task to complete.

            using (var manualEvent = new AsyncManualResetEvent(false))
            {
                taskRunning = false;
                taskCompleted = false;

                Task.Run(async () =>
                {
                    taskRunning = true;
                    await manualEvent.WaitAsync();
                    taskCompleted = true;
                });

                NeonHelper.WaitFor(() => taskRunning, defaultTimeout);
                Assert.False(taskCompleted);
                manualEvent.Set();
                NeonHelper.WaitFor(() => taskCompleted, defaultTimeout);
            }

            // Verify that an event that can be signalled while already signalled
            // without a problem.

            using (var manualEvent = new AsyncManualResetEvent(false))
            {
                taskRunning = false;
                taskCompleted = false;

                Task.Run(async () =>
                {
                    taskRunning = true;
                    await manualEvent.WaitAsync();
                    taskCompleted = true;
                });

                NeonHelper.WaitFor(() => taskRunning, defaultTimeout);
                Assert.False(taskCompleted);
                manualEvent.Set();
                manualEvent.Set();
                manualEvent.Set();
                manualEvent.Set();
                NeonHelper.WaitFor(() => taskCompleted, defaultTimeout);
            }

            // Verify that an event that starts out unsignalled is subsequently
            // signalled allows a task to complete, that another task also completes
            // on the signalled event and then when the event is reset, the next
            // task will block.

            using (var manualEvent = new AsyncManualResetEvent(false))
            {
                taskRunning = false;
                taskCompleted = false;

                Task.Run(async () =>
                {
                    taskRunning = true;
                    await manualEvent.WaitAsync();
                    taskCompleted = true;
                });

                NeonHelper.WaitFor(() => taskRunning, defaultTimeout);
                Assert.False(taskCompleted);
                manualEvent.Set();
                NeonHelper.WaitFor(() => taskCompleted, defaultTimeout);

                // Verify that we can another task won't block on the already 
                // signalled event.

                taskRunning = false;
                taskCompleted = false;

                Task.Run(async () =>
                {
                    taskRunning = true;
                    await manualEvent.WaitAsync();
                    taskCompleted = true;
                });

                NeonHelper.WaitFor(() => taskRunning, defaultTimeout);
                NeonHelper.WaitFor(() => taskCompleted, TimeSpan.FromSeconds(5));
                Assert.True(taskCompleted);

                // Now reset the event and verify that the next task blocks.

                manualEvent.Reset();

                taskRunning = false;
                taskCompleted = false;

                Task.Run(async () =>
                {
                    taskRunning = true;
                    await manualEvent.WaitAsync();
                    taskCompleted = true;
                });

                NeonHelper.WaitFor(() => taskRunning, defaultTimeout);
                Thread.Sleep(TimeSpan.FromSeconds(5));
                Assert.False(taskCompleted);
            }

            // Verify that we can reuse an event multiple times.

            using (var manualEvent = new AsyncManualResetEvent(false))
            {
                for (int i = 0; i < 10; i++)
                {
                    taskRunning = false;
                    taskCompleted = false;

                    Task.Run(async () =>
                    {
                        taskRunning = true;
                        await manualEvent.WaitAsync();
                        taskCompleted = true;
                    });

                    NeonHelper.WaitFor(() => taskRunning, defaultTimeout);
                    manualEvent.Set();
                    NeonHelper.WaitFor(() => taskCompleted, defaultTimeout);

                    manualEvent.Reset();
                }
            }

            // Verify that we can dispose an event multiple times without an error.

            using (var manualEvent = new AsyncManualResetEvent(false))
            {
                manualEvent.Dispose();
            }
        }

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
        public void MultipleThreads()
        {
            // Verify that an event that starts out unsignalled doesn't allow
            // any tasks to execute.

            using (var manualEvent = new AsyncManualResetEvent(false))
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
                                await manualEvent.WaitAsync();
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

            // Verify that an event that starts out signalled allows
            // multiple tasks to execute.

            using (var manualEvent = new AsyncManualResetEvent(true))
            {
                var taskInfo = new TaskStateCollection();

                for (int i = 0; i < taskInfo.Count; i++)
                {
                    new Task(
                        async state =>
                        {
                            int taskIndex = (int)state;

                            taskInfo[taskIndex].IsRunning = true;
                            await manualEvent.WaitAsync();
                            taskInfo[taskIndex].IsComplete = true;
                        },
                        i).Start();
                }

                NeonHelper.WaitFor(() => taskInfo.AllRunning, defaultTimeout);
                NeonHelper.WaitFor(() => taskInfo.AllComplete, defaultTimeout);
            }

            // Verify that an event that starts out unsignalled and is subsequently
            // signalled allows multiple tasks to complete.

            using (var manualEvent = new AsyncManualResetEvent(false))
            {
                var taskInfo = new TaskStateCollection();

                for (int i = 0; i < taskInfo.Count; i++)
                {
                    new Task(
                        async state =>
                        {
                            int taskIndex = (int)state;

                            taskInfo[taskIndex].IsRunning = true;
                            await manualEvent.WaitAsync();
                            taskInfo[taskIndex].IsComplete = true;
                        },
                        i).Start();
                }

                NeonHelper.WaitFor(() => taskInfo.AllRunning, defaultTimeout);
                Assert.False(taskInfo.AnyComplete);
                manualEvent.Set();
                NeonHelper.WaitFor(() => taskInfo.AllComplete, defaultTimeout);
            }

            // Verify that we can reuse an event multiple times for multiple tasks.

            using (var manualEvent = new AsyncManualResetEvent(false))
            {
                for (int j = 0; j < 10; j++)
                {
                    var taskInfo = new TaskStateCollection();

                    for (int i = 0; i < taskInfo.Count; i++)
                    {
                        new Task(
                            async state =>
                            {
                                int taskIndex = (int)state;

                                taskInfo[taskIndex].IsRunning = true;
                                await manualEvent.WaitAsync();
                                taskInfo[taskIndex].IsComplete = true;
                            },
                            i).Start();
                    }

                    NeonHelper.WaitFor(() => taskInfo.AllRunning, defaultTimeout);
                    Assert.False(taskInfo.AnyComplete);
                    manualEvent.Set();
                    NeonHelper.WaitFor(() => taskInfo.AllComplete, defaultTimeout);
                    manualEvent.Reset();
                }
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public void Error()
        {
            AsyncManualResetEvent manualEvent;

            // Verify that we get and [ObjectDisposedException] for [Set()] and [Reset()]
            // a disposed event.

            manualEvent = new AsyncManualResetEvent();

            manualEvent.Dispose();
            Assert.Throws<ObjectDisposedException>(() => manualEvent.Set());
            Assert.Throws<ObjectDisposedException>(() => manualEvent.Reset());
            Task.Run(() => Assert.ThrowsAsync<ObjectDisposedException>(async () => await manualEvent.WaitAsync())).Wait();

            // Verify that disposing an event causes any waiting tasks
            // to unblock with an [ObjectDisposedException].

            manualEvent = new AsyncManualResetEvent();

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
                            await manualEvent.WaitAsync();
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

            manualEvent.Dispose();

            NeonHelper.WaitFor(() => taskInfo.AllFaulted, defaultTimeout);
            Assert.False(badException);
        }
    }
}
