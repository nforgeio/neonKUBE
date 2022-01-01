//-----------------------------------------------------------------------------
// FILE:	    WorkflowEnqueueReply.cs
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
    /// <b>proxy --> client:</b> Answers a <see cref="WorkflowQueueWriteRequest"/>
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.WorkflowQueueWriteReply)]
    internal class WorkflowQueueWriteReply : WorkflowReply
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public WorkflowQueueWriteReply()
        {
            Type = InternalMessageTypes.WorkflowQueueWriteReply;
        }

        /// <summary>
        /// Indicates when the queue is full and the item could not be written.
        /// </summary>
        public bool IsFull
        {
            get => GetBoolProperty(PropertyNames.IsFull);
            set => SetBoolProperty(PropertyNames.IsFull, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new WorkflowQueueWriteReply();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (WorkflowQueueWriteReply)target;

            typedTarget.IsFull = this.IsFull;
        }
    }
}
