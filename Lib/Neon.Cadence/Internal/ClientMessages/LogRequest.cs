//-----------------------------------------------------------------------------
// FILE:	    LogRequest.cs
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
using Neon.Diagnostics;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// <b>proxy --> client:</b> Sent by <b>cadence-proxy</b> to log Cadence and cadence-proxy
    /// events to the host's event stream.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.LogRequest)]
    internal class LogRequest : ProxyRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public LogRequest()
        {
            Type = InternalMessageTypes.LogRequest;
        }

        /// <inheritdoc/>
        public override InternalMessageTypes ReplyType => InternalMessageTypes.LogReply;

        /// <summary>
        /// Identifies when the event being logged occurred (UTC).
        /// </summary>
        public DateTime TimeUtc
        {
            get => GetDateTimeProperty(PropertyNames.Time);
            set => SetDateTimeProperty(PropertyNames.Time, value);
        }

        /// <summary>
        /// Identifies the log level.
        /// </summary>
        public LogLevel LogLevel
        {
            get => GetEnumProperty<LogLevel>(PropertyNames.LogLevel);
            set => SetEnumProperty<LogLevel>(PropertyNames.LogLevel, value);
        }

        /// <summary>
        /// Specifies the source of the event veing logged.  Set this to <c>true</c>
        /// for events coming from the GOLANG Cadence client or <c>false</c> for
        /// events coming from the <b>cadence-proxy</b> wrapper.
        /// </summary>
        public bool FromCadence
        {
            get => GetBoolProperty(PropertyNames.FromCadence);
            set => SetBoolProperty(PropertyNames.FromCadence, value);
        }

        /// <summary>
        /// The message being logged.
        /// </summary>
        public string LogMessage
        {
            get => GetStringProperty(PropertyNames.LogMessage);
            set => SetStringProperty(PropertyNames.LogMessage, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new LogRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (LogRequest)target;

            typedTarget.TimeUtc     = this.TimeUtc;
            typedTarget.LogLevel    = this.LogLevel;
            typedTarget.FromCadence = this.FromCadence;
            typedTarget.LogMessage  = this.LogMessage;

        }
    }
}
