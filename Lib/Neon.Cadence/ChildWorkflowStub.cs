//-----------------------------------------------------------------------------
// FILE:	    ChildWorkflowStub.cs
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
    /// Manages starting and signalling a child workflow instance based on
    /// its workflow type name and arguments.  This is useful when the child
    /// workflow type is not known at compile time as well provinding a way
    /// to call child workflows written in another language.
    /// </para>
    /// <para>
    /// Use this version for workflows that don't return a result.
    /// </para>
    /// </summary>
    public class ChildWorkflowStub
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
        internal ChildWorkflowStub(Workflow parentWorkflow, string workflowTypeName, ChildWorkflowOptions options = null)
        {
            Covenant.Requires<ArgumentNullException>(parentWorkflow != null, nameof(parentWorkflow));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowTypeName), nameof(workflowTypeName));

            this.parentWorkflow   = parentWorkflow;
            this.client           = parentWorkflow.Client;
            this.WorkflowTypeName = workflowTypeName;
            this.Options          = ChildWorkflowOptions.Normalize(client, options);

            if (string.IsNullOrEmpty(options.Domain))
            {
                options.Domain = parentWorkflow.WorkflowInfo.Domain;
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
                if (this.childExecution != null)
                {
                    throw new InvalidOperationException($"Workflow[{ WorkflowTypeName }] has not been started.");
                }

                return childExecution.Execution;
            }
        }

        /// <summary>
        /// Starts the child workflow.
        /// </summary>
        /// <param name="args">The workflow arguments.</param>
        /// <returns>An <see cref="IAsyncFuture"/> that can be used to retrieve the workflow result as an <c>object</c>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the child workflow has already been started.</exception>
        public async Task<IAsyncFuture> ExecuteAsync(params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            if (Execution != null)
            {
                throw new InvalidOperationException("Cannot start a stub more than once.");
            }

            childExecution = await client.StartChildWorkflowAsync(parentWorkflow, WorkflowTypeName, client.DataConverter.ToData(args), Options);

            // Create and return the future.

            return new AsyncChildFuture(parentWorkflow, childExecution);
        }

        /// <summary>
        /// Signals the workflow.
        /// </summary>
        /// <param name="signalName">The signal name.</param>
        /// <param name="args">The signal arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the child workflow has not been started.</exception>
        public async Task SignalAsync(string signalName, params object[] args)
        {
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
                             ChildId    = childExecution.ChildId,
                             SignalName = signalName,
                             SignalArgs = client.DataConverter.ToData(args)
                        });
                });

            reply.ThrowOnError();
        }
    }

    /// <summary>
    /// <para>
    /// Manages starting and signalling a child workflow instance based on
    /// its workflow type name and arguments.  This is useful when the child
    /// workflow type is not known at compile time as well provinding a way
    /// to call child workflows written in another language.
    /// </para>
    /// <para>
    /// Use this version for workflows that return a result.
    /// </para>
    /// </summary>
    /// <typeparam name="TResult">Specifies the workflow result type.</typeparam>
    public class ChildWorkflowStub<TResult>
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
        internal ChildWorkflowStub(Workflow parentWorkflow, string workflowTypeName, ChildWorkflowOptions options = null)
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
                    throw new InvalidOperationException($"Workflow[{ WorkflowTypeName }] has not been started.");
                }

                return childExecution.Execution;
            }
        }

        /// <summary>
        /// Starts the child workflow.
        /// </summary>
        /// <param name="args">The workflow arguments.</param>
        /// <returns>An <see cref="IAsyncFuture{T}"/> that can be used to retrieve the workflow result.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the child workflow has already been started.</exception>
        public async Task<IAsyncFuture<TResult>> ExecuteAsync(params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            if (childExecution != null)
            {
                throw new InvalidOperationException("Cannot start a stub more than once.");
            }

            childExecution = await client.StartChildWorkflowAsync(parentWorkflow, WorkflowTypeName, client.DataConverter.ToData(args), Options);

            // Create and return the future.

            return new AsyncChildFuture<TResult>(parentWorkflow, childExecution, typeof(TResult));
        }

        /// <summary>
        /// Signals the workflow.
        /// </summary>
        /// <param name="signalName">The signal name.</param>
        /// <param name="args">The signal arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the child workflow has not been started.</exception>
        public async Task SignalAsync(string signalName, params object[] args)
        {
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
                             ChildId    = childExecution.ChildId,
                             SignalName = signalName,
                             SignalArgs = client.DataConverter.ToData(args)
                        });
                });

            reply.ThrowOnError();
        }
    }
}
