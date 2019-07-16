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
        public async Task RegisterWorkflowAsync<TWorkflow>(string workflowTypeName = null)
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
                        Name = workflowTypeName
                    });

                reply.ThrowOnError();
            }
        }

        /// <summary>
        /// Scans the assembly passed looking for workflow implementations derived from
        /// <see cref="Workflow"/> and tagged with <see cref="AutoRegisterAttribute"/>
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
                        // Ignore these here.
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

        public WorkflowStub NewUntypedWorkflowStub(string workflowId, string runId = null, string workflowType = null, string domain = null)
        {
            throw new NotImplementedException();
        }

        public WorkflowStub NewUntypedWorkflowStub(string workflowType, WorkflowOptions options = null, string domain = null)
        {
            throw new NotImplementedException();
        }

        public TWorkflow NewWorkflowStub<TWorkflow>(string workflowId, string runId = null, string domain = null)
            where TWorkflow : IWorkflow
        {
            throw new NotImplementedException();
        }

        public TWorkflow NewWorkflowStub<TWorkflow>(WorkflowOptions options = null, string workflowType = null, string domain = null)
            where TWorkflow : IWorkflow
        {
            throw new NotImplementedException();
        }
    }
}
