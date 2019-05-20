//-----------------------------------------------------------------------------
// FILE:	    NewWorkerRequest.cs
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
    /// <b>client --> proxy:</b> Registers with Cadence that the current
    /// connection is capable of executing task and/or activities.
    /// </summary>
    [ProxyMessage(MessageTypes.NewWorkerRequest)]
    internal class NewWorkerRequest : ProxyRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public NewWorkerRequest()
        {
            Type = MessageTypes.NewWorkerRequest;
        }

        /// <inheritdoc/>
        public override MessageTypes ReplyType => MessageTypes.NewWorkerReply;

        /// <summary>
        /// Specifies the name to be used to register the workflow or activity worker.
        /// </summary>
        public string Name
        {
            get => GetStringProperty("Name");
            set => SetStringProperty("Name", value);
        }

        /// <summary>
        /// Indicates whether we're starting a workflow or an activity worker.
        /// </summary>
        public bool IsWorkflow
        {
            get => GetBoolProperty("IsWorkflow");
            set => SetBoolProperty("IsWorkflow", value);
        }

        /// <summary>
        /// The domain hosting the Cadence workflow.
        /// </summary>
        public string Domain
        {
            get => GetStringProperty("Domain");
            set => SetStringProperty("Domain", value);
        }

        /// <summary>
        /// Identifies the task list for the source workflows and activities.
        /// </summary>
        public string TaskList
        {
            get => GetStringProperty("TaskList");
            set => SetStringProperty("TaskList", value);
        }

        /// <summary>
        /// The worker options.
        /// </summary>
        public InternalWorkerOptions Options
        {
            get => GetJsonProperty<InternalWorkerOptions>("Options");
            set => SetJsonProperty<InternalWorkerOptions>("Options", value);
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

            typedTarget.Name       = this.Name;
            typedTarget.IsWorkflow = this.IsWorkflow;
            typedTarget.Domain     = this.Domain;
            typedTarget.TaskList   = this.TaskList;
            typedTarget.Options    = this.Options;
        }
    }
}
