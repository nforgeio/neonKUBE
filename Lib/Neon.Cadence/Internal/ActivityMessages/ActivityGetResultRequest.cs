//-----------------------------------------------------------------------------
// FILE:	    ActivityGetResultRequest.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
    /// <b>client --> proxy:</b> Requests the results from a <see cref="ActivityStartRequest"/>.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.ActivityGetResultRequest)]
    internal class ActivityGetResultRequest : WorkflowRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ActivityGetResultRequest()
        {
            Type = InternalMessageTypes.ActivityGetResultRequest;
        }

        /// <inheritdoc/>
        public override InternalMessageTypes ReplyType => InternalMessageTypes.ActivityGetResultReply;

        /// <summary>
        /// Identifies the target activity.
        /// </summary>
        public long ActivityId
        {
            get => GetLongProperty(PropertyNames.ActivityId);
            set => SetLongProperty(PropertyNames.ActivityId, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new ActivityGetResultRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (ActivityGetResultRequest)target;

            typedTarget.ActivityId = this.ActivityId;
        }
    }
}
