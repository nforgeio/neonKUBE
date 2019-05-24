//-----------------------------------------------------------------------------
// FILE:	    WorkflowSignalWithStartRequest.cs
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
    /// <b>proxy --> client:</b> Sends a signal to a workflow, starting the
    /// workflow if it doesn't exist.
    /// </summary>
    [ProxyMessage(InternalMessageTypes.WorkflowSignalWithStartRequest)]
    internal class WorkflowSignalWithStartRequest : WorkflowRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public WorkflowSignalWithStartRequest()
        {
            Type = InternalMessageTypes.WorkflowSignalWithStartRequest;
        }

        /// <inheritdoc/>
        public override InternalMessageTypes ReplyType => InternalMessageTypes.WorkflowSignalWithStartReply;

        /// <summary>
        /// Identifies the workflow to be executed if the workflow instance
        /// idntified by <see cref="WorkflowId"/> is nor currently running.
        /// </summary>
        public string Workflow
        {
            get => GetStringProperty("Workflow");
            set => SetStringProperty("Workflow", value);
        }

        /// <summary>
        /// Identifies the workflow by ID.
        /// </summary>
        public string WorkflowId
        {
            get => GetStringProperty("WorkflowId");
            set => SetStringProperty("WorkflowId", value);
        }

        /// <summary>
        /// Identifies the signal.
        /// </summary>
        public string SignalName
        {
            get => GetStringProperty("SignalName");
            set => SetStringProperty("SignalName", value);
        }

        /// <summary>
        /// Optionally specifies the signal arguments.
        /// </summary>
        public byte[] SignalArgs
        {
            get => GetBytesProperty("SignalArgs");
            set => SetBytesProperty("SignalArgs", value);
        }

        /// <summary>
        /// Optionally specifies the workflow start options.
        /// </summary>
        public InternalStartWorkflowOptions Options
        {
            get => GetJsonProperty<InternalStartWorkflowOptions>("Options");
            set => SetJsonProperty<InternalStartWorkflowOptions>("Options", value);
        }

        /// <summary>
        /// Optionally specifies the workflow arguments.
        /// </summary>
        public byte[] WorkflowArgs
        {
            get => GetBytesProperty("WorkflowArgs");
            set => SetBytesProperty("WorkflowArgs", value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new WorkflowSignalWithStartRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (WorkflowSignalWithStartRequest)target;

            typedTarget.Workflow = this.Workflow;
            typedTarget.WorkflowId   = this.WorkflowId;
            typedTarget.SignalName   = this.SignalName;
            typedTarget.SignalArgs   = this.SignalArgs;
            typedTarget.Options      = this.Options;
            typedTarget.WorkflowArgs = this.WorkflowArgs;
        }
    }
}
