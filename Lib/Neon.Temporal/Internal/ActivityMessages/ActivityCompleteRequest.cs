//-----------------------------------------------------------------------------
// FILE:	    ActivityCompleteRequest.cs
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

using Neon.Common;
using Neon.Temporal;

namespace Neon.Temporal.Internal
{
    /// <summary>
    /// <b>client --> proxy:</b> Sent to complete an activity externally.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.ActivityCompleteRequest)]
    internal class ActivityCompleteRequest : ActivityRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ActivityCompleteRequest()
        {
            Type = InternalMessageTypes.ActivityCompleteRequest;
        }

        /// <inheritdoc/>
        public override InternalMessageTypes ReplyType => InternalMessageTypes.ActivityCompleteReply;

        /// <summary>
        /// The opaque activity task token.
        /// </summary>
        public byte[] TaskToken
        {
            get => GetBytesProperty(PropertyNames.TaskToken);
            set => SetBytesProperty(PropertyNames.TaskToken, value);
        }

        /// <summary>
        /// The target namespace.
        /// </summary>
        public string Namespace
        {
            get => GetStringProperty(PropertyNames.Namespace);
            set => SetStringProperty(PropertyNames.Namespace, value ?? string.Empty);
        }

        /// <summary>
        /// The target workflow ID.
        /// </summary>
        public string WorkflowId
        {
            get => GetStringProperty(PropertyNames.WorkflowId);
            set => SetStringProperty(PropertyNames.WorkflowId, value);
        }

        /// <summary>
        /// The target run ID.
        /// </summary>
        public string RunId
        {
            get => GetStringProperty(PropertyNames.RunId);
            set => SetStringProperty(PropertyNames.RunId, value ?? string.Empty);
        }

        /// <summary>
        /// The target activity ID.
        /// </summary>
        public string ActivityId
        {
            get => GetStringProperty(PropertyNames.ActivityId);
            set => SetStringProperty(PropertyNames.ActivityId, value);
        }

        /// <summary>
        /// The activity result.
        /// </summary>
        public byte[] Result
        {
            get => GetBytesProperty(PropertyNames.Result);
            set => SetBytesProperty(PropertyNames.Result, value);
        }

        /// <summary>
        /// The activity error.
        /// </summary>
        public TemporalError Error
        {
            get => GetJsonProperty<TemporalError>(PropertyNames.Error);
            set => SetJsonProperty<TemporalError>(PropertyNames.Error, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new ActivityCompleteRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (ActivityCompleteRequest)target;

            typedTarget.TaskToken  = this.TaskToken;
            typedTarget.Namespace  = this.Namespace;
            typedTarget.WorkflowId = this.WorkflowId;
            typedTarget.RunId      = this.RunId;
            typedTarget.ActivityId = this.ActivityId;
            typedTarget.Result     = this.Result;
            typedTarget.Error      = this.Error;
        }
    }
}
