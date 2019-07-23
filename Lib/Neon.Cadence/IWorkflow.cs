//-----------------------------------------------------------------------------
// FILE:	    IWorkflow.cs
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
using System.Threading;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Diagnostics;

namespace Neon.Cadence
{
    /// <summary>
    /// Provides useful information and functionality for workflow implementations.
    /// This will be available via the <see cref="IWorkflowBase.Workflow"/> property.
    /// </summary>
    public interface IWorkflow
    {
        /// <summary>
        /// Returns the <see cref="CadenceClient"/> managing this workflow.
        /// </summary>
        CadenceClient Client { get; }

        /// <summary>
        /// Creates a client stub that can be used to launch one or more activity instances
        /// via the type-safe interface methods.
        /// </summary>
        /// <typeparam name="TActivity">The activity interface.</typeparam>
        /// <param name="options">Optionally specifies the activity options.</param>
        /// <returns>The new <see cref="IActivityStub"/>.</returns>
        /// <remarks>
        /// <note>
        /// Unlike workflow stubs, a single activity stub instance can be used to
        /// launch multiple activities.
        /// </note>
        /// <para>
        /// Activities launched by the returned stub will be scheduled normally
        /// by Cadence to executed on one of the worker nodes.  Use <see cref="NewLocalActivityStub{TActivity}(ActivityOptions)"/>
        /// to execute short-lived activities locally within the current process.
        /// </para>
        /// </remarks>
        TActivity NewActivityStub<TActivity>(ActivityOptions options = null)
            where TActivity : IActivityBase;

        /// <summary>
        /// Creates a new untyped activity client stub that can be used to launch activities.
        /// </summary>
        /// <param name="options">Optionally specifies the activity options.</param>
        /// <returns>The new <see cref="IActivityStub"/>.</returns>
        /// <remarks>
        /// <note>
        /// Unlike workflow stubs, a single activity stub instance can be used to
        /// launch multiple activities.
        /// </note>
        /// <para>
        /// Activities launched by the returned stub will be scheduled normally
        /// by Cadence to executed on one of the worker nodes.  Use <see cref="NewLocalActivityStub{TActivity}(ActivityOptions)"/>
        /// to execute short-lived activities locally within the current process.
        /// </para>
        /// </remarks>
        IActivityStub NewUntypedActivityStub(ActivityOptions options = null);

        /// <summary>
        /// Creates a client stub that can be used to launch one or more local activity 
        /// instances via the type-safe interface methods.
        /// </summary>
        /// <typeparam name="TActivity">The activity interface.</typeparam>
        /// <param name="options">Optionally specifies activity options.</param>
        /// <returns>The new <see cref="IActivityStub"/>.</returns>
        /// <remarks>
        /// <note>
        /// Unlike workflow stubs, a single activity stub instance can be used to
        /// launch multiple activities.
        /// </note>
        /// <para>
        /// Activities launched by the returned stub will be executed in the current
        /// process.  This is intended to easily and efficiently execute activities
        /// that will complete very quickly (usually within a few seconds).  Local
        /// activities are similar to normal activities with these differences:
        /// </para>
        /// <list type="bullet">
        ///     <item>
        ///     Local activities are always scheduled to executed within the current process.
        ///     </item>
        ///     <item>
        ///     Local activity types do not need to be registered and local activities.
        ///     </item>
        ///     <item>
        ///     Local activities must complete within the <see cref="WorkflowOptions.DecisionTaskStartToCloseTimeout"/>.
        ///     This defaults to 10 seconds and can be set to a maximum of 60 seconds.
        ///     </item>
        ///     <item>
        ///     Local activities cannot heartbeat.
        ///     </item>
        /// </list>
        /// </remarks>
        TActivity NewLocalActivityStub<TActivity>(ActivityOptions options = null)
            where TActivity : IActivityBase;

        /// <summary>
        /// Creates a workflow client stub that can be used to launch, signal, and query child
        /// workflows via the type-safe workflow interface methods.
        /// </summary>
        /// <typeparam name="TWorkflow">The workflow interface.</typeparam>
        /// <param name="options"></param>
        /// <returns>The child workflow stub.</returns>
        /// <remarks>
        /// Unlike activity stubs, a workflow stub may only be used to launch a single
        /// workflow.  You'll need to create a new stub for each workflow you wish to
        /// invoke and then the first method called on a workflow stub must be
        /// the one of the methods tagged by <see cref="WorkflowMethodAttribute"/>.
        /// </remarks>
        TWorkflow NewChildWorkflowStub<TWorkflow>(ChildWorkflowOptions options = null)
            where TWorkflow : IWorkflowBase;

        /// <summary>
        /// Creates a workflow client stub that can be used communicate with an
        /// existing workflow identified by <see cref="WorkflowExecution"/>.
        /// </summary>
        /// <typeparam name="TWorkflow">The workflow interface.</typeparam>
        /// <param name="execution">Identifies the workflow execution.</param>
        /// <param name="domain">Optionally specifies the domain.  This defaults to the domain of the parent workflow.</param>
        /// <returns>The workflow stub.</returns>
        TWorkflow NewExternalWorkflowStub<TWorkflow>(WorkflowExecution execution, string domain = null)
            where TWorkflow : IWorkflowBase;

        /// <summary>
        /// Creates a workflow client stub that can be used communicate with an
        /// existing workflow identified by workflow ID.
        /// </summary>
        /// <typeparam name="TWorkflow">The workflow interface.</typeparam>
        /// <param name="workflowId">Identifies the workflow.</param>
        /// <param name="domain">Optionally specifies the domain.  This defaults to the domain of the parent workflow.</param>
        /// <returns>The workflow stub.</returns>
        TWorkflow NewExternalWorkflowStub<TWorkflow>(string workflowId, string domain = null)
            where TWorkflow : IWorkflowBase;

        /// <summary>
        /// Creates an untyped child workflow stub that can be used to start, signal, and query
        /// child workflows.
        /// </summary>
        /// <param name="workflowTypeName">The workflow type name.</param>
        /// <param name="options">Optionally specifies the child workflow options.</param>
        /// <returns></returns>
        /// <remarks>
        /// Unlike activity stubs, a workflow stub may only be used to launch a single
        /// workflow.  You'll need to create a new stub for each workflow you wish to
        /// invoke and then the first method called on a workflow stub must be
        /// the one of the methods tagged by <see cref="WorkflowMethodAttribute"/>.
        /// </remarks>
        IChildWorkflowStub NewUntypedChildWorkflowStub(string workflowTypeName, ChildWorkflowOptions options = null);

        /// <summary>
        /// Creates an untyped stub that can be used to signal or cancel a child
        /// workflow identified by its <see cref="WorkflowExecution"/>.
        /// </summary>
        /// <param name="execution">The target <see cref="WorkflowExecution"/>.</param>
        /// <param name="domain">Optionally specifies the target domain.  This defaults to the parent workflow's domain.</param>
        /// <returns>The <see cref="IExternalWorkflowStub"/>.</returns>
        IExternalWorkflowStub NewUntypedExternalWorkflowStub(WorkflowExecution execution, string domain = null);

        /// <summary>
        /// Creates an untyped stub that can be used to signal or cancel a child
        /// workflow identified by its workflow ID.
        /// </summary>
        /// <param name="workflowId">The target workflow ID.</param>
        /// <param name="domain">Optionally specifies the target domain.  This defaults to the parent workflow's domain.</param>
        /// <returns>The <see cref="IExternalWorkflowStub"/>.</returns>
        IExternalWorkflowStub NewUntypedExternalWorkflowStub(string workflowId, string domain = null);

        /// <summary>
        /// Returns the <see cref="WorkflowExecution"/> for a child workflow.
        /// </summary>
        /// <param name="childWorkflowStub">
        /// A child workflow stub created.  This may be a type-safe,
        /// external, or untyped workflow stub instance.
        /// </param>
        /// <returns>The <see cref="WorkflowExecution"/>.</returns>
        Task<WorkflowExecution> GetWorkflowExecutionAsync(object childWorkflowStub);

        /// <summary>
        /// Creates a typed-safe client stub that can be used to continue the workflow as a new run.
        /// </summary>
        /// <typeparam name="TWorkflow">The workflow interface.</typeparam>
        /// <param name="options">Optionally specifies the new options to use when continuing the workflow.</param>
        /// <returns>The type-safe stub.</returns>
        /// <remarks>
        /// The workflow stub returned is intended just for continuing the workflow by
        /// calling one of the workflow entry point methods tagged by <see cref="WorkflowMethodAttribute"/>.
        /// Any signal or query methods defined by <typeparamref name="TWorkflow"/> will 
        /// throw a <see cref="InvalidOperationException"/> when called.
        /// </remarks>
        Task<TWorkflow> NewContinueAsNewStub<TWorkflow>(ContinueAsNewOptions options = null)
            where TWorkflow : IWorkflowBase;

        /// <summary>
        /// Continues the current workflow as a new run using the same workflow options.
        /// </summary>
        /// <param name="args">The new run arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        Task ContinueAsNew(params object[] args);

        /// <summary>
        /// Continues the current workflow as a new run allowing the specification of
        /// new workflow uptions.
        /// </summary>
        /// <param name="options">The continuation options.</param>
        /// <param name="args">The new run arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        Task ContinueAsNew(ContinueAsNewOptions options, params object[] args);

        /// <summary>
        /// Returns information about the running workflow.
        /// </summary>
        WorkflowInfo WorkflowInfo { get; }

        // $todo(jeff.lill): Do something equivalent to the Java client's cancellation scope.
        // $todo(jeff.lill): Do something equivalent to the Java client's queues.

        /// <summary>
        /// <para>
        /// Returns the current workflow time (UTC).
        /// </para>
        /// <note>
        /// This must used instead of calling <see cref="DateTime.UtcNow"/> or any other
        /// time method to guarantee determinism when a workflow is replayed.
        /// </note>
        /// </summary>
        DateTime UtcNow { get; }

        /// <summary>
        /// <para>
        /// Pauses the workflow for the specified time span.
        /// </para>
        /// <note>
        /// This must be used instead of calling <see cref="Task.Delay(TimeSpan)"/> or <see cref="Thread.Sleep(TimeSpan)"/>
        /// to guarantee determinism when a workflow is replayed.
        /// </note>
        /// <note>
        /// Cadence time interval resolution is limited to whole seconds and
        /// the duration will be rounded up to the nearest second.
        /// </note>
        /// </summary>
        /// <param name="duration">The duration to pause.</param>
        /// <returns>The tracking <see cref="Task"/></returns>
        Task SleepAsync(TimeSpan duration);

        /// <summary>
        /// <para>
        /// Returns a replay safe <see cref="Guid"/>.
        /// </para>
        /// <note>
        /// This must be used instead of calling <see cref="Guid.NewGuid"/>
        /// to guarantee determinism when a workflow is replayed.
        /// </note>
        /// </summary>
        /// <returns>The new <see cref="Guid"/>.</returns>
        Task<Guid> NewGuidAsync();

        /// <summary>
        /// <para>
        /// Returns a replay safe random number generator which will bee seeded 
        /// differently for each workflow run.
        /// </para>
        /// <note>
        /// This must be used instead of <see cref="Random"/> to guarantee 
        /// determinism when a workflow is replayed.
        /// </note>
        /// </summary>
        /// <returns></returns>
        Task<Random> NewRandomAsync();

        /// <summary>
        /// <para>
        /// Indicates whether the workflow code is being replayed.
        /// </para>
        /// <note>
        /// <b>WARNING:</b> Never have workflow logic depend on this flag as doing so will
        /// break determinism.  The only reasonable uses for this flag are for managing
        /// external things like logging or metric reporting.
        /// </note>
        /// </summary>
        bool IsReplaying { get; }


    }
}
