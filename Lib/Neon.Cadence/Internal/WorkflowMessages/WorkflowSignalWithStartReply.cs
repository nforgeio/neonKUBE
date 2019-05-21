//-----------------------------------------------------------------------------
// FILE:	    WorkflowSignalWithStartReply.cs
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
    /// <b>proxy --> client:</b> Answers a <see cref="WorkflowSignalWithStartRequest"/>
    /// </summary>
    [ProxyMessage(MessageTypes.WorkflowSignalWithStartReply)]
    internal class WorkflowSignalWithStartReply : WorkflowReply
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public WorkflowSignalWithStartReply()
        {
            Type = MessageTypes.WorkflowSignalWithStartReply;
        }

        /// <summary>
        /// Returns details identifying the workflow execution.
        /// </summary>
        public InternalWorkflowExecution Execution
        {
            get => GetJsonProperty<InternalWorkflowExecution>("Execution");
            set => SetJsonProperty<InternalWorkflowExecution>("Execution", value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new WorkflowSignalWithStartReply();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (WorkflowSignalWithStartReply)target;

            typedTarget.Execution = this.Execution;
        }
    }
}
