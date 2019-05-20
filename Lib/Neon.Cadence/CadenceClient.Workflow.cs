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
        /// Starts a global workflow, identifying the workers that implement the workflow
        /// as well as the Cadence domain where the workflow will run.  Global workflows
        /// have no parent, as opposed to child workflows that run in the context of 
        /// another workflow.
        /// </summary>
        /// <param name="name">
        /// The name used when registering the workers that will handle this workflow.
        /// This name will often be the fully qualified name of the workflow  type but this may 
        /// have been customized when the workflow worker was registered.
        /// </param>
        /// <param name="domain">Specifies the Cadence domain where the workflow will run.</param>
        /// <param name="options">Specifies the workflow options.</param>
        /// <param name="args">Optionally specifies the workflow arguments encoded into a byte array.</param>
        /// <returns>A <see cref="WorkflowRun"/> identifying the new running workflow instance.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if there is no workflow worker registered for <paramref name="name"/>.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown if the request is not valid.</exception>
        /// <exception cref="CadenceWorkflowRunningException">Thrown if a workflow with this ID is already running.</exception>
        /// <remarks>
        /// This method kicks off a new workflow instance and returns after Cadence has
        /// queued the operation but the method <b>does not</b> wait for the workflow to
        /// complete.
        /// </remarks>
        public async Task<WorkflowRun> StartWorkflowAsync(string name, string domain, WorkflowOptions options = null, byte[] args = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(domain));
            Covenant.Requires<ArgumentNullException>(options != null);

            var reply = (WorkflowExecuteReply)await CallProxyAsync(
                new WorkflowExecuteRequest()
                {
                    Name    = name,
                    Domain  = domain,
                    Args    = args,
                    Options = options.ToInternal()
                });

            reply.ThrowOnError();

            var execution = reply.Execution;

            return new WorkflowRun(execution.RunID, execution.ID);
        }

        /// <summary>
        /// <para>
        /// Cancels a workflow if it has not already finished.
        /// </para>
        /// <note>
        /// Workflow cancellation is typically considered to be a normal activity
        /// and not an error as opposed to workflow termination which will usually
        /// happen due to an error.
        /// </note>
        /// </summary>
        /// <param name="workflowId">The workflow ID.</param>
        /// <param name="runId">
        /// Optionally specifies the workflow's current run ID.  When <c>null</c> or empty
        /// Cadence will automatically cancel the lastest workflow run.
        /// </param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the workflow no longer exists.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown if the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence problems.</exception>
        public async Task CancelWorkflow(string workflowId, string runId = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowId));

            var reply = (WorkflowCancelReply)await CallProxyAsync(
                new WorkflowCancelRequest()
                {
                    WorkflowId = workflowId,
                    RunId      = runId
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// <para>
        /// Cancels a workflow if it has not already finished.
        /// </para>
        /// <note>
        /// Workflow termination is typically considered to be due to an error as
        /// opposed to cancellation which is usually considered as a normal activity.
        /// </note>
        /// </summary>
        /// <param name="workflowId">The workflow ID.</param>
        /// <param name="runId">
        /// Optionally specifies the workflow's current run ID.  When <c>null</c> or empty
        /// Cadence will automatically cancel the lastest workflow run.
        /// </param>
        /// <param name="reason">Optionally specifies an error reason string.</param>
        /// <param name="details">Optionally specifies additional details as a byte array.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the workflow no longer exists.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown if the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence problems.</exception>
        public async Task TerminateWorkflow(string workflowId, string runId = null, string reason = null, byte[] details = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowId));

            var reply = (WorkflowTerminateReply)await CallProxyAsync(
                new WorkflowTerminateRequest()
                {
                    WorkflowId = workflowId,
                    RunId      = runId,
                    Reason     = reason,
                    Details    = details
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Transmits a signal to a running workflow.
        /// </summary>
        /// <param name="workflowId">The workflow ID.</param>
        /// <param name="signalName">Identifies the signal.</param>
        /// <param name="runId">
        /// Optionally specifies the workflow's current run ID.  When <c>null</c> or empty
        /// Cadence will automatically cancel the lastest workflow run.
        /// </param>
        /// <param name="signalArgs">Optionally specifies signal arguments as a byte array.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the workflow no longer exists.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence problems.</exception>
        public async Task SignalWorkflow(string workflowId, string signalName, string runId = null, byte[] signalArgs = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowId));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(signalName));

            var reply = (WorkflowSignalReply)await CallProxyAsync(
                new WorkflowSignalRequest()
                {
                    WorkflowId = workflowId,
                    SignalName = signalName,
                    RunId      = runId,
                    SignalArgs = signalArgs
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Transmits a signal to a workflow, starting the workflow if it's not currently running.
        /// </summary>
        /// <param name="workflowId">The workflow ID.</param>
        /// <param name="options">Specifies the options to be used for starting the workflow if required.</param>
        /// <param name="signalName">Identifies the signal.</param>
        /// <param name="signalArgs">Optionally specifies signal arguments as a byte array.</param>
        /// <param name="workflowArgs">Optionally specifies the workflow arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the domain does not exist.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown if the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence problems.</exception>
        public async Task SignalWorkflow(string workflowId, WorkflowOptions options, string signalName, byte[] signalArgs = null, byte[] workflowArgs = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowId));
            Covenant.Requires<ArgumentNullException>(options != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(signalName));

            var reply = (WorkflowSignalWithStartReply)await CallProxyAsync(
                new WorkflowSignalWithStartRequest()
                {
                    WorkflowId   = workflowId,
                    Options      = options.ToInternal(),
                    SignalName   = signalName,
                    SignalArgs   = signalArgs,
                    WorkflowArgs = workflowArgs
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// <para>
        /// Sets the maximum number of bytes of history that will be retained
        /// for sticky workflows for workflow workers created by this client
        /// as a performance optimization.  When this is exceeded, Cadence will
        /// need to retrieve the entire workflow history from the Cadence cluster
        /// every time the workflow is assigned to a worker.
        /// </para>
        /// <para>
        /// This defaults to <b>10K</b> bytes.
        /// </para>
        /// </summary>
        /// <param name="maxCacheSize">The maximum number of bytes to cache for each sticky workflow.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task SetWorkflowCacheSize(int maxCacheSize)
        {
            Covenant.Requires<ArgumentNullException>(maxCacheSize >= 0);

            var reply = (WorkflowSetCacheSizeReply)await CallProxyAsync(
                new WorkflowSetCacheSizeRequest()
                {
                    Size = maxCacheSize
                });

            reply.ThrowOnError();

            workflowCacheSize = maxCacheSize;
        }

        /// <summary>
        /// Returns the current maximum maximum number of bytes of history that 
        /// will be retained for sticky workflows for workflow workers created 
        /// by this client as a performance optimization.
        /// </summary>
        /// <returns>The maximum individual workflow cache size in bytes.</returns>
        public async Task<int> GetworkflowCacheSize()
        {
            return await Task.FromResult(workflowCacheSize);
        }
    }
}
