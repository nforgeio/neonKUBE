//-----------------------------------------------------------------------------
// FILE:	    WorkflowInvokeRequest.cs
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
    /// <b>proxy --> library:</b> Invokes a workflow instance.
    /// </summary>
    [ProxyMessage(MessageTypes.WorkflowInvokeRequest)]
    internal class WorkflowInvokeRequest : WorkflowContextRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public WorkflowInvokeRequest()
        {
            Type = MessageTypes.WorkflowInvokeRequest;
        }

        /// <inheritdoc/>
        public override MessageTypes ReplyType => MessageTypes.WorkflowExecuteReply;

        /// <summary>
        /// Identifies the workflow implementation to be started.
        /// </summary>
        public string Name
        {
            get => GetStringProperty("Name");
            set => SetStringProperty("Name", value);
        }

        /// <summary>
        /// The workflow arguments dictionary (or <c>null</c>).
        /// </summary>
        public Dictionary<string, object> Args
        {
            get => GetJsonProperty<Dictionary<string, object>>("Args");
            set => SetJsonProperty<Dictionary<string, object>>("Args", value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new WorkflowInvokeRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (WorkflowInvokeRequest)target;

            typedTarget.Name = this.Name;

            if (this.Args != null)
            {
                var clonedArgs = new Dictionary<string, object>();

                foreach (var arg in this.Args)
                {
                    clonedArgs.Add(arg.Key, arg.Value);
                }

                typedTarget.Args = clonedArgs;
            }
        }
    }
}
