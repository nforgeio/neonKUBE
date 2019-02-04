//-----------------------------------------------------------------------------
// FILE:	    GatedTimer.cs
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
using System.Diagnostics.Contracts;
using System.Threading;

using Neon.Common;
using Neon.Diagnostics;

namespace Neon.Time
{
    /// <summary>
    /// Implements a timer that allows only one thread at a time to process timer events.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is necessary to because the <see cref="Timer"/> class will continue firing
    /// thread handlers even if a long-running thread is still handling an earlier
    /// timer event.
    /// </para>
    /// </remarks>
    public class GatedTimer : IDisposable
    {
        private object          syncLock = new object();
        private INeonLogger     logger   = LogManager.Default.GetLogger<GatedTimer>();

        private Timer           timer;          // The underlying timer
        private TimeSpan        dueTime;        // Time to wait before firing the first event
        private TimeSpan        period;         // Time to wait between firing events
        private TimerCallback   callback;       // The timer event handler
        private bool            inCallback;     // True if we're processing a callback

        /// <summary>
        /// Initializes and starts the timer.
        /// </summary>
        /// <param name="callback">The callback to be called when the timer fires.</param>
        /// <param name="state">Application state.</param>
        /// <param name="period">Time to wait between firing events.</param>
        public GatedTimer(TimerCallback callback, object state, TimeSpan period)
            : this(callback, state, period, period)
        {
            Covenant.Requires<ArgumentNullException>(callback != null);
            Covenant.Requires<ArgumentException>(period >= TimeSpan.Zero);
        }

        /// <summary>
        /// Initializes and starts the timer.
        /// </summary>
        /// <param name="callback">The callback to be called when the timer fires.</param>
        /// <param name="state">Application state.</param>
        /// <param name="dueTime">Time to wait before firing the first event.</param>
        /// <param name="period">Time to wait between firing events.</param>
        public GatedTimer(TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period)
        {
            Covenant.Requires<ArgumentNullException>(callback != null);
            Covenant.Requires<ArgumentException>(dueTime >= TimeSpan.Zero);
            Covenant.Requires<ArgumentException>(period >= TimeSpan.Zero);

            // The .NET framework doesn't like really long timespans so use
            // 1 day instead.

            var oneDay = TimeSpan.FromDays(1);

            if (dueTime > oneDay)
            {
                dueTime = oneDay;
            }

            if (period > oneDay)
            {
                period = oneDay;
            }

            this.dueTime    = dueTime;
            this.period     = period;
            this.callback   = callback;
            this.inCallback = false;
            this.timer      = new Timer(new TimerCallback(OnTimer), state, dueTime, period);
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~GatedTimer()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases all resources associated with the timer.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Releases any important resources associated with the instance.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if the instance is being disposed as opposed to being finalized.</param>
        protected void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (syncLock)
                {
                    if (timer == null)
                    {
                        return;
                    }

                    timer.Dispose();
                    timer = null;

                    GC.SuppressFinalize(this);
                }
            }

            timer = null;
        }

        /// <summary>
        /// Modifies the timer.
        /// </summary>
        /// <param name="dueTime">Time to wait before firing the first event.</param>
        /// <param name="period">Time to wait between firing events.</param>
        public void Change(TimeSpan dueTime, TimeSpan period)
        {
            Covenant.Requires<ArgumentException>(dueTime >= TimeSpan.Zero);
            Covenant.Requires<ArgumentException>(period >= TimeSpan.Zero);

            lock (syncLock)
            {
                this.dueTime = dueTime;
                this.period  = period;

                if (!inCallback)
                {
                    timer.Change(dueTime, period);
                }
            }
        }

        /// <summary>
        /// Handles the timer dispatch.
        /// </summary>
        /// <param name="state"></param>
        private void OnTimer(object state)
        {
            lock (syncLock)
            {
                // Ignore timer events if we're already processing one or if the
                // timer has been disposed.

                if (inCallback || timer == null)
                {
                    return;
                }

                // Disable the timer and indicate that we're processing
                // the callback

                timer.Change(Timeout.Infinite, Timeout.Infinite);
                inCallback = true;
            }

            try
            {
                callback(state);
            }
            catch (Exception e)
            {
                logger.LogError(e);
            }

            lock (syncLock)
            {
                inCallback = false;

                // If the timer hasn't been disposed then reenable it.

                if (timer != null)
                {
                    timer.Change(period, period);
                }
            }
        }
    }
}
