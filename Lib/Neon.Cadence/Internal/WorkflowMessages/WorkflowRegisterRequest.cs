//-----------------------------------------------------------------------------
// FILE:	    WorkflowRegisterRequest.cs
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
    /// <b>library --> proxy:</b> Registers a workflow handler by name.
    /// </summary>
    [ProxyMessage(MessageTypes.WorkflowRegisterRequest)]
    internal class WorkflowRegisterRequest : ProxyRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public WorkflowRegisterRequest()
        {
            Type = MessageTypes.WorkflowRegisterRequest;
        }

        /// <inheritdoc/>
        public override MessageTypes ReplyType => MessageTypes.WorkflowRegisterReply;

        /// <summary>
        /// Identifies the workflow implementation.
        /// </summary>
        public string Name
        {
            get => GetStringProperty("Name");
            set => SetStringProperty("Name", value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new WorkflowRegisterRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (WorkflowRegisterRequest)target;

            typedTarget.Name = this.Name;
        }
    }
}
