//-----------------------------------------------------------------------------
// FILE:	    Timer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

// $todo(jeff.lill):
//
// The PCL profile doesn't implement [System.Threading.Timer] so we'll need
// to provide our own implementation.  I hope we can remove this when we can
// upgrade to .NET Standard.

// $todo(jeff.lill): Probably can delete this file at some point.

#if FALSE

using System;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;

namespace System.Threading
{
    /// <summary>
    /// The <see cref="Timer"/> callback definition.
    /// </summary>
    /// <param name="state">The timer state.</param>
    public delegate void TimerCallback(object state);

    /// <summary>
    /// A partial implementation of a timer class for PCL applications.
    /// </summary>
    public sealed class Timer : IDisposable
    {
        private object                  syncRoot    = new object();
        private Task                    timerTask;
        private CancellationTokenSource cancelToken;
        private TimerCallback           callback;
        private object                  state;

        /// <summary>
        /// Constructs a timer, specifying time periods as seconds.
        /// </summary>
        /// <param name="callback">The callback to invoke when the timer fires.</param>
        /// <param name="state">The timer state.</param>
        /// <param name="dueTime">Seconds to delay before the first callback.</param>
        /// <param name="period">Seconds between subsequent callbacks.</param>
        public Timer(TimerCallback callback, object state, int dueTime, int period)
            : this(callback, state, TimeSpan.FromSeconds(dueTime), TimeSpan.FromSeconds(period))
        {
        }

        /// <summary>
        /// Constructs a timer, specifying time periods as <see cref="TimeSpan"/>.
        /// </summary>
        /// <param name="callback">The callback to invoke when the timer fires.</param>
        /// <param name="state">The timer state.</param>
        /// <param name="dueTime">Interval to delay before the first callback.</param>
        /// <param name="period">Interval between subsequent callbacks.</param>
        public Timer(TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period)
        {
            Covenant.Requires<ArgumentNullException>(callback != null);
            Covenant.Requires(dueTime == Timeout.InfiniteTimeSpan || dueTime >= TimeSpan.Zero);
            Covenant.Requires(period == Timeout.InfiniteTimeSpan || period >= TimeSpan.Zero);

            this.callback = callback;
            this.state    = state;

            Run(dueTime, period);
        }

        /// <summary>
        /// Modifies the the timer settings as integer seconds.
        /// </summary>
        /// <param name="dueTime">Seconds to delay before the next callback.</param>
        /// <param name="period">Seconds between subsequent callbacks.</param>
        /// <returns><c>true</c> if the timer was modified successfully.</returns>
        public bool Change(int dueTime, int period)
        {
            return Change(TimeSpan.FromSeconds(dueTime), TimeSpan.FromSeconds(period));
        }

        /// <summary>
        /// Modifies the the timer settings as <see cref="TimeSpan"/>.
        /// </summary>
        /// <param name="dueTime">Interval to delay before the next callback.</param>
        /// <param name="period">Interval between subsequent callbacks.</param>
        /// <returns><c>true</c> if the timer was modified successfully.</returns>
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            Covenant.Requires(dueTime == Timeout.InfiniteTimeSpan || dueTime >= TimeSpan.Zero);
            Covenant.Requires(period == Timeout.InfiniteTimeSpan || period >= TimeSpan.Zero);

            lock (syncRoot)
            {
                // Stop the existing timer task.

                if (timerTask == null)
                {
                    return false;
                }

                cancelToken.Cancel();
                timerTask.Wait();

                // Then start a new one.

                Run(dueTime, period);

                return true;
            }
        }

        /// <summary>
        /// Starts the timer task.
        /// </summary>
        /// <param name="dueTime">Interval to delay before the next callback.</param>
        /// <param name="period">Interval between subsequent callbacks.</param>
        private void Run(TimeSpan dueTime, TimeSpan period)
        {
            // Implementation Note:
            //
            // I'm going to have the timer wake up at a maximum of 1 second
            // intervals to check for a cancellation.

            timerTask = Task.Run(
                async () =>
                {
                    TimeSpan    wakeInterval = TimeSpan.FromSeconds(1);
                    TimeSpan    waitedTime   = TimeSpan.Zero;

                    // Handle the initial wait period.

                    if (dueTime == TimeSpan.Zero)
                    {
                        callback(state);
                    }
                    else
                    {
                        if (dueTime < wakeInterval)
                        {
                            wakeInterval = dueTime;
                        }

                        waitedTime = TimeSpan.Zero;

                        while (true)
                        {
                            await Task.Delay(wakeInterval);

                            if (cancelToken.IsCancellationRequested)
                            {
                                return;
                            }

                            if (dueTime != Timeout.InfiniteTimeSpan)
                            {
                                waitedTime += wakeInterval;

                                if (waitedTime >= dueTime)
                                {
                                    callback(state);
                                    break;
                                }
                            }
                        }
                    }

                    // Handle the regular timer periods.

                    wakeInterval = TimeSpan.FromSeconds(1);

                    if (period != Timeout.InfiniteTimeSpan)
                    {
                        dueTime = period; 

                        if (period < wakeInterval)
                        {
                            wakeInterval = period;
                        }
                    }

                    while (true)
                    {
                        await Task.Delay(wakeInterval);

                        if (cancelToken.IsCancellationRequested)
                        {
                            return;
                        }

                        if (period != Timeout.InfiniteTimeSpan)
                        {
                            waitedTime += wakeInterval;

                            if (waitedTime >= dueTime)
                            {
                                callback(state);
                                break;
                            }

                            waitedTime = TimeSpan.Zero;
                        }
                    }
                });
        }

        /// <summary>
        /// Permanently stops the timer.
        /// </summary>
        public void Dispose()
        {
            lock (syncRoot)
            {
                if (timerTask == null)
                {
                    return; // Already disposed
                }

                cancelToken.Cancel();

                timerTask   = null;
                cancelToken = null;
            }
        }
    }
}

#endif
