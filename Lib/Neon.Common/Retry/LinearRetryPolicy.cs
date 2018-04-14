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

using Neon.Diagnostics;

namespace Neon.Retry
{
    /// <summary>
    /// Implements a simple <see cref="IRetryPolicy"/> that retries an operation 
    /// at a fixed interval for a specified maximum number of times.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You can enable transient error logging by passing a non-empty <b>logCategory</b>
    /// name to the constructor.  This creates an embedded <see cref="INeonLogger"/>
    /// using that name and any retried transient errors will then be logged as
    /// warnings including <b>[transient-retry]</b> in the message.
    /// </para>
    /// <note>
    /// Only the retried errors will be logged.  The final exception thrown after
    /// all retries fail will not be logged because it's assumed that these will
    /// be caught and handled upstack by application code.
    /// </note>
    /// <para>
    /// Choose a category name that can be used to easily identify the affected
    /// component.  For example, <b>couchbase:my-cluster</b> to identify a
    /// specific Couchbase cluster.
    /// </para>
    /// </remarks>
    public class LinearRetryPolicy : IRetryPolicy
    {
        private Func<Exception, bool>   transientDetector;
        private INeonLogger             log;

        /// <summary>
        /// Constructs the retry policy with a specific transitent detection function.d
        /// </summary>
        /// <param name="transientDetector">
        /// Optionally specifies the function that determines whether an exception is transient 
        /// (see <see cref="TransientDetector"/>).  You can pass <c>null</c>
        /// if all exceptions are to be considered to be transient.
        /// </param>
        /// <param name="maxAttempts">Optionally specifies the maximum number of times an action should be retried (defaults to <b>5</b>).</param>
        /// <param name="retryInterval">Optionally specifies time interval between retry attempts (defaults to <b>1 second</b>).</param>
        /// <param name="logCategory">Optionally enables transient error logging by specifying a log category.</param>
        public LinearRetryPolicy(Func<Exception, bool> transientDetector = null, int maxAttempts = 5, TimeSpan? retryInterval = null, string logCategory = null)
        {
            Covenant.Requires<ArgumentException>(maxAttempts > 0);
            Covenant.Requires<ArgumentException>(retryInterval == null || retryInterval >= TimeSpan.Zero);

            this.transientDetector = transientDetector ?? (e => true);
            this.MaxAttempts       = maxAttempts;
            this.RetryInterval     = retryInterval ?? TimeSpan.FromSeconds(1);

            if (!string.IsNullOrEmpty(logCategory))
            {
                log = LogManager.Default.GetLogger(logCategory);
            }
        }

        /// <summary>
        /// Constructs the retry policy to handle a specific exception type as transient.
        /// </summary>
        /// <param name="exceptionType">The exception type to be considered to be transient.</param>
        /// <param name="maxAttempts">Optionally specifies the maximum number of times an action should be retried (defaults to <b>5</b>).</param>
        /// <param name="retryInterval">Optionally specifies the time interval between retry attempts (defaults to <b>1 second</b>).</param>
        /// <param name="logCategory">Optionally enables transient error logging by specifying a log category.</param>
        public LinearRetryPolicy(Type exceptionType, int maxAttempts = 5, TimeSpan?retryInterval = null, string logCategory = null)
            : this
            (
                e => e != null && exceptionType == e.GetType(),
                maxAttempts,
                retryInterval,
                logCategory
            )
        {
        }

        /// <summary>
        /// Constructs the retry policy to handle a multiple exception types as transient.
        /// </summary>
        /// <param name="exceptionTypes">The exception type to be considered to be transient.</param>
        /// <param name="maxAttempts">Optionally specifies the maximum number of times an action should be retried (defaults to <b>5</b>).</param>
        /// <param name="retryInterval">Optionally specifies the time interval between retry attempts (defaults to <b>1 second</b>).</param>
        /// <param name="logCategory">Optionally enables transient error logging by specifying a log category.</param>
        public LinearRetryPolicy(Type[] exceptionTypes, int maxAttempts = 5, TimeSpan? retryInterval = null, string logCategory = null)
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
                retryInterval,
                logCategory
            )
        {
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

                    log?.LogWarn("[transient-retry]", e);
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

                    log?.LogWarn("[transient-retry]", e);
                    await Task.Delay(RetryInterval);
                }
            }
        }
    }
}
