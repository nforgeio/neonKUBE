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
                workflowTypeName = CadenceHelper.GetWorkflowTypeName(workflowType, workflowType.GetCustomAttribute<WorkflowAttribute>());
            }

            await WorkflowBase.RegisterAsync(this, workflowType, workflowTypeName, ResolveDomain(domain));
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
        /// Creates an untyped stub that can be used to start a single workflow execution.
        /// </summary>
        /// <param name="workflowTypeName">Specifies the workflow type name.</param>
        /// <param name="options">Optionally specifies the workflow options.</param>
        /// <returns>The <see cref="WorkflowStub"/>.</returns>
        /// <remarks>
        /// <para>
        /// Unlike activity stubs, a workflow stub may only be used to launch a single
        /// workflow.  You'll need to create a new stub for each workflow you wish to
        /// invoke and then the first method called on a workflow stub must be
        /// the one of the methods tagged by <see cref="WorkflowMethodAttribute"/>.
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
        /// </remarks>
        public WorkflowStub NewUntypedWorkflowStub(string workflowTypeName, WorkflowOptions options = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowTypeName));
            EnsureNotDisposed();

            return new WorkflowStub(this)
            {
                WorkflowTypeName = workflowTypeName,
                Options          = options ?? new WorkflowOptions()
            };
        }

        /// <summary>
        /// Creates an untyped stub for a known workflow execution.
        /// </summary>
        /// <param name="workflowId">The workflow ID.</param>
        /// <param name="runId">The workflow run ID.</param>
        /// <param name="workflowTypeName">The workflow type name.</param>
        /// <returns>The <see cref="WorkflowStub"/>.</returns>
        /// <remarks>
        /// <para>
        /// Unlike activity stubs, a workflow stub may only be used to launch a single
        /// workflow.  You'll need to create a new stub for each workflow you wish to
        /// invoke and then the first method called on a workflow stub must be
        /// the one of the methods tagged by <see cref="WorkflowMethodAttribute"/>.
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
        /// </remarks>
        public WorkflowStub NewUntypedWorkflowStub(string workflowId, string runId, string workflowTypeName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowId));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(runId));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowTypeName));
            EnsureNotDisposed();

            return new WorkflowStub(this)
            {
                Execution = new WorkflowExecution(workflowId, runId)
            };
        }

        /// <summary>
        /// Creates an untyped stub for a known workflow execution.
        /// </summary>
        /// <param name="execution">The workflow execution.</param>
        /// <param name="workflowTypeName">The workflow type name.</param>
        /// <returns>The <see cref="WorkflowStub"/>.</returns>
        /// <remarks>
        /// <para>
        /// Unlike activity stubs, a workflow stub may only be used to launch a single
        /// workflow.  You'll need to create a new stub for each workflow you wish to
        /// invoke and then the first method called on a workflow stub must be
        /// the one of the methods tagged by <see cref="WorkflowMethodAttribute"/>.
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
        /// </remarks>
        public WorkflowStub NewUntypedWorkflowStub(WorkflowExecution execution, string workflowTypeName)
        {
            Covenant.Requires<ArgumentNullException>(execution != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowTypeName));
            EnsureNotDisposed();

            return new WorkflowStub(this)
            {
                Execution = execution
            };
        }

        /// <summary>
        /// Creates a typed workflow stub connected to a known workflow execution.
        /// This can be used to signal and query the workflow via the type-safe
        /// interface methods.
        /// </summary>
        /// <typeparam name="TWorkflowInterface">Identifies the workflow interface.</typeparam>
        /// <param name="workflowId">Specifies the workflow ID.</param>
        /// <param name="runId">Optionally specifies the workflow's run ID.</param>
        /// <param name="domain">Optionally specifies a domain that </param>
        /// <returns>The dynamically generated stub that implements the workflow methods defined by <typeparamref name="TWorkflowInterface"/>.</returns>
        /// <remarks>
        /// <para>
        /// Unlike activity stubs, a workflow stub may only be used to launch a single
        /// workflow.  You'll need to create a new stub for each workflow you wish to
        /// invoke and then the first method called on a workflow stub must be
        /// the one of the methods tagged by <see cref="WorkflowMethodAttribute"/>.
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
        /// </remarks>
        public TWorkflowInterface NewWorkflowStub<TWorkflowInterface>(string workflowId, string runId = null, string domain = null)
            where TWorkflowInterface : class
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowId));
            CadenceHelper.ValidateWorkflowInterface(typeof(TWorkflowInterface));
            EnsureNotDisposed();

            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates an untyped workflow stub to be used for launching a workflow.
        /// </summary>
        /// <param name="workflowTypeName">Specifies the workflow type name.</param>
        /// <param name="options">Optionally specifies the workflow options.</param>
        /// <returns>The new <see cref="WorkflowStub"/>.</returns>
        /// <remarks>
        /// <para>
        /// Unlike activity stubs, a workflow stub may only be used to launch a single
        /// workflow.  You'll need to create a new stub for each workflow you wish to
        /// invoke and then the first method called on a workflow stub must be
        /// the one of the methods tagged by <see cref="WorkflowMethodAttribute"/>.
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
        /// </remarks>
        public WorkflowStub NewWorkflowStub(string workflowTypeName, WorkflowOptions options = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowTypeName));
            EnsureNotDisposed();

            return new WorkflowStub(this)
            {
                WorkflowTypeName = workflowTypeName,
                Options          = options
            };
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
        /// <returns>The dynamically generated stub that implements the workflow methods defined by <typeparamref name="TWorkflowInterface"/>.</returns>
        /// <remarks>
        /// <para>
        /// Unlike activity stubs, a workflow stub may only be used to launch a single
        /// workflow.  You'll need to create a new stub for each workflow you wish to
        /// invoke and then the first method called on a workflow stub must be
        /// the one of the methods tagged by <see cref="WorkflowMethodAttribute"/>.
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
        /// </remarks>
        public TWorkflowInterface NewWorkflowStub<TWorkflowInterface>(WorkflowOptions options = null, string workflowTypeName = null)
            where TWorkflowInterface : class
        {
            CadenceHelper.ValidateWorkflowInterface(typeof(TWorkflowInterface));
            EnsureNotDisposed();

            return StubManager.NewWorkflowStub<TWorkflowInterface>(this, options: options, workflowTypeName: workflowTypeName);
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
        /// <param name="args">Specifies the workflow arguments encoded into a byte array.</param>
        /// <param name="options">Specifies the workflow options.</param>
        /// <returns>A <see cref="WorkflowExecution"/> identifying the new running workflow instance.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if there is no workflow registered for <paramref name="workflowTypeName"/>.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown if the request is not valid.</exception>
        /// <exception cref="CadenceWorkflowRunningException">Thrown if a workflow with this ID is already running.</exception>
        /// <remarks>
        /// This method kicks off a new workflow instance and returns after Cadence has
        /// queued the operation but the method <b>does not</b> wait for the workflow to
        /// complete.
        /// </remarks>
        internal async Task<WorkflowExecution> StartWorkflowAsync(string workflowTypeName, byte[] args, WorkflowOptions options)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowTypeName));
            EnsureNotDisposed();

            options = WorkflowOptions.Normalize(this, options);

            var reply = (WorkflowExecuteReply)await CallProxyAsync(
                new WorkflowExecuteRequest()
                {
                    Workflow = workflowTypeName,
                    Domain   = options.Domain,
                    Args     = args,
                    Options  = options.ToInternal()
                });

            reply.ThrowOnError();

            var execution = reply.Execution;

            return new WorkflowExecution(execution.ID, execution.RunID);
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
        /// Starts a child workflow.
        /// </summary>
        /// <param name="parentWorkflow">The parent workflow.</param>
        /// <param name="workflowTypeName">
        /// The type name used when registering the workers that will handle this workflow.
        /// This name will often be the fully qualified name of the workflow type but 
        /// this may have been customized when the workflow worker was registered.
        /// </param>
        /// <param name="args">Specifies the workflow arguments encoded into a byte array.</param>
        /// <param name="options">Specifies the workflow options.</param>
        /// <returns>A <see cref="ChildExecution"/> identifying the new running workflow instance.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if there is no workflow registered for <paramref name="workflowTypeName"/>.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown if the request is not valid.</exception>
        /// <exception cref="CadenceWorkflowRunningException">Thrown if a workflow with this ID is already running.</exception>
        /// <remarks>
        /// This method kicks off a new child workflow instance and returns after Cadence has
        /// queued the operation but the method <b>does not</b> wait for the workflow to
        /// complete.
        /// </remarks>
        internal async Task<ChildExecution> StartChildWorkflowAsync(Workflow parentWorkflow, string workflowTypeName, byte[] args, ChildWorkflowOptions options)
        {
            Covenant.Requires<ArgumentNullException>(parentWorkflow != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowTypeName));
            EnsureNotDisposed();

            options = options.Clone();

            if (!options.ScheduleToCloseTimeout.HasValue)
            {
                options.ScheduleToCloseTimeout = Settings.WorkflowScheduleToCloseTimeout;
            }

            if (!options.ScheduleToStartTimeout.HasValue)
            {
                options.ScheduleToStartTimeout = Settings.WorkflowScheduleToStartTimeout;
            }

            if (!options.TaskStartToCloseTimeout.HasValue)
            {
                options.TaskStartToCloseTimeout = Settings.WorkflowTaskStartToCloseTimeout;
            }

            var reply = (WorkflowExecuteChildReply)await CallProxyAsync(
                new WorkflowExecuteChildRequest()
                {
                    ContextId              = parentWorkflow.ContextId,
                    Workflow               = workflowTypeName,
                    Args                   = args,
                    Options                = options.ToInternal(),
                    ScheduleToStartTimeout = options.ScheduleToStartTimeout ?? Settings.WorkflowScheduleToStartTimeout
                });

            reply.ThrowOnError();

            return new ChildExecution(reply.Execution.ToPublic(), reply.ChildId);
        }

        /// <summary>
        /// Returns the result from a child workflow execution, blocking until the workflow
        /// completes if it is still running.
        /// </summary>
        /// <param name="parentWorkflow">The parent workflow.</param>
        /// <param name="execution">Identifies the child workflow execution.</param>
        /// <returns>The workflow result encoded as bytes or <c>null</c>.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the workflow no longer exists.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown if the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence problems.</exception>
        internal async Task<byte[]> GetChildWorkflowResultAsync(Workflow parentWorkflow, ChildExecution execution)
        {
            Covenant.Requires<ArgumentNullException>(parentWorkflow != null);
            Covenant.Requires<ArgumentNullException>(execution != null);
            EnsureNotDisposed();

            var reply = (WorkflowWaitForChildReply)await CallProxyAsync(
                new WorkflowWaitForChildRequest()
                {
                    ContextId = parentWorkflow.ContextId,
                    ChildId   = execution.ChildId
                });

            reply.ThrowOnError();

            return reply.Result;
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
        /// Transmits a signal to a running external workflow.  This low-level method accepts a byte array
        /// with the already encoded parameters.
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
        /// Transmits a signal to an external workflow, starting the workflow if it's not currently running.
        /// This low-level method accepts a byte array with the already encoded parameters.
        /// </summary>
        /// <param name="workflowTypeName">The target workflow type name.</param>
        /// <param name="signalName">Identifies the signal.</param>
        /// <param name="signalArgs">Optionally specifies the signal arguments as a byte array.</param>
        /// <param name="startArgs">Optionally specifies the workflow arguments.</param>
        /// <param name="options">Optionally specifies the options to be used for starting the workflow when required.</param>
        /// <returns>The <see cref="WorkflowExecution"/>.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the domain does not exist.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown if the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence problems.</exception>
        internal async Task<WorkflowExecution> SignalWorkflowWithStartAsync(string workflowTypeName, string signalName, byte[] signalArgs, byte[] startArgs, WorkflowOptions options)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowTypeName));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(signalName));
            EnsureNotDisposed();

            options = WorkflowOptions.Normalize(this, options);

            var reply = (WorkflowSignalWithStartReply)await CallProxyAsync(
                new WorkflowSignalWithStartRequest()
                {
                    Workflow     = workflowTypeName,
                    WorkflowId   = options.WorkflowId,
                    Options      = options.ToInternal(),
                    SignalName   = signalName,
                    SignalArgs   = signalArgs,
                    WorkflowArgs = startArgs,
                    Domain       = options.Domain
                });

            reply.ThrowOnError();

            return reply.Execution.ToPublic();
        }

        /// <summary>
        /// Queries an external workflow.  This low-level method accepts a byte array
        /// with the already encoded parameters and returns an encoded result.
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

        /// <summary>
        /// <para>
        /// Signals a child workflow.  This low-level method accepts a byte array
        /// with the already encoded parameters.
        /// </para>
        /// <note>
        /// This method blocks until the child workflow is scheduled and
        /// actually started on a worker.
        /// </note>
        /// </summary>
        /// <param name="parentWorkflow">The parent workflow.</param>
        /// <param name="execution">The child workflow execution.</param>
        /// <param name="signalName">Specifies the signal name.</param>
        /// <param name="signalArgs">Specifies the signal arguments as an encoded byte array.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the named domain does not exist.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="CadenceServiceBusyException">Thrown when Cadence is too busy.</exception>
        internal async Task SignalChildWorkflowAsync(Workflow parentWorkflow, ChildExecution execution, string signalName, byte[] signalArgs)
        {
            Covenant.Requires<ArgumentNullException>(parentWorkflow != null);
            Covenant.Requires<ArgumentNullException>(execution != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(signalName));

            var reply = (WorkflowSignalChildReply)await CallProxyAsync(
                new WorkflowSignalChildRequest()
                {
                    ContextId   = parentWorkflow.ContextId,
                    ChildId     = execution.ChildId,
                    SignalName  = signalName,
                    SignalArgs  = signalArgs
                }); ;

            reply.ThrowOnError();
        }
    }
}
