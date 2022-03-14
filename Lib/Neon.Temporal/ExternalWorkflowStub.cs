//-----------------------------------------------------------------------------
// FILE:	    ExternalWorkflowStub.cs
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

using Neon.Common;
using Neon.Tasks;
using Neon.Temporal;
using Neon.Temporal.Internal;

namespace Neon.Temporal
{
    /// <summary>
    /// Supports signalling and cancelling any workflow.  This is useful when an
    /// external workflow interface type is not known at compile time or to manage 
    /// workflows written in another language.
    /// </summary>
    public class ExternalWorkflowStub
    {
        //---------------------------------------------------------------------
        // Local types

        [ActivityInterface]
        private interface ILocalOperations : IActivity
        {
            /// <summary>
            /// Cancels the specified workflow.
            /// </summary>
            /// <param name="execution">The target workflow execution.</param>
            /// <returns>The tracking <see cref="Task"/>.</returns>
            Task CancelAsync(WorkflowExecution execution);

            /// <summary>
            /// Waits for the specified workflow to complete.
            /// </summary>
            /// <param name="execution">The target workflow execution.</param>
            /// <returns>The tracking <see cref="Task"/>.</returns>
            Task GetResultAsync(WorkflowExecution execution);

            /// <summary>
            /// Waits for the specified workflow to complete and then returns the
            /// workflow result.
            /// </summary>
            /// <param name="execution">The target workflow execution.</param>
            /// <returns>The workflow result.</returns>
            Task<byte[]> GetResultBytesAsync(WorkflowExecution execution);

            /// <summary>
            /// Signals the specified workflow.
            /// </summary>
            /// <param name="execution">The target workflow execution.</param>
            /// <param name="signalName">The signal name.</param>
            /// <param name="args">The signal arguments.</param>
            /// <returns>The tracking <see cref="Task"/>.</returns>
            Task SignalAsync(WorkflowExecution execution, string signalName, params object[] args);
        }

        private class LocalOperations : ActivityBase, ILocalOperations
        {
            public async Task CancelAsync(WorkflowExecution execution)
            {
                await SyncContext.Clear();
                await Activity.Client.CancelWorkflowAsync(execution);
            }

            public async Task GetResultAsync(WorkflowExecution execution)
            {
                await SyncContext.Clear();
                await Activity.Client.GetWorkflowResultAsync(execution);
            }

            public async Task<byte[]> GetResultBytesAsync(WorkflowExecution execution)
            {
                await SyncContext.Clear();
                return await Activity.Client.GetWorkflowResultAsync(execution);
            }

            public async Task SignalAsync(WorkflowExecution execution, string signalName, params object[] args)
            {
                await SyncContext.Clear();

                var dataConverter = Activity.Client.DataConverter;

                await Activity.Client.SignalWorkflowAsync(execution, signalName, TemporalHelper.ArgsToBytes(dataConverter, args));
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private Workflow        parentWorkflow;
        private TemporalClient  client;
        private string          @namespace;

        /// <summary>
        /// Internal constructor for use outside of a workflow.
        /// </summary>
        /// <param name="client">Specifies the associated client.</param>
        /// <param name="execution">Specifies the target workflow execution.</param>
        /// <param name="namespace">Optionally specifies the target namespace (defaults to the client's default namespace).</param>
        internal ExternalWorkflowStub(TemporalClient client, WorkflowExecution execution, string @namespace = null)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(execution != null, nameof(execution));

            this.client     = client;
            this.@namespace = client.ResolveNamespace(@namespace);
            this.Execution  = execution;
        }

        /// <summary>
        /// Internal constructor for use within a workflow.
        /// </summary>
        /// <param name="parentWorkflow">Specifies the parent workflow.</param>
        /// <param name="execution">Specifies the target workflow execution.</param>
        /// <param name="namespace">Optionally specifies the target namespace (defaults to the client's default namespace).</param>
        internal ExternalWorkflowStub(Workflow parentWorkflow, WorkflowExecution execution, string @namespace = null)
        {
            Covenant.Requires<ArgumentNullException>(parentWorkflow != null, nameof(parentWorkflow));
            Covenant.Requires<ArgumentNullException>(execution != null, nameof(execution));

            this.parentWorkflow = parentWorkflow;
            this.client         = parentWorkflow.Client;
            this.@namespace     = client.ResolveNamespace(@namespace);
            this.Execution      = execution;
        }

        /// <summary>
        /// Returns the workflow execution.
        /// </summary>
        public WorkflowExecution Execution { get; private set; }

        /// <summary>
        /// Cancels the workflow.
        /// </summary>
        public async Task CancelAsync()
        {
            await SyncContext.Clear();

            if (parentWorkflow != null)
            {
                var stub = parentWorkflow.NewLocalActivityStub<ILocalOperations, LocalOperations>();

                await stub.CancelAsync(Execution);
            }
            else
            {
                await client.CancelWorkflowAsync(Execution, @namespace);
            }
        }

        /// <summary>
        /// Signals the workflow.
        /// </summary>
        /// <param name="signalName">Specifies the signal name.</param>
        /// <param name="args">Specifies the signal arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task SignalAsync(string signalName, params object[] args)
        {
            await SyncContext.Clear();
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(signalName), nameof(signalName));
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            if (parentWorkflow != null)
            {
                var stub = parentWorkflow.NewLocalActivityStub<ILocalOperations, LocalOperations>();

                await stub.SignalAsync(Execution, signalName, args);
            }
            else
            {
                await client.SignalWorkflowAsync(Execution, signalName, TemporalHelper.ArgsToBytes(client.DataConverter, args));
            }
        }

        /// <summary>
        /// Waits for the workflow complete if necessary, without returning the result.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task GetResultAsync()
        {
            await SyncContext.Clear();

            if (parentWorkflow != null)
            {
                var stub = parentWorkflow.NewLocalActivityStub<ILocalOperations, LocalOperations>();

                await stub.GetResultAsync(Execution);
            }
            else
            {
                await client.GetWorkflowResultAsync(Execution, @namespace);
            }
        }

        /// <summary>
        /// Returns the workflow result, waiting for the workflow to complete if necessary.
        /// </summary>
        /// <typeparam name="TResult">The workflow result type.</typeparam>
        /// <returns>The workflow result.</returns>
        public async Task<TResult> GetResultAsync<TResult>()
        {
            await SyncContext.Clear();

            if (parentWorkflow != null)
            {
                var stub  = parentWorkflow.NewLocalActivityStub<ILocalOperations, LocalOperations>();
                var bytes = await stub.GetResultBytesAsync(Execution);

                return client.DataConverter.FromData<TResult>(bytes);
            }
            else
            {
                return client.DataConverter.FromData<TResult>(await client.GetWorkflowResultAsync(Execution, @namespace));
            }
        }
    }
}
