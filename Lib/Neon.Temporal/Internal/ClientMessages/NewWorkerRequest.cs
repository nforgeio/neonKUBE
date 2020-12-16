//-----------------------------------------------------------------------------
// FILE:	    NewWorkerRequest.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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

using Neon.Common;
using Neon.Temporal;

namespace Neon.Temporal.Internal
{
    /// <summary>
    /// <b>client --> proxy:</b> Creates a new worker but doesn't start it.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.NewWorkerRequest)]
    internal class NewWorkerRequest : ProxyRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public NewWorkerRequest()
        {
            Type = InternalMessageTypes.NewWorkerRequest;
        }

        /// <inheritdoc/>
        public override InternalMessageTypes ReplyType => InternalMessageTypes.NewWorkerReply;

        /// <summary>
        /// Specifies the name to be used to register the workflow or activity worker.
        /// </summary>
        public string Name
        {
            get => GetStringProperty(PropertyNames.Name);
            set => SetStringProperty(PropertyNames.Name, value);
        }

        /// <summary>
        /// The worker options.
        /// </summary>
        public WorkerOptions Options
        {
            get => GetJsonProperty<WorkerOptions>(PropertyNames.Options);
            set => SetJsonProperty<WorkerOptions>(PropertyNames.Options, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new NewWorkerRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (NewWorkerRequest)target;

            typedTarget.Name    = this.Name;
            typedTarget.Options = this.Options;
        }
    }
}
