//-----------------------------------------------------------------------------
// FILE:	    LinearRetryPolicy.cs
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
    /// Implements a simple <see cref="IRetryPolicy"/> that retries an operation 
    /// at a fixed interval for a specified maximum number of times.
    /// </summary>
    public class LinearRetryPolicy : IRetryPolicy
    {
        private Func<Exception, bool> transientDetector;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="transientDetector">The function that determines whether an exception is transient (see <see cref="TransientDetector"/>).</param>
        /// <param name="maxAttempts">The maximum number of times an action should be retried (defaults to <b>5</b>.</param>
        /// <param name="retryInterval">The time interval between retry attempts (defaults to <b>1 second</b>).</param>
        public LinearRetryPolicy(Func<Exception, bool> transientDetector, int maxAttempts = 5, TimeSpan? retryInterval = null)
        {
            Covenant.Requires<ArgumentNullException>(transientDetector != null);
            Covenant.Requires<ArgumentException>(maxAttempts > 0);
            Covenant.Requires<ArgumentException>(retryInterval == null || retryInterval >= TimeSpan.Zero);

            this.transientDetector = transientDetector;
            this.MaxAttempts       = maxAttempts;
            this.RetryInterval     = retryInterval ?? TimeSpan.FromSeconds(1);
        }

        /// <summary>
        /// Returns the maximum number of times the action should be attempted.
        /// </summary>
        public int MaxAttempts { get; private set; }

        /// <summary>
        /// Returns the fixed interval between action retry attempts.
        /// </summary>
        public TimeSpan RetryInterval { get; private set; }

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

                    await Task.Delay(RetryInterval);
                }
            }
        }

        /// <inheritdoc/>
        public async Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> action)
        {
            var attempts = 0;

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

                    await Task.Delay(RetryInterval);
                }
            }
        }
    }
}
