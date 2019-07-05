//-----------------------------------------------------------------------------
// FILE:	    ActivityRecordHeartbeatRequest.cs
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
    /// <b>client --> proxy:</b> Records an activity heartbeat.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.ActivityRecordHeartbeatRequest)]
    internal class ActivityRecordHeartbeatRequest : ActivityRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ActivityRecordHeartbeatRequest()
        {
            Type = InternalMessageTypes.ActivityRecordHeartbeatRequest;
        }

        /// <inheritdoc/>
        public override InternalMessageTypes ReplyType => InternalMessageTypes.ActivityRecordHeartbeatReply;

        /// <summary>
        /// Overrides the <see cref="ActivityRequest.ContextId"/> message property when
        /// non-null, indicating that the activity heartbeat is being sent externally.
        /// </summary>
        public byte[] TaskToken
        {
            get => GetBytesProperty(PropertyNames.TaskToken);
            set => SetBytesProperty(PropertyNames.TaskToken, value);
        }

        /// <summary>
        /// The activity heartbeat details encoded as a byte array.
        /// </summary>
        public byte[] Details
        {
            get => GetBytesProperty(PropertyNames.Details);
            set => SetBytesProperty(PropertyNames.Details, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new ActivityRecordHeartbeatRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (ActivityRecordHeartbeatRequest)target;

            typedTarget.TaskToken = this.TaskToken;
            typedTarget.Details   = this.Details;
        }
    }
}
