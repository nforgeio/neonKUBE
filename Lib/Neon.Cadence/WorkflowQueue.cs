//-----------------------------------------------------------------------------
// FILE:	    WorkflowQueue.cs
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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Tasks;
using Neon.Time;

namespace Neon.Cadence
{
    /// <summary>
    /// Implements a workflow-safe first-in-first-out (FIFO) queue that can be used by
    /// workflow signal methods to communicate with the running workflow.
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
    /// <para>
    /// <see cref="GetLengthAsync"/> returns the number of items currently residing
    /// in the queue and <see cref="CloseAsync"/> closes the queue.
    /// </para>
    /// <note>
    /// <para>
    /// The <see cref="WorkflowQueue{T}"/> class is intended only for three scenarios
    /// within an executing workflow:
    /// </para>
    /// <list type="number">
    ///     <item>
    ///     <b>Workflow Entry Point:</b> Workflow entry points have full access queues 
    ///     including creating, closing, reading, writing, and fetching the length.
    ///     </item>
    ///     <item>
    ///     <b>Workflow Query:</b> Workflow query methods may only get the length
    ///     of a queue.
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
        /// The default number of items allowed in a queue.
        /// </summary>
        public const int DefaultCapacity = 100;

        private Workflow        parentWorkflow;
        private CadenceClient   client;
        private long            queueId;
        private bool            isClosed;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="parentWorkflow">The parent workflow.</param>
        /// <param name="queueId">The queue ID.</param>
        /// <param name="capacity">The maximum number of items allowed in the queue.</param>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        internal WorkflowQueue(Workflow parentWorkflow, long queueId, int capacity)
        {
            Covenant.Requires<ArgumentNullException>(parentWorkflow != null, nameof(parentWorkflow));
            Covenant.Requires<ArgumentException>(queueId > 0, nameof(queueId));
            Covenant.Requires<ArgumentException>(capacity >= 2, nameof(capacity));
            WorkflowBase.CheckCallContext(allowWorkflow: true);

            this.parentWorkflow = parentWorkflow;
            this.client         = parentWorkflow.Client;
            this.Capacity       = capacity;
            this.queueId        = queueId;
            this.isClosed       = false;
        }

        /// <inheritdoc/>
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
        }

        /// <summary>
        /// Returns the maximum number of items allowed in the queue at any given moment.
        /// This may not be less than 2.
        /// </summary>
        public int Capacity { get; private set; }

        /// <summary>
        /// Adds an item to the queue.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="NotSupportedException">Thrown if the serialized size of <paramref name="item"/> is not less than 64KiB.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the associated workflow client is disposed.</exception>
        /// <exception cref="CadenceQueueClosedException">Thrown if the associated queue has been closed.</exception>
        /// <remarks>
        /// <para>
        /// This method returns immediately if the queue is full, otherwise
        /// it will block until there's enough space to append the new item.
        /// </para>
        /// <note>
        /// Serialized item sizes must be less than 64 KiB.
        /// </note>
        /// </remarks>
        public async Task EnqueueAsync(T item)
        {
            await SyncContext.ClearAsync;
            client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true, allowSignal: true);

            if (isClosed)
            {
                throw new CadenceQueueClosedException($"[{nameof(WorkflowQueue<T>)}] is closed.");
            }

            var encodedItem = client.DataConverter.ToData(item);

            if (encodedItem.Length >= 64 * ByteUnits.KibiBytes)
            {
                throw new NotSupportedException($"Serialized items enqueued to a [{nameof(WorkflowQueue<T>)}] must be less than 64 KiB.");
            }

            var stub   = parentWorkflow.NewLocalActivityStub<IQueueActivities, QueueActivities>();
            var stream = MemoryStreamPool.Alloc();

            try
            {
                var replyBytes = await stub.EnqueueAsync(parentWorkflow.ContextId, queueId, encodedItem);

                stream.Position = 0;
                stream.Write(replyBytes, 0, replyBytes.Length);
                stream.Position = 0;

                var reply = ProxyMessage.Deserialize<WorkflowQueueWriteReply>(stream);

                reply.ThrowOnError();
            }
            finally
            {
                MemoryStreamPool.Free(stream);
            }
        }

        /// <summary>
        /// Attempts to dequeue an item from the queue with an optional timeout.
        /// </summary>
        /// <param name="timeout">The optional timeout.</param>
        /// <returns>The next item from the queue.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated workflow client is disposed.</exception>
        /// <exception cref="CadenceTimeoutException">Thrown if the timeout was reached before a value could be returned.</exception>
        /// <exception cref="CadenceQueueClosedException">Thrown if the the queue is closed.</exception>
        public async Task<T> DequeueAsync(TimeSpan timeout = default)
        {
            await SyncContext.ClearAsync;
            client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);

            if (isClosed)
            {
                throw new CadenceQueueClosedException("Queue is closed.");
            }

            var stub   = parentWorkflow.NewLocalActivityStub<IQueueActivities, QueueActivities>();
            var stream = MemoryStreamPool.Alloc();

            try
            {
                var replyBytes = await stub.DequeueAsync(parentWorkflow.ContextId, queueId, timeout);

                stream.Position = 0;
                stream.Write(replyBytes, 0, replyBytes.Length);
                stream.Position = 0;

                var reply = ProxyMessage.Deserialize<WorkflowQueueReadReply>(stream);

                reply.ThrowOnError();

                if (reply.IsClosed)
                {
                    throw new CadenceQueueClosedException("Queue is closed.");
                }

                if (reply.Data == null)
                {
                    throw new CadenceTimeoutException("Dequeue operation timed out.");
                }

                return client.DataConverter.FromData<T>(reply.Data);
            }
            finally
            {
                MemoryStreamPool.Free(stream);
            }
        }

        /// <summary>
        /// Returns the number of items currently waiting in the queue.
        /// </summary>
        /// <returns>The item count.</returns>
        /// <exception cref="CadenceQueueClosedException">Thrown if the queue is closed.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the associated workflow client is disposed.</exception>
        public async Task<int> GetLengthAsync()
        {
            await SyncContext.ClearAsync;
            client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true, allowSignal: true, allowQuery: true);

            if (isClosed)
            {
                throw new CadenceQueueClosedException($"[{nameof(WorkflowQueue<T>)}] is closed.");
            }

            var stub   = parentWorkflow.NewLocalActivityStub<IQueueActivities, QueueActivities>();
            var stream = MemoryStreamPool.Alloc();

            try
            {
                var replyBytes = await stub.GetLengthAsync(parentWorkflow.ContextId, queueId);

                stream.Position = 0;
                stream.Write(replyBytes, 0, replyBytes.Length);
                stream.Position = 0;

                var reply = ProxyMessage.Deserialize<WorkflowQueueLengthReply>(stream);

                reply.ThrowOnError();

                return reply.Length;
            }
            finally
            {
                MemoryStreamPool.Free(stream);
            }
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
        public async Task CloseAsync()
        {
            await SyncContext.ClearAsync;
            client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true, allowSignal: true);

            if (isClosed)
            {
                return;
            }

            var stub = parentWorkflow.NewLocalActivityStub < IQueueActivities, QueueActivities>();
            var stream = MemoryStreamPool.Alloc();

            try
            {
                var replyBytes = await stub.CloseAsync(parentWorkflow.ContextId, queueId);

                stream.Position = 0;
                stream.Write(replyBytes, 0, replyBytes.Length);
                stream.Position = 0;

                var reply = ProxyMessage.Deserialize<WorkflowQueueCloseReply>(stream);

                reply.ThrowOnError();
            }
            finally
            {
                MemoryStreamPool.Free(stream);
            }

            isClosed = true;
        }
    }
}

// These types are internal but need to be public for stub generation but we don't 
// want them in the public Neon.Cadence namespace.

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// <b>INTERNAL USE ONLY:</b> Queue operations need to be executed as a 
    /// local activity so that they can be replayed from history when a workflow
    /// needs to be rehydrated.  This defines that local activity interface.
    /// </summary>
    [ActivityInterface]
    public interface IQueueActivities : IActivity
    {
        /// <summary>
        /// Writes an item to the queue.
        /// </summary>
        /// <param name="contextId">The workflow context ID.</param>
        /// <param name="queueId">The queue ID.</param>
        /// <param name="encodedItem">The encoded data bytes.</param>
        /// <returns>The low-level operation <see cref="WorkflowQueueWriteReply"/> encoded as bytes.</returns>
        [ActivityMethod(Name = "enqueue")]
        Task<byte[]> EnqueueAsync(long contextId, long queueId, byte[] encodedItem);

        /// <summary>
        /// Attempts to write an item to the queue but doesn't block for
        /// queues that are already filled to capacity.
        /// </summary>
        /// <param name="contextId">The workflow context ID.</param>
        /// <param name="queueId">The queue ID.</param>
        /// <param name="encodedItem">The encoded data bytes.</param>
        /// <returns>The low-level operation <see cref="WorkflowQueueWriteReply"/> encoded as bytes.</returns>
        [ActivityMethod(Name = "try-enqueue")]
        Task<byte[]> TryEnqueueAsync(long contextId, long queueId, byte[] encodedItem);

        /// <summary>
        /// Reads an item from the queue.
        /// </summary>
        /// <param name="contextId">The workflow context ID.</param>
        /// <param name="queueId">The queue ID.</param>
        /// <param name="timeout">The maximum time to wait or <see cref="TimeSpan.Zero"/> to wait indefinitely.</param>
        /// <returns>The low-level operation <see cref="WorkflowQueueReadReply"/> encoded as bytes.</returns>
        [ActivityMethod(Name = "dequeue")]
        Task<byte[]> DequeueAsync(long contextId, long queueId, TimeSpan timeout);

        /// <summary>
        /// Returns the current number of items in the queue.
        /// </summary>
        /// <param name="contextId">The workflow context ID.</param>
        /// <param name="queueId">The queue ID.</param>
        /// <returns>The low-level operation <see cref="WorkflowQueueLengthReply"/> encoded as bytes.</returns>
        [ActivityMethod(Name = "get-length")]
        Task<byte[]> GetLengthAsync(long contextId, long queueId);

        /// <summary>
        /// Closes the queue.
        /// </summary>
        /// <param name="contextId">The workflow context ID.</param>
        /// <param name="queueId">The queue ID.</param>
        /// <returns>The low-level operation <see cref="WorkflowQueueCloseReply"/> encoded as bytes.</returns>
        [ActivityMethod(Name = "close")]
        Task<byte[]> CloseAsync(long contextId, long queueId);
    }

    /// <inheritdoc/>
    public class QueueActivities : ActivityBase, IQueueActivities
    {
        /// <inheritdoc/>
        public async Task<byte[]> EnqueueAsync(long contextId, long queueId, byte[] encodedItem)
        {
            var reply = (WorkflowQueueWriteReply)await Activity.Client.CallProxyAsync(
                new WorkflowQueueWriteRequest()
                {
                    ContextId = contextId,
                    QueueId   = queueId,
                    Data      = encodedItem
                });

            return reply.SerializeAsBytes();
        }

        /// <inheritdoc/>
        public async Task<byte[]> TryEnqueueAsync(long contextId, long queueId, byte[] encodedItem)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public async Task<byte[]> DequeueAsync(long contextId, long queueId, TimeSpan timeout)
        {
            var reply = (WorkflowQueueReadReply)await Activity.Client.CallProxyAsync(
                new WorkflowQueueReadRequest()
                {
                    ContextId = contextId,
                    QueueId   = queueId,
                    Timeout   = timeout
                });

            return reply.SerializeAsBytes();
        }

        /// <inheritdoc/>
        public async Task<byte[]> GetLengthAsync(long contextId, long queueId)
        {
            var reply =  (WorkflowQueueLengthReply)await Activity.Client.CallProxyAsync(
                new WorkflowQueueLengthRequest()
                {
                    ContextId = contextId,
                    QueueId   = queueId,
                });

            return reply.SerializeAsBytes();
        }

        /// <inheritdoc/>
        public async Task<byte[]> CloseAsync(long contextId, long queueId)
        {
            var reply = (WorkflowQueueCloseReply)await Activity.Client.CallProxyAsync(
                new WorkflowQueueCloseRequest()
                {
                    ContextId = contextId,
                    QueueId   = queueId,
                });

            return reply.SerializeAsBytes();
        }
    }
}
