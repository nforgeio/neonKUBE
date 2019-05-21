//-----------------------------------------------------------------------------
// FILE:	    WorkflowSignalRequest.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;

using Newtonsoft.Json;
using YamlDotNet.Serialization;

using Neon.Cadence;
using Neon.Common;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// <b>proxy --> client:</b> Sends a signal to a running workflow.
    /// </summary>
    [ProxyMessage(MessageTypes.WorkflowSignalRequest)]
    internal class WorkflowSignalRequest : WorkflowRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public WorkflowSignalRequest()
        {
            Type = MessageTypes.WorkflowSignalRequest;
        }

        /// <inheritdoc/>
        public override MessageTypes ReplyType => MessageTypes.WorkflowSignalReply;

        /// <summary>
        /// Identifies the workflow by ID.
        /// </summary>
        public string WorkflowId
        {
            get => GetStringProperty("WorkflowId");
            set => SetStringProperty("WorkflowId", value);
        }

        /// <summary>
        /// Identifies the specific workflow run to be cancelled.  The latest run
        /// will be cancelled when this is <c>null</c> or empty.
        /// </summary>
        public string RunId
        {
            get => GetStringProperty("RunId");
            set => SetStringProperty("RunId", value);
        }

        /// <summary>
        /// Identifies the signal.
        /// </summary>
        public string SignalName
        {
            get => GetStringProperty("SignalName");
            set => SetStringProperty("SignalName", value);
        }

        /// <summary>
        /// Optionally specifies the signal arguments.
        /// </summary>
        public byte[] SignalArgs
        {
            get => GetBytesProperty("SignalArgs");
            set => SetBytesProperty("SignalArgs", value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new WorkflowSignalRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (WorkflowSignalRequest)target;

            typedTarget.WorkflowId = this.WorkflowId;
            typedTarget.RunId      = this.RunId;
            typedTarget.SignalName = this.SignalName;
            typedTarget.SignalArgs = this.SignalArgs;
        }
    }
}
