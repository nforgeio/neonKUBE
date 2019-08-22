//-----------------------------------------------------------------------------
// FILE:	    WorkflowStub.cs
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

namespace Neon.Cadence
{
    /// <summary>
    /// Implements an untyped client side stub to a single workflow instance.  This can 
    /// be used to invoke, signal, query, and cancel a workflow when the actual workflow 
    /// interface isn't available.
    /// </summary>
    public class WorkflowStub
    {
        /// <summary>
        /// Returns the untyped <see cref="WorkflowStub"/> from a typed stub.
        /// </summary>
        /// <param name="typedStub">The source workflow stub.</param>
        /// <returns>The <see cref="WorkflowStub"/>.</returns>
        public static WorkflowStub FromTyped(object typedStub)
        {
            Covenant.Requires<ArgumentNullException>(typedStub != null);
            Covenant.Requires<ArgumentException>(typedStub is ITypedWorkflowStub, $"[{typedStub.GetType().FullName}] is not a typed workflow stub.");

            return ((ITypedWorkflowStub)typedStub).ToUntyped();
        }

        //---------------------------------------------------------------------
        // Instance members

        private CadenceClient   client;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="client">The associated client.</param>
        internal WorkflowStub(CadenceClient client)
        {
            Covenant.Requires<ArgumentNullException>(client != null);

            this.client = client;
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
            EnsureStarted();

            if (Execution == null)
            {
                throw new InvalidOperationException("The stub can't cancel the workflow because it doesn't have the workflow execution.");
            }

            await client.CancelWorkflowAsync(Execution, client.ResolveDomain(Options?.Domain));
        }

        /// <summary>
        /// Attempts to retrieve the associated workflow result.
        /// </summary>
        /// <typeparam name="TResult">The result type.</typeparam>
        /// <returns>The result.</returns>
        public async Task<TResult> GetResultAsync<TResult>()
        {
            EnsureStarted();

            if (Execution == null)
            {
                throw new InvalidOperationException("The stub can't obtain the workflow result because it doesn't have the workflow execution.");
            }

            return client.DataConverter.FromData<TResult>(await client.GetWorkflowResultAsync(Execution, client.ResolveDomain(Options?.Domain)));
        }

        /// <summary>
        /// Attempts to retrieve the associated workflow result specifying 
        /// expected result type as a parameter.
        /// </summary>
        /// <param name="resultType">Specifies the result type.</param>
        /// <returns>The result as a <c>dynamic</c>.</returns>
        public async Task<object> GetResultAsync(Type resultType)
        {
            Covenant.Requires<ArgumentNullException>(resultType != null);
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
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(queryType));
            Covenant.Requires<ArgumentNullException>(args != null);
            EnsureStarted();

            if (Execution == null)
            {
                throw new InvalidOperationException("Query cannot be sent because the stub doesn't have the workflow execution.");
            }

            var argBytes = client.DataConverter.ToData(args);

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
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(queryType));
            Covenant.Requires<ArgumentNullException>(args != null);
            EnsureStarted();

            if (Execution == null)
            {
                throw new InvalidOperationException("Query cannot be sent because the stub doesn't have the workflow execution.");
            }

            var argBytes = client.DataConverter.ToData(args);

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
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(signalName));
            Covenant.Requires<ArgumentNullException>(args != null);
            EnsureStarted();

            if (Execution == null)
            {
                throw new InvalidOperationException("Signal cannot be sent because the stub doesn't have the workflow execution.");
            }

            var argBytes = client.DataConverter.ToData(args);

            await client.SignalWorkflowAsync(Execution, signalName, argBytes, client.ResolveDomain(Options?.Domain));
        }

        /// <summary>
        /// Signals the associated workflow, starting it if it hasn't already been started.
        /// </summary>
        /// <param name="signalName">Specifies the signal name.</param>
        /// <param name="signalArgs">Specifies the signal arguments.</param>
        /// <param name="startArgs">Specifies the workflow start arguments.</param>
        /// <returns></returns>
        public async Task<WorkflowExecution> SignalWithStartAsync(string signalName, object[] signalArgs, object[] startArgs)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(signalName));
            Covenant.Requires<ArgumentNullException>(signalArgs != null);
            Covenant.Requires<ArgumentNullException>(startArgs != null);

            var signalArgBytes = client.DataConverter.ToData(signalArgs);
            var startArgBytes  = client.DataConverter.ToData(startArgs);

            return await client.SignalWorkflowWithStartAsync(this.WorkflowTypeName, signalName, signalArgBytes, startArgBytes, this.Options);
        }

        /// <summary>
        /// Starts the associated workflow.
        /// </summary>
        /// <param name="args">The workflow arguments.</param>
        /// <returns>The <see cref="WorkflowExecution"/>.</returns>
        public async Task<WorkflowExecution> StartAsync(params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(args != null);
            EnsureNotStarted();

            var argBytes = client.DataConverter.ToData(args);

            Execution = await client.StartWorkflowAsync(WorkflowTypeName, argBytes, Options);

            return Execution;
        }
    }
}
