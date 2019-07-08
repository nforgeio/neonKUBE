//-----------------------------------------------------------------------------
// FILE:	    ActivityInvokeLocalRequest.cs
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
    /// <b>proxy --> client:</b> Sent to a worker, instructing it to begin executing
    /// a local workflow activity.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.ActivityInvokeLocalRequest)]
    internal class ActivityInvokeLocalRequest : ActivityRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ActivityInvokeLocalRequest()
        {
            Type = InternalMessageTypes.ActivityInvokeLocalRequest;
        }

        /// <inheritdoc/>
        public override InternalMessageTypes ReplyType => InternalMessageTypes.ActivityInvokeLocalReply;

        /// <summary>
        /// Identifies the activity context.
        /// </summary>
        public long ActivityContextId
        {
            get => GetLongProperty(PropertyNames.ActivityContextId);
            set => SetLongProperty(PropertyNames.ActivityContextId, value);
        }

        /// <summary>
        /// Identifies the .NET type that implements the local activity. 
        /// </summary>
        public long ActivityTypeId
        {
            get => GetLongProperty(PropertyNames.ActivityTypeId);
            set => SetLongProperty(PropertyNames.ActivityTypeId, value);
        }

        /// <summary>
        /// Optionally specifies the activity arguments encoded as a byte array.
        /// </summary>
        public byte[] Args
        {
            get => GetBytesProperty(PropertyNames.Args);
            set => SetBytesProperty(PropertyNames.Args, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new ActivityInvokeLocalRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (ActivityInvokeLocalRequest)target;

            typedTarget.ActivityContextId = this.ActivityContextId;
            typedTarget.ActivityTypeId    = this.ActivityTypeId;
            typedTarget.Args              = this.Args;
        }
    }
}
