//-----------------------------------------------------------------------------
// FILE:        AsyncManualResetEvent.cs
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
//
// Code based on a MSDN article by Stephen Toub (MSFT):
// http://blogs.msdn.com/b/pfxteam/archive/2012/02/11/10266920.aspx

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// $todo(jeff.lill):
//
// Implement variations of WaitAsync() that include timeouts.  This will probably
// require a global background thread (or something) to poll for timeouts.
// Kind of icky, but this would be useful.

namespace Neon.Tasks
{
    /// <summary>
    /// Implements an <c>async</c>/<c>await</c> friendly equivalent of <see cref="ManualResetEvent"/>.
    /// </summary>
    /// <threadsafety instance="true"/>
    public class AsyncManualResetEvent : IDisposable
    {
        private object                      syncLock = new object();
        private bool                        isDisposed;
        private TaskCompletionSource<bool>  tcs;    // NULL if signalled, otherwise the TCS tasks will wait on.

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="initialState">
        /// Pass <c>true</c> to set the initial event state to signaled, <c>false</c>
        /// for unsignaled.
        /// </param>
        public AsyncManualResetEvent(bool initialState = false)
        {
            this.isDisposed = false;

            if (initialState)
            {
                this.tcs = null;
            }
            else
            {
                this.tcs = new TaskCompletionSource<bool>();
            }
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~AsyncManualResetEvent()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases any important resources associated with the instance.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This method will cause a <see cref="ObjectDisposedException"/> to be thrown on
        /// any task waiting on this event.
        /// </note>
        /// </remarks>
        public void Close()
        {
            Dispose(true);
        }

        /// <summary>
        /// Releases any important resources associated with the instance.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This method will cause a <see cref="ObjectDisposedException"/> to be thrown on
        /// any task waiting on this event.
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
            if (disposing)
            {
                lock (syncLock)
                {
                    if (isDisposed)
                    {
                        return;
                    }

                    if (tcs != null)
                    {
                        tcs.SetException(new ObjectDisposedException(this.GetType().FullName));
                    }

                    isDisposed = true;
                    GC.SuppressFinalize(this);
                }
            }

            tcs        = null;
            isDisposed = true;
        }

        /// <summary>
        /// Sets the state of the event to signalled allowing one or more waiting tasks
        /// to proceed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if the event has already been closed.</exception>
        public void Set() 
        {
            lock (syncLock)
            {
                if (isDisposed)
                {
                    throw new ObjectDisposedException(this.GetType().FullName);
                }

                if (tcs == null)
                {
                    return; // Event is already set.
                }

                var tempTcs = tcs;

                tcs = null; // Indicate that the event is now signalled.

                // Signal any tasks waiting on the TCS by setting the result 
                // within another task.  This ensures that a waiting task
                // will not execute on the current thread which would not
                // be what applications will expect.
                
                Task.Factory.StartNew(
                    s => ((TaskCompletionSource<bool>)s).SetResult(true),
                    tempTcs, 
                    CancellationToken.None, 
                    TaskCreationOptions.PreferFairness, 
                    TaskScheduler.Default);
            }
        }

        /// <summary>
        /// Sets the state of the event to non-signalled, causing tasks to block.
        /// </summary>
        public void Reset()
        {
            lock (syncLock)
            {
                if (isDisposed)
                {
                    throw new ObjectDisposedException(this.GetType().FullName);
                }

                if (tcs != null)
                {
                    return; // Event is already reset.
                }

                tcs = new TaskCompletionSource<bool>();
            }
        }

        /// <summary>
        /// Wait asynchronously for the event to be signalled.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if the event has already been closed or is closed before it is signalled.</exception>
        public NonDisposableTask WaitAsync()
        {
            lock (syncLock)
            {
                if (isDisposed)
                {
                    throw new ObjectDisposedException(this.GetType().FullName);
                }

                if (tcs == null)
                {
                    // The event is signalled so there's no need to block the caller.

                    return new NonDisposableTask(Task.FromResult(true));
                }
                else
                {
                    return new NonDisposableTask(tcs.Task);
                }
            }
        }
    }
}
