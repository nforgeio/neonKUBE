//-----------------------------------------------------------------------------
// FILE:	    WorkflowExecuteRequest.cs
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
    /// <b>proxy --> client:</b> Starts a workflow execution.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.WorkflowExecuteRequest)]
    internal class WorkflowExecuteRequest : WorkflowRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public WorkflowExecuteRequest()
        {
            Type = InternalMessageTypes.WorkflowExecuteRequest;
        }

        /// <inheritdoc/>
        public override InternalMessageTypes ReplyType => InternalMessageTypes.WorkflowExecuteReply;

        /// <summary>
        /// Identifies the Cadence domain hosting the workflow.
        /// </summary>
        public string Domain
        {
            get => GetStringProperty(PropertyNames.Domain);
            set => SetStringProperty(PropertyNames.Domain, value);
        }

        /// <summary>
        /// Identifies the workflow implementation to be started.
        /// </summary>
        public string Workflow
        {
            get => GetStringProperty(PropertyNames.Workflow);
            set => SetStringProperty(PropertyNames.Workflow, value);
        }

        /// <summary>
        /// Optionally specifies the workflow arguments encoded as a byte array.
        /// </summary>
        public byte[] Args
        {
            get => GetBytesProperty(PropertyNames.Args);
            set => SetBytesProperty(PropertyNames.Args, value);
        }

        /// <summary>
        /// Optionally specifies the workflow start options.
        /// </summary>
        public InternalStartWorkflowOptions Options
        {
            get => GetJsonProperty<InternalStartWorkflowOptions>(PropertyNames.Options);
            set => SetJsonProperty<InternalStartWorkflowOptions>(PropertyNames.Options, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new WorkflowExecuteRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (WorkflowExecuteRequest)target;

            typedTarget.Args     = this.Args;
            typedTarget.Domain   = this.Domain;
            typedTarget.Workflow = this.Workflow;
            typedTarget.Options  = this.Options;
        }
    }
}
