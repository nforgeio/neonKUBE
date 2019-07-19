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
using System.Diagnostics.Contracts;
using System.Reflection;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Time;

namespace Neon.Cadence
{
    public partial class CadenceClient
    {
        //---------------------------------------------------------------------
        // Cadence workflow related operations.

        /// <summary>
        /// Registers a workflow implementation with Cadence.
        /// </summary>
        /// <typeparam name="TWorkflow">The <see cref="Workflow"/> derived type implementing the workflow.</typeparam>
        /// <param name="workflowTypeName">
        /// Optionally specifies a custom workflow type name that will be used 
        /// for identifying the workflow implementation in Cadence.  This defaults
        /// to the fully qualified <typeparamref name="TWorkflow"/> type name.
        /// </param>
        /// <param name="domain">Optionally overrides the default client domain.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if another workflow class has already been registered for <paramref name="workflowTypeName"/>.</exception>
        /// <exception cref="CadenceWorkflowWorkerStartedException">
        /// Thrown if a workflow worker has already been started for the client.  You must
        /// register activity workflow implementations before starting workers.
        /// </exception>
        /// <remarks>
        /// <note>
        /// Be sure to register all of your workflow implementations before starting a workflow worker.
        /// </note>
        /// </remarks>
        public async Task RegisterWorkflowAsync<TWorkflow>(string workflowTypeName = null, string domain = null)
            where TWorkflow : Workflow
        {
            if (string.IsNullOrEmpty(workflowTypeName))
            {
                workflowTypeName = workflowTypeName ?? typeof(TWorkflow).FullName;
            }

            if (workflowWorkerStarted)
            {
                throw new CadenceWorkflowWorkerStartedException();
            }

            if (!Workflow.Register(this, typeof(TWorkflow), workflowTypeName))
            {
                var reply = (WorkflowRegisterReply)await CallProxyAsync(
                    new WorkflowRegisterRequest()
                    {
                        Name   = workflowTypeName,
                        Domain = ResolveDomain(domain)
                    });

                reply.ThrowOnError();
            }
        }

        /// <summary>
        /// Scans the assembly passed looking for workflow implementations derived from
        /// <see cref="Workflow"/> and tagged by <see cref="AutoRegisterAttribute"/>
        /// and registers them with Cadence.
        /// </summary>
        /// <param name="assembly">The target assembly.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="TypeLoadException">
        /// Thrown for types tagged by <see cref="AutoRegisterAttribute"/> that are not 
        /// derived from <see cref="Workflow"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">Thrown if one of the tagged classes conflict with an existing registration.</exception>
        /// <exception cref="CadenceWorkflowWorkerStartedException">
        /// Thrown if a workflow worker has already been started for the client.  You must
        /// register activity workflow implementations before starting workers.
        /// </exception>
        /// <remarks>
        /// <note>
        /// Be sure to register all of your workflow implementations before starting a workflow worker.
        /// </note>
        /// </remarks>
        public async Task RegisterAssemblyWorkflowsAsync(Assembly assembly)
        {
            Covenant.Requires<ArgumentNullException>(assembly != null);

            if (workflowWorkerStarted)
            {
                throw new CadenceWorkflowWorkerStartedException();
            }

            foreach (var type in assembly.GetTypes())
            {
                var autoRegisterAttribute = type.GetCustomAttribute<AutoRegisterAttribute>();

                if (autoRegisterAttribute != null)
                {
                    if (type.IsSubclassOf(typeof(Workflow)))
                    {
                        var workflowTypeName = autoRegisterAttribute.TypeName ?? type.FullName;

                        if (!Workflow.Register(this, type, workflowTypeName))
                        {
                            var reply = (WorkflowRegisterReply)await CallProxyAsync(
                                new WorkflowRegisterRequest()
                                {
                                    Name = workflowTypeName
                                });

                            reply.ThrowOnError();
                        }
                    }
                    else if (type.IsSubclassOf(typeof(Activity)))
                    {
                        // Ignore these.
                    }
                    else
                    {
                        throw new TypeLoadException($"Type [{type.FullName}] is tagged by [{nameof(AutoRegisterAttribute)}] but is not derived from [{nameof(Workflow)}].");
                    }
                }
            }
        }

        /// <summary>
        /// <para>
        /// Sets the maximum number of sticky workflows for which of history will be 
        /// retained for workflow workers created by this client as a performance 
        /// optimization.  When this is exceeded, Cadence will may need to retrieve 
        /// the entire workflow history from the Cadence cluster when a workflow is 
        /// scheduled on the client's workers.
        /// </para>
        /// <para>
        /// This defaults to <b>10K</b> sticky workflows.
        /// </para>
        /// </summary>
        /// <param name="maxCacheSize">The maximum number of workflows to be cached.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task SetStickyWorkflowCacheSizeAsync(int maxCacheSize)
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
        /// Returns the current maximum number of sticky workflows for which history
        /// will be retained as a performance optimization.
        /// </summary>
        /// <returns>The maximum number of cached workflows.</returns>
        public async Task<int> GetStickyWorkflowCacheSizeAsync()
        {
            return await Task.FromResult(workflowCacheSize);
        }

        /// <summary>
        /// Creates an untyped stub connected to a known workflow execution.  This can be
        /// used to query, signal, or retrieve the result for a workflow.
        /// </summary>
        /// <param name="workflowId">Specifies the workflow ID.</param>
        /// <param name="runId">Optionally specifies the workflow's run ID.</param>
        /// <param name="workflowType">Optionally specifies the workflow type.</param>
        /// <param name="domain">Optionally overrides the client's default domain.</param>
        /// <returns>The <see cref="IWorkflowStub"/>.</returns>
        public IWorkflowStub NewUntypedWorkflowStub(string workflowId, string runId = null, string workflowType = null, string domain = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowId));

            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates an untyped stub that will be used to execute a workflow as well as
        /// query and signal the new workflow.
        /// </summary>
        /// <param name="workflowType">Specifies workflow type.</param>
        /// <param name="options">Optionally specifies the workflow options.</param>
        /// <param name="domain">Optionally overrides the client's default domain.</param>
        /// <returns>The <see cref="IWorkflowStub"/>.</returns>
        public IWorkflowStub NewUntypedWorkflowStub(string workflowType, WorkflowOptions options = null, string domain = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowType));

            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates a typed workflow stub connected to a known workflow execution.
        /// This can be used to signal and query the workflow.
        /// </summary>
        /// <typeparam name="TWorkflow">Identifies the workflow type.</typeparam>
        /// <param name="workflowId">Specifies the workflow ID.</param>
        /// <param name="runId">Optionally specifies the workflow's run ID.</param>
        /// <param name="workflowType">
        /// Optionally specifies the workflow type by overriding the fully 
        /// qualified <typeparamref name="TWorkflow"/> type name or the name
        /// specified by a <see cref="AutoRegisterAttribute"/>.
        /// </param>
        /// <param name="domain">Optionally overrides the client's default domain.</param>
        /// <returns>The dynamically generated stub that implements the workflow methods defined by <typeparamref name="TWorkflow"/>.</returns>
        public TWorkflow NewWorkflowStub<TWorkflow>(string workflowId, string runId = null, string workflowType = null, string domain = null)
            where TWorkflow : IWorkflow
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowId));

            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates a typed workflow stub that can be used to start as well as 
        /// query and signal the workflow.
        /// </summary>
        /// <typeparam name="TWorkflow">Identifies the workflow type.</typeparam>
        /// <param name="options">Optionally specifies the workflow options.</param>
        /// <param name="workflowType">
        /// Optionally specifies the workflow type by overriding the fully 
        /// qualified <typeparamref name="TWorkflow"/> type name or the name
        /// specified by a <see cref="AutoRegisterAttribute"/>.
        /// </param>
        /// <param name="domain">Optionally overrides the client's default domain.</param>
        /// <returns>The dynamically generated stub that implements the workflow methods defined by <typeparamref name="TWorkflow"/>.</returns>
        public TWorkflow NewWorkflowStub<TWorkflow>(WorkflowOptions options = null, string workflowType = null, string domain = null)
            where TWorkflow : IWorkflow
        {
            throw new NotImplementedException();
        }

        //---------------------------------------------------------------------
        // Internal workflow related methods that will be available to be called
        // by dynamically generated workflow stubs.


    }
}
