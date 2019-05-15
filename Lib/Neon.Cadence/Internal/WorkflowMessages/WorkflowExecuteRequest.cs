//-----------------------------------------------------------------------------
// FILE:	    WorkflowExecuteRequest.cs
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
    /// <b>proxy --> library:</b> Starts a workflow execution.
    /// </summary>
    [ProxyMessage(MessageTypes.WorkflowExecuteRequest)]
    internal class WorkflowExecuteRequest : ProxyRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public WorkflowExecuteRequest()
        {
            Type = MessageTypes.WorkflowExecuteRequest;
        }

        /// <inheritdoc/>
        public override MessageTypes ReplyType => MessageTypes.WorkflowExecuteReply;

        /// <summary>
        /// Identifies the Cadence domain hosting the workflow.
        /// </summary>
        public string Domain
        {
            get => GetStringProperty("Domain");
            set => SetStringProperty("Domain", value);
        }

        /// <summary>
        /// Identifies the workflow implementation to be started.
        /// </summary>
        public string Name
        {
            get => GetStringProperty("Name");
            set => SetStringProperty("Name", value);
        }

        /// <summary>
        /// The workflow arguments encoded as a byte array (or <c>null</c>).
        /// </summary>
        public byte[] Args
        {
            get => GetBytesProperty("Args");
            set => SetBytesProperty("Args", value);
        }

        /// <summary>
        /// The workflow start options.
        /// </summary>
        public StartWorkflowOptions Options
        {
            get => GetJsonProperty<StartWorkflowOptions>("Options");
            set => SetJsonProperty<StartWorkflowOptions>("Options", value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new WorkflowExecuteRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (WorkflowExecuteRequest)target;

            typedTarget.Args    = this.Args;
            typedTarget.Domain  = this.Domain;
            typedTarget.Name    = this.Name;
            typedTarget.Options = this.Options;
        }
    }
}
