//-----------------------------------------------------------------------------
// FILE:	    WorkflowRequest.cs
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
    /// Base class for all workflow related requests.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.Unspecified)]
    internal class WorkflowRequest : ProxyRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public WorkflowRequest()
        {
        }

        /// <summary>
        /// <para>
        /// Uniquely identifies the workflow context associated with this request.
        /// </para>
        /// <note>
        /// Not all derived classes actually require this property.  In those cases,
        /// this can remain as its default zero value.
        /// </note>
        /// </summary>
        public long ContextId
        {
            get => GetLongProperty(PropertyNames.ContextId);
            set => SetLongProperty(PropertyNames.ContextId, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new WorkflowRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (WorkflowRequest)target;

            typedTarget.ContextId = this.ContextId;
        }
    }
}
