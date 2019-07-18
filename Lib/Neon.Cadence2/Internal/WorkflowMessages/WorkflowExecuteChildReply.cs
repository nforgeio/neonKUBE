//-----------------------------------------------------------------------------
// FILE:	    WorkflowExecuteChildReply.cs
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

using Neon.Cadence;
using Neon.Common;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// <b>proxy --> client:</b> Answers a <see cref="WorkflowExecuteChildReply"/>.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.WorkflowExecuteChildReply)]
    internal class WorkflowExecuteChildReply : WorkflowReply
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public WorkflowExecuteChildReply()
        {
            Type = InternalMessageTypes.WorkflowExecuteChildReply;
        }

        /// <summary>
        /// The child workflow identifier.  This will be used in other messages such 
        /// as <see cref="WorkflowSignalChildRequest"/>, <see cref="WorkflowWaitForChildRequest"/>,
        /// and <see cref="WorkflowCancelChildRequest"/> to identify the target child workflow.
        /// </summary>
        public long ChildId
        {
            get => GetLongProperty(PropertyNames.ChildId);
            set => SetLongProperty(PropertyNames.ChildId, value);
        }

        /// <summary>
        /// Returns details identifying the child workflow execution.
        /// </summary>
        public InternalWorkflowExecution Execution
        {
            get => GetJsonProperty<InternalWorkflowExecution>(PropertyNames.Execution);
            set => SetJsonProperty<InternalWorkflowExecution>(PropertyNames.Execution, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new WorkflowExecuteChildReply();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (WorkflowExecuteChildReply)target;

            typedTarget.ChildId     = this.ChildId;
            typedTarget.Execution   = this.Execution;
        }
    }
}
