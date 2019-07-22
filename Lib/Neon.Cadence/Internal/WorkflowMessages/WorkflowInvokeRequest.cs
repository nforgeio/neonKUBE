//-----------------------------------------------------------------------------
// FILE:	    WorkflowInvokeRequest.cs
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
    /// <b>proxy --> client:</b> Invokes a workflow instance.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.WorkflowInvokeRequest)]
    internal class WorkflowInvokeRequest : WorkflowRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public WorkflowInvokeRequest()
        {
            Type = InternalMessageTypes.WorkflowInvokeRequest;
        }

        /// <inheritdoc/>
        public override InternalMessageTypes ReplyType => InternalMessageTypes.WorkflowInvokeReply;

        /// <summary>
        /// Identifies the workflow implementation to be started.
        /// </summary>
        public string Name
        {
            get => GetStringProperty(PropertyNames.Name);
            set => SetStringProperty(PropertyNames.Name, value);
        }

        /// <summary>
        /// The workflow arguments encoded into a byte array (or <c>null</c>).
        /// </summary>
        public byte[] Args
        {
            get => GetBytesProperty(PropertyNames.Args);
            set => SetBytesProperty(PropertyNames.Args, value);
        }

        /// <summary>
        /// The domain hosting the workflow.
        /// </summary>
        public string Domain
        {
            get => GetStringProperty(PropertyNames.Domain);
            set => SetStringProperty(PropertyNames.Domain, value);
        }

        /// <summary>
        /// The original workflow ID.
        /// </summary>
        public string WorkflowId
        {
            get => GetStringProperty(PropertyNames.WorkflowId);
            set => SetStringProperty(PropertyNames.WorkflowId, value);
        }

        /// <summary>
        /// The workflow run ID.
        /// </summary>
        public string RunId
        {
            get => GetStringProperty(PropertyNames.RunId);
            set => SetStringProperty(PropertyNames.RunId, value);
        }

        /// <summary>
        /// The workflow type.
        /// </summary>
        public string WorkflowType
        {
            get => GetStringProperty(PropertyNames.WorkflowType);
            set => SetStringProperty(PropertyNames.WorkflowType, value);
        }

        /// <summary>
        /// The task list where the workflow is executing.
        /// </summary>
        public string TaskList
        {
            get => GetStringProperty(PropertyNames.TaskList);
            set => SetStringProperty(PropertyNames.TaskList, value);
        }

        /// <summary>
        /// The maximum duration the workflow is allowed to run.
        /// </summary>
        public TimeSpan ExecutionStartToCloseTimeout
        {
            get => GetTimeSpanProperty(PropertyNames.ExecutionStartToCloseTimeout);
            set => SetTimeSpanProperty(PropertyNames.ExecutionStartToCloseTimeout, value);
        }

        /// <summary>
        /// Indicates the current workflow replay state.
        /// </summary>
        public InternalReplayStatus ReplayStatus
        {
            get => GetEnumProperty<InternalReplayStatus>(PropertyNames.ReplayStatus);
            set => SetEnumProperty<InternalReplayStatus>(PropertyNames.ReplayStatus, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new WorkflowInvokeRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (WorkflowInvokeRequest)target;

            typedTarget.Name                         = this.Name;
            typedTarget.Args                         = this.Args;
            typedTarget.Domain                       = this.Domain;
            typedTarget.WorkflowId                   = this.WorkflowId;
            typedTarget.RunId                        = this.RunId;
            typedTarget.WorkflowType                 = this.WorkflowType;
            typedTarget.TaskList                     = this.TaskList;
            typedTarget.ExecutionStartToCloseTimeout = this.ExecutionStartToCloseTimeout;
            typedTarget.ReplayStatus                 = this.ReplayStatus;
        }
    }
}
