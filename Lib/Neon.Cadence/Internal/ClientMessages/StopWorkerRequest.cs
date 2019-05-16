//-----------------------------------------------------------------------------
// FILE:	    StopWorkerRequest.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Neon.Cadence;
using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;
using Neon.Tasks;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// <b>library --> proxy:</b> Stops a Cadence worker.
    /// </summary>
    [ProxyMessage(MessageTypes.StopWorkerRequest)]
    internal class StopWorkerRequest : ProxyRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public StopWorkerRequest()
        {
            Type = MessageTypes.StopWorkerRequest;
        }

        /// <inheritdoc/>
        public override MessageTypes ReplyType => MessageTypes.StopWorkerReply;

        /// <summary>
        /// Identifies the worker being stopped.
        /// </summary>
        public long WorkerId
        {
            get => GetLongProperty("WorkerId");
            set => SetLongProperty("WorkerId", value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new StopWorkerRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (StopWorkerRequest)target;

            typedTarget.WorkerId = this.WorkerId;
        }
    }
}
