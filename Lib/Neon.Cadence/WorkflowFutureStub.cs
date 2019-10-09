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
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Tasks;

namespace Neon.Cadence
{
    /// <summary>
    /// <para>
    /// Manages starting and signalling an external workflow instance based on
    /// its workflow type name and arguments.  This class separates workflow 
    /// execution and retrieving the result into separate operations.
    /// </para>
    /// <para>
    /// Use this version for workflows that don't return a result.
    /// </para>
    /// </summary>
    public class WorkflowFutureStub
    {
        private CadenceClient       client;
        private WorkflowExecution   execution;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="client">The associated client.</param>
        /// <param name="workflowTypeName">The workflow type name.</param>
        /// <param name="options">Optional workflow options.</param>
        internal WorkflowFutureStub(CadenceClient client, string workflowTypeName, WorkflowOptions options = null)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowTypeName), nameof(workflowTypeName));

            this.client           = client;
            this.WorkflowTypeName = workflowTypeName;
            this.Options          = WorkflowOptions.Normalize(client, options);
        }

        /// <summary>
        /// Returns the workflow options.
        /// </summary>
        public WorkflowOptions Options { get; private set; }

        /// <summary>
        /// Returns the workflow type name.
        /// </summary>
        public string WorkflowTypeName { get; private set; }

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
                    throw new InvalidOperationException($"Workflow [{WorkflowTypeName}] has not been started.");
                }

                return execution;
            }
        }

        /// <summary>
        /// Starts the workflow, returning an <see cref="IAsyncFuture"/> that can be used
        /// to retrieve the workflow result.
        /// </summary>
        /// <param name="args">The workflow arguments.</param>
        /// <returns>An <see cref="IAsyncFuture"/> that can be used to retrieve the workflow result as an <c>object</c>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the workflow has already been started.</exception>
        /// <remarks>
        /// <note>
        /// <b>IMPORTANT:</b> You need to take care to ensure that the parameters passed
        /// are compatible with the target workflow arguments.
        /// </note>
        /// </remarks>
        public async Task<IAsyncFuture> StartAsync(params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            if (execution != null)
            {
                throw new InvalidOperationException("Cannot start a stub more than once.");
            }

            execution = await client.StartWorkflowAsync(WorkflowTypeName, client.DataConverter.ToData(args), Options);

            // Create and return the future.

            return new AsyncExternalWorkflowFuture(client, execution);
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
                        Domain     = Options.Domain,
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
                    Domain     = Options.Domain,
                    QueryName  = queryName,
                    QueryArgs  = client.DataConverter.ToData(args)
                });

            reply.ThrowOnError();

            return client.DataConverter.FromData<TQueryResult>(reply.Result);
        }
    }

    /// <summary>
    /// <para>
    /// Manages starting and signalling an external workflow instance based on
    /// its workflow type name and arguments.  This class separates workflow 
    /// execution and retrieving the result into separate operations.
    /// </para>
    /// <para>
    /// Use this version for workflows that return a result.
    /// </para>
    /// </summary>
    /// <typeparam name="TResult">Specifies the workflow result type.</typeparam>
    public class WorkflowFutureStub<TResult>
    {
        private CadenceClient       client;
        private WorkflowExecution   execution;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="client">The associated client.</param>
        /// <param name="workflowTypeName">The workflow type name.</param>
        /// <param name="options">Optional child workflow options.</param>
        internal WorkflowFutureStub(CadenceClient client, string workflowTypeName, WorkflowOptions options = null)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowTypeName), nameof(workflowTypeName));

            this.client           = client;
            this.WorkflowTypeName = workflowTypeName;
            this.Options          = WorkflowOptions.Normalize(client, options);
        }

        /// <summary>
        /// Returns the child workflow options.
        /// </summary>
        public WorkflowOptions Options { get; private set; }

        /// <summary>
        /// Returns the workflow type name.
        /// </summary>
        public string WorkflowTypeName { get; private set; }

        /// <summary>
        /// Returns the child workflow <see cref="WorkflowExecution"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the workflow has not been started.</exception>
        public WorkflowExecution Execution
        {
            get
            {
                if (execution == null)
                {
                    throw new InvalidOperationException($"Workflow [{WorkflowTypeName}] has not been started.");
                }

                return execution;
            }
        }

        /// <summary>
        /// Starts the workflow, returning an <see cref="IAsyncFuture{T}"/> that can be used
        /// to retrieve the workflow result.
        /// </summary>
        /// <param name="args">The workflow arguments.</param>
        /// <returns>An <see cref="IAsyncFuture{T}"/> that can be used to retrieve the workflow result.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the child workflow has already been started.</exception>
        /// <remarks>
        /// <note>
        /// <b>IMPORTANT:</b> You need to take care to ensure that the parameters and 
        /// result type passed are compatible with the target workflow arguments.
        /// </note>
        /// </remarks>
        public async Task<IAsyncFuture<TResult>> StartAsync(params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            if (execution != null)
            {
                throw new InvalidOperationException("Cannot start a stub more than once.");
            }

            execution = await client.StartWorkflowAsync(WorkflowTypeName, client.DataConverter.ToData(args), Options);

            // Create and return the future.

            return new AsyncExternalWorkflowFuture<TResult>(client, execution, typeof(TResult));
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
        /// are compatible with the target workflow signal arguments.
        /// </note>
        /// </remarks>
        public async Task SignalAsync(string signalName, params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(signalName), nameof(signalName));
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            if (Execution == null)
            {
                throw new InvalidOperationException("The stub must be started first.");
            }

            var reply = (WorkflowSignalReply)await client.CallProxyAsync(
                new WorkflowSignalRequest()
                {
                    WorkflowId = execution.WorkflowId,
                    RunId      = execution.RunId,
                    Domain     = Options.Domain,
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
                    Domain     = Options.Domain,
                    QueryName  = queryName,
                    QueryArgs  = client.DataConverter.ToData(args)
                });

            reply.ThrowOnError();

            return client.DataConverter.FromData<TQueryResult>(reply.Result);
        }
    }
}
