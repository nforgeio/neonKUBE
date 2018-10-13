//-----------------------------------------------------------------------------
// FILE:	    AsyncAutoResetEvent.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
//
// Code based on a MSDN article by Stephen Toub (MSFT):
// http://blogs.msdn.com/b/pfxteam/archive/2012/02/11/10266923.aspx

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
    /// Implements an <c>async</c>/<c>await</c> friendly equivalent of <see cref="AutoResetEvent"/>.
    /// </summary>
    /// <threadsafety instance="true"/>
    public class AsyncAutoResetEvent : IDisposable
    {
        private object                              syncLock            = new object();
        private Queue<TaskCompletionSource<bool>>   waitingTasks        = new Queue<TaskCompletionSource<bool>>();
        private static Task                         cachedCompletedTask = Task.FromResult(true);
        private bool                                isSignalled;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="initialState">
        /// Pass <c>true</c> to set the initial event state to signaled, <c>false</c>
        /// for unsignaled.
        /// </param>
        public AsyncAutoResetEvent(bool initialState = false)
        {
            if (initialState)
            {
                Set();
            }
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~AsyncAutoResetEvent()
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
                    if (waitingTasks == null)
                    {
                        return; // Already disposed
                    }

                    while (waitingTasks.Count > 0)
                    {
                        waitingTasks.Dequeue().SetException(new ObjectDisposedException(this.GetType().FullName));
                    }
                }

                waitingTasks = null;
                GC.SuppressFinalize(this);
            }

            waitingTasks = null;
        }

        /// <summary>
        /// Sets the state of the event to signalled allowing a single task that is currently
        /// waiting or the next task that waits on the event to proceed.
        /// </summary>
        public void Set()
        {
            var releasedTask = (TaskCompletionSource<bool>)null;

            lock (syncLock)
            {
                if (waitingTasks == null)
                {
                    throw new ObjectDisposedException(this.GetType().FullName);
                }

                if (waitingTasks.Count > 0)
                {
                    releasedTask = waitingTasks.Dequeue();
                }
                else if (!isSignalled)
                {
                    isSignalled = true;
                }
            }

            if (releasedTask != null)
            {
                releasedTask.SetResult(true);
            }
        }

        /// <summary>
        /// Sets the state of the event to unsignalled, so that tasks will have to wait.
        /// </summary>
        public void Reset()
        {
            lock (syncLock)
            {
                if (waitingTasks == null)
                {
                    throw new ObjectDisposedException(this.GetType().FullName);
                }

                isSignalled = false;
            }
        }

        /// <summary>
        /// Waits until the event is signalled.
        /// </summary>
        public Task WaitAsync()
        {
            lock (syncLock)
            {
                if (waitingTasks == null)
                {
                    throw new ObjectDisposedException(this.GetType().FullName);
                }

                if (isSignalled)
                {
                    isSignalled = false;
                    return cachedCompletedTask;
                }
                else
                {
                    var tcs = new TaskCompletionSource<bool>();

                    waitingTasks.Enqueue(tcs);
                    return tcs.Task;
                }
            }
        }
    }
}

