//-----------------------------------------------------------------------------
// FILE:	    WorkflowFutureStub.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
            var method            = CadenceHelper.GetWorkflowMethod(workflowInterface, methodName);

            CadenceHelper.ValidateWorkflowInterface(workflowInterface);

            this.client           = client;
            this.workflowTypeName = CadenceHelper.GetWorkflowTarget(workflowInterface, methodName).WorkflowTypeName;
            this.options          = WorkflowOptions.Normalize(client, options, workflowInterface, method);
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
                    throw new InvalidOperationException("Workflow has not been started.");
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
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            if (execution != null)
            {
                throw new InvalidOperationException("Cannot start a future stub more than once.");
            }

            execution = await client.StartWorkflowAsync(workflowTypeName, CadenceHelper.ArgsToBytes(client.DataConverter, args), options);

            // Create and return the future.

            return new ExternalWorkflowFuture(client, execution, options.Domain);
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
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            if (execution != null)
            {
                throw new InvalidOperationException("Cannot start a future stub more than once.");
            }

            execution = await client.StartWorkflowAsync(workflowTypeName, CadenceHelper.ArgsToBytes(client.DataConverter, args), options);

            // Create and return the future.

            return new ExternalWorkflowFuture<TResult>(client, execution, options.Domain);
        }

        /// <summary>
        /// Signals the workflow.
        /// </summary>
        /// <param name="signalName">
        /// The signal name as defined by the <see cref="SignalMethodAttribute"/>
        /// decorating the workflow signal method.
        /// </param>
        /// <param name="args">The signal arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the child workflow has not been started.</exception>
        /// <remarks>
        /// <note>
        /// <b>IMPORTANT:</b> You need to take care to ensure that the parameters passed
        /// are compatible with the target workflow arguments.  No compile-time type checking
        /// is performed for this method.
        /// </note>
        /// </remarks>
        public async Task SignalAsync(string signalName, params object[] args)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(signalName), nameof(signalName));
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            if (execution == null)
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
                    SignalArgs = CadenceHelper.ArgsToBytes(client.DataConverter, args)
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// <b>EXPERIMENTAL:</b> This method synchronously signals the workflow and returns
        /// only after the workflow has processed received and processed the signal as opposed
        /// to <see cref="SignalAsync"/> which is fire-and-forget and does not wait for the
        /// signal to be processed.
        /// </summary>
        /// <param name="signalName">
        /// The signal name as defined by the <see cref="SignalMethodAttribute"/>
        /// decorating the workflow signal method.
        /// </param>
        /// <param name="args">The signal arguments.</param>
        /// <remarks>
        /// <note>
        /// <b>IMPORTANT:</b> You need to take care to ensure that the parameters passed
        /// are compatible with the target workflow arguments.  No compile-time type checking
        /// is performed for this method.
        /// </note>
        /// </remarks>
        public async Task SyncSignalAsync(string signalName, params object[] args)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(signalName), nameof(signalName));
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            if (execution == null)
            {
                throw new InvalidOperationException("The stub must be started first.");
            }

            var signalId        = Guid.NewGuid().ToString("d");
            var argBytes        = CadenceHelper.ArgsToBytes(client.DataConverter, args);
            var signalCall      = new SyncSignalCall(signalName, signalId, argBytes);
            var signalCallBytes = CadenceHelper.ArgsToBytes(client.DataConverter, new object[] { signalCall });

            await client.SyncSignalWorkflowAsync(execution, signalName, signalId, signalCallBytes, options.Domain);
        }

        /// <summary>
        /// <b>EXPERIMENTAL:</b> This method synchronously signals the workflow and returns
        /// the signal result only after the workflow has processed received and processed the 
        /// signal as opposed to <see cref="SignalAsync"/> which is fire-and-forget and does 
        /// not wait for the signal to be processed and cannot return a result.
        /// </summary>
        /// <typeparam name="TResult">The signal result type.</typeparam>
        /// <param name="signalName">
        /// The signal name as defined by the <see cref="SignalMethodAttribute"/>
        /// decorating the workflow signal method.
        /// </param>
        /// <param name="args">The signal arguments.</param>
        /// <returns>The signal result.</returns>
        /// <remarks>
        /// <note>
        /// <b>IMPORTANT:</b> You need to take care to ensure that the parameters passed
        /// are compatible with the target workflow arguments.  No compile-time type checking
        /// is performed for this method.
        /// </note>
        /// </remarks>
        public async Task<TResult> SyncSignalAsync<TResult>(string signalName, params object[] args)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(signalName), nameof(signalName));
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            if (execution == null)
            {
                throw new InvalidOperationException("The stub must be started first.");
            }

            var signalId        = Guid.NewGuid().ToString("d");
            var argBytes        = CadenceHelper.ArgsToBytes(client.DataConverter, args);
            var signalCall      = new SyncSignalCall(signalName, signalId, argBytes);
            var signalCallBytes = CadenceHelper.ArgsToBytes(client.DataConverter, new object[] { signalCall });
            var resultBytes     = await client.SyncSignalWorkflowAsync(execution, signalName, signalId, signalCallBytes, options.Domain);

            return client.DataConverter.FromData<TResult>(resultBytes);
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
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(queryName), nameof(queryName));
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            if (execution == null)
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
                    QueryArgs  = CadenceHelper.ArgsToBytes(client.DataConverter, args)
                });

            reply.ThrowOnError();

            return client.DataConverter.FromData<TQueryResult>(reply.Result);
        }
    }
}
