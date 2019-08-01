//-----------------------------------------------------------------------------
// FILE:	    WorkflowSignalInvokeRequest.cs
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
    /// <b>proxy --> client:</b> Sends a received signal to a running workflow.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.WorkflowSignalInvokeRequest)]
    internal class WorkflowSignalInvokeRequest : WorkflowRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public WorkflowSignalInvokeRequest()
        {
            Type = InternalMessageTypes.WorkflowSignalInvokeRequest;
        }

        /// <inheritdoc/>
        public override InternalMessageTypes ReplyType => InternalMessageTypes.WorkflowSignalInvokeReply;

        /// <summary>
        /// Identifies the signal.
        /// </summary>
        public string SignalName
        {
            get => GetStringProperty(PropertyNames.SignalName);
            set => SetStringProperty(PropertyNames.SignalName, value);
        }

        /// <summary>
        /// Optionally specifies the signal arguments.
        /// </summary>
        public byte[] SignalArgs
        {
            get => GetBytesProperty(PropertyNames.SignalArgs);
            set => SetBytesProperty(PropertyNames.SignalArgs, value);
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
            var clone = new WorkflowSignalInvokeRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (WorkflowSignalInvokeRequest)target;

            typedTarget.SignalName   = this.SignalName;
            typedTarget.SignalArgs   = this.SignalArgs;
            typedTarget.ReplayStatus = this.ReplayStatus;
        }
    }
}
