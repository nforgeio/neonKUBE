//-----------------------------------------------------------------------------
// FILE:	    ChildWorkflowFutureStub.cs
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
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Tasks;

// $todo(jefflill):
//
// It would be nice to add query methods to this like we did for external
// workflows.  Note that queries will need to be run within local activities
// so that they'll replay from history correctly.
//
//      https://github.com/nforgeio/neonKUBE/issues/617

namespace Neon.Cadence
{
    /// <summary>
    /// <para>
    /// Manages starting and signalling a child workflow instance based on
    /// its workflow type name and arguments.  This is useful when you need
    /// to perform other operations in parallel with a child by separating
    /// workflow execution and retrieving the result into separate operations.
    /// </para>
    /// <para>
    /// Use this version for workflows that don't return a result.
    /// </para>
    /// </summary>
    public class ChildWorkflowFutureStub
    {
        private Workflow            parentWorkflow;
        private CadenceClient       client;
        private ChildExecution      childExecution;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="parentWorkflow">The parent workflow.</param>
        /// <param name="workflowTypeName">The workflow type name.</param>
        /// <param name="options">Optional child workflow options.</param>
        internal ChildWorkflowFutureStub(Workflow parentWorkflow, string workflowTypeName, ChildWorkflowOptions options = null)
        {
            Covenant.Requires<ArgumentNullException>(parentWorkflow != null, nameof(parentWorkflow));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowTypeName), nameof(workflowTypeName));

            this.parentWorkflow   = parentWorkflow;
            this.client           = parentWorkflow.Client;
            this.WorkflowTypeName = workflowTypeName;
            this.Options          = ChildWorkflowOptions.Normalize(client, options);
        }

        /// <summary>
        /// Returns the child workflow options.
        /// </summary>
        public ChildWorkflowOptions Options { get; private set; }

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
                if (this.childExecution == null)
                {
                    throw new InvalidOperationException($"Workflow [{WorkflowTypeName}] has not been started.");
                }

                return childExecution.Execution;
            }
        }

        /// <summary>
        /// Starts the child workflow, returning an <see cref="IAsyncFuture"/> that can be used
        /// to wait for the workflow to complete.  This version doesn't return a workflow result.
        /// </summary>
        /// <param name="args">The workflow arguments.</param>
        /// <returns>An <see cref="ChildWorkflowFuture"/> that can be used to retrieve the workflow result as an <c>object</c>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the child workflow has already been started.</exception>
        /// <remarks>
        /// <note>
        /// <b>IMPORTANT:</b> You need to take care to ensure that the parameters passed
        /// are compatible with the target workflow arguments.
        /// </note>
        /// </remarks>
        public async Task<ChildWorkflowFuture> StartAsync(params object[] args)
        {
            await SyncContext.Clear();
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            if (childExecution != null)
            {
                throw new InvalidOperationException("Cannot start a future stub more than once.");
            }

            childExecution = await client.StartChildWorkflowAsync(parentWorkflow, WorkflowTypeName, CadenceHelper.ArgsToBytes(client.DataConverter, args), Options);

            // Create and return the future.

            return new ChildWorkflowFuture(parentWorkflow, childExecution);
        }

        /// <summary>
        /// Starts the child workflow, returning an <see cref="IAsyncFuture"/> that can be used
        /// to wait for the the workflow to complete and obtain its result.
        /// </summary>
        /// <typeparam name="TResult">The workflow result type.</typeparam>
        /// <param name="args">The workflow arguments.</param>
        /// <returns>An <see cref="ChildWorkflowFuture{TResult}"/> that can be used to retrieve the workflow result as an <c>object</c>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the child workflow has already been started.</exception>
        /// <remarks>
        /// <note>
        /// <b>IMPORTANT:</b> You need to take care to ensure that the parameters passed
        /// are compatible with the target workflow arguments.
        /// </note>
        /// </remarks>
        public async Task<ChildWorkflowFuture<TResult>> StartAsync<TResult>(params object[] args)
        {
            await SyncContext.Clear();
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            if (childExecution != null)
            {
                throw new InvalidOperationException("Cannot start a future stub more than once.");
            }

            childExecution = await client.StartChildWorkflowAsync(parentWorkflow, WorkflowTypeName, CadenceHelper.ArgsToBytes(client.DataConverter, args), Options);

            // Create and return the future.

            return new ChildWorkflowFuture<TResult>(parentWorkflow, childExecution);
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
            await SyncContext.Clear();
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(signalName), nameof(signalName));
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            if (Execution == null)
            {
                throw new InvalidOperationException("The stub must be started first.");
            }

            var reply = await parentWorkflow.ExecuteNonParallel(
                async () =>
                {
                    return (WorkflowSignalChildReply) await client.CallProxyAsync(
                        new WorkflowSignalChildRequest()
                        {
                             ContextId  = parentWorkflow.ContextId,
                             ChildId    = childExecution.ChildId,
                             SignalName = signalName,
                             SignalArgs = CadenceHelper.ArgsToBytes(client.DataConverter, args)
                        });
                });

            reply.ThrowOnError();
        }
    }

    /// <summary>
    /// <para>
    /// Manages starting and signalling a child workflow instance based on
    /// its workflow type name and arguments.  This is useful when you need
    /// to perform other operations in parallel with a child.
    /// </para>
    /// <para>
    /// Use this version for workflows that return a result.
    /// </para>
    /// </summary>
    /// <typeparam name="TResult">Specifies the workflow result type.</typeparam>
    public class UntypedChildWorkflowFutureStub<TResult>
    {
        private Workflow            parentWorkflow;
        private CadenceClient       client;
        private ChildExecution      childExecution;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="parentWorkflow">The parent workflow.</param>
        /// <param name="workflowTypeName">The workflow type name.</param>
        /// <param name="options">Optional child workflow options.</param>
        internal UntypedChildWorkflowFutureStub(Workflow parentWorkflow, string workflowTypeName, ChildWorkflowOptions options = null)
        {
            Covenant.Requires<ArgumentNullException>(parentWorkflow != null, nameof(parentWorkflow));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowTypeName), nameof(workflowTypeName));

            this.parentWorkflow   = parentWorkflow;
            this.client           = parentWorkflow.Client;
            this.WorkflowTypeName = workflowTypeName;
            this.Options          = ChildWorkflowOptions.Normalize(client, options);

            if (string.IsNullOrEmpty(Options.Domain))
            {
                Options.Domain = parentWorkflow.WorkflowInfo.Domain;
            }
        }

        /// <summary>
        /// Returns the child workflow options.
        /// </summary>
        public ChildWorkflowOptions Options { get; private set; }

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
                if (childExecution == null)
                {
                    throw new InvalidOperationException($"Workflow [{WorkflowTypeName}] has not been started.");
                }

                return childExecution.Execution;
            }
        }

        /// <summary>
        /// Starts the child workflow, returning an <see cref="ChildWorkflowFuture{T}"/> that can be used
        /// to retrieve the workflow result.
        /// </summary>
        /// <param name="args">The workflow arguments.</param>
        /// <returns>An <see cref="ChildWorkflowFuture{T}"/> that can be used to retrieve the workflow result.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the child workflow has already been started.</exception>
        /// <remarks>
        /// <note>
        /// <b>IMPORTANT:</b> You need to take care to ensure that the parameters and 
        /// result type passed are compatible with the target workflow arguments.
        /// </note>
        /// </remarks>
        public async Task<ChildWorkflowFuture<TResult>> StartAsync(params object[] args)
        {
            await SyncContext.Clear();
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            if (childExecution != null)
            {
                throw new InvalidOperationException("Cannot start a future stub more than once.");
            }

            childExecution = await client.StartChildWorkflowAsync(parentWorkflow, WorkflowTypeName, CadenceHelper.ArgsToBytes(client.DataConverter, args), Options);

            // Create and return the future.

            return new ChildWorkflowFuture<TResult>(parentWorkflow, childExecution);
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
            await SyncContext.Clear();
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(signalName), nameof(signalName));
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            if (Execution == null)
            {
                throw new InvalidOperationException("The stub must be started first.");
            }

            var reply = await parentWorkflow.ExecuteNonParallel(
                async () =>
                {
                    return (WorkflowSignalChildReply) await client.CallProxyAsync(
                        new WorkflowSignalChildRequest()
                        {
                             ContextId  = parentWorkflow.ContextId,
                             ChildId    = childExecution.ChildId,
                             SignalName = signalName,
                             SignalArgs = CadenceHelper.ArgsToBytes(client.DataConverter, args)
                        });
                });

            reply.ThrowOnError();
        }
    }
}
