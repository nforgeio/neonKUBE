//-----------------------------------------------------------------------------
// FILE:	    CancelRequest.cs
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
    /// <b>client --> proxy:</b> Sent periodically to confirm that the proxy is
    /// still healthy.  The proxy should send a <see cref="CancelReply"/>
    /// optionally indicating that there's a problem by specifying an error.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.CancelRequest)]
    internal class CancelRequest : ProxyRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public CancelRequest()
        {
            Type = InternalMessageTypes.CancelRequest;
        }

        /// <inheritdoc/>
        public override InternalMessageTypes ReplyType => InternalMessageTypes.CancelReply;

        /// <summary>
        /// The ID of the request being cancelled.
        /// </summary>
        public long TargetRequestId
        {
            get => GetLongProperty(PropertyNames.TargetRequestId);
            set => SetLongProperty(PropertyNames.TargetRequestId, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new CancelRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (CancelRequest)target;

            typedTarget.TargetRequestId = this.TargetRequestId;
        }
    }
}
