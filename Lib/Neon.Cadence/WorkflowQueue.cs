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
    /// the maximum capacity of the queue.  This defaults to 2 and cannot be
    /// less that 2 items.
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
    /// an optional timeout.  This returns a <see cref="DequeuedItem{T}"/> which
    /// will hold the item read on success or indicate that the operation timed
    /// out or the queue is closed.
    /// </para>
    /// <para>
    /// <see cref="GetLengthAsync"/> returns the number of items currently residing
    /// in the queue and <see cref="CloseAsync"/> closes the queue.
    /// </para>
    /// </remarks>
    public class WorkflowQueue<T>
    {
        private Workflow        parentWorkflow;
        private CadenceClient   client;
        private long            queueId;
        private bool            isClosed;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="parentWorkflow">The parent workflow.</param>
        /// <param name="queueId">The queue ID.</param>
        internal WorkflowQueue(Workflow parentWorkflow, long queueId)
        {
            Covenant.Requires<ArgumentNullException>(parentWorkflow != null, nameof(parentWorkflow));
            Covenant.Requires<ArgumentException>(queueId > 0, nameof(queueId));

            this.parentWorkflow = parentWorkflow;
            this.client         = parentWorkflow.Client;
            this.queueId        = queueId;
            this.isClosed       = false;
        }

        /// <summary>
        /// Adds an item to the queue.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the queue is closed.</exception>
        /// <exception cref="NotSupportedException">Thrown if the serialized size of <paramref name="item"/> is not less than 64KiB.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the associated workflow client is disposed.</exception>
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
            await SyncContext.ResetAsync;
            client.EnsureNotDisposed();

            if (isClosed)
            {
                throw new InvalidOperationException($"[{nameof(WorkflowQueue<T>)}] is closed.");
            }

            var bytes = client.DataConverter.ToData(item);

            if (bytes.Length >= 64 * ByteUnits.KibiBytes)
            {
                throw new NotSupportedException($"Serialized items enqueued to a [{nameof(WorkflowQueue<T>)}] must be less than 64 KiB.");
            }

            var reply = await parentWorkflow.ExecuteNonParallel(
                async () =>
                {
                    return (WorkflowQueueWriteReply)await client.CallProxyAsync(
                        new WorkflowQueueWriteRequest()
                        {
                            ContextId = parentWorkflow.ContextId,
                            QueueId   = queueId,
                            Data      = bytes
                        });
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Attempts to dequeue an item from the queue with an optional timeout.
        /// </summary>
        /// <param name="timeout">The optional timeout.</param>
        /// <returns>A <see cref="DequeuedItem{T}"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated workflow client is disposed.</exception>
        /// <remarks>
        /// <para>
        /// By default, this method will wait until an item can be read from the queue
        /// or the queue is closed.  You may specify a timeout and when this is greater
        /// than <see cref="TimeSpan.Zero"/>, the method will return after that time if
        /// no item is waiting.
        /// </para>
        /// <para>
        /// The <see cref="DequeuedItem{T}"/> return holds the item if one was read or
        /// indicates whether the operation timed out or the queue is closed.
        /// </para>
        /// </remarks>
        public async Task<DequeuedItem<T>> DequeueAsync(TimeSpan timeout = default)
        {
            await SyncContext.ResetAsync;
            client.EnsureNotDisposed();

            if (isClosed)
            {
                return new DequeuedItem<T>(isClosed: true);
            }

            var reply = await parentWorkflow.ExecuteNonParallel(
                async () =>
                {
                    return (WorkflowQueueReadReply)await client.CallProxyAsync(
                        new WorkflowQueueReadRequest()
                        {
                            ContextId = parentWorkflow.ContextId,
                            QueueId   = queueId,
                        });
                });

            reply.ThrowOnError();

            if (reply.IsClosed)
            {
                return new DequeuedItem<T>(isClosed: true);
            }
            else if (reply.Data == null)
            {
                return new DequeuedItem<T>(timedOut: true);
            }

            return new DequeuedItem<T>(client.DataConverter.FromData<T>(reply.Data));
        }

        /// <summary>
        /// Returns the number of items currently waiting in the queue.
        /// </summary>
        /// <returns>The item count.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the queue is closed.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the associated workflow client is disposed.</exception>
        public async Task<int> GetLengthAsync()
        {
            await SyncContext.ResetAsync;
            client.EnsureNotDisposed();

            if (isClosed)
            {
                throw new InvalidOperationException($"[{nameof(WorkflowQueue<T>)}] is closed.");
            }

            var reply = await parentWorkflow.ExecuteNonParallel(
                async () =>
                {
                    return (WorkflowQueueLengthReply)await client.CallProxyAsync(
                        new WorkflowQueueLengthRequest()
                        {
                            ContextId = parentWorkflow.ContextId,
                            QueueId   = queueId,
                        });
                });

            reply.ThrowOnError();

            return reply.Length;
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
            await SyncContext.ResetAsync;
            client.EnsureNotDisposed();

            if (isClosed)
            {
                return;
            }

            var reply = await parentWorkflow.ExecuteNonParallel(
                async () =>
                {
                    return (WorkflowQueueCloseReply)await client.CallProxyAsync(
                        new WorkflowQueueCloseRequest()
                        {
                            ContextId = parentWorkflow.ContextId,
                            QueueId   = queueId,
                        });
                });

            reply.ThrowOnError();

            isClosed = true;
        }
    }
}
