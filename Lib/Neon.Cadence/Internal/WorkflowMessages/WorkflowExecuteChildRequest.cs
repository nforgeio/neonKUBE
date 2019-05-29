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
    /// <b>client --> proxy:</b> Commands the workflow to sleep for a period of time.
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
        /// Specifies the child workflow arguments.
        /// </summary>
        public byte[] Args
        {
            get => GetBytesProperty("Args");
            set => SetBytesProperty("Args", value);
        }

        /// <summary>
        /// Specifies the child workflow options.
        /// </summary>
        public InternalChildWorkflowOptions Options
        {
            get => GetJsonProperty<InternalChildWorkflowOptions>("Options");
            set => SetJsonProperty<InternalChildWorkflowOptions>("Options", value);
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

            typedTarget.Args    = this.Args;
            typedTarget.Options = this.Options;
        }
    }
}
