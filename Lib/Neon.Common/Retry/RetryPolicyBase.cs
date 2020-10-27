//-----------------------------------------------------------------------------
// FILE:	    RetryPolicyBase.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using System.Threading.Tasks;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Time;

namespace Neon.Retry
{
    /// <summary>
    /// Base class for used to help implement a <see cref="IRetryPolicy"/>.
    /// </summary>
    public abstract class RetryPolicyBase : IRetryPolicy
    {
        private INeonLogger log;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="sourceModule">Optionally enables transient error logging by identifying the source module (defaults to <c>null</c>).</param>
        /// <param name="timeout">Optionally specifies the maximum time the operation will be retried (defaults to unconstrained)</param>
        public RetryPolicyBase(string sourceModule = null, TimeSpan? timeout = null)
        {
            this.SourceModule = sourceModule;
            this.Timeout      = timeout;

            if (!string.IsNullOrEmpty(sourceModule))
            {
                this.log = LogManager.Default.GetLogger(sourceModule);
            }
        }

        /// <inheritdoc/>
        public TimeSpan? Timeout { get; private set; }

        /// <inheritdoc/>
        public abstract IRetryPolicy Clone(Func<Exception, bool> transientDetector = null);

        /// <inheritdoc/>
        public abstract Task InvokeAsync(Func<Task> action);

        /// <inheritdoc/>
        public abstract Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> action);

        /// <summary>
        /// Returns the source module.
        /// </summary>
        protected string SourceModule { get; private set; }

        /// <summary>
        /// Logs a transient exception that will be retried if logging
        /// is enabled.
        /// </summary>
        /// <param name="e">The exception.</param>
        protected void LogTransient(Exception e)
        {
            log?.LogTransient(e);
        }

        /// <summary>
        /// Computes the time (SYS) after which the operation should not be retried.
        /// </summary>
        protected DateTime SysDeadline()
        {
            var timeout = Timeout ?? TimeSpan.MaxValue;

            if (timeout >= TimeSpan.Zero)
            {
                this.Timeout = timeout;

                // Compute the SYS deadline, taking care not not to exceed the end-of-time.

                var sysNow = SysTime.Now;

                if (timeout >= DateTime.MaxValue - sysNow)
                {
                    return DateTime.MaxValue;
                }
                else
                {
                    return sysNow + timeout;
                }
            }
            else
            {
                return DateTime.MaxValue;
            }
        }

        /// <summary>
        /// Adjusts the delay <see cref="TimeSpan"/> passed to ensure such
        /// that delaying the next retry won't exceed the overall retry
        /// timeout (if specified).
        /// </summary>
        /// <param name="delay">The requested delay.</param>
        /// <param name="sysDeadline">The retry deadline (SYS) computed by <see cref="SysDeadline()"/>.</param>
        /// <returns>The adjusted delay.</returns>
        /// <remarks>
        /// <note>
        /// If the result is <see cref="TimeSpan.Zero"/> or negative, the
        /// calling retry policy should immediately stop retrying.
        /// </note>
        /// </remarks>
        protected TimeSpan AdjustDelay(TimeSpan delay, DateTime sysDeadline)
        {
            Covenant.Requires<ArgumentException>(delay >= TimeSpan.Zero, nameof(delay));

            var sysNow   = SysTime.Now;
            var maxDelay = sysDeadline - sysNow;

            if (delay > maxDelay)
            {
                delay = maxDelay;
            }

            if (delay <= TimeSpan.Zero)
            {
                return TimeSpan.Zero;
            }
            else
            {
                return delay;
            }
        }
    }
}
