//-----------------------------------------------------------------------------
// FILE:	    NoRetryPolicy.cs
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
    /// Implements an <see cref="IRetryPolicy"/> that does not attempt to retry operations.
    /// </summary>
    public class NoRetryPolicy : IRetryPolicy
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns a global invariant instance.
        /// </summary>
        public static NoRetryPolicy Instance { get; private set; } = new NoRetryPolicy();

        //---------------------------------------------------------------------
        // Instance members
        
        /// <summary>
        /// Constructor.
        /// </summary>
        public NoRetryPolicy()
        {
        }

        /// <inheritdoc/>
        public IRetryPolicy Clone()
        {
            // The class is invariant we can safely return ourself.
            
            return this;
        }

        /// <inheritdoc/>
        public async Task InvokeAsync(Func<Task> action)
        {
            await action();
        }

        /// <inheritdoc/>
        public async Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> action)
        {
            return await action();
        }
    }
}
