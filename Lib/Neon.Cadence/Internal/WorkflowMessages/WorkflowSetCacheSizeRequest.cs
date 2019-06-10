//-----------------------------------------------------------------------------
// FILE:	    WorkflowSetCacheSizeRequest.cs
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
    /// <b>client --> proxy:</b> Sets the maximum number of bytes the client will use
    /// to cache the history of a sticky workflow on a workflow worker as a performance
    /// optimization.  When this is exceeded for a workflow, its full history will
    /// need to be retrieved from the Cadence cluster the next time the workflow
    /// instance is assigned to a worker. 
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.WorkflowSetCacheSizeRequest)]
    internal class WorkflowSetCacheSizeRequest : WorkflowRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public WorkflowSetCacheSizeRequest()
        {
            Type = InternalMessageTypes.WorkflowSetCacheSizeRequest;
        }

        /// <inheritdoc/>
        public override InternalMessageTypes ReplyType => InternalMessageTypes.WorkflowSetCacheSizeReply;

        /// <summary>
        /// Specifies the maximum number of bytes used for caching sticky workflows.
        /// </summary>
        public int Size
        {
            get => GetIntProperty(PropertyNames.Size);
            set => SetIntProperty(PropertyNames.Size, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new WorkflowSetCacheSizeRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (WorkflowSetCacheSizeRequest)target;

            typedTarget.Size = this.Size;
        }
    }
}
