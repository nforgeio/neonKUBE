//-----------------------------------------------------------------------------
// FILE:	    TemporalClient.Activity.cs
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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Tasks;
using Neon.Temporal;
using Neon.Temporal.Internal;

namespace Neon.Temporal
{
    public partial class TemporalClient
    {
        //---------------------------------------------------------------------
        // Temporal activity related operations.

        /// <summary>
        /// Raised when a normal (non-local) is executed.  This is used internally
        /// for unit tests that verify that activity options are configured correctly. 
        /// </summary>
        internal event EventHandler<ActivityOptions> ActivityExecuteEvent;

        /// <summary>
        /// Raised when a local is executed.  This is used internally for unit tests 
        /// that verify that activity options are configured correctly. 
        /// </summary>
        internal event EventHandler<LocalActivityOptions> LocalActivityExecuteEvent;

        /// <summary>
        /// Raises the <see cref="ActivityExecuteEvent"/>.
        /// </summary>
        /// <param name="options">The activity options.</param>
        internal void RaiseActivityExecuteEvent(ActivityOptions options)
        {
            ActivityExecuteEvent?.Invoke(this, options);
        }

        /// <summary>
        /// Raises the <see cref="LocalActivityExecuteEvent"/>.
        /// </summary>
        /// <param name="options">The activity options.</param>
        internal void RaiseLocalActivityExecuteEvent(LocalActivityOptions options)
        {
            LocalActivityExecuteEvent?.Invoke(this, options);
        }

        /// <summary>
        /// Used to send record activity heartbeat externally by task token.
        /// </summary>
        /// <param name="taskToken">The opaque base-64 encoded activity task token.</param>
        /// <param name="details">Optional heartbeart details.</param>
        /// <param name="namespace">Optionally overrides the default <see cref="TemporalClient"/> namespace.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task ActivityHeartbeatByTokenAsync(string taskToken, object details = null, string @namespace = null)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(taskToken), nameof(taskToken));
            EnsureNotDisposed();

            var reply = (ActivityRecordHeartbeatReply)await CallProxyAsync(
                new ActivityRecordHeartbeatRequest()
                {
                    Namespace = ResolveNamespace(@namespace),
                    TaskToken = Convert.FromBase64String(taskToken),
                    Details   = GetClient(ClientId).DataConverter.ToData(details)
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Used to send record activity heartbeat externally by <see cref="WorkflowExecution"/> and activity ID.
        /// </summary>
        /// <param name="execution">The workflow execution.</param>
        /// <param name="activityId">The activity ID.</param>
        /// <param name="details">Optional heartbeart details.</param>
        /// <param name="namespace">Optionally overrides the default <see cref="TemporalClient"/> namespace.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task ActivityHeartbeatByIdAsync(WorkflowExecution execution, string activityId, object details = null, string @namespace = null)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(execution != null, nameof(execution));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(activityId), nameof(activityId));
            EnsureNotDisposed();

            var reply = (ActivityRecordHeartbeatReply)await CallProxyAsync(
                new ActivityRecordHeartbeatRequest()
                {
                    Namespace  = ResolveNamespace(@namespace),
                    WorkflowId = execution.WorkflowId,
                    RunId      = execution.RunId,
                    ActivityId = activityId,
                    Details    = GetClient(ClientId).DataConverter.ToData(details)
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Used to externally complete an activity identified by task token.
        /// </summary>
        /// <param name="taskToken">The opaque base-64 encoded activity task token.</param>
        /// <param name="namespace">Optionally overrides the default <see cref="TemporalClient"/> namespace.</param>
        /// <param name="result">Passed as the activity result for activity success.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="EntityNotExistsException">Thrown if the activity no longer exists.</exception>
        public async Task ActivityCompleteByTokenAsync(string taskToken, object result = null, string @namespace = null)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(taskToken), nameof(taskToken));
            EnsureNotDisposed();

            var reply = (ActivityCompleteReply)await CallProxyAsync(
                new ActivityCompleteRequest()
                {
                    Namespace = ResolveNamespace(@namespace),
                    TaskToken = Convert.FromBase64String(taskToken),
                    Result    = GetClient(ClientId).DataConverter.ToData(result)
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Used to externally complete an activity identified by <see cref="WorkflowExecution"/> and activity ID.
        /// </summary>
        /// <param name="execution">The workflow execution.</param>
        /// <param name="activityId">The activity ID.</param>
        /// <param name="result">Passed as the activity result for activity success.</param>
        /// <param name="namespace">Optionally overrides the default <see cref="TemporalClient"/> namespace.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="EntityNotExistsException">Thrown if the activity no longer exists.</exception>
        public async Task ActivityCompleteByIdAsync(WorkflowExecution execution, string activityId, object result = null, string @namespace = null)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(execution != null, nameof(execution));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(activityId), nameof(activityId));
            EnsureNotDisposed();

            var reply = (ActivityCompleteReply)await CallProxyAsync(
                new ActivityCompleteRequest()
                {
                    Namespace  = ResolveNamespace(@namespace),
                    WorkflowId = execution.WorkflowId,
                    RunId      = execution.RunId,
                    ActivityId = activityId,
                    Result     = GetClient(ClientId).DataConverter.ToData(result)
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Used to externally fail an activity by task token.
        /// </summary>
        /// <param name="taskToken">The opaque base-64 encoded activity task token.</param>
        /// <param name="error">Specifies the activity error.</param>
        /// <param name="namespace">Optionally overrides the default <see cref="TemporalClient"/> namespace.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="EntityNotExistsException">Thrown if the activity no longer exists.</exception>
        public async Task ActivityErrorByTokenAsync(string taskToken, Exception error, string @namespace = null)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(taskToken), nameof(taskToken));
            Covenant.Requires<ArgumentNullException>(error != null, nameof(error));
            EnsureNotDisposed();

            var reply = (ActivityCompleteReply)await CallProxyAsync(
                new ActivityCompleteRequest()
                {
                    Namespace = ResolveNamespace(@namespace),
                    TaskToken = Convert.FromBase64String(taskToken),
                    Error     = new TemporalError(error)
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Used to externally fail an activity by <see cref="WorkflowExecution"/> and activity ID.
        /// </summary>
        /// <param name="execution">The workflowm execution.</param>
        /// <param name="activityId">The activity ID.</param>
        /// <param name="error">Specifies the activity error.</param>
        /// <param name="namespace">Optionally overrides the default <see cref="TemporalClient"/> namespace.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="EntityNotExistsException">Thrown if the activity no longer exists.</exception>
        public async Task ActivityErrorByIdAsync(WorkflowExecution execution, string activityId, Exception error, string @namespace = null)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(execution != null, nameof(execution));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(activityId), nameof(activityId));
            Covenant.Requires<ArgumentNullException>(error != null, nameof(error));
            EnsureNotDisposed();

            var reply = (ActivityCompleteReply)await CallProxyAsync(
                new ActivityCompleteRequest()
                {
                    Namespace  = ResolveNamespace(@namespace),
                    WorkflowId = execution.WorkflowId,
                    RunId      = execution.RunId,
                    ActivityId = activityId,
                    Error      = new TemporalError(error)
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Ensures that the activity stub type for the <typeparamref name="TActivityInterface"/> has
        /// already been generated.  This can be used to prevent decision task times out in
        /// relatively rare situations.
        /// </summary>
        /// <typeparam name="TActivityInterface">The activity interface.</typeparam>
        /// <remarks>
        /// <para>
        /// By default, <b>Neon.Temporal</b> API generates and compiles activity and workflow stub
        /// types on demand when stubs are created by workflows.  This takes about 500ms the first
        /// time a stub is created for any given activity or workflow type.  These generated types
        /// are then cached in the current process such that subsequent stubs created for the type
        /// won't need to be regenerated.
        /// </para>
        /// <para>
        /// This can work OK for less complex workflows, but it's possible for a workflow to create
        /// so many stubs at once (say at the top of the workflow implementation) such that the
        /// cumulative compile time can exceed the decision task time out, failing the workflow.
        /// For example, say a workflow constructs 22 activity stubs immediately after starting.
        /// At 500ms per stub compilation this could take something like 11 seconds to complete,
        /// which exceeds the default decision task timeout of 10 seconds.
        /// </para>
        /// <para>
        /// When this happens, you can pre-build and cache activity and workflow stubs before
        /// starting your workers by calling <see cref="BuildActivityStub{TActivityInterface}"/>
        /// and/or <see cref="BuildWorkflowStub{TActivityInterface}"/>
        /// </para>
        /// </remarks>
        public void BuildActivityStub<TActivityInterface>()
            where TActivityInterface : IActivity
        {
            StubManager.GetActivityStub(typeof(TActivityInterface));
        }
    }
}
