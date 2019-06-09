//-----------------------------------------------------------------------------
// FILE:        AsyncReaderWriterLock.cs
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
// Implement variations of *LockAsync() that include timeouts.  This will probably
// require a global background thread (or something) to poll for timeouts.
// Kind of icky, but this would be useful.

// $todo(jeff.lill):
//
// Look into implementing additional fairness policies so that writes can't
// completely starve reads.

namespace Neon.Tasks
{
    /// <summary>
    /// Implements an <c>async</c>/<c>await</c> friendly equivalent of <b>ReaderWriterLock</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class can be used to grant a single writer task exclusive access to a resource
    /// or multiple reader tasks.  This class is pretty easy to use.  Simply instantiate
    /// an instance and then call <see cref="GetReadLockAsync"/> or <see cref="GetWriteLockAsync"/>
    /// within a <c>using</c> statement:
    /// </para>
    /// <code language="cs">
    /// var rwLock = new AsyncReaderWriterLock();
    /// 
    /// using (await rwLock.GetReadLockAsync())
    /// {
    ///     // Protected reading code
    /// }
    /// 
    /// using (await rwLock.GetWriteLockAsync())
    /// {
    ///     // Protected writing code
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
    /// var rwLock   = new AsyncReaderWriterLock();
    /// var readLock = await rwLock.GetReadLockAsync();
    /// 
    /// // Protected reading code.
    /// 
    /// readLock.Dispose();
    /// </code>
    /// <para>
    /// <see cref="AsyncReaderWriterLock"/>'s <see cref="Dispose()"/> method ensures that any tasks
    /// waiting for a lock will be unblocked with an <see cref="ObjectDisposedException"/>.
    /// </para>
    /// <para>
    /// This class is implemented is fairly simple and always favors writers over readers.
    /// Also, all waiting readers will be released together.
    /// </para>
    /// <note>
    /// <see cref="AsyncReaderWriterLock"/> does not support any kind of reentrant <see cref="Task"/>
    /// locking support.  Child tasks will be considered to be completely independent of the parent
    /// and <b>will not</b> inherit the parent's lock and a single task will not be able to acquire 
    /// the same lock multiple times.
    /// </note>
    /// </remarks>
    /// <threadsafety instance="true"/>
    public class AsyncReaderWriterLock : IDisposable
    {
        //---------------------------------------------------------------------
        // Local types

        /// <summary>
        /// The disposable lock returned by an <see cref="AsyncReaderWriterLock"/> granting read
        /// or write access to a resource.  Call <see cref="Dispose"/> to release the lock.
        /// </summary>
        private struct Lock : IDisposable
        {
            private AsyncReaderWriterLock   parent;
            private bool                    isWriteLock;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="parent">The parent lock.</param>
            /// <param name="isWriteLock"><c>true</c> if for a write lock.</param>
            internal Lock(AsyncReaderWriterLock parent, bool isWriteLock)
            {
                this.parent      = parent;
                this.isWriteLock = isWriteLock;
            }

            /// <summary>
            /// Releases the lock acquired from a <see cref="AsyncReaderWriterLock"/>.
            /// </summary>
            public void Dispose()
            {
                // Note that I'm not implementing any kind of [isDisposed] check here
                // since I'm reusing the instances below below as the [cachedReaderLock]
                // and [cachedWriterLock] as a performance improvement.

                if (isWriteLock)
                {
                    parent.ReleaseWriteLock();
                }
                else
                {
                    parent.ReleaseReadLock();
                }

                GC.SuppressFinalize(this);
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private const string ObjectName = "AsyncReaderWriterLock";

        private object                                      syncLock = new object();
        private Lock                                        readerLock;
        private Lock                                        writerLock;
        private Task<IDisposable>                           noWaitReaderLockTask;
        private Task<IDisposable>                           noWaitWriterLockTask;
        private Queue<TaskCompletionSource<IDisposable>>    waitingWriterTcsQueue;
        private TaskCompletionSource<IDisposable>           waitingReaderTcs;
        private int                                         waitingReaderCount;
        private bool                                        isDisposed;
#if DEBUG
        private int                                         writerCount;
        private int                                         readerCount;
#endif

        //  -1: writer has the lock
        //   0: lock is unclaimed
        // > 0: number of readers holding the lock

        private int lockStatus;

        /// <summary>
        /// Constructor.
        /// </summary>
        public AsyncReaderWriterLock()
        {
            this.readerLock            = new Lock(this, isWriteLock: false);
            this.writerLock            = new Lock(this, isWriteLock: true);
            this.noWaitReaderLockTask  = Task.FromResult((IDisposable)readerLock);
            this.noWaitWriterLockTask  = Task.FromResult((IDisposable)writerLock);
            this.waitingWriterTcsQueue = new Queue<TaskCompletionSource<IDisposable>>();
            this.waitingReaderTcs      = null;
            this.waitingReaderCount    = 0;
            this.lockStatus            = 0;
            this.isDisposed            = false;
#if DEBUG
            this.writerCount           = 0;
            this.readerCount           = 0;
#endif
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~AsyncReaderWriterLock()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases any important resources associated with the instance.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This method will cause a <see cref="ObjectDisposedException"/> to be thrown on
        /// any task waiting to acquire this lock.
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
        /// any task waiting to acquire this lock.
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

                    if (waitingReaderTcs != null)
                    {
                        waitingReaderTcs.SetException(new ObjectDisposedException(ObjectName));
                    }

                    while (waitingWriterTcsQueue.Count > 0)
                    {
                        waitingWriterTcsQueue.Dequeue().SetException(new ObjectDisposedException(ObjectName));
                    }
                }

                isDisposed = true;
                GC.SuppressFinalize(this);
            }

            waitingReaderTcs      = null;
            waitingWriterTcsQueue = null;
            isDisposed            = true;
        }

        /// <summary>
        /// Acquires a non-exclusive read lock.
        /// </summary>
        /// <returns>The <see cref="IDisposable"/> instance to be disposed to release the lock.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the lock is disposed before or after this method is called.</exception>
        /// <remarks>
        /// <note>
        /// This class allows multiple readers to hold the lock at any given time but requires
        /// that writers have exclusive access.  Writers are given priority over readers.
        /// </note>
        /// </remarks>
        public Task<IDisposable> GetReadLockAsync()
        {
            lock (syncLock)
            {
                if (isDisposed)
                {
                    throw new ObjectDisposedException(ObjectName);
                }

                if (lockStatus >= 0 && waitingWriterTcsQueue.Count == 0)
                {
#if DEBUG
                    Interlocked.Increment(ref readerCount);
#endif
                    ++lockStatus;
                    return noWaitReaderLockTask;
                }
                else
                {
                    ++waitingReaderCount;

                    if (waitingReaderTcs == null)
                    {
                        waitingReaderTcs = new TaskCompletionSource<IDisposable>();
                    }

                    return waitingReaderTcs.Task;
                }
            }
        }

        /// <summary>
        /// Called by a <see cref="Lock"/> to release a read lock.
        /// </summary>
        private void ReleaseReadLock()
        {
            TaskCompletionSource<IDisposable> wokenTcs;

            lock (syncLock)
            {
                if (isDisposed)
                {
                    return;
                }

                if (lockStatus <= 0)
                {
                    throw new InvalidOperationException("The current task does not own the read lock.  Make sure that your [GetXXXLock/ReleaseXXXLock] calls are matched and that you use [await] for all [GetXXXLock()] calls.");
                }

                lockStatus--;
#if DEBUG
                Interlocked.Decrement(ref readerCount);
#endif
                if (lockStatus == 0 && waitingWriterTcsQueue.Count > 0)
                {
#if DEBUG
                    Interlocked.Increment(ref writerCount);
#endif
                    lockStatus = -1;
                    wokenTcs   = waitingWriterTcsQueue.Dequeue();

                    wokenTcs.SetResult(writerLock);
                }
                else if (waitingReaderTcs != null)
                {
#if DEBUG
                    Interlocked.Add(ref readerCount, waitingReaderCount);
#endif
                    wokenTcs           = waitingReaderTcs;
                    lockStatus        += waitingReaderCount;
                    waitingReaderCount = 0;
                    waitingReaderTcs   = null;

                    wokenTcs.SetResult(readerLock);
                }
            }
        }

        /// <summary>
        /// Acquires an exclusive write lock.
        /// </summary>
        /// <returns>The <see cref="IDisposable"/> instance to be disposed to release the lock.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the lock is disposed before or after this method is called.</exception>
        /// <remarks>
        /// <note>
        /// This class allows multiple readers to hold the lock at any given time but requires
        /// that writers have exclusive access.  Writers are given priority over readers.
        /// </note>
        /// </remarks>
        public NonDisposableTask<IDisposable> GetWriteLockAsync()
        {
            lock (syncLock)
            {
                if (isDisposed)
                {
                    throw new ObjectDisposedException(ObjectName);
                }

                if (lockStatus == 0)
                {
#if DEBUG
                    Interlocked.Increment(ref writerCount);
#endif
                    lockStatus = -1;
                    return new NonDisposableTask<IDisposable>(noWaitWriterLockTask);
                }
                else
                {
                    var writeWaitTcs = new TaskCompletionSource<IDisposable>();

                    waitingWriterTcsQueue.Enqueue(writeWaitTcs);
                    return new NonDisposableTask<IDisposable>(writeWaitTcs.Task);
                }
            }
        }

        /// <summary>
        /// Called by a <see cref="Lock"/> to release a write lock.
        /// </summary>
        private void ReleaseWriteLock()
        {
            TaskCompletionSource<IDisposable> wokenTcs;

            lock (syncLock)
            {
                if (isDisposed)
                {
                    return;
                }

                if (lockStatus != -1)
                {
                    throw new InvalidOperationException("The current task does not own the write lock.  Make sure that your [GetXXXLock/ReleaseXXXLock] calls are matched and that you use [await] for all [GetXXXLock()] calls.");
                }
#if DEBUG
                Interlocked.Decrement(ref writerCount);
#endif
                if (waitingWriterTcsQueue.Count > 0)
                {
#if DEBUG
                    Interlocked.Increment(ref writerCount);
#endif
                    wokenTcs = waitingWriterTcsQueue.Dequeue();

                    wokenTcs.SetResult(writerLock);
                }
                else if (waitingReaderTcs != null)
                {
#if DEBUG
                    Interlocked.Add(ref readerCount, waitingReaderCount);
#endif
                    wokenTcs           = waitingReaderTcs;
                    lockStatus         = waitingReaderCount;
                    waitingReaderCount = 0;
                    waitingReaderTcs   = null;

                    wokenTcs.SetResult(readerLock);
                }
                else 
                {
                    lockStatus = 0;
                }
            }
        }
    }
}
