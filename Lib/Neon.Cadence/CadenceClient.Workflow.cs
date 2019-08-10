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
using System.Linq;
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
        /// <typeparam name="TWorkflow">The <see cref="WorkflowBase"/> derived class implementing the workflow.</typeparam>
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
        /// register workflow implementations before starting workers.
        /// </exception>
        /// <remarks>
        /// <note>
        /// Be sure to register all of your workflow implementations before starting workers.
        /// </note>
        /// </remarks>
        public async Task RegisterWorkflowAsync<TWorkflow>(string workflowTypeName = null, string domain = null)
            where TWorkflow : WorkflowBase
        {
            CadenceHelper.ValidateWorkflowImplementation(typeof(TWorkflow));
            CadenceHelper.ValidateWorkflowTypeName(workflowTypeName);
            EnsureNotDisposed();

            if (workflowWorkerStarted)
            {
                throw new CadenceWorkflowWorkerStartedException();
            }

            var workflowType = typeof(TWorkflow);

            if (string.IsNullOrEmpty(workflowTypeName))
            {
                workflowTypeName = workflowTypeName ?? CadenceHelper.GetWorkflowTypeName(workflowType);
            }

            await WorkflowBase.RegisterAsync(this, typeof(TWorkflow), workflowTypeName, ResolveDomain(domain));
        }

        /// <summary>
        /// Scans the assembly passed looking for workflow implementations derived from
        /// <see cref="WorkflowBase"/> and tagged by <see cref="WorkflowAttribute"/> with
        /// <see cref="WorkflowAttribute.AutoRegister"/> set to <c>true</c> and registers 
        /// them with Cadence.
        /// </summary>
        /// <param name="assembly">The target assembly.</param>
        /// <param name="domain">Optionally overrides the default client domain.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="TypeLoadException">
        /// Thrown for types tagged by <see cref="WorkflowAttribute"/> that are not 
        /// derived from <see cref="WorkflowBase"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">Thrown if one of the tagged classes conflict with an existing registration.</exception>
        /// <exception cref="CadenceWorkflowWorkerStartedException">
        /// Thrown if a workflow worker has already been started for the client.  You must
        /// register workflow implementations before starting workers.
        /// </exception>
        /// <remarks>
        /// <note>
        /// Be sure to register all of your workflow implementations before starting workers.
        /// </note>
        /// </remarks>
        public async Task RegisterAssemblyWorkflowsAsync(Assembly assembly, string domain = null)
        {
            Covenant.Requires<ArgumentNullException>(assembly != null);
            EnsureNotDisposed();

            foreach (var type in assembly.GetTypes().Where(t => t.IsClass))
            {
                var workflowAttribute = type.GetCustomAttribute<WorkflowAttribute>();

                if (workflowAttribute != null && workflowAttribute.AutoRegister)
                {
                    var workflowTypeName = CadenceHelper.GetWorkflowTypeName(type, workflowAttribute);

                    await WorkflowBase.RegisterAsync(this, type, workflowTypeName, ResolveDomain(domain));
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
        public async Task SetCacheMaximumSizeAsync(int cacheMaximumSize)
        {
            Covenant.Requires<ArgumentNullException>(cacheMaximumSize >= 0);
            EnsureNotDisposed();

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
        public async Task<int> GetWorkflowCacheSizeAsync()
        {
            EnsureNotDisposed();

            return await Task.FromResult(workflowCacheSize);
        }

        /// <summary>
        /// Creates an untyped stub that can be used to execute, query, and signal a new workflow
        /// specified using raw bytes.
        /// </summary>
        /// <param name="workflowId">Specifies the workflow ID.</param>
        /// <param name="runId">Optionally specifies the workflow's run ID.</param>
        /// <param name="workflowTypeName">Optionally specifies the workflow type name.</param>
        /// <param name="domain">Optionally overrides the client's default domain.</param>
        /// <returns>The <see cref="IWorkflowStub"/>.</returns>
        /// <remarks>
        /// Unlike activity stubs, a workflow stub may only be used to launch a single
        /// workflow.  You'll need to create a new stub for each workflow you wish to
        /// invoke and then the first method called on a workflow stub must be
        /// the one of the methods tagged by <see cref="WorkflowMethodAttribute"/>.
        /// </remarks>
        public IWorkflowStub NewUntypedWorkflowStub(string workflowId, string runId = null, string workflowTypeName = null, string domain = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowId));
            EnsureNotDisposed();

            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates an untyped stub that can be used to execute, query, and signal a new workflow
        /// specified using an explicit type parameter.
        /// </summary>
        /// <param name="workflowTypeName">Specifies workflow type name (see the remarks).</param>
        /// <param name="options">Optionally specifies the workflow options.</param>
        /// <param name="domain">Optionally overrides the client's default domain.</param>
        /// <returns>The <see cref="IWorkflowStub"/>.</returns>
        /// <remarks>
        /// <para>
        /// Unlike activity stubs, a workflow stub may only be used to launch a single
        /// workflow.  You'll need to create a new stub for each workflow you wish to
        /// invoke.
        /// </para>
        /// <para>
        /// <paramref name="workflowTypeName"/> specifies the target workflow implementation type name and optionally,
        /// the specific workflow method to be called for workflow interfaces that have multiple methods.  For
        /// workflow methods tagged by <c>[WorkflowMethod]</c> with specifying a name, the workflow type name will default
        /// to the fully qualified interface type name or the custom type name specified by <see cref="WorkflowAttribute.Name"/>.
        /// </para>
        /// <para>
        /// For workflow methods with <see cref="WorkflowMethodAttribute.Name"/> specified, the workflow type will
        /// look like:
        /// </para>
        /// <code>
        /// WORKFLOW-TYPE-NAME::METHOD-NAME
        /// </code>
        /// <para>
        /// You'll need to use this format when calling workflows using external untyped stubs or 
        /// from other languages.  The Java Cadence client works the same way.
        /// </para>
        /// </remarks>
        public WorkflowStub NewUntypedWorkflowStub(string workflowTypeName, WorkflowOptions options = null, string domain = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowTypeName));
            EnsureNotDisposed();

            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates a typed workflow stub connected to a known workflow execution.
        /// This can be used to signal and query the workflow via the type-safe
        /// interface methods.
        /// </summary>
        /// <typeparam name="TWorkflowInterface">Identifies the workflow interface.</typeparam>
        /// <param name="workflowId">Specifies the workflow ID.</param>
        /// <param name="runId">Optionally specifies the workflow's run ID.</param>
        /// <param name="workflowTypeName">
        /// Optionally specifies the workflow type name by overriding the fully 
        /// qualified <typeparamref name="TWorkflowInterface"/> type name or the name
        /// specified by a <see cref="WorkflowAttribute"/>.
        /// </param>
        /// <param name="domain">Optionally overrides the client's default domain.</param>
        /// <returns>The dynamically generated stub that implements the workflow methods defined by <typeparamref name="TWorkflowInterface"/>.</returns>
        /// <remarks>
        /// Unlike activity stubs, a workflow stub may only be used to launch a single
        /// workflow.  You'll need to create a new stub for each workflow you wish to
        /// invoke and then the first method called on a workflow stub must be
        /// the one of the methods tagged by <see cref="WorkflowMethodAttribute"/>.
        /// </remarks>
        public TWorkflowInterface NewWorkflowStub<TWorkflowInterface>(string workflowId, string runId = null, string workflowTypeName = null, string domain = null)
            where TWorkflowInterface : class
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowId));
            CadenceHelper.ValidateWorkflowInterface(typeof(TWorkflowInterface));
            EnsureNotDisposed();

            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates a typed workflow stub that can be used to start as well as 
        /// query and signal the workflow via the type-safe interface methods.
        /// </summary>
        /// <typeparam name="TWorkflowInterface">Identifies the workflow interface.</typeparam>
        /// <param name="options">Optionally specifies the workflow options.</param>
        /// <param name="workflowTypeName">
        /// Optionally specifies the workflow type name by overriding the fully 
        /// qualified <typeparamref name="TWorkflowInterface"/> type name or the name
        /// specified by a <see cref="WorkflowAttribute"/>.
        /// </param>
        /// <param name="domain">Optionally overrides the client's default domain.</param>
        /// <returns>The dynamically generated stub that implements the workflow methods defined by <typeparamref name="TWorkflowInterface"/>.</returns>
        /// <remarks>
        /// Unlike activity stubs, a workflow stub may only be used to launch a single
        /// workflow.  You'll need to create a new stub for each workflow you wish to
        /// invoke and then the first method called on a workflow stub must be
        /// the one of the methods tagged by <see cref="WorkflowMethodAttribute"/>.
        /// </remarks>
        public TWorkflowInterface NewWorkflowStub<TWorkflowInterface>(WorkflowOptions options = null, string workflowTypeName = null, string domain = null)
            where TWorkflowInterface : class
        {
            CadenceHelper.ValidateWorkflowInterface(typeof(TWorkflowInterface));
            EnsureNotDisposed();

            return StubManager.NewWorkflowStub<TWorkflowInterface>(this, options: options, workflowTypeName: workflowTypeName, domain: domain);
        }

        //---------------------------------------------------------------------
        // Internal workflow related methods used by dynamically generated workflow stubs.

        /// <summary>
        /// Starts an external workflow using a specific workflow type name, returning a <see cref="WorkflowExecution"/>
        /// that can be used to track the workflow and also wait for its result via <see cref="GetWorkflowResultAsync(WorkflowExecution, string)"/>.
        /// </summary>
        /// <param name="workflowTypeName">
        /// The type name used when registering the workers that will handle this workflow.
        /// This name will often be the fully qualified name of the workflow type but 
        /// this may have been customized when the workflow worker was registered.
        /// </param>
        /// <param name="args">Optionally specifies the workflow arguments encoded into a byte array.</param>
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
        internal async Task<WorkflowExecution> StartWorkflowAsync(string workflowTypeName, byte[] args = null, WorkflowOptions options = null, string domain = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowTypeName));
            EnsureNotDisposed();

            options = options ?? new WorkflowOptions();
            options = options.Clone();

            if (!options.ExecutionStartToCloseTimeout.HasValue)
            {
                options.ExecutionStartToCloseTimeout = Settings.WorkflowScheduleToCloseTimeout;
            }

            if (!options.ScheduleToStartTimeout.HasValue)
            {
                options.ScheduleToStartTimeout = Settings.WorkflowScheduleToStartTimeout;
            }

            if (!options.TaskStartToCloseTimeout.HasValue)
            {
                options.TaskStartToCloseTimeout = Settings.WorkflowTaskStartToCloseTimeout;
            }

            if (string.IsNullOrEmpty(options.TaskList))
            {
                options.TaskList = Settings.DefaultTaskList;
            }

            var reply = (WorkflowExecuteReply)await CallProxyAsync(
                new WorkflowExecuteRequest()
                {
                    Workflow = workflowTypeName,
                    Domain   = domain ?? Settings.DefaultDomain,
                    Args     = args,
                    Options  = options.ToInternal(this)
                });

            reply.ThrowOnError();

            var execution = reply.Execution;

            return new WorkflowExecution(execution.ID, execution.RunID);
        }

        /// <summary>
        /// Returns the current state of a running workflow.
        /// </summary>
        /// <param name="execution">Identifies the workflow execution.</param>
        /// <param name="domain">Optionally specifies the domain.  This defaults to the client domain.</param>
        /// <returns>A <see cref="WorkflowDescription"/>.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the workflow no longer exists.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown if the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence problems.</exception>
        internal async Task<WorkflowDescription> GetWorkflowDescriptionAsync(WorkflowExecution execution, string domain = null)
        {
            Covenant.Requires<ArgumentNullException>(execution != null);
            EnsureNotDisposed();

            var reply = (WorkflowDescribeExecutionReply)await CallProxyAsync(
                new WorkflowDescribeExecutionRequest()
                {
                    WorkflowId = execution.WorkflowId,
                    RunId      = execution.RunId,
                    Domain     = ResolveDomain(domain)
                });

            reply.ThrowOnError();

            return reply.Details.ToPublic();
        }

        /// <summary>
        /// Returns the result from a workflow execution, blocking until the workflow
        /// completes if it is still running.
        /// </summary>
        /// <param name="execution">Identifies the workflow execution.</param>
        /// <param name="domain">Optionally specifies the domain.  This defaults to the client domain.</param>
        /// <returns>The workflow result encoded as bytes or <c>null</c>.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the workflow no longer exists.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown if the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence problems.</exception>
        internal async Task<byte[]> GetWorkflowResultAsync(WorkflowExecution execution, string domain = null)
        {
            Covenant.Requires<ArgumentNullException>(execution != null);
            EnsureNotDisposed();

            var reply = (WorkflowGetResultReply)await CallProxyAsync(
                new WorkflowGetResultRequest()
                {
                    WorkflowId = execution.WorkflowId,
                    RunId      = execution.RunId,
                    Domain     = ResolveDomain(domain)
                });

            reply.ThrowOnError();

            return reply.Result;
        }

        /// <summary>
        /// Cancels a workflow if it has not already finished.
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
            EnsureNotDisposed();

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
        /// Cancels a workflow if it has not already finished.
        /// </summary>
        /// <param name="execution">Identifies the running workflow.</param>
        /// <param name="reason">Optionally specifies an error reason string.</param>
        /// <param name="details">Optionally specifies additional details as a byte array.</param>
        /// <param name="domain">Optionally specifies the domain.  This defaults to the client domain.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the workflow no longer exists.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown if the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence problems.</exception>
        internal async Task TerminateWorkflowAsync(WorkflowExecution execution, string reason = null, byte[] details = null, string domain = null)
        {
            Covenant.Requires<ArgumentNullException>(execution != null);
            EnsureNotDisposed();

            var reply = (WorkflowTerminateReply)await CallProxyAsync(
                new WorkflowTerminateRequest()
                {
                    WorkflowId = execution.WorkflowId,
                    RunId      = execution.RunId,
                    Domain     = ResolveDomain(domain),
                    Reason     = reason,
                    Details    = details
                });;

            reply.ThrowOnError();
        }

        /// <summary>
        /// Transmits a signal to a running workflow.
        /// </summary>
        /// <param name="execution">The <see cref="WorkflowExecution"/>.</param>
        /// <param name="signalName">Identifies the signal.</param>
        /// <param name="signalArgs">Optionally specifies the signal arguments as a byte array.</param>
        /// <param name="domain">Optionally specifies the domain.  This defaults to the client domain.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the workflow no longer exists.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence problems.</exception>
        internal async Task SignalWorkflowAsync(WorkflowExecution execution, string signalName, byte[] signalArgs = null, string domain = null)
        {
            Covenant.Requires<ArgumentNullException>(execution != null);
            EnsureNotDisposed();

            var reply = (WorkflowSignalReply)await CallProxyAsync(
                new WorkflowSignalRequest()
                {
                    WorkflowId = execution.WorkflowId,
                    SignalName = signalName,
                    SignalArgs = signalArgs,
                    RunId      = execution.RunId,
                    Domain     = ResolveDomain(domain)
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Transmits a signal to a workflow, starting the workflow if it's not currently running.
        /// </summary>
        /// <param name="signalName">Identifies the signal.</param>
        /// <param name="signalArgs">Optionally specifies the signal arguments as a byte array.</param>
        /// <param name="startArgs">Optionally specifies the workflow arguments.</param>
        /// <param name="options">Optionally specifies the options to be used for starting the workflow when required.</param>
        /// <param name="taskList">Optionally overrides the <see cref="CadenceClient"/> default task list.</param>
        /// <param name="domain">Optionally overrides the <see cref="CadenceClient"/> default domain.</param>
        /// <returns>The <see cref="WorkflowExecution"/>.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the domain does not exist.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown if the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence problems.</exception>
        internal async Task<WorkflowExecution> SignalWorkflowWithStartAsync(string signalName, byte[] signalArgs = null, byte[] startArgs = null, string taskList = null, WorkflowOptions options = null, string domain = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(signalName));
            EnsureNotDisposed();

            options = options ?? new WorkflowOptions();

            var reply = (WorkflowSignalWithStartReply)await CallProxyAsync(
                new WorkflowSignalWithStartRequest()
                {
                    WorkflowId   = options.WorkflowId,
                    Options      = options.ToInternal(this),
                    SignalName   = signalName,
                    SignalArgs   = signalArgs,
                    WorkflowArgs = startArgs,
                    Domain       = ResolveDomain(domain)
                });

            reply.ThrowOnError();

            return reply.Execution.ToPublic();
        }

        /// <summary>
        /// Queries a workflow.
        /// </summary>
        /// <param name="execution">The <see cref="WorkflowExecution"/>.</param>
        /// <param name="queryType">Identifies the query.</param>
        /// <param name="queryArgs">Optionally specifies the query arguments encoded as a byte array.</param>
        /// <param name="domain">Optionally specifies the domain.  This defaults to the client domain.</param>
        /// <returns>The query result encoded as a byte array.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the workflow no longer exists.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence problems.</exception>
        internal async Task<byte[]> QueryWorkflowAsync(WorkflowExecution execution, string queryType, byte[] queryArgs = null, string domain = null)
        {
            Covenant.Requires<ArgumentNullException>(execution != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(queryType));
            EnsureNotDisposed();

            var reply = (WorkflowQueryReply)await CallProxyAsync(
                new WorkflowQueryRequest()
                {
                    WorkflowId = execution.WorkflowId,
                    QueryName  = queryType,
                    QueryArgs  = queryArgs,
                    RunId      = execution.RunId,
                    Domain     = ResolveDomain(domain)
                });

            reply.ThrowOnError();

            return reply.Result;
        }
    }
}
