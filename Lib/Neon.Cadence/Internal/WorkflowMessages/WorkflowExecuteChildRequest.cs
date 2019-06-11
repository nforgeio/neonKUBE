//-----------------------------------------------------------------------------
// FILE:	    WorkflowExecuteChildRequest.cs
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
    /// <b>client --> proxy:</b> Begins execution of a child workflow returning the
    /// new workflow IDs.  A subsequent <see cref="WorkflowWaitForChildRequest"/> message
    /// will be sent to wait for the workflow to actually finish.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.WorkflowExecuteChildRequest)]
    internal class WorkflowExecuteChildRequest : WorkflowRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public WorkflowExecuteChildRequest()
        {
            Type = InternalMessageTypes.WorkflowExecuteChildRequest;
        }

        /// <inheritdoc/>
        public override InternalMessageTypes ReplyType => InternalMessageTypes.WorkflowExecuteChildReply;

        /// <summary>
        /// Specifies the child workflow to be executed.
        /// </summary>
        public string Workflow
        {
            get => GetStringProperty(PropertyNames.Workflow);
            set => SetStringProperty(PropertyNames.Workflow, value);
        }

        /// <summary>
        /// Specifies the child workflow arguments.
        /// </summary>
        public byte[] Args
        {
            get => GetBytesProperty(PropertyNames.Args);
            set => SetBytesProperty(PropertyNames.Args, value);
        }

        /// <summary>
        /// Specifies the child workflow options.
        /// </summary>
        public InternalChildWorkflowOptions Options
        {
            get => GetJsonProperty<InternalChildWorkflowOptions>(PropertyNames.Options);
            set => SetJsonProperty<InternalChildWorkflowOptions>(PropertyNames.Options, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new WorkflowExecuteChildRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (WorkflowExecuteChildRequest)target;

            typedTarget.Workflow    = this.Workflow;
            typedTarget.Args        = this.Args;
            typedTarget.Options     = this.Options;
        }
    }
}
