//-----------------------------------------------------------------------------
// FILE:	    WorkflowQueueNewRequest.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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

using Neon.Common;
using Neon.Temporal;

namespace Neon.Temporal.Internal
{
    /// <summary>
    /// <b>proxy --> client:</b> Creates a new workflow queue.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.WorkflowQueueNewRequest)]
    internal class WorkflowQueueNewRequest : WorkflowRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public WorkflowQueueNewRequest()
        {
            Type = InternalMessageTypes.WorkflowQueueNewRequest;
        }

        /// <inheritdoc/>
        public override InternalMessageTypes ReplyType => InternalMessageTypes.WorkflowQueueNewReply;

        /// <summary>
        /// Identifies the queue.
        /// </summary>
        public long QueueId
        {
            get => GetLongProperty(PropertyNames.QueueId);
            set => SetLongProperty(PropertyNames.QueueId, value);
        }

        /// <summary>
        /// Specifies the capacity of the queue.
        /// </summary>
        public int Capacity
        {
            get => GetIntProperty(PropertyNames.Capacity);
            set => SetIntProperty(PropertyNames.Capacity, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new WorkflowQueueNewRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (WorkflowQueueNewRequest)target;

            typedTarget.QueueId  = this.QueueId;
            typedTarget.Capacity = this.Capacity;
        }
    }
}
