//-----------------------------------------------------------------------------
// FILE:	    WorkflowQueryRequest.cs
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
    /// <b>proxy --> client:</b> Queries a running workflow.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.WorkflowQueryRequest)]
    internal class WorkflowQueryRequest : WorkflowRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public WorkflowQueryRequest()
        {
            Type = InternalMessageTypes.WorkflowQueryRequest;
        }

        /// <inheritdoc/>
        public override InternalMessageTypes ReplyType => InternalMessageTypes.WorkflowQueryReply;

        /// <summary>
        /// Identifies the workflow by ID.
        /// </summary>
        public string WorkflowId
        {
            get => GetStringProperty(PropertyNames.WorkflowId);
            set => SetStringProperty(PropertyNames.WorkflowId, value);
        }

        /// <summary>
        /// Identifies the specific workflow run to be queried.  The latest run
        /// will be queried when this is <c>null</c> or empty.
        /// </summary>
        public string RunId
        {
            get => GetStringProperty(PropertyNames.RunId);
            set => SetStringProperty(PropertyNames.RunId, value);
        }

        /// <summary>
        /// Identifies the query.
        /// </summary>
        public string QueryName
        {
            get => GetStringProperty(PropertyNames.QueryName);
            set => SetStringProperty(PropertyNames.QueryName, value);
        }

        /// <summary>
        /// Optionally specifies the query arguments.
        /// </summary>
        public byte[] QueryArgs
        {
            get => GetBytesProperty(PropertyNames.QueryArgs);
            set => SetBytesProperty(PropertyNames.QueryArgs, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new WorkflowQueryRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (WorkflowQueryRequest)target;

            typedTarget.WorkflowId = this.WorkflowId;
            typedTarget.RunId      = this.RunId;
            typedTarget.QueryName  = this.QueryName;
            typedTarget.QueryArgs  = this.QueryArgs;
        }
    }
}
