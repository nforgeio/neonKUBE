//-----------------------------------------------------------------------------
// FILE:	    WorkflowMutableRequest.cs
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
    /// <b>proxy --> client:</b> Invokes a workflow instance.
    /// </summary>
    [Obsolete("This was replaced by a local activity.")]
    [InternalProxyMessage(InternalMessageTypes.WorkflowMutableRequest)]
    internal class WorkflowMutableRequest : WorkflowRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public WorkflowMutableRequest()
        {
            Type = InternalMessageTypes.WorkflowMutableRequest;
        }

        /// <inheritdoc/>
        public override InternalMessageTypes ReplyType => InternalMessageTypes.WorkflowMutableReply;

        /// <summary>
        /// Identifies the mutable value.
        /// </summary>
        public string MutableId
        {
            get => GetStringProperty(PropertyNames.MutableId);
            set => SetStringProperty(PropertyNames.MutableId, value);
        }

        /// <summary>
        /// The mutable value to be returned.
        /// </summary>
        public byte[] Result
        {
            get => GetBytesProperty(PropertyNames.Result);
            set => SetBytesProperty(PropertyNames.Result, value);
        }

        /// <summary>
        /// Indicates that the value should be persisted to the workflow
        /// history if it doesn't already exist or the value has changed.
        /// </summary>
        public bool Update
        {
            get => GetBoolProperty(PropertyNames.Update);
            set => SetBoolProperty(PropertyNames.Update, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new WorkflowMutableRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (WorkflowMutableRequest)target;

            typedTarget.MutableId = this.MutableId;
            typedTarget.Result    = this.Result;
        typedTarget.Update = this.Update;
        }
    }
}
