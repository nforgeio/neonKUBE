//-----------------------------------------------------------------------------
// FILE:	    RetryPolicy.cs
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
using System.Threading.Tasks;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Time;

namespace Neon.Retry
{
    /// <summary>
    /// Base class for used to help implement a <see cref="IRetryPolicy"/>.
    /// </summary>
    public abstract class RetryPolicy : IRetryPolicy
    {
        private INeonLogger log;
        private DateTime    sysDeadline;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="sourceModule">Optionally enables transient error logging by identifying the source module (defaults to <c>null</c>).</param>
        /// <param name="timeout">Optionally specifies the maximum time the operation should be retried (defaults to no limit).</param>
        public RetryPolicy(string sourceModule = null, TimeSpan? timeout = null)
        {
            if (!string.IsNullOrEmpty(sourceModule))
            {
                this.log = LogManager.Default.GetLogger(sourceModule);
            }

            if (timeout != null && timeout >= TimeSpan.Zero)
            {
                this.Timeout = timeout;

                // Compute the UTC deadline, taking care not not to
                // exceed the end-of-time.

                var utcNow = SysTime.Now;

                if (timeout >= DateTime.MaxValue - utcNow)
                {
                    sysDeadline = DateTime.MaxValue;
                }
                else
                {
                    sysDeadline = utcNow + timeout.Value;
                }
            }
            else
            {
                sysDeadline = DateTime.MaxValue;
            }
        }

        /// <inheritdoc/>
        public TimeSpan? Timeout { get; private set; }

        /// <inheritdoc/>
        public abstract IRetryPolicy Clone();

        /// <inheritdoc/>
        public abstract Task InvokeAsync(Func<Task> action);

        /// <inheritdoc/>
        public abstract Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> action);

        /// <summary>
        /// Logs a transient exception that is going to be retried if logging
        /// is enabled.
        /// </summary>
        /// <param name="e">The exception.</param>
        protected void LogTransient(Exception e)
        {
            log?.LogWarn("[transient-retry]", e);
        }

        /// <summary>
        /// Adjusts the delay <see cref="TimeSpan"/> passed to ensure such
        /// that delaying the next retry won't exceed the overall retry
        /// timeout (if specified).
        /// </summary>
        /// <param name="delay">The requested delay.</param>
        /// <returns>The adjusted delay.</returns>
        /// <remarks>
        /// <note>
        /// If the result is <see cref="TimeSpan.Zero"/> or negative, the
        /// calling retry policy should immediately stop retrying.
        /// </note>
        /// </remarks>
        protected TimeSpan AdjustDelay(TimeSpan delay)
        {
            Covenant.Requires<ArgumentException>(delay >= TimeSpan.Zero);

            var maxDelay = sysDeadline - DateTime.UtcNow;

            if (delay > maxDelay)
            {
                return maxDelay;
            }
            else
            {
                return delay;
            }
        }
    }
}
