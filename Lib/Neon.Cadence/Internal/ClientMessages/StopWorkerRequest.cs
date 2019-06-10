//-----------------------------------------------------------------------------
// FILE:	    StopWorkerRequest.cs
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
    /// <b>client --> proxy:</b> Stops a Cadence worker.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.StopWorkerRequest)]
    internal class StopWorkerRequest : ProxyRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public StopWorkerRequest()
        {
            Type = InternalMessageTypes.StopWorkerRequest;
        }

        /// <inheritdoc/>
        public override InternalMessageTypes ReplyType => InternalMessageTypes.StopWorkerReply;

        /// <summary>
        /// Identifies the worker being stopped.
        /// </summary>
        public long WorkerId
        {
            get => GetLongProperty(PropertyNames.WorkerId);
            set => SetLongProperty(PropertyNames.WorkerId, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new StopWorkerRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (StopWorkerRequest)target;

            typedTarget.WorkerId = this.WorkerId;
        }
    }
}
