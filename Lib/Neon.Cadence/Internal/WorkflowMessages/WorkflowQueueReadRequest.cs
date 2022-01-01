//-----------------------------------------------------------------------------
// FILE:	    WorkflowQueueReadRequest.cs
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

using System;
using System.Collections.Generic;
using System.ComponentModel;

using Neon.Cadence;
using Neon.Common;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// <b>proxy --> client:</b> Reads data from a workflow queue.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.WorkflowQueueReadRequest)]
    internal class WorkflowQueueReadRequest : WorkflowRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public WorkflowQueueReadRequest()
        {
            Type = InternalMessageTypes.WorkflowQueueReadRequest;
        }

        /// <inheritdoc/>
        public override InternalMessageTypes ReplyType => InternalMessageTypes.WorkflowQueueReadReply;

        /// <summary>
        /// Identifies the queue.
        /// </summary>
        public long QueueId
        {
            get => GetLongProperty(PropertyNames.QueueId);
            set => SetLongProperty(PropertyNames.QueueId, value);
        }

        /// <summary>
        /// The maximum time to wait for a data item or <see cref="TimeSpan.Zero"/> to 
        /// wait indefinitiely.
        /// </summary>
        public TimeSpan Timeout
        {
            get => GetTimeSpanProperty(PropertyNames.Timeout);
            set => SetTimeSpanProperty(PropertyNames.Timeout, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new WorkflowQueueReadRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (WorkflowQueueReadRequest)target;

            typedTarget.QueueId = this.QueueId;
            typedTarget.Timeout = this.Timeout;
        }
    }
}
