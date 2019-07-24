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
    public class WorkflowStub : IWorkflowStub
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the untyped <see cref="IWorkflowStub"/> from a typed stub.
        /// </summary>
        /// <param name="typedStub">The source workflow stub.</param>
        /// <returns>The <see cref="IWorkflowStub"/>.</returns>
        public static IWorkflowStub FromTyped(object typedStub)
        {
            Covenant.Requires<ArgumentNullException>(typedStub != null);
            Covenant.Requires<ArgumentException>(typedStub is ITypedWorkflowStub, $"[{typedStub.GetType().FullName}] is not a typed workflow stub.");

            return ((ITypedWorkflowStub)typedStub).ToUntyped();
        }

        //---------------------------------------------------------------------
        // Instance members

        private CadenceClient   client;
        private string          taskList;
        private string          domain;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="client">The associated client.</param>
        /// <param name="workflowType">The workflow type name.</param>
        /// <param name="execution">The workflow execution or <c>null</c> if the workflow hasn't been started.</param>
        /// <param name="taskList">Specifies the task list.</param>
        /// <param name="options">Specifies the workflow options.</param>
        /// <param name="domain">Specifies specifies the domain.</param>
        internal WorkflowStub(CadenceClient client, string workflowType, WorkflowExecution execution, string taskList, WorkflowOptions options, string domain)
        {
            Covenant.Requires<ArgumentNullException>(client != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowType));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(taskList));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(domain));

            this.client       = client;
            this.WorkflowType = workflowType;
            this.Execution    = execution;
            this.taskList     = taskList;
            this.Options      = options;
            this.domain       = domain;
        }

        /// <inheritdoc/>
        public WorkflowExecution Execution { get; internal set; }

        /// <inheritdoc/>
        public WorkflowOptions Options { get; internal set; }

        /// <inheritdoc/>
        public string WorkflowType { get; internal set; }

        /// <summary>
        /// Ensures that the workflow has been started.
        /// </summary>
        private void EnsureStarted()
        {
            if (Execution == null)
            {
                throw new InvalidOperationException($"Workflow stub for workflow type name [{WorkflowType}] has not been started.");
            }
        }

        /// <summary>
        /// Ensures that the workflow has not been started.
        /// </summary>
        private void EnsureNotStarted()
        {
            if (Execution != null)
            {
                throw new InvalidOperationException($"Workflow stub for workflow type name [{WorkflowType}] has already been started.");
            }
        }

        /// <inheritdoc/>
        public async Task CancelAsync()
        {
            EnsureStarted();

            await client.CancelWorkflowAsync(Execution, domain);
        }

        /// <inheritdoc/>
        public async Task<TResult> GetResultAsync<TResult>()
        {
            EnsureStarted();

            return client.DataConverter.FromData<TResult>(await client.GetWorkflowResultAsync(Execution, domain));
        }

        /// <inheritdoc/>
        public async Task<object> GetResultAsync(Type resultType)
        {
            Covenant.Requires<ArgumentNullException>(resultType != null);

            EnsureStarted();

            return client.DataConverter.FromData(resultType, await client.GetWorkflowResultAsync(Execution, domain));
        }

        /// <inheritdoc/>
        public async Task<TResult> QueryAsync<TResult>(string queryType, params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(queryType));
            Covenant.Requires<ArgumentNullException>(args != null);
            EnsureStarted();

            var argBytes = client.DataConverter.ToData(args);

            return client.DataConverter.FromData<TResult>(await client.QueryWorkflowAsync(Execution, queryType, argBytes, domain));
        }

        /// <inheritdoc/>
        public async Task<object> QueryAsync(Type resultType, string queryType, params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(queryType));
            Covenant.Requires<ArgumentNullException>(args != null);
            EnsureStarted();

            var argBytes = client.DataConverter.ToData(args);

            return client.DataConverter.FromData(resultType, await client.QueryWorkflowAsync(Execution, queryType, argBytes, domain));
        }

        /// <inheritdoc/>
        public async Task SignalAsync(string signalName, params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(signalName));
            Covenant.Requires<ArgumentNullException>(args != null);
            EnsureStarted();

            var argBytes = client.DataConverter.ToData(args);

            await client.SignalWorkflowAsync(Execution, signalName, argBytes, domain);
        }

        /// <inheritdoc/>
        public async Task<WorkflowExecution> SignalWithStartAsync(string signalName, object[] signalArgs, object[] startArgs)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(signalName));
            Covenant.Requires<ArgumentNullException>(signalArgs != null);
            Covenant.Requires<ArgumentNullException>(startArgs != null);

            var signalArgBytes = client.DataConverter.ToData(signalArgs);
            var startArgBytes  = client.DataConverter.ToData(startArgs);

            return await client.SignalWorkflowWithStartAsync(signalName, signalArgBytes, startArgBytes, domain);
        }

        /// <inheritdoc/>
        public async Task<WorkflowExecution> StartAsync(params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(args != null);
            EnsureNotStarted();

            var argBytes = client.DataConverter.ToData(args);

            return await client.StartWorkflowAsync(WorkflowType, argBytes, taskList, Options, domain);
        }
    }
}
