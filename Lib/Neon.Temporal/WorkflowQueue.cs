//-----------------------------------------------------------------------------
// FILE:	    WorkflowQueue.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Tasks;
using Neon.Temporal;
using Neon.Temporal.Internal;
using Neon.Time;

namespace Neon.Temporal
{
    /// <summary>
    /// Implements a workflow-safe first-in-first-out (FIFO) queue that can be used by
    /// workflow signal methods to communicate with the running workflow logic.
    /// </summary>
    /// <typeparam name="T">Specifies the type of the queued items.</typeparam>
    /// <remarks>
    /// <para>
    /// You can construct workflow queue instances in your workflows via
    /// <see cref="Workflow.NewQueueAsync{T}(int)"/>, optionally specifying 
    /// the maximum capacity of the queue.  This defaults to <see cref="DefaultCapacity"/>
    /// and may not be less that 2 queued items.
    /// </para>
    /// <para>
    /// Items are added to the queue via <see cref="EnqueueAsync(T)"/>.  This
    /// method will return immediately when the number of items currently in
    /// the queue is less than the capacity, otherwise the operation will block
    /// until an item has been dequeued and the queue is no longer full.
    /// </para>
    /// <note>
    /// Serialized item sizes must be less than 64 KiB.
    /// </note>
    /// <para>
    /// Use <see cref="DequeueAsync(TimeSpan)"/> to read from the queue using
    /// an optional timeout.
    /// </para>
    /// <note>
    /// <para>
    /// The <see cref="WorkflowQueue{T}"/> class is intended only for two scenarios
    /// within an executing workflow:
    /// </para>
    /// <list type="number">
    ///     <item>
    ///     <b>Workflow Entry Point:</b> Workflow entry points have full access queues 
    ///     including creating, closing, reading, writing, and fetching the length.
    ///     </item>
    ///     <item>
    ///     <b>Workflow Signal:</b> Workflow signal methods have partial access to
    ///     queues including closing, writing, and fetching the length.  Signals 
    ///     cannot create or read from queues.
    ///     </item>
    ///     </list>
    /// </note>
    /// </remarks>
    public class WorkflowQueue<T> : IDisposable
    {
        /// <summary>
        /// The default maximum number of items allowed in a queue.
        /// </summary>
        public const int DefaultCapacity = 100;

        private TemporalClient       client;
        private long                contextId;
        private long                queueId;
        private int                 capacity;
        private bool                isClosed;
        private bool                isDisposed;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="parentWorkflow">The parent workflow.</param>
        /// <param name="queueId">The queue ID.</param>
        /// <param name="capacity">The maximum number of items allowed in the queue.</param>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        /// <remarks>
        /// <note>
        /// <see cref="WorkflowQueue{T}"/> instances may only be created within 
        /// workflow entry point methods.
        /// </note>
        /// </remarks>
        internal WorkflowQueue(Workflow parentWorkflow, long queueId, int capacity)
        {
            Covenant.Requires<ArgumentNullException>(parentWorkflow != null, nameof(parentWorkflow));
            Covenant.Requires<ArgumentException>(queueId > 0, nameof(queueId));
            Covenant.Requires<ArgumentException>(capacity >= 2, nameof(capacity));
            WorkflowBase.CheckCallContext(allowWorkflow: true);

            this.client    = parentWorkflow.Client;
            this.contextId = parentWorkflow.ContextId;
            this.Capacity  = capacity;
            this.queueId   = queueId;
            this.isClosed  = false;
        }

        /// <summary>
        /// Closes the queue if it's not already closed.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Queues may be disposed only from within workflow entrypoint or signal methods.
        /// </note>
        /// </remarks>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Releases all associated resources.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected virtual void Dispose(bool disposing)
        {
            WorkflowBase.CheckCallContext(allowWorkflow: true, allowSignal: true);

            if (disposing && !isClosed)
            {
                CloseAsync().Wait();
            }

            isDisposed = true;
        }

        /// <summary>
        /// Ensures that the instance is not disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if the queue is disposed.</exception>
        private void CheckDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(WorkflowQueue<T>));
            }
        }

        /// <summary>
        /// Returns the maximum number of items allowed in the queue at any given moment.
        /// This may not be set to a value less than 2.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if the queue is disposed.</exception>
        public int Capacity
        {
            get
            {
                CheckDisposed();

                return capacity;
            }

            set => capacity = value;
        }

        /// <summary>
        /// Adds an item to the queue.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the queue is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown if the serialized size of <paramref name="item"/> is not less than 64KiB.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the associated workflow client is disposed.</exception>
        /// <exception cref="WorkflowQueueClosedException">Thrown if the associated queue has been closed.</exception>
        /// <remarks>
        /// <para>
        /// This method returns immediately if the queue is not full, otherwise
        /// it will block until there's enough space to append the new item.
        /// </para>
        /// <note>
        /// Item data after being serialized must be less than 64 KiB.
        /// </note>
        /// </remarks>
        /// <remarks>
        /// <note>
        /// Items may be added to queues only within workflow entrypoint or signal methods.
        /// </note>
        /// </remarks>
        public async Task EnqueueAsync(T item)
        {
            await SyncContext.ClearAsync;
            client.EnsureNotDisposed();
            CheckDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true, allowSignal: true);

            if (isClosed)
            {
                throw new WorkflowQueueClosedException($"[{nameof(WorkflowQueue<T>)}] is closed.");
            }

            var encodedItem = client.DataConverter.ToData(item);

            if (encodedItem.Length >= 64 * ByteUnits.KibiBytes)
            {
                throw new NotSupportedException($"Serialized items enqueued to a [{nameof(WorkflowQueue<T>)}] must be less than 64 KiB.");
            }

            var reply = (WorkflowQueueWriteReply)await client.CallProxyAsync(
                new WorkflowQueueWriteRequest()
                {
                    ContextId = contextId,
                    QueueId   = queueId,
                    Data      = encodedItem
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Attempts to add an item to the queue.  Unlike <see cref="EnqueueAsync(T)"/>, this method
        /// does not block when the queue is full and returns <c>false</c> instead.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns><c>true</c> if the item was written or <c>false</c> if the queue is full and the item was not written.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the queue is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown if the serialized size of <paramref name="item"/> is not less than 64KiB.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the associated workflow client is disposed.</exception>
        /// <exception cref="WorkflowQueueClosedException">Thrown if the associated queue has been closed.</exception>
        /// <remarks>
        /// <note>
        /// Item data after being serialized must be less than 64 KiB.
        /// </note>
        /// </remarks>
        /// <remarks>
        /// <note>
        /// Items may be added to queues only within workflow entrypoint or signal methods.
        /// </note>
        /// </remarks>
        public async Task<bool> TryEnqueueAsync(T item)
        {
            await SyncContext.ClearAsync;
            client.EnsureNotDisposed();
            CheckDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true, allowSignal: true);

            var encodedItem = client.DataConverter.ToData(item);

            if (encodedItem.Length >= 64 * ByteUnits.KibiBytes)
            {
                throw new NotSupportedException($"Serialized items enqueued to a [{nameof(WorkflowQueue<T>)}] must be less than 64 KiB.");
            }

            var reply = (WorkflowQueueWriteReply)await client.CallProxyAsync(
                new WorkflowQueueWriteRequest()
                {
                    ContextId = contextId,
                    QueueId   = queueId,
                    NoBlock   = true,
                    Data      = encodedItem
                });

            reply.ThrowOnError();

            return !reply.IsFull;
        }

        /// <summary>
        /// Attempts to dequeue an item from the queue with an optional timeout.
        /// </summary>
        /// <param name="timeout">The optional timeout.</param>
        /// <returns>The next item from the queue.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated workflow client is disposed.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the queue is disposed.</exception>
        /// <exception cref="TemporalTimeoutException">Thrown if the timeout was reached before a value could be returned.</exception>
        /// <exception cref="WorkflowQueueClosedException">Thrown if the the queue is closed.</exception>
        /// <remarks>
        /// <note>
        /// Items may be read from queues only from within workflow entrypoint methods.
        /// </note>
        /// </remarks>
        public async Task<T> DequeueAsync(TimeSpan timeout = default)
        {
            await SyncContext.ClearAsync;
            client.EnsureNotDisposed();
            CheckDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);

            var reply = (WorkflowQueueReadReply)await client.CallProxyAsync(
                new WorkflowQueueReadRequest()
                {
                    ContextId = contextId,
                    QueueId   = queueId,
                    Timeout   = timeout
                });

            reply.ThrowOnError();

            if (reply.IsClosed)
            {
                throw new WorkflowQueueClosedException();
            }

            return JsonDataConverter.Instance.FromData<T>(reply.Data);
        }

        /// <summary>
        /// <para>
        /// Closes the queue.
        /// </para>
        /// <note>
        /// This does nothing if the queue is already closed.
        /// </note>
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated workflow client is disposed.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the queue is disposed.</exception>
        /// <remarks>
        /// <note>
        /// Queues may be closed only from within workflow entrypoint or signal methods.
        /// </note>
        /// </remarks>
        public async Task CloseAsync()
        {
            await SyncContext.ClearAsync;
            client.EnsureNotDisposed();
            CheckDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true, allowSignal: true);

            if (isClosed)
            {
                return;
            }

            var reply = (WorkflowQueueCloseReply)await client.CallProxyAsync(
                new WorkflowQueueCloseRequest()
                {
                    ContextId = contextId,
                    QueueId   = queueId,
                });

            reply.ThrowOnError();

            isClosed = true;
        }
    }
}
