//-----------------------------------------------------------------------------
// FILE:	    IRetryPolicy.cs
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
    /// Describes the behavior of an operation retry policy.  These are used
    /// to retry operations that have failed due to transient errors.
    /// </summary>
    [ContractClass(typeof(IRetryPolicyContract))]
    public interface IRetryPolicy
    {
        /// <summary>
        /// Returns a copy of the retry policy.
        /// </summary>
        /// <returns>The policy copy.</returns>
        IRetryPolicy Clone();

        /// <summary>
        /// Retries an action that returns no result when it throws exceptions due to 
        /// transient errors.  The classification of what is a transient error, the interval
        /// between the retries as well as the number of times the operation are retried are
        /// determined by the policy implementation.
        /// </summary>
        /// <param name="action">The action to be performed.</param>
        Task InvokeAsync(Func<Task> action);

        /// <summary>
        /// Retries an action that returns <typeparamref name="TResult"/> when it throws exceptions
        /// due to transient errors.  he classification of what is a transient error, the interval 
        /// between the retries as well as the number of times the operation are retried are 
        /// determined by the policy implementation. 
        /// </summary>
        /// <typeparam name="TResult">The action result type.</typeparam>
        /// <param name="action">The action to be performed.</param>
        /// <returns>The action result.</returns>
        Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> action);
    }

    [ContractClassFor(typeof(IRetryPolicy))]
    internal abstract class IRetryPolicyContract : IRetryPolicy
    {
        public IRetryPolicy Clone()
        {
            return null;
        }

        public Task InvokeAsync(Func<Task> action)
        {
            Covenant.Requires<ArgumentNullException>(action != null);

            return null;
        }

        public Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> action)
        {
            Covenant.Requires<ArgumentNullException>(action != null);

            return null;
        }
    }
}
