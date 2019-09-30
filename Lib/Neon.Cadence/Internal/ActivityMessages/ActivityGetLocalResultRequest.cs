//-----------------------------------------------------------------------------
// FILE:	    ActivityGetLocalResultRequest.cs
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
    /// <b>client --> proxy:</b> Requests the results from a <see cref="ActivityStartLocalRequest"/>.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.ActivityGetLocalResultRequest)]
    internal class ActivityGetLocalResultRequest : WorkflowRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ActivityGetLocalResultRequest()
        {
            Type = InternalMessageTypes.ActivityGetLocalResultRequest;
        }

        /// <inheritdoc/>
        public override InternalMessageTypes ReplyType => InternalMessageTypes.ActivityGetLocalResultReply;

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
            var clone = new ActivityGetLocalResultRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (ActivityGetLocalResultRequest)target;

            typedTarget.ActivityId = this.ActivityId;
        }
    }
}
