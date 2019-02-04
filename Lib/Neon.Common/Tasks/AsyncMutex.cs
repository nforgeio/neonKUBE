//-----------------------------------------------------------------------------
// FILE:        AsyncMutex.cs
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
// http://blogs.msdn.com/b/pfxteam/archive/2012/02/12/10267069.aspx

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// $todo(jeff.lill):
//
// The current implementation is based on [AsyncReaderWriterLock] for simplicitly 
// and because that is well tested.  There could be minor efficency gains to
// reimplement this from scratch.

namespace Neon.Tasks
{
    /// <summary>
    /// Implements an <c>async</c>/<c>await</c> friendly equivalent of <see cref="Mutex"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class can be used to grant a task exclusive access to a resource.  This class is
    /// pretty easy to use.  Simply instantiate an instance and then call <see cref="AcquireAsync"/>
    /// within a <c>using</c> statement:
    /// </para>
    /// <code language="cs">
    /// var mutex = new AsyncMutex();
    /// 
    /// using (await mutex.Acquire())
    /// {
    ///     // Protected code
    /// }
    /// </code>
    /// <note>
    /// Be very sure to include the <c>await</c> within the <c>using</c> statement to avoid
    /// hard to debug problems.  The <c>await</c> ensures that the <c>using</c> statement
    /// will dispose the acquired lock as opposed to the <see cref="Task"/> that returns
    /// the lock.
    /// </note>
    /// <para>
    /// Applications that cannot use a <c>using</c> statement may release the lock explicitly
    /// by disposing the object returned by the lock method, like this:
    /// </para>
    /// <code language="cs">
    /// var mutex  = new AsyncMutex();
    /// var myLock = await mutex.AcquireAsync();
    /// 
    /// // Protected code.
    /// 
    /// myLock.Dispose();
    /// </code>
    /// <para>
    /// <see cref="AsyncMutex"/>'s <see cref="Dispose()"/> method ensures that any tasks
    /// waiting for a lock will be unblocked with an <see cref="ObjectDisposedException"/>.
    /// </para>
    /// <note>
    /// <see cref="AsyncMutex"/> does not support any kind of reentrant <see cref="Task"/> locking 
    /// support.  Child tasks will be considered to be completely independent of the parent
    /// and <b>will not</b> inherit the parent's lock and a single task will not be able to acquire 
    /// the same lock multiple times.
    /// </note>
    /// </remarks>
    /// <threadsafety instance="true"/>
    public class AsyncMutex : IDisposable
    {
        private const string ObjectName = "AsyncMutex";

        private AsyncReaderWriterLock   rwLock;
        private bool                    isDisposed;

        /// <summary>
        /// Constructor.
        /// </summary>
        public AsyncMutex()
        {
            this.rwLock = new AsyncReaderWriterLock();
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~AsyncMutex()
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
                rwLock.Dispose();
                GC.SuppressFinalize(this);
            }

            isDisposed = true;
        }

        /// <summary>
        /// Acquires exclusive access to the mutex.
        /// </summary>
        /// <returns>The <see cref="IDisposable"/> instance to be disposed to release the lock.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the lock is disposed before or after this method is called.</exception>
        /// <remarks>
        /// <note>
        /// This class allows multiple readers to hold the lock at any given time but requires
        /// that writers have exclusive access.  Writers are given priority over readers.
        /// </note>
        /// </remarks>
        public Task<IDisposable> AcquireAsync()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(ObjectName);
            }

            // Acquire the write lock since it is exclusive.

            return rwLock.GetWriteLockAsync();
        }
    }
}
