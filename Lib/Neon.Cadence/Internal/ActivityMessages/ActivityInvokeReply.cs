//-----------------------------------------------------------------------------
// FILE:	    ActivityInvokeReply.cs
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
    /// <b>client --> proxy:</b> Answers a <see cref="ActivityInvokeRequest"/>
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.ActivityInvokeReply)]
    internal class ActivityInvokeReply : WorkflowReply
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ActivityInvokeReply()
        {
            Type = InternalMessageTypes.ActivityInvokeReply;
        }

        /// <summary>
        /// Returns the activity results encoded as a byte array.
        /// </summary>
        public byte[] Result
        {
            get => GetBytesProperty(PropertyNames.Result);
            set => SetBytesProperty(PropertyNames.Result, value);
        }

        /// <summary>
        /// Indicates that the activity will be completed externally.
        /// </summary>
        public bool Pending
        {
            get => GetBoolProperty(PropertyNames.Pending);
            set => SetBoolProperty(PropertyNames.Pending, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new ActivityInvokeReply();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (ActivityInvokeReply)target;

            typedTarget.Result  = this.Result;
            typedTarget.Pending = this.Pending;
        }
    }
}
