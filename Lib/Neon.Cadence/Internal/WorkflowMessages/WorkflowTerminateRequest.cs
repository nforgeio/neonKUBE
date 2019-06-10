//-----------------------------------------------------------------------------
// FILE:	    WorkflowTerminateRequest.cs
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
    /// <b>proxy --> client:</b> Terminates a workflow execution.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.WorkflowTerminateRequest)]
    internal class WorkflowTerminateRequest : WorkflowRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public WorkflowTerminateRequest()
        {
            Type = InternalMessageTypes.WorkflowTerminateRequest;
        }

        /// <inheritdoc/>
        public override InternalMessageTypes ReplyType => InternalMessageTypes.WorkflowTerminateReply;

        /// <summary>
        /// Identifies the workflow by ID.
        /// </summary>
        public string WorkflowId
        {
            get => GetStringProperty(PropertyNames.WorkflowId);
            set => SetStringProperty(PropertyNames.WorkflowId, value);
        }

        /// <summary>
        /// Identifies the specific workflow run to be cancelled.  The latest run
        /// will be cancelled when this is <c>null</c> or empty.
        /// </summary>
        public string RunId
        {
            get => GetStringProperty(PropertyNames.RunId);
            set => SetStringProperty(PropertyNames.RunId, value);
        }

        /// <summary>
        /// Optionally indicates the termination reason.
        /// </summary>
        public string Reason
        {
            get => GetStringProperty(PropertyNames.Reason);
            set => SetStringProperty(PropertyNames.Reason, value);
        }

        /// <summary>
        /// Optionally includes additional termination details encoded as a byte array.
        /// </summary>
        public byte[] Details
        {
            get => GetBytesProperty(PropertyNames.Details);
            set => SetBytesProperty(PropertyNames.Details, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new WorkflowTerminateRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (WorkflowTerminateRequest)target;

            typedTarget.WorkflowId = this.WorkflowId;
            typedTarget.RunId      = this.RunId;
            typedTarget.Reason     = this.Reason;
            typedTarget.Details    = this.Details;
        }
    }
}
