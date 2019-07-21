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
        // Public Cadence workflow related operations.

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
        /// <param name="cacheMaximumSize">The maximum number of workflows to be cached.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task SetSCacheMaximumSizeAsync(int cacheMaximumSize)
        {
            Covenant.Requires<ArgumentNullException>(cacheMaximumSize >= 0);

            var reply = (WorkflowSetCacheSizeReply)await CallProxyAsync(
                new WorkflowSetCacheSizeRequest()
                {
                    Size = cacheMaximumSize
                });

            reply.ThrowOnError();

            workflowCacheSize = cacheMaximumSize;
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

        /// <summary>
        /// Starts an external workflow using a specific workflow type name, returning a <see cref="WorkflowExecution"/>
        /// that can be used to track the workflow and also wait for its result via <see cref="GetWorkflowResultAsync(WorkflowExecution)"/>.
        /// </summary>
        /// <param name="workflowTypeName">
        /// The type name used when registering the workers that will handle this workflow.
        /// This name will often be the fully qualified name of the workflow type but 
        /// this may have been customized when the workflow worker was registered.
        /// </param>
        /// <param name="args">Optionally specifies the workflow arguments encoded into a byte array.</param>
        /// <param name="taskList">Optionally specifies the target task list.  This defaults to the client task list.</param>
        /// <param name="options">Specifies the workflow options.</param>
        /// <param name="domain">Optionally specifies the Cadence domain where the workflow will run.  This defaults to the client domain.</param>
        /// <returns>A <see cref="WorkflowExecution"/> identifying the new running workflow instance.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if there is no workflow registered for <paramref name="workflowTypeName"/>.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown if the request is not valid.</exception>
        /// <exception cref="CadenceWorkflowRunningException">Thrown if a workflow with this ID is already running.</exception>
        /// <remarks>
        /// This method kicks off a new workflow instance and returns after Cadence has
        /// queued the operation but the method <b>does not</b> wait for the workflow to
        /// complete.
        /// </remarks>
        internal async Task<WorkflowExecution> StartWorkflowAsync(string workflowTypeName, byte[] args = null, string taskList = null, WorkflowOptions options = null, string domain = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowTypeName));

            options = options ?? new WorkflowOptions();

            var reply = (WorkflowExecuteReply)await CallProxyAsync(
                new WorkflowExecuteRequest()
                {
                    Workflow = workflowTypeName,
                    Domain   = domain ?? Settings.DefaultDomain,
                    Args     = args,
                    Options  = options.ToInternal(this, taskList)
                });

            reply.ThrowOnError();

            var execution = reply.Execution;

            return new WorkflowExecution(execution.ID, execution.RunID);
        }

        /// <summary>
        /// Returns the current state of a running workflow.
        /// </summary>
        /// <param name="execution">Identifies the workflow execution.</param>
        /// <returns>A <see cref="WorkflowDescription"/>.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the workflow no longer exists.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown if the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence problems.</exception>
        internal async Task<WorkflowDescription> GetWorkflowDescriptionAsync(WorkflowExecution execution)
        {
            Covenant.Requires<ArgumentNullException>(execution != null);

            var reply = (WorkflowDescribeExecutionReply)await CallProxyAsync(
                new WorkflowDescribeExecutionRequest()
                {
                    WorkflowId = execution.WorkflowId,
                    RunId      = execution.RunId
                });

            reply.ThrowOnError();

            return reply.Details.ToPublic();
        }

        /// <summary>
        /// Returns the result from a workflow execution, blocking until the workflow
        /// completes if it is still running.
        /// </summary>
        /// <param name="execution">Identifies the workflow execution.</param>
        /// <returns>The workflow result encoded as bytes or <c>null</c>.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the workflow no longer exists.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown if the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence problems.</exception>
        internal async Task<byte[]> GetWorkflowResultAsync(WorkflowExecution execution)
        {
            Covenant.Requires<ArgumentNullException>(execution != null);

            var reply = (WorkflowGetResultReply)await CallProxyAsync(
                new WorkflowGetResultRequest()
                {
                    WorkflowId = execution.WorkflowId,
                    RunId      = execution.RunId
                });

            reply.ThrowOnError();

            return reply.Result;
        }

        /// <summary>
        /// <para>
        /// Cancels a workflow if it has not already finished.
        /// </para>
        /// <note>
        /// Workflow cancellation is typically considered to be a normal activity
        /// and not an error as opposed to workflow termination which will usually
        /// happen due to an error.
        /// </note>
        /// </summary>
        /// <param name="execution">Identifies the running workflow.</param>
        /// <param name="domain">Optionally identifies the domain.  This defaults to the client domain.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the workflow no longer exists.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown if the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence problems.</exception>
        internal async Task CancelWorkflowAsync(WorkflowExecution execution, string domain = null)
        {
            Covenant.Requires<ArgumentNullException>(execution != null);

            var reply = (WorkflowCancelReply)await CallProxyAsync(
                new WorkflowCancelRequest()
                {
                    WorkflowId = execution.WorkflowId,
                    RunId      = execution.RunId,
                    Domain     = ResolveDomain(domain)
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// <para>
        /// Cancels a workflow if it has not already finished.
        /// </para>
        /// <note>
        /// Workflow termination is typically considered to be due to an error as
        /// opposed to cancellation which is usually considered as a normal activity.
        /// </note>
        /// </summary>
        /// <param name="execution">Identifies the running workflow.</param>
        /// <param name="reason">Optionally specifies an error reason string.</param>
        /// <param name="details">Optionally specifies additional details as a byte array.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the workflow no longer exists.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown if the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence problems.</exception>
        internal async Task TerminateWorkflowAsync(WorkflowExecution execution, string reason = null, byte[] details = null)
        {
            Covenant.Requires<ArgumentNullException>(execution != null);

            var reply = (WorkflowTerminateReply)await CallProxyAsync(
                new WorkflowTerminateRequest()
                {
                    WorkflowId = execution.WorkflowId,
                    RunId      = execution.RunId,
                    Reason     = reason,
                    Details    = details
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Transmits a signal to a running workflow.
        /// </summary>
        /// <param name="execution">The <see cref="WorkflowExecution"/>.</param>
        /// <param name="signalName">Identifies the signal.</param>
        /// <param name="signalArgs">Optionally specifies signal arguments as a byte array.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the workflow no longer exists.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence problems.</exception>
        internal async Task SignalWorkflowAsync(WorkflowExecution execution, string signalName, byte[] signalArgs = null)
        {
            Covenant.Requires<ArgumentNullException>(execution != null);

            var reply = (WorkflowSignalReply)await CallProxyAsync(
                new WorkflowSignalRequest()
                {
                    WorkflowId = execution.WorkflowId,
                    SignalName = signalName,
                    SignalArgs = signalArgs,
                    RunId      = execution.RunId
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Transmits a signal to a workflow, starting the workflow if it's not currently running.
        /// </summary>
        /// <param name="workflowId">The workflow ID.</param>
        /// <param name="signalName">Identifies the signal.</param>
        /// <param name="signalArgs">Optionally specifies signal arguments as a byte array.</param>
        /// <param name="workflowArgs">Optionally specifies the workflow arguments.</param>
        /// <param name="options">Optionally specifies the options to be used for starting the workflow when required.</param>
        /// <param name="taskList">Optionally specifies the task list.  This defaults to <b>"default"</b>.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the domain does not exist.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown if the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence problems.</exception>
        internal async Task SignalWorkflowWithStartAsync(string workflowId, string signalName, byte[] signalArgs = null, byte[] workflowArgs = null, string taskList = null, WorkflowOptions options = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowId));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(signalName));

            options = options ?? new WorkflowOptions();

            var reply = (WorkflowSignalWithStartReply)await CallProxyAsync(
                new WorkflowSignalWithStartRequest()
                {
                    WorkflowId   = workflowId,
                    Options      = options.ToInternal(this, taskList),
                    SignalName   = signalName,
                    SignalArgs   = signalArgs,
                    WorkflowArgs = workflowArgs
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Queries a workflow.
        /// </summary>
        /// <param name="execution">The <see cref="WorkflowExecution"/>.</param>
        /// <param name="queryType">Identifies the query.</param>
        /// <param name="queryArgs">Optionally specifies query arguments encoded as a byte array.</param>
        /// <returns>The query result encoded as a byte array.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the workflow no longer exists.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence problems.</exception>
        internal async Task<byte[]> QueryWorkflowAsync(WorkflowExecution execution, string queryType, byte[] queryArgs = null)
        {
            Covenant.Requires<ArgumentNullException>(execution != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(queryType));

            var reply = (WorkflowQueryReply)await CallProxyAsync(
                new WorkflowQueryRequest()
                {
                    WorkflowId = execution.WorkflowId,
                    QueryName  = queryType,
                    QueryArgs  = queryArgs,
                    RunId      = execution.RunId
                });

            reply.ThrowOnError();

            return reply.Result;
        }
    }
}
