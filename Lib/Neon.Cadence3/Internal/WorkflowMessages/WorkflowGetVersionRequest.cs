//-----------------------------------------------------------------------------
// FILE:	    WorkflowGetVersionRequest.cs
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
    /// <b>client --> proxy:</b> Manages workflow versioning.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.WorkflowGetVersionRequest)]
    internal class WorkflowGetVersionRequest : WorkflowRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public WorkflowGetVersionRequest()
        {
            Type = InternalMessageTypes.WorkflowGetVersionRequest;
        }

        /// <inheritdoc/>
        public override InternalMessageTypes ReplyType => InternalMessageTypes.WorkflowGetVersionReply;

        /// <summary>
        /// Identifies change from one workflow implementation version to another.
        /// </summary>
        public string ChangeId
        {
            get => GetStringProperty(PropertyNames.ChangeId);
            set => SetStringProperty(PropertyNames.ChangeId, value);
        }

        /// <summary>
        /// Specifies the minimum supported workflow implementation version.
        /// </summary>
        public int MinSupported
        {
            get => GetIntProperty(PropertyNames.MinSupported);
            set => SetIntProperty(PropertyNames.MinSupported, value);
        }

        /// <summary>
        /// Specifies the maximum supported workflow implementation version.
        /// </summary>
        public int MaxSupported
        {
            get => GetIntProperty(PropertyNames.MaxSupported);
            set => SetIntProperty(PropertyNames.MaxSupported, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new WorkflowGetVersionRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (WorkflowGetVersionRequest)target;

            typedTarget.ChangeId     = this.ChangeId;
            typedTarget.MinSupported = this.MinSupported;
            typedTarget.MaxSupported = this.MaxSupported;
        }
    }
}
