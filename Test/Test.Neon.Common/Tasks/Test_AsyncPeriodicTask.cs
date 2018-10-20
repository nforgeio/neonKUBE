//-----------------------------------------------------------------------------
// FILE:	    Test_AsyncPeriodicTask.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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
    public class Test_AsyncPeriodicTask
    {
        private TimeSpan defaultTimeout = TimeSpan.FromSeconds(15);

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task SimpleAsync()
        {
            // Verify that we can execute a simple periodic task that terminates 
            // itself by returning TRUE.

            var taskCalls = 0;

            var periodicTask = new AsyncPeriodicTask(
                interval: TimeSpan.FromSeconds(0.5),
                onTaskAsync:
                    async () =>
                    {
                        await Task.CompletedTask;
                        return ++taskCalls == 5;
                    });

            await periodicTask.Run();

            Assert.Equal(5, taskCalls);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task ExceptionAsync()
        {
            // Verify that the exception callback is called and that
            // we can terminate the task by returning TRUE.

            var taskCalls = 0;
            var exception = (Exception)null;

            var periodicTask = new AsyncPeriodicTask(
                interval: TimeSpan.FromSeconds(0.5),
                onTaskAsync:
                    async () =>
                    {
                        taskCalls++;
                        await Task.CompletedTask;
                        throw new TimeoutException();
                    },
                onExceptionAsync:
                    async e =>
                    {
                        exception = e;
                        await Task.CompletedTask;
                        return taskCalls == 5;
                    });

            await periodicTask.Run();

            Assert.Equal(5, taskCalls);
            Assert.IsType<TimeoutException>(exception);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task TerminateViaTaskAsync()
        {
            // Verify that the termination callback is called when the task
            // is terminated by the task callback.

            var terminated = false;
            var exception  = (Exception)null;

            var periodicTask = new AsyncPeriodicTask(
                interval: TimeSpan.FromSeconds(0.5),
                onTaskAsync:
                    async () =>
                    {
                        await Task.CompletedTask;
                        return true;
                    },
                onExceptionAsync:
                    async e =>
                    {
                        exception = e;
                        await Task.CompletedTask;
                        return false;
                    },
                onTerminateAsync:
                    async () =>
                    {
                        terminated = true;
                        await Task.CompletedTask;
                    });

            await periodicTask.Run();

            Assert.True(terminated);
            Assert.Null(exception);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task TerminateViaExceptionAsync()
        {
            // Verify that the termination callback is called when the task
            // throws an exception and there's no exception callback.

            var terminated = false;
            var exception  = (Exception)null;

            var periodicTask = new AsyncPeriodicTask(
                interval: TimeSpan.FromSeconds(0.5),
                onTaskAsync:
                    async () =>
                    {
                        await Task.CompletedTask;
                        throw new TimeoutException();
                    },
                onTerminateAsync:
                    async () =>
                    {
                        terminated = true;
                        await Task.CompletedTask;
                    });

            await periodicTask.Run();

            Assert.True(terminated);
            Assert.Null(exception);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task TerminateViaExceptionHandlerAsync()
        {
            // Verify that the termination callback is called when the task
            // throws an exception and the exception callback returns TRUE.

            var terminated = false;
            var exception  = (Exception)null;

            var periodicTask = new AsyncPeriodicTask(
                interval: TimeSpan.FromSeconds(0.5),
                onTaskAsync:
                    async () =>
                    {
                        await Task.CompletedTask;
                        throw new TimeoutException();
                    },
                onExceptionAsync:
                    async e =>
                    {
                        exception = e;
                        await Task.CompletedTask;
                        return true;
                    },
                onTerminateAsync:
                    async () =>
                    {
                        terminated = true;
                        await Task.CompletedTask;
                    });

            await periodicTask.Run();

            Assert.True(terminated);
            Assert.IsType<TimeoutException>(exception);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task TerminateViaExternalCancellationAsync()
        {
            // Verify that the termination callback is called when the task
            // is cancelled from outside the task and also that the exception
            // callback was not called.

            var terminated = false;
            var exception  = (Exception)null;

            var periodicTask = new AsyncPeriodicTask(
                interval: TimeSpan.FromSeconds(0.5),
                onTaskAsync:
                    async () =>
                    {
                        await Task.CompletedTask;
                        return false;
                    },
                onExceptionAsync:
                    async e =>
                    {
                        exception = e;
                        await Task.CompletedTask;
                        return false;
                    },
                onTerminateAsync:
                    async () =>
                    {
                        terminated = true;
                        await Task.CompletedTask;
                    });

            var tasks = new Task[]
                {
                    periodicTask.Run(),
                    Task.Run(
                        async () =>
                        {
                            await Task.Delay(TimeSpan.FromSeconds(2)); 
                            periodicTask.CancellationTokenSource.Cancel();
                        })
                };

            await NeonHelper.WaitAllAsync(tasks, TimeSpan.FromSeconds(10));

            Assert.True(terminated);
            Assert.Null(exception);
        }
    }
}
