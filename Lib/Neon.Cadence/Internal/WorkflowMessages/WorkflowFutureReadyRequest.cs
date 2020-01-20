//-----------------------------------------------------------------------------
// FILE:	    WorkflowFutureReadyRequest.cs
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
    /// <b>proxy --> client:</b> Sent for workflow operations that are implemented in GOLANG as futures 
    /// and may be executed in parallel.  <b>cadence-proxy</b> will send this message after it has 
    /// submitted the operation to Cadence but before the future actually completes.  The .NET client 
    /// uses this as an indication that another Cadence operation may be started.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.WorkflowFutureReadyRequest)]
    internal class WorkflowFutureReadyRequest : WorkflowRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public WorkflowFutureReadyRequest()
        {
            Type = InternalMessageTypes.WorkflowFutureReadyRequest;
        }

        /// <inheritdoc/>
        public override InternalMessageTypes ReplyType => InternalMessageTypes.WorkflowFutureReadyReply;

        /// <summary>
        /// The ID of the original operation what has been submitted to Cadence
        /// and who's future has been returned.
        /// </summary>
        public long FutureOperationId
        {
            get => GetLongProperty(PropertyNames.FutureOperationId);
            set => SetLongProperty(PropertyNames.FutureOperationId, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new WorkflowFutureReadyRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (WorkflowFutureReadyRequest)target;

            typedTarget.FutureOperationId = this.FutureOperationId;
        }
    }
}
