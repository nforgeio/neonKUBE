//-----------------------------------------------------------------------------
// FILE:	    WorkflowFutureReadyReply.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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

using Neon.Common;
using Neon.Temporal;

namespace Neon.Temporal.Internal
{
    /// <summary>
    /// <para>
    /// <b>proxy --> client:</b> This is a special reply message sent for workflow operations that
    /// are implemented in GOLANG as futures and may be executed in parallel.  <b>temporal-proxy</b>
    /// will send this message after it has submitted the operation to Temporal but before the future
    /// actually completes.  The .NET client uses this reply as an indication that another Temporal
    /// operation may be started.
    /// </para>
    /// <note>
    /// This message does not have a corresponding request message (which is why the name doesn't end with "Reply".
    /// </note>
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.WorkflowFutureReadyReply)]
    internal class WorkflowFutureReadyReply : WorkflowReply
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public WorkflowFutureReadyReply()
        {
            Type = InternalMessageTypes.WorkflowFutureReadyReply;
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new WorkflowFutureReadyReply();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);
        }
    }
}
