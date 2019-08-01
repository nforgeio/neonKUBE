//-----------------------------------------------------------------------------
// FILE:	    WorkflowSleepRequest.cs
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
    [InternalProxyMessage(InternalMessageTypes.WorkflowSleepRequest)]
    internal class WorkflowSleepRequest : WorkflowRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public WorkflowSleepRequest()
        {
            Type = InternalMessageTypes.WorkflowSleepRequest;
        }

        /// <inheritdoc/>
        public override InternalMessageTypes ReplyType => InternalMessageTypes.WorkflowSleepReply;

        /// <summary>
        /// Specifies the time to sleep.
        /// </summary>
        public TimeSpan Duration
        {
            get => GetTimeSpanProperty(PropertyNames.Duration);
            set => SetTimeSpanProperty(PropertyNames.Duration, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new WorkflowSleepRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (WorkflowSleepRequest)target;

            typedTarget.Duration = this.Duration;
        }
    }
}
