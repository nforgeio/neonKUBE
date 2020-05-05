//-----------------------------------------------------------------------------
// FILE:	    WorkflowStub.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
    /// Implements an untyped client side stub to a single external workflow instance.  This can 
    /// be used to invoke, signal, query, and cancel a workflow when the actual workflow 
    /// interface isn't available.
    /// </summary>
    public class WorkflowStub
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Defines the helper methods used to perform external stub operations
        /// as a local activity so they can be replayed from history.
        /// </summary>
        /// <remarks>
        /// <note>
        /// These methods are going to use byte arrays to receive arguments from
        /// the caller and also to return results.  These will use the current
        /// data converter for encoding.
        /// </note>
        /// </remarks>
        private interface IHelperActivity : IActivity
        {
            /// <summary>
            /// Cancels an external workflow.
            /// </summary>
            /// <param name="workflowId">The target workflow ID.</param>
            /// <param name="runId">The target runID.</param>
            /// <returns>The tracking <see cref="Task"/>.</returns>
            Task CancelAsync(string workflowId, string runId);

            /// <summary>
            /// Waits for and returns the result for an external workflow.
            /// </summary>
            /// <param name="workflowId">The target workflow ID.</param>
            /// <param name="runId">The target runID.</param>
            /// <returns>The encoded workflow result.</returns>
            Task<byte[]> GetResultAsync(string workflowId, string runId);

            /// <summary>
            /// Queries an external workflow.
            /// </summary>
            /// <param name="workflowId">The target workflow ID.</param>
            /// <param name="runId">The target runID.</param>
            /// <param name="queryType">Identifies the query.</param>
            /// <param name="args">The encoded query arguments.</param>
            /// <returns>The encode query result.</returns>
            Task<byte[]> QueryAsync(string workflowId, string runId, string queryType, byte[] args);

            /// <summary>
            /// Signals an external workflow.
            /// </summary>
            /// <param name="workflowId">The target workflow ID.</param>
            /// <param name="runId">The target runID.</param>
            /// <param name="signalName">Identifies the signal.</param>
            /// <param name="args">The encoded signal arguments.</param>
            /// <returns>The tracking <see cref="Task"/>.</returns>
            Task SignalAsync(string workflowId, string runId, string signalName, byte[] args);
        }

        /// <summary>
        /// Implements <see cref="IHelperActivity"/>.
        /// </summary>
        private class HelperActivity : ActivityBase, IHelperActivity
        {
            public async Task CancelAsync(string workflowId, string runId)
            {
                var stub = Activity.Client.NewUntypedWorkflowStub(workflowId, runId);

                await stub.CancelAsync();
            }

            public async Task<byte[]> GetResultAsync(string workflowId, string runId)
            {
                var stub = Activity.Client.NewUntypedWorkflowStub(workflowId, runId);

                return await stub.GetResultAsync<byte[]>();
            }

            public async Task<byte[]> QueryAsync(string workflowId, string runId, string queryType, byte[] args)
            {
                var stub = Activity.Client.NewUntypedWorkflowStub(workflowId, runId);

                return await stub.QueryAsync<byte[]>(queryType, args);
            }

            public async Task SignalAsync(string workflowId, string runId, string signalName, byte[] args)
            {
                var stub = Activity.Client.NewUntypedWorkflowStub(workflowId, runId);

                await stub.SignalAsync(signalName, args);
            }
        }

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// <para>
        /// Returns the untyped <see cref="WorkflowStub"/> from a typed stub.
        /// </para>
        /// <note>
        /// This works only for external workflow stubs (not child stubs) and only for
        /// stubs that have already been started.
        /// </note>
        /// </summary>
        /// <param name="stub">The source typed workflow stub.</param>
        /// <returns>The <see cref="WorkflowStub"/>.</returns>
        public static async Task<WorkflowStub> FromTypedAsync(object stub)
        {
            Covenant.Requires<ArgumentNullException>(stub != null, nameof(stub));
            Covenant.Requires<ArgumentException>(stub is ITypedWorkflowStub, nameof(stub), $"[{stub.GetType().FullName}] is not a typed workflow stub.");

            return await ((ITypedWorkflowStub)stub).ToUntypedAsync();
        }

        //---------------------------------------------------------------------
        // Instance members

        private CadenceClient   client;
        private bool            withinWorkflow;

        /// <summary>
        /// Default internal constructor.
        /// </summary>
        /// <param name="client">The associated client.</param>
        /// <param name="withinWorkflow">
        /// Optionally indicates that the stub was created from within a workflow and that 
        /// operations such as get result, query, signal, and cancel must be performed
        /// within local activities such that that can be replayed from history correctly.
        /// </param>
        internal WorkflowStub(CadenceClient client, bool withinWorkflow = false)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));

            this.client         = client;
            this.withinWorkflow = withinWorkflow;
        }

        /// <summary>
        /// Used to construct an untyped workflow stub that can be used to start an external workflow.
        /// </summary>
        /// <param name="client">The associated client.</param>
        /// <param name="workflowTypeName">The workflow type name.</param>
        /// <param name="execution">The workflow execution.</param>
        /// <param name="options">The workflow options.</param>
        internal WorkflowStub(CadenceClient client, string workflowTypeName, WorkflowExecution execution, WorkflowOptions options)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowTypeName));
            Covenant.Requires<ArgumentNullException>(execution != null);
            Covenant.Requires<ArgumentNullException>(options != null);

            this.client           = client;
            this.WorkflowTypeName = workflowTypeName;
            this.Execution        = execution;
            this.Options          = options;
            this.withinWorkflow   = false;
        }

        /// <summary>
        /// Used to construct an untyped workflow stub that can manage an existing external workflow.
        /// </summary>
        /// <param name="client">The associated client.</param>
        /// <param name="execution">The workflow execution.</param>
        /// <param name="withinWorkflow">
        /// Optionally indicates that the stub was created from within a workflow and that 
        /// operations such as get result, query, signal, and cancel must be performed
        /// within local activities such that that can be replayed from history correctly.
        /// </param>
        internal WorkflowStub(CadenceClient client, WorkflowExecution execution, bool withinWorkflow = false)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(execution != null);

            this.client         = client;
            this.Execution      = execution;
            this.withinWorkflow = withinWorkflow;
        }

        /// <summary>
        /// <para>
        /// Returns the workflow type name.
        /// </para>
        /// <note>
        /// <para>
        /// .NET and Java workflows can implement multiple workflow method using attributes
        /// and annotations to assign unique names to each.  Each workflow method is actually
        /// registered with Cadence as a distinct workflow type.  Workflow methods with a blank
        /// or <c>null</c> name will simply be registered using the workflow type name.
        /// </para>
        /// <para>
        /// Workflow methods with a name will be registered using a combination  of the workflow
        /// type name and the method name, using <b>"::"</b> as the separator, like:
        /// </para>
        /// <code>
        /// WORKFLOW-TYPENAME::METHOD-NAME
        /// </code>
        /// </note>
        /// </summary>
        public string WorkflowTypeName { get; internal set; }

        /// <summary>
        /// Returns the workflow execution.
        /// </summary>
        public WorkflowExecution Execution { get; internal set; }

        /// <summary>
        /// Returns the workflow options.
        /// </summary>
        public WorkflowOptions Options { get; internal set; }

        /// <summary>
        /// Ensures that the workflow has been started.
        /// </summary>
        private void EnsureStarted()
        {
            if (Execution == null)
            {
                throw new InvalidOperationException($"Workflow stub for workflow type name [{WorkflowTypeName}] has not been started.");
            }
        }

        /// <summary>
        /// Ensures that the workflow has not been started.
        /// </summary>
        private void EnsureNotStarted()
        {
            if (Execution != null)
            {
                throw new InvalidOperationException($"Workflow stub for workflow type name [{WorkflowTypeName}] has already been started.");
            }
        }

        /// <summary>
        /// Attempts to cancel the associated workflow.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task CancelAsync()
        {
            await SyncContext.ClearAsync;
            EnsureStarted();

            if (Execution == null)
            {
                throw new InvalidOperationException("The stub can't cancel the workflow because it doesn't have the workflow execution.");
            }

            await client.CancelWorkflowAsync(Execution, client.ResolveDomain(Options?.Domain));
        }

        /// <summary>
        /// Waits for the workflow to complete or throws an error exception.  Use this for 
        /// workflows that don't return a result.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task GetResultAsync()
        {
            await SyncContext.ClearAsync;
            EnsureStarted();

            if (Execution == null)
            {
                throw new InvalidOperationException("The stub can't obtain the workflow result because it doesn't have the workflow execution.");
            }

            await client.GetWorkflowResultAsync(Execution, client.ResolveDomain(Options?.Domain));
        }

        /// <summary>
        /// Waits for the workflow to complete and then returns the result or throws
        /// an error exception.  This override accepts the result type as a type parameter.
        /// </summary>
        /// <typeparam name="TResult">The result type.</typeparam>
        /// <returns>The result.</returns>
        public async Task<TResult> GetResultAsync<TResult>()
        {
            await SyncContext.ClearAsync;
            EnsureStarted();

            if (Execution == null)
            {
                throw new InvalidOperationException("The stub can't obtain the workflow result because it doesn't have the workflow execution.");
            }

            return client.DataConverter.FromData<TResult>(await client.GetWorkflowResultAsync(Execution, client.ResolveDomain(Options?.Domain)));
        }

        /// <summary>
        /// Waits for the workflow to complete and then returns the result or throws
        /// an error exception.  This override accepts the result type as a normal parameter.
        /// </summary>
        /// <param name="resultType">Specifies the result type.</param>
        /// <returns>The result as a <c>dynamic</c>.</returns>
        public async Task<object> GetResultAsync(Type resultType)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(resultType != null, nameof(resultType));
            EnsureStarted();

            if (Execution == null)
            {
                throw new InvalidOperationException("The stub can't obtain the workflow result because it doesn't have the workflow execution.");
            }

            return client.DataConverter.FromData(resultType, await client.GetWorkflowResultAsync(Execution, client.ResolveDomain(Options?.Domain)));
        }

        /// <summary>
        /// Queries the associated workflow.
        /// </summary>
        /// <typeparam name="TResult">The query result type.</typeparam>
        /// <param name="queryType">Specifies the query type.</param>
        /// <param name="args">Specifies the query arguments.</param>
        /// <returns>The query result.</returns>
        public async Task<TResult> QueryAsync<TResult>(string queryType, params object[] args)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(queryType), nameof(queryType));
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));
            EnsureStarted();

            if (Execution == null)
            {
                throw new InvalidOperationException("Query cannot be sent because the stub doesn't have the workflow execution.");
            }

            var argBytes = CadenceHelper.ArgsToBytes(client.DataConverter, args);

            return client.DataConverter.FromData<TResult>(await client.QueryWorkflowAsync(Execution, queryType, argBytes, client.ResolveDomain(Options?.Domain)));
        }

        /// <summary>
        ///  Queries the associated workflow specifying the expected result type as
        ///  a parameter.
        /// </summary>
        /// <param name="resultType">Specifies the query result type.</param>
        /// <param name="queryType">Specifies the query type.</param>
        /// <param name="args">Specifies the query arguments.</param>
        /// <returns>The query result as a <c>dynamic</c>.</returns>
        public async Task<object> QueryAsync(Type resultType, string queryType, params object[] args)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(queryType), nameof(queryType));
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));
            EnsureStarted();

            if (Execution == null)
            {
                throw new InvalidOperationException("Query cannot be sent because the stub doesn't have the workflow execution.");
            }

            var argBytes = CadenceHelper.ArgsToBytes(client.DataConverter, args);

            return client.DataConverter.FromData(resultType, await client.QueryWorkflowAsync(Execution, queryType, argBytes, client.ResolveDomain(Options?.Domain)));
        }

        /// <summary>
        /// Signals the associated workflow.
        /// </summary>
        /// <param name="signalName">Specifies the signal name.</param>
        /// <param name="args">Specifies the signal arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task SignalAsync(string signalName, params object[] args)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(signalName), nameof(signalName));
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));
            EnsureStarted();

            if (Execution == null)
            {
                throw new InvalidOperationException("Signal cannot be sent because the stub doesn't have the workflow execution.");
            }

            var argBytes = CadenceHelper.ArgsToBytes(client.DataConverter, args);

            await client.SignalWorkflowAsync(Execution, signalName, argBytes, client.ResolveDomain(Options?.Domain));
        }

        /// <summary>
        /// Signals the associated workflow, starting it if it hasn't already been started.
        /// </summary>
        /// <param name="signalName">Specifies the signal name.</param>
        /// <param name="signalArgs">Specifies the signal arguments.</param>
        /// <param name="startArgs">Specifies the workflow start arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task<WorkflowExecution> SignalWithStartAsync(string signalName, object[] signalArgs, object[] startArgs)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(signalName), nameof(signalName));
            Covenant.Requires<ArgumentNullException>(signalArgs != null, nameof(signalArgs));
            Covenant.Requires<ArgumentNullException>(startArgs != null, nameof(startArgs));

            var signalArgBytes = CadenceHelper.ArgsToBytes(client.DataConverter, signalArgs);
            var startArgBytes  = CadenceHelper.ArgsToBytes(client.DataConverter, startArgs);

            return await client.SignalWorkflowWithStartAsync(this.WorkflowTypeName, signalName, signalArgBytes, startArgBytes, this.Options);
        }

        /// <summary>
        /// Starts the associated workflow.
        /// </summary>
        /// <param name="args">The workflow arguments.</param>
        /// <returns>The <see cref="WorkflowExecution"/>.</returns>
        public async Task<WorkflowExecution> StartAsync(params object[] args)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));
            EnsureNotStarted();

            var argBytes = CadenceHelper.ArgsToBytes(client.DataConverter, args);

            Execution = await client.StartWorkflowAsync(WorkflowTypeName, argBytes, Options);

            return Execution;
        }

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> Used internally for unit tests that need to control
        /// how the workflow arguments are encoded.
        /// </summary>
        /// <param name="argBytes">The encoded workflow arguments.</param>
        /// <returns>The <see cref="WorkflowExecution"/>.</returns>
        internal async Task<WorkflowExecution> StartAsync(byte[] argBytes)
        {
            await SyncContext.ClearAsync;
            EnsureNotStarted();

            Execution = await client.StartWorkflowAsync(WorkflowTypeName, argBytes, Options);

            return Execution;
        }

        /// <summary>
        /// Executes the associated workflow and waits for it to complete.
        /// </summary>
        /// <param name="args">The workflow arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task ExecutesAsync(params object[] args)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));
            EnsureNotStarted();

            var argBytes = CadenceHelper.ArgsToBytes(client.DataConverter, args);

            Execution = await client.StartWorkflowAsync(WorkflowTypeName, argBytes, Options);

            await GetResultAsync();
        }

        /// <summary>
        /// Executes the associated workflow and waits for it to complete,
        /// returning the workflow result.
        /// </summary>
        /// <typeparam name="TResult">The workflow result type.</typeparam>
        /// <param name="args">The workflow arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task<TResult> ExecutesAsync<TResult>(params object[] args)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));
            EnsureNotStarted();

            var argBytes = CadenceHelper.ArgsToBytes(client.DataConverter, args);

            Execution = await client.StartWorkflowAsync(WorkflowTypeName, argBytes, Options);

            return await GetResultAsync<TResult>();
        }
    }
}
