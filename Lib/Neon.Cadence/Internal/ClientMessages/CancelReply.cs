//-----------------------------------------------------------------------------
// FILE:	    CancelReply.cs
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
    /// <b>proxy --> client:</b> Sent in response to a <see cref="CancelRequest"/>
    /// indicating that the operation was canceled, has already completed or doesn't
    /// exist.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.CancelReply)]
    internal class CancelReply : ProxyReply
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public CancelReply()
        {
            Type = InternalMessageTypes.CancelReply;
        }

        /// <summary>
        /// Set to <c>true</c> if the operation was actually cancelled or <c>false</c>
        /// if the operation had already completed, doesn't exist, or if cancellation
        /// is not appropriate for the operation and no action was performed.
        /// </summary>
        public bool WasCancelled
        {
            get => GetBoolProperty(PropertyNames.WasCancelled);
            set => SetBoolProperty(PropertyNames.WasCancelled, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new CancelReply();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (CancelReply)target;

            typedTarget.WasCancelled = this.WasCancelled;
        }
    }
}
