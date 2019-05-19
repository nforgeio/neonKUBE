//-----------------------------------------------------------------------------
// FILE:	    CadenceClient.Workflow.cs
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

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;
using Neon.Tasks;

using Neon.Cadence.Internal;

namespace Neon.Cadence
{
    public partial class CadenceClient
    {
        //---------------------------------------------------------------------
        // Cadence workflow and activity related operations.

        /// <summary>
        /// Starts an instance of the named workflow within the current client domain.
        /// </summary>
        /// <param name="name">
        /// The name used when registering the workflow workers that will handle this workflow.
        /// This name will often be the fully qualified name of the workflow  type but this may 
        /// have been customized when the workflow worker was registered.
        /// </param>
        /// <param name="domain">Specifies the Cadence domain where the workflow will run.</param>
        /// <param name="args">Optionally specifies the workflow arguments encoded into a byte array.</param>
        /// <param name="options">Optionally specifies the workflow options.</param>
        /// <returns>A <see cref="WorkflowRun"/> identifying the new running workflow instance.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if there is no workflow worker registered for <paramref name="name"/>.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown if the request is not valid.</exception>
        /// <exception cref="CadenceWorkflowRunningException">Thrown if a workflow with this ID is already running.</exception>
        /// <remarks>
        /// This method kicks off a new workflow instance and returns after Cadence has
        /// queued the operation but the method <b>does not</b> wait for the workflow to
        /// complete.
        /// </remarks>
        public async Task<WorkflowRun> ExecuteWorkflowAsync(string name, string domain, byte[] args = null, WorkflowOptions options = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(domain));

            var internalOptions = options?.ToInternal();

            internalOptions = internalOptions ?? new InternalStartWorkflowOptions();

            var reply = (WorkflowExecuteReply)await CallProxyAsync(
                new WorkflowExecuteRequest()
                {
                    Name    = name,
                    Domain  = domain,
                    Args    = args,
                    Options = internalOptions
                });

            reply.ThrowOnError();

            var execution = reply.Execution;

            return new WorkflowRun(execution.RunID, execution.ID);
        }
    }
}
