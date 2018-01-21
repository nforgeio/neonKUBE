//-----------------------------------------------------------------------------
// FILE:	    ExponentialRetryPolicy.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Retry
{
    /// <summary>
    /// Implements an <see cref="IRetryPolicy"/> that retries an operation 
    /// first at an initial interval and then doubles the interval up to a limit
    /// for a specified maximum number of times.
    /// </summary>
    public class ExponentialRetryPolicy : IRetryPolicy
    {
        private Func<Exception, bool> transientDetector;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="transientDetector">The function that determines whether an exception is transient (see <see cref="TransientDetector"/>).</param>
        /// <param name="maxAttempts">The maximum number of times an action should be retried (defaults to <b>5</b>.</param>
        /// <param name="initialRetryInterval">The initial retry interval between retry attempts (defaults to <b>1 second</b>).</param>
        /// <param name="maxRetryInterval">The maximum retry interval (defaults to essentially unlimited: 24 hours).</param>
        public ExponentialRetryPolicy(Func<Exception, bool> transientDetector, int maxAttempts = 5, TimeSpan? initialRetryInterval = null, TimeSpan? maxRetryInterval = null)
        {
            Covenant.Requires<ArgumentNullException>(transientDetector != null);
            Covenant.Requires<ArgumentException>(maxAttempts > 0);
            Covenant.Requires<ArgumentException>(initialRetryInterval == null || initialRetryInterval > TimeSpan.Zero);
            Covenant.Requires<ArgumentNullException>(maxRetryInterval >= initialRetryInterval || initialRetryInterval > TimeSpan.Zero || maxRetryInterval == null);

            this.transientDetector    = transientDetector;
            this.MaxAttempts          = maxAttempts;
            this.InitialRetryInterval = initialRetryInterval ?? TimeSpan.FromSeconds(1);
            this.MaxRetryInterval     = maxRetryInterval ?? TimeSpan.FromHours(24);

            if (InitialRetryInterval > MaxRetryInterval)
            {
                InitialRetryInterval = MaxRetryInterval;
            }
        }

        /// <summary>
        /// Returns the maximum number of times the action should be attempted.
        /// </summary>
        public int MaxAttempts { get; private set; }

        /// <summary>
        /// Returns the initial interval between action retry attempts.
        /// </summary>
        public TimeSpan InitialRetryInterval { get; private set; }

        /// <summary>
        /// Returns the maximum intervaL between action retry attempts. 
        /// </summary>
        public TimeSpan MaxRetryInterval { get; private set; }

        /// <inheritdoc/>
        public IRetryPolicy Clone()
        {
            // The class is invariant we can safely return ourself.

            return this;
        }

        /// <inheritdoc/>
        public async Task InvokeAsync(Func<Task> action)
        {
            var attempts = 0;
            var interval = InitialRetryInterval;

            while (true)
            {
                try
                {
                    await action();
                    return;
                }
                catch (Exception e)
                {
                    if (++attempts >= MaxAttempts || !transientDetector(e))
                    {
                        throw;
                    }

                    await Task.Delay(interval);

                    interval = TimeSpan.FromTicks(interval.Ticks * 2);

                    if (interval > MaxRetryInterval)
                    {
                        interval = MaxRetryInterval;
                    }
                }
            }
        }

        /// <inheritdoc/>
        public async Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> action)
        {
            var attempts = 0;
            var interval = InitialRetryInterval;

            while (true)
            {
                try
                {
                    return await action();
                }
                catch (Exception e)
                {
                    if (++attempts >= MaxAttempts || !transientDetector(e))
                    {
                        throw;
                    }

                    await Task.Delay(interval);

                    interval = TimeSpan.FromTicks(interval.Ticks * 2);

                    if (interval > MaxRetryInterval)
                    {
                        interval = MaxRetryInterval;
                    }
                }
            }
        }
    }
}
