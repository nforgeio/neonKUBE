//-----------------------------------------------------------------------------
// FILE:	    WorkflowFutureStub.cs
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
using System.Reflection;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Tasks;

namespace Neon.Cadence
{
    /// <summary>
    /// <para>
    /// Manages starting, signalling, or querying an external workflow instance
    /// based on its workflow type name and arguments.  This class separates workflow 
    /// execution and retrieving the result into separate operations.
    /// </para>
    /// <para>
    /// Use this version for workflows that don't return a result.
    /// </para>
    /// </summary>
    /// <typeparam name="WorkflowInterface">Specifies the workflow interface.</typeparam>
    public class WorkflowFutureStub<WorkflowInterface>
    {
        private CadenceClient       client;
        private WorkflowOptions     options;
        private string              workflowTypeName;
        private WorkflowExecution   execution;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="client">The associated client.</param>
        /// <param name="methodName">
        /// Optionally identifies the target workflow method by the name specified in
        /// the <c>[WorkflowMethod]</c> attribute tagging the method.  Pass a <c>null</c>
        /// or empty string to target the default method.
        /// </param>
        /// <param name="options">Optional workflow options.</param>
        internal WorkflowFutureStub(CadenceClient client, string methodName = null, WorkflowOptions options = null)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));

            var workflowInterface = typeof(WorkflowInterface);

            CadenceHelper.ValidateWorkflowInterface(workflowInterface);

            this.client           = client;
            this.workflowTypeName = CadenceHelper.GetWorkflowTarget(workflowInterface, methodName).WorkflowTypeName;
            this.options          = WorkflowOptions.Normalize(client, options);
        }

        /// <summary>
        /// Returns the workflow <see cref="WorkflowExecution"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the workflow has not been started.</exception>
        public WorkflowExecution Execution
        {
            get
            {
                if (this.execution == null)
                {
                    throw new InvalidOperationException("Cannot start a future stub more than once.");
                }

                return execution;
            }
        }

        /// <summary>
        /// Starts the workflow, returning an <see cref="IAsyncFuture"/> that can be used
        /// to wait for the the workflow to complete.  This version does not return a workflow
        /// result.
        /// </summary>
        /// <param name="args">The workflow arguments.</param>
        /// <returns>An <see cref="ExternalWorkflowFuture"/> that can be used to retrieve the workflow result as an <c>object</c>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the workflow has already been started.</exception>
        /// <remarks>
        /// <note>
        /// <b>IMPORTANT:</b> You need to take care to ensure that the parameters passed
        /// are compatible with the target workflow method.
        /// </note>
        /// </remarks>
        public async Task<ExternalWorkflowFuture> StartAsync(params object[] args)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            if (execution != null)
            {
                throw new InvalidOperationException("Cannot start a future stub more than once.");
            }

            execution = await client.StartWorkflowAsync(workflowTypeName, client.DataConverter.ToData(args), options);

            // Create and return the future.

            return new ExternalWorkflowFuture(client, execution);
        }

        /// <summary>
        /// Starts the workflow, returning an <see cref="IAsyncFuture"/> that can be used
        /// to wait for the the workflow to complete and obtain its result.
        /// </summary>
        /// <typeparam name="TResult">The workflow result type.</typeparam>
        /// <param name="args">The workflow arguments.</param>
        /// <returns>An <see cref="ExternalWorkflowFuture{TResult}"/> that can be used to retrieve the workflow result as an <c>object</c>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the workflow has already been started.</exception>
        /// <remarks>
        /// <note>
        /// <b>IMPORTANT:</b> You need to take care to ensure that the parameters passed
        /// and the result type are compatible with the target workflow method.
        /// </note>
        /// </remarks>
        public async Task<ExternalWorkflowFuture<TResult>> StartAsync<TResult>(params object[] args)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            if (execution != null)
            {
                throw new InvalidOperationException("Cannot start a future stub more than once.");
            }

            execution = await client.StartWorkflowAsync(workflowTypeName, client.DataConverter.ToData(args), options);

            // Create and return the future.

            return new ExternalWorkflowFuture<TResult>(client, execution);
        }

        /// <summary>
        /// Signals the workflow.
        /// </summary>
        /// <param name="signalName">The signal name.</param>
        /// <param name="args">The signal arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the child workflow has not been started.</exception>
        /// <remarks>
        /// <note>
        /// <b>IMPORTANT:</b> You need to take care to ensure that the parameters passed
        /// are compatible with the target workflow arguments.
        /// </note>
        /// </remarks>
        public async Task SignalAsync(string signalName, params object[] args)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(signalName), nameof(signalName));
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            if (Execution == null)
            {
                throw new InvalidOperationException("The stub must be started first.");
            }

            var reply = (WorkflowSignalReply) await client.CallProxyAsync(
                new WorkflowSignalRequest()
                {
                        WorkflowId = execution.WorkflowId,
                        RunId      = execution.RunId,
                        Domain     = options.Domain,
                        SignalName = signalName,
                        SignalArgs = client.DataConverter.ToData(args)
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Queries the workflow.
        /// </summary>
        /// <typeparam name="TQueryResult">The query result type.</typeparam>
        /// <param name="queryName">Identifies the query.</param>
        /// <param name="args">The query arguments.</param>
        /// <returns>The query result.</returns>
        /// <remarks>
        /// <note>
        /// <b>IMPORTANT:</b> You need to take care to ensure that the parameters and
        /// result type passed are compatible with the target workflow query arguments.
        /// </note>
        /// </remarks>
        public async Task<TQueryResult> QueryAsync<TQueryResult>(string queryName, params object[] args)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(queryName), nameof(queryName));
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            if (Execution == null)
            {
                throw new InvalidOperationException("The stub must be started first.");
            }

            var reply = (WorkflowQueryReply)await client.CallProxyAsync(
                new WorkflowQueryRequest()
                {
                    WorkflowId = execution.WorkflowId,
                    RunId      = execution.RunId,
                    Domain     = options.Domain,
                    QueryName  = queryName,
                    QueryArgs  = client.DataConverter.ToData(args)
                });

            reply.ThrowOnError();

            return client.DataConverter.FromData<TQueryResult>(reply.Result);
        }
    }
}
