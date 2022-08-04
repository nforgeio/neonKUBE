//-----------------------------------------------------------------------------
// FILE:        AsyncTimer.cs
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
//
// Code based on a MSDN article by Stephen Toub (MSFT):
// http://blogs.msdn.com/b/pfxteam/archive/2012/02/11/10266920.aspx

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Tasks;

namespace Neon.Tasks
{
    /// <summary>
    /// Implements a timer that runs on a background <see cref="Task"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is pretty easy to use.  Simply use the <see cref="AsyncTimer(Func{Task})"/>
    /// constructor to create an instance, passing the async callback to be called when the 
    /// timer fires and then call <see cref="Start(TimeSpan, bool, Func{Task})"/> to start the timer, passing
    /// the timer interval.
    /// </para>
    /// <para>
    /// <see cref="Start"/> starts a background task that fires the callback at the interval
    /// specified.  You can call <see cref="Start"/> again to restart the timer with a different
    /// interval.  The <see cref="IsRunning"/> property can be used to determine whether a 
    /// timer is running or not.
    /// </para>
    /// <para>
    /// Call <see cref="Stop"/> to stop a timer.  <see cref="Start"/> may be called again to
    /// restart the timer.
    /// </para>
    /// <note>
    /// This class implements <see cref="IDisposable"/> so this should be called for every 
    /// instance created or <see cref="Stop"/> should be called explicitly.
    /// </note>
    /// <note>
    /// This class handles any exceptions thrown by the callback by logging them to the
    /// default <see cref="LogManager"/> and then continuing on with firing ticks.  
    /// You'll need to add a try/catch to your callback to do your own exception handling.
    /// </note>
    /// </remarks>
    public class AsyncTimer : IDisposable
    {
        //---------------------------------------------------------------------
        // Static members

        private static INeonLogger      log = LogManager.Default.GetLogger<AsyncTimer>();

        //---------------------------------------------------------------------
        // Instance members

        private object                  syncLock = new object();
        private Func<Task>              callback;
        private TimeSpan                interval;
        private bool                    delayFirstTick;
        private Task                    timerTask;
        private CancellationTokenSource cts;
        private bool                    isDisposed;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="callback">Optionally specifies the callback.</param>
        public AsyncTimer(Func<Task> callback = null)
        {
            this.callback = callback;
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~AsyncTimer()
        {
            Dispose(false);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        /// <summary>
        /// Releases any important resources associated with the instance.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if the instance is being disposed as opposed to being finalized.</param>
        protected void Dispose(bool disposing)
        {
            if (isDisposed)
            {
                return;
            }

            lock (syncLock)
            {
                try
                {
                    Stop();
                }
                finally
                {
                    isDisposed = true;
                }
            }
        }

        /// <summary>
        /// <para>
        /// Indicates whether the timer is currently running.
        /// </para>
        /// <note>
        /// This returns <see cref="TimeSpan.Zero"/> until <see cref="Start(TimeSpan, bool, Func{Task})"/>
        /// is called for the first time.
        /// </note>
        /// </summary>
        public bool IsRunning => timerTask != null;

        /// <summary>
        /// Returns the timer interval.
        /// </summary>
        public TimeSpan Interval => interval;

        /// <summary>
        /// Ensures that the instance is not disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown then the instance is disposed.</exception>
        private void CheckDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(AsyncTimer));
            }
        }

        /// <summary>
        /// <para>
        /// Starts or restarts the timer.
        /// </para>
        /// <para>
        /// The <paramref name="interval"/> must be specified as a positive interval when
        /// the timer is first started but this is optional thereafter, defaulting to
        /// value from the original <see cref="Start(TimeSpan, bool, Func{Task})"/> call.
        /// </para>
        /// </summary>
        /// <param name="interval">Optionally specifies the timer interval.</param>
        /// <param name="delayFirstTick">
        /// The callback is called immediately by default.  You can delay this for 
        /// <paramref name="interval"/> by passing this as <c>true</c>.  This defaults
        /// to <c>false</c>.
        /// </param>
        /// <param name="callback">
        /// <para>
        /// Optionally specifies the timer callback.
        /// </para>
        /// <note>
        /// This must be specified if no callback was passed to the constructor or a previous
        /// call to <see cref="Start(TimeSpan, bool, Func{Task})"/>.
        /// </note>
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this is the first time <see cref="Start(TimeSpan, bool, Func{Task})"/> is called for the
        /// instance and <paramref name="interval"/> is not passed or when <paramref name="callback"/> is
        /// <c>null</c> and no callback was specified in constructor or a previous call to 
        /// <see cref="Start(TimeSpan, bool, Func{Task})"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">Thrown then the instance is disposed.</exception>
        public void Start(TimeSpan interval = default, bool delayFirstTick = false, Func<Task> callback = null)
        {
            Covenant.Requires<ArgumentException>(interval >= TimeSpan.Zero, nameof(interval));

            if (callback == null && this.callback == null)
            {
                throw new InvalidOperationException($"[{nameof(callback)}] must be non-null when no callback was passed to the constructor or a previous call to [{nameof(Start)}()].");
            }
            else if (callback != null)
            {
                this.callback = callback;
            }

            lock (syncLock)
            {
                CheckDisposed();

                if (this.interval == TimeSpan.Zero)
                {
                    // This is the first time this method has been called for the instance.

                    Covenant.Assert(!IsRunning);

                    if (interval == TimeSpan.Zero)
                    {
                        throw new InvalidOperationException($"[{nameof(interval)}] must be positive the first time [{nameof(Start)}()] is called.");
                    }

                    this.interval = interval;
                }
                else
                {
                    // We're restarting the timer so signal any existing timer task to
                    // exit and set the new interval (if any).

                    if (IsRunning)
                    {
                        Covenant.Assert(cts != null);

                        cts.Cancel();
                        timerTask.WaitWithoutAggregate();

                        cts = null;
                        timerTask = null;
                    }

                    if (interval > TimeSpan.Zero)
                    {
                        this.interval = interval;
                    }
                }

                // Start/Restart the timer.

                this.delayFirstTick = delayFirstTick;
                this.cts            = new CancellationTokenSource();
                this.timerTask      = TimerLoopAsync();
            }
        }

        /// <summary>
        /// Stops the timer.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown then the instance is disposed.</exception>
        public void Stop()
        {
            lock (syncLock)
            {
                CheckDisposed();

                if (!IsRunning)
                {
                    return;
                }

                Covenant.Assert(cts != null);

                cts.Cancel();
                timerTask.WaitWithoutAggregate();
                timerTask = null;

                cts.Dispose();
                cts = null;
            }
        }

        /// <summary>
        /// Implements the async timer loop.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task TimerLoopAsync()
        {
            await SyncContext.Clear;

            while (!cts.IsCancellationRequested)
            {
                try
                {
                    if (delayFirstTick)
                    {
                        await Task.Delay(interval, cts.Token);
                    }

                    try
                    {
                        await callback();
                    }
                    catch (Exception e)
                    {
                        log.LogError(e);
                    }

                    if (!delayFirstTick)
                    {
                        await Task.Delay(interval, cts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
