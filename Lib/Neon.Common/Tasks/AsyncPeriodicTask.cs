//-----------------------------------------------------------------------------
// FILE:	    AsyncPeriodicTask.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Diagnostics;

namespace Neon.Tasks
{
    /// <summary>
    /// Implements a common asynchronous coding pattern where an asynchronous
    /// operation is performed periodically and cancellation and exceptions
    /// are handled cleanly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class provides a nice way to implement the very common server
    /// side pattern where we need to periodically perform some operation 
    /// until the operation signals that it's done or the task is canceled,
    /// while handling some of the messy exception handling details.
    /// </para>
    /// <para>
    /// You'll use the <see cref="AsyncPeriodicTask.AsyncPeriodicTask(TimeSpan, Func{Task{bool}}, Func{Exception, Task{bool}}, Func{Task}, CancellationTokenSource)"/>
    /// constructor to create a task, passing the task interval, asynchronous task handler and optional exception handler and cancellation token
    /// and then call <see cref="AsyncPeriodicTask.Run"/> to execute the task.
    /// </para>
    /// <para>
    /// <see cref="AsyncPeriodicTask"/> will call the task handler, wait for the interval and then repeat.  The task handler
    /// return <c>false</c> to continue running or <c>true</c> to signal that <see cref="AsyncPeriodicTask"/> should stop.
    /// <see cref="AsyncPeriodicTask"/> also monitors the cancellation token passed and watches for <see cref="OperationCanceledException"/>
    /// thrown by the task handler to stop itself.
    /// </para>
    /// <para>
    /// The exception handler will be called for all exceptions thrown by the task handler except for <see cref="OperationCanceledException"/>
    /// exceptions as these signal that <see cref="AsyncPeriodicTask"/> should terminate.  Exception handlers return <c>false</c> to continue 
    /// running or <c>true</c> to signal that <see cref="AsyncPeriodicTask"/> should stop.
    /// stop.
    /// </para>
    /// <para>
    /// Finally, an optional handler can be specified that will be called just before the <see cref="AsyncPeriodicTask"/> terminates.
    /// </para>
    /// <note>
    /// This class implements <see cref="IDisposable"/> and the task will be terminated
    /// when this is called.
    /// </note>
    /// </remarks>
    public sealed class AsyncPeriodicTask : IDisposable
    {
        private Func<Task<bool>>            onTaskAsync;
        private Func<Exception, Task<bool>> onExceptionAsync;
        private Func<Task>                  onTerminateAsync;

        /// <summary>
        /// Constructs a periodic task.
        /// </summary>
        /// <param name="interval">The interval between task executions.</param>
        /// <param name="onTaskAsync">Called periodically to implement the task.  The callback should return <c>true</c> if the task should terminate.</param>
        /// <param name="onExceptionAsync">Optional callback that handles exceptions thrown by the task.  The callback should return <c>true</c> if the task should terminate.</param>
        /// <param name="onTerminateAsync">Optional callback that will be called just before the task terminates.</param>
        /// <param name="cancellationTokenSource">Optionally specifies the <see cref="CancellationTokenSource"/> that can be used to stop the task.</param>
        public AsyncPeriodicTask(TimeSpan interval, Func<Task<bool>> onTaskAsync, Func<Exception, Task<bool>> onExceptionAsync = null, Func<Task> onTerminateAsync = null, CancellationTokenSource cancellationTokenSource = default)
        {
            Covenant.Requires<ArgumentException>(interval >= TimeSpan.Zero);
            Covenant.Requires<ArgumentNullException>(onTaskAsync != null);

            this.Interval                = interval;
            this.onTaskAsync             = onTaskAsync;
            this.onExceptionAsync        = onExceptionAsync;
            this.onTerminateAsync        = onTerminateAsync;
            this.CancellationTokenSource = cancellationTokenSource ?? new CancellationTokenSource();
        }

        /// <summary>
        /// Stops the task if it's running.
        /// </summary>
        public void Dispose()
        {
            CancellationTokenSource.Cancel();
        }

        /// <summary>
        /// Returns the task interval.
        /// </summary>
        public TimeSpan Interval { get; private set; }

        /// <summary>
        /// Returns the cancellation token.
        /// </summary>
        public CancellationTokenSource CancellationTokenSource { get; private set; }

        /// <summary>
        /// Asynchronously executes the task until it exits or is canceled.
        /// </summary>
        public async Task Run()
        {
            while (true)
            {
                // Proactively test for cancellation.

                if (CancellationTokenSource.IsCancellationRequested)
                {
                    await OnTerminateAsync();
                    return;
                }

                // Execute the task, terminating the task when we see a [OperationCanceledException]
                // and calling the exception handler for other exception.k

                try
                {
                    if (await onTaskAsync())
                    {
                        await OnTerminateAsync();
                        return;
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception e)
                {
                    if (await OnExceptionAsync(e))
                    {
                        await OnTerminateAsync();
                        return;
                    }
                }

                // Pause for the interval.

                try
                {
                    await Task.Delay(Interval, CancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    await OnTerminateAsync();
                    return;
                }
            }
        }

        /// <summary>
        /// Asynchronously invokes the termination handler (if present).
        /// </summary>
        /// <returns></returns>
        private async Task OnTerminateAsync()
        {
            if (onTerminateAsync != null)
            {
                await onTerminateAsync();
            }
            else
            {
                await Task.CompletedTask;
            }
        }

        /// <summary>
        /// Asynchronously executes the exception handler (if present).
        /// </summary>
        /// <param name="e">The exception.</param>
        /// <returns><c>true</c> if the handler indicates that the task should be terminated.</returns>
        private async Task<bool> OnExceptionAsync(Exception e)
        {
            if (onExceptionAsync != null)
            {
                return await onExceptionAsync(e);
            }
            else
            {
                return await Task.FromResult(true);
            }
        }
    }
}
