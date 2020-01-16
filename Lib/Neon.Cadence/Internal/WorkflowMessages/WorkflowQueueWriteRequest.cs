//-----------------------------------------------------------------------------
// FILE:	    WorkflowQueueWriteRequest.cs
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

using Neon.Cadence;
using Neon.Common;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// <b>proxy --> client:</b> Writes data to a workflow queue.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.WorkflowQueueWriteRequest)]
    internal class WorkflowQueueWriteRequest : WorkflowRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public WorkflowQueueWriteRequest()
        {
            Type = InternalMessageTypes.WorkflowQueueWriteRequest;
        }

        /// <inheritdoc/>
        public override InternalMessageTypes ReplyType => InternalMessageTypes.WorkflowQueueWriteReply;

        /// <summary>
        /// Identifies the queue.
        /// </summary>
        public long QueueId
        {
            get => GetLongProperty(PropertyNames.QueueId);
            set => SetLongProperty(PropertyNames.QueueId, value);
        }

        /// <summary>
        /// Indicates whether the write operation should not block when
        /// the queue is full.
        /// </summary>
        public bool NoBlock
        {
            get => GetBoolProperty(PropertyNames.NoBlock);
            set => SetBoolProperty(PropertyNames.NoBlock, value);
        }

        /// <summary>
        /// The data to be written to the queue.
        /// </summary>
        public byte[] Data
        {
            get => GetBytesProperty(PropertyNames.Data);
            set => SetBytesProperty(PropertyNames.Data, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new WorkflowQueueWriteRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (WorkflowQueueWriteRequest)target;

            typedTarget.QueueId = this.QueueId;
            typedTarget.NoBlock = this.NoBlock;
            typedTarget.Data    = this.Data;
        }
    }
}
