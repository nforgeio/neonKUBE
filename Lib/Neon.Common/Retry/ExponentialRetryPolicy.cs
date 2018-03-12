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
        /// Constructs the retry policy with a specific transitent detection function.
        /// </summary>
        /// <param name="transientDetector">
        /// A function that determines whether an exception is transient 
        /// (see <see cref="TransientDetector"/>).  You can pass <c>null</c>
        /// if all exceptions are to be considered to be transient.
        /// </param>
        /// <param name="maxAttempts">The maximum number of times an action should be retried (defaults to <b>5</b>.</param>
        /// <param name="initialRetryInterval">The initial retry interval between retry attempts (defaults to <b>1 second</b>).</param>
        /// <param name="maxRetryInterval">The maximum retry interval (defaults to essentially unlimited: 24 hours).</param>
        public ExponentialRetryPolicy(Func<Exception, bool> transientDetector = null, int maxAttempts = 5, TimeSpan? initialRetryInterval = null, TimeSpan? maxRetryInterval = null)
        {
            Covenant.Requires<ArgumentException>(maxAttempts > 0);
            Covenant.Requires<ArgumentException>(initialRetryInterval == null || initialRetryInterval > TimeSpan.Zero);
            Covenant.Requires<ArgumentNullException>(maxRetryInterval >= initialRetryInterval || initialRetryInterval > TimeSpan.Zero || maxRetryInterval == null);

            this.transientDetector = transientDetector ?? (e => true);
            this.MaxAttempts = maxAttempts;
            this.InitialRetryInterval = initialRetryInterval ?? TimeSpan.FromSeconds(1);
            this.MaxRetryInterval = maxRetryInterval ?? TimeSpan.FromHours(24);

            if (InitialRetryInterval > MaxRetryInterval)
            {
                InitialRetryInterval = MaxRetryInterval;
            }
        }

        /// <summary>
        /// Constructs the retry policy to handle a specific exception type as transient.
        /// </summary>
        /// <param name="exceptionType">The exception type to be considered to be transient.</param>
        /// <param name="maxAttempts">The maximum number of times an action should be retried (defaults to <b>5</b>.</param>
        /// <param name="initialRetryInterval">The initial retry interval between retry attempts (defaults to <b>1 second</b>).</param>
        /// <param name="maxRetryInterval">The maximum retry interval (defaults to essentially unlimited: 24 hours).</param>
        public ExponentialRetryPolicy(Type exceptionType, int maxAttempts = 5, TimeSpan? initialRetryInterval = null, TimeSpan? maxRetryInterval = null)
            : this
            (
                e => e != null && exceptionType == e.GetType(),
                maxAttempts,
                initialRetryInterval,
                maxRetryInterval
            )
        {
        }

        /// <summary>
        /// Constructs the retry policy to handle a multiple exception types as transient.
        /// </summary>
        /// <param name="exceptionTypes">The exception type to be considered to be transient.</param>
        /// <param name="maxAttempts">The maximum number of times an action should be retried (defaults to <b>5</b>.</param>
        /// <param name="initialRetryInterval">The initial retry interval between retry attempts (defaults to <b>1 second</b>).</param>
        /// <param name="maxRetryInterval">The maximum retry interval (defaults to essentially unlimited: 24 hours).</param>
        public ExponentialRetryPolicy(Type[] exceptionTypes, int maxAttempts = 5, TimeSpan? initialRetryInterval = null, TimeSpan? maxRetryInterval = null)
            : this
            (
                e =>
                {
                    if (exceptionTypes == null)
                    {
                        return false;
                    }

                    var exceptionType = e.GetType();

                    foreach (var type in exceptionTypes)
                    {
                        if (type == exceptionType)
                        {
                            return true;
                        }
                    }

                    return false;
                },
                maxAttempts,
                initialRetryInterval,
                maxRetryInterval
            )
        {
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
