//-----------------------------------------------------------------------------
// FILE:	    ActivityRecordHeartbeatRequest.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
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
        /// <para>
        /// Overrides the <see cref="ActivityRequest.ContextId"/> message property when
        /// non-null, indicating that the activity heartbeat is being sent externally.
        /// </para>
        /// <note>
        /// Only one of <see cref="TaskToken"/> or <see cref="Domain"/> may be non-null
        /// within a given message.
        /// </note>
        /// </summary>
        public byte[] TaskToken
        {
            get => GetBytesProperty(PropertyNames.TaskToken);
            set => SetBytesProperty(PropertyNames.TaskToken, value);
        }

        /// <summary>
        /// <para>
        /// Overrides the <see cref="ActivityRequest.ContextId"/> message property when
        /// non-null, indicating that the activity heartbeat is being sent externally.
        /// </para>
        /// </summary>
        /// <note>
        /// Only one of <see cref="TaskToken"/> or <see cref="Domain"/> may be non-null
        /// within a given message.  The <see cref="WorkflowId"/> and <see cref="RunId"/>
        /// will be valid only when <see cref="Domain"/> is non-null.
        /// </note>
        public string Domain
        {
            get => GetStringProperty(PropertyNames.Domain);
            set => SetStringProperty(PropertyNames.Domain, value);
        }

        /// <summary>
        /// <para>
        /// The target workflow ID.
        /// </para>
        /// <note>
        /// This is required when <see cref="Domain"/> is non-null.
        /// </note>
        /// </summary>
        public string WorkflowId
        {
            get => GetStringProperty(PropertyNames.WorkflowId);
            set => SetStringProperty(PropertyNames.WorkflowId, value);
        }

        /// <summary>
        /// <para>
        /// The target run ID.
        /// </para>
        /// <note>
        /// This is optional when <see cref="Domain"/> is non-null.
        /// </note>
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

            typedTarget.TaskToken  = this.TaskToken;
            typedTarget.Domain     = this.Domain;
            typedTarget.WorkflowId = this.WorkflowId;
            typedTarget.RunId      = this.RunId;
            typedTarget.ActivityId = this.ActivityId;
            typedTarget.Details    = this.Details;
        }
    }
}
