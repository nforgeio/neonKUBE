//-----------------------------------------------------------------------------
// FILE:	    WorkflowWaitForChildRequest.cs
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
    /// <b>client --> proxy:</b> Waits for a child workflow to complete.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.WorkflowWaitForChildRequest)]
    internal class WorkflowWaitForChildRequest : WorkflowRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public WorkflowWaitForChildRequest()
        {
            Type = InternalMessageTypes.WorkflowWaitForChildRequest;
        }

        /// <inheritdoc/>
        public override InternalMessageTypes ReplyType => InternalMessageTypes.WorkflowWaitForChildReply;

        /// <summary>
        /// Identifies the child workflow.
        /// </summary>
        public long ChildId
        {
            get => GetLongProperty(PropertyNames.ChildId);
            set => SetLongProperty(PropertyNames.ChildId, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new WorkflowWaitForChildRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (WorkflowWaitForChildRequest)target;

            typedTarget.ChildId = this.ChildId;
        }
    }
}
