//-----------------------------------------------------------------------------
// FILE:	    DequeuedItem.cs
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
using Neon.Time;

namespace Neon.Cadence
{
    /// <summary>
    /// Holds an item read from a <see cref="WorkflowQueue{T}"/>.
    /// </summary>
    /// <typeparam name="T">Specifies the queue item type.</typeparam>
    /// <remarks>
    /// <para>
    /// <see cref="WorkflowQueue{T}.DequeueAsync(TimeSpan)"/> returns 
    /// one of these with the item read from the queue or with 
    /// <see cref="TimedOut"/> or <see cref="IsClosed"/> set to
    /// <c>true</c>, indicating the reason why the read failed.
    /// </para>
    /// </remarks>
    public struct DequeuedItem<T>
    {
        /// <summary>
        /// Constructs an instance to hold the item read.
        /// </summary>
        /// <param name="item">The item</param>
        internal DequeuedItem(T item)
        {
            this.Item     = item;
            this.TimedOut = false;
            this.IsClosed = false;
        }

        /// <summary>
        /// Constructs an instance that indicates that the operation has timed
        /// out or the queue is closed.
        /// </summary>
        /// <param name="timedOut">Indicates that the read timed out.</param>
        /// <param name="isClosed">Indicates that the queue is closed.</param>
        internal DequeuedItem(bool timedOut = false, bool isClosed = false)
        {
            Covenant.Requires<ArgumentException>(timedOut || isClosed);
            Covenant.Requires<ArgumentException>(timedOut != isClosed);

            this.Item     = default;
            this.TimedOut = timedOut;
            this.IsClosed = isClosed;
        }

        /// <summary>
        /// Returns the item read from the queue if both <see cref="TimedOut"/>
        /// and <see cref="IsClosed"/> are <c>false</c>.
        /// </summary>
        public T Item { get; private set; }

        /// <summary>
        /// Indicates that the queue read operation timed out.
        /// </summary>
        public bool TimedOut { get; private set; }

        /// <summary>
        /// Indicates that the queue read failed because the queue is closed.
        /// </summary>
        public bool IsClosed { get; private set; }

        /// <summary>
        /// Throws a <see cref="TimeoutException"/> if <see cref="TimedOut"/> is
        /// <c>true</c> or an <see cref="InvalidOperationException"/> is <see cref="IsClosed"/>
        /// is <c>true</c>.
        /// </summary>
        public void ThrowOnError()
        {
            if (TimedOut)
            {
                throw new TimeoutException($"[{nameof(WorkflowQueue<T>)}] dequeue operation timed out.");
            }

            if (IsClosed)
            {
                throw new InvalidOperationException($"[{nameof(WorkflowQueue<T>)}] dequeue operation failed because the queue is closed.");
            }
        }
    }
}
