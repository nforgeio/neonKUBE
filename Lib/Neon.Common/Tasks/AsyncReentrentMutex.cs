//-----------------------------------------------------------------------------
// FILE:        AsyncReentrantMutex.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
//
// Code based on a MSDN article by Stephen Toub (MSFT):
// http://blogs.msdn.com/b/pfxteam/archive/2012/02/12/10267069.aspx

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Neon.Tasks
{
    /// <summary>
    /// Extends <see cref="AsyncMutex"/> to support reentrancy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike the <see cref="AsyncMutex"/> class, this class supports reentrency, meaning
    /// that once an asynchronous <see cref="Task"/> flow has acquired the mutex, any additional
    /// acquistions by nested operations will also be accepted.
    /// </para>
    /// <code language="cs">
    /// var mutex = new AsyncReentrantMutex();
    /// 
    /// await mutex.AcquireAsync(
    ///     async () =>
    ///     {
    ///         // Protected code
    ///         
    ///         await mutex.ExecuteActionAsync(   // &lt;--- This doesn't block
    ///             async () =>
    ///             {
    ///                 // More protected code
    ///             });
    ///     });
    /// </code>
    /// <para>
    /// The <see cref="ExecuteFuncAsync{TResult}(Func{Task{TResult}})"/> can be used to execute an async
    /// function that returns a result instead.
    /// </para>
    /// <para>
    /// <see cref="AsyncReentrantMutex"/> is disposable.  Calling dispose will cause
    /// <see cref="ObjectDisposedException"/> to be thrown on any tasks waiting
    /// to acquire the mutex.
    /// </para>
    /// </remarks>
    public class AsyncReentrantMutex : IDisposable
    {
        //---------------------------------------------------------------------
        // Static members

        private const string ObjectName = "AsyncReentrantMutex";

        private static AsyncLocal<int> nesting = new AsyncLocal<int>();

        //---------------------------------------------------------------------
        // Instance members

        private AsyncMutex  mutex;
        private bool        isDisposed;

        /// <summary>
        /// Constructor.
        /// </summary>
        public AsyncReentrantMutex()
        {
            mutex = new AsyncMutex();
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~AsyncReentrantMutex()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases any important resources associated with the instance.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This method will cause a <see cref="ObjectDisposedException"/> to be thrown on
        /// any task waiting to acquire this mutex.
        /// </note>
        /// </remarks>
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
            if (isDisposed)
            {
                return;
            }

            if (disposing)
            {
                mutex.Dispose();
                GC.SuppressFinalize(this);
            }

            isDisposed = true;
        }

        /// <summary>
        /// Acquires the mutex and then invokes the asynchronous action passed.  This method
        /// returns after the action completes.
        /// </summary>
        /// <param name="action">The asynchronous action.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task ExecuteActionAsync(Func<Task> action)
        {
            Covenant.Requires<ArgumentNullException>(action != null, nameof(action));

            if (isDisposed)
            {
                throw new ObjectDisposedException(ObjectName);
            }

            if (nesting.Value == 0)
            {
                using (await mutex.AcquireAsync())
                {
                    nesting.Value++;

                    try
                    {
                        await action.Invoke();
                    }
                    finally
                    {
                        nesting.Value--;
                        Covenant.Assert(nesting.Value >= 0, "Nesting underflow.");
                    }
                }
            }
            else
            {
                nesting.Value++;

                try
                {
                    await action.Invoke();
                }
                finally
                {
                    nesting.Value--;
                    Covenant.Assert(nesting.Value >= 0, "Nesting underflow.");
                }
            }
        }

        /// <summary>
        /// Acquires the mutex and then invokes the asynchronous function passed, returning
        /// the function's result.
        /// </summary>
        /// <typeparam name="TResult">Specifies the result returned by the async function.</typeparam>
        /// <param name="function">The asynchronous function.</param>
        /// <returns>The function result.</returns>
        public async Task<TResult> ExecuteFuncAsync<TResult>(Func<Task<TResult>> function)
        {
            Covenant.Requires<ArgumentNullException>(function != null, nameof(function));

            if (isDisposed)
            {
                throw new ObjectDisposedException(ObjectName);
            }

            if (nesting.Value == 0)
            {
                using (await mutex.AcquireAsync())
                {
                    nesting.Value++;

                    try
                    {
                        return await function();
                    }
                    finally
                    {
                        nesting.Value--;
                        Covenant.Assert(nesting.Value >= 0, "Nesting underflow.");
                    }
                }
            }
            else
            {
                nesting.Value++;

                try
                {
                    return await function.Invoke();
                }
                finally
                {
                    nesting.Value--;
                    Covenant.Assert(nesting.Value >= 0, "Nesting underflow.");
                }
            }
        }
    }
}
