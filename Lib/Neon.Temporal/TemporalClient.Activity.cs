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
        /// Registers an activity implementation with Temporal.
        /// </summary>
        /// <typeparam name="TActivity">The <see cref="ActivityBase"/> derived class implementing the activity.</typeparam>
        /// <param name="activityTypeName">
        /// Optionally specifies a custom activity type name that will be used 
        /// for identifying the activity implementation in Temporal.  This defaults
        /// to the fully qualified <typeparamref name="TActivity"/> type name.
        /// </param>
        /// <param name="domain">Optionally overrides the default client domain.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if a different activity class has already been registered for <paramref name="activityTypeName"/>.</exception>
        /// <exception cref="ActivityWorkerStartedException">
        /// Thrown if an activity worker has already been started for the client.  You must
        /// register activity implementations before starting workers.
        /// </exception>
        /// <remarks>
        /// <note>
        /// Be sure to register all of your activity implementations before starting workers.
        /// </note>
        /// </remarks>
        public async Task RegisterActivityAsync<TActivity>(string activityTypeName = null, string domain = null)
            where TActivity : ActivityBase
        {
            await SyncContext.ClearAsync;
            TemporalHelper.ValidateActivityImplementation(typeof(TActivity));
            TemporalHelper.ValidateActivityTypeName(activityTypeName);
            EnsureNotDisposed();

            if (activityWorkerStarted)
            {
                throw new ActivityWorkerStartedException();
            }

            var activityType = typeof(TActivity);

            if (string.IsNullOrEmpty(activityTypeName))
            {
                activityTypeName = TemporalHelper.GetActivityTypeName(activityType, activityType.GetCustomAttribute<ActivityAttribute>());
            }

            await ActivityBase.RegisterAsync(this, activityType, activityTypeName, ResolveDomain(domain));

            lock (registeredActivityTypes)
            {
                registeredActivityTypes.Add(TemporalHelper.GetActivityInterface(typeof(TActivity)));
            }
        }

        /// <summary>
        /// Scans the assembly passed looking for activity implementations derived from
        /// <see cref="ActivityBase"/> and tagged by <see cref="ActivityAttribute"/> and
        /// registers them with Temporal.
        /// </summary>
        /// <param name="assembly">The target assembly.</param>
        /// <param name="domain">Optionally overrides the default client domain.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="TypeLoadException">
        /// Thrown for types tagged by <see cref="ActivityAttribute"/> that are not 
        /// derived from <see cref="ActivityBase"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">Thrown if one of the tagged classes conflict with an existing registration.</exception>
        /// <exception cref="ActivityWorkerStartedException">
        /// Thrown if an activity worker has already been started for the client.  You must
        /// register activity implementations before starting workers.
        /// </exception>
        /// <remarks>
        /// <note>
        /// Be sure to register all of your activity implementations before starting workers.
        /// </note>
        /// </remarks>
        public async Task RegisterAssemblyActivitiesAsync(Assembly assembly, string domain = null)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(assembly != null, nameof(assembly));
            EnsureNotDisposed();

            if (activityWorkerStarted)
            {
                throw new ActivityWorkerStartedException();
            }

            foreach (var type in assembly.GetTypes().Where(t => t.IsClass))
            {
                var activityAttribute = type.GetCustomAttribute<ActivityAttribute>();

                if (activityAttribute != null && activityAttribute.AutoRegister)
                {
                    var activityTypeName = TemporalHelper.GetActivityTypeName(type, activityAttribute);

                    await ActivityBase.RegisterAsync(this, type, activityTypeName, ResolveDomain(domain));

                    lock (registeredActivityTypes)
                    {
                        registeredActivityTypes.Add(TemporalHelper.GetActivityInterface(type));
                    }
                }
            }
        }

        /// <summary>
        /// Used to send record activity heartbeat externally by task token.
        /// </summary>
        /// <param name="taskToken">The opaque base-64 encoded activity task token.</param>
        /// <param name="details">Optional heartbeart details.</param>
        /// <param name="domain">Optionally overrides the default <see cref="TemporalClient"/> domain.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task ActivityHeartbeatByTokenAsync(string taskToken, object details = null, string domain = null)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(taskToken), nameof(taskToken));
            EnsureNotDisposed();

            var reply = (ActivityRecordHeartbeatReply)await CallProxyAsync(
                new ActivityRecordHeartbeatRequest()
                {
                    Domain    = ResolveDomain(domain),
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
        /// <param name="domain">Optionally overrides the default <see cref="TemporalClient"/> domain.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task ActivityHeartbeatByIdAsync(WorkflowExecution execution, string activityId, object details = null, string domain = null)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(execution != null, nameof(execution));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(activityId), nameof(activityId));
            EnsureNotDisposed();

            var reply = (ActivityRecordHeartbeatReply)await CallProxyAsync(
                new ActivityRecordHeartbeatRequest()
                {
                    Domain     = ResolveDomain(domain),
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
        /// <param name="domain">Optionally overrides the default <see cref="TemporalClient"/> domain.</param>
        /// <param name="result">Passed as the activity result for activity success.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="EntityNotExistsException">Thrown if the activity no longer exists.</exception>
        public async Task ActivityCompleteByTokenAsync(string taskToken, object result = null, string domain = null)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(taskToken), nameof(taskToken));
            EnsureNotDisposed();

            var reply = (ActivityCompleteReply)await CallProxyAsync(
                new ActivityCompleteRequest()
                {
                    Domain    = ResolveDomain(domain),
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
        /// <param name="domain">Optionally overrides the default <see cref="TemporalClient"/> domain.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="EntityNotExistsException">Thrown if the activity no longer exists.</exception>
        public async Task ActivityCompleteByIdAsync(WorkflowExecution execution, string activityId, object result = null, string domain = null)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(execution != null, nameof(execution));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(activityId), nameof(activityId));
            EnsureNotDisposed();

            var reply = (ActivityCompleteReply)await CallProxyAsync(
                new ActivityCompleteRequest()
                {
                    Domain     = ResolveDomain(domain),
                    WorkflowId = execution.WorkflowId,
                    RunId      = execution.RunId,
                    ActivityId = activityId,
                    Result     = GetClient(ClientId).DataConverter.ToData(result)
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Used to externally cancel an activity identified by task token.
        /// </summary>
        /// <param name="taskToken">The opaque base-64 encoded activity task token.</param>
        /// <param name="domain">Optionally overrides the default <see cref="TemporalClient"/> domain.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="EntityNotExistsException">Thrown if the activity no longer exists.</exception>
        public async Task ActivityCancelByTokenAsync(string taskToken, string domain = null)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(taskToken), nameof(taskToken));
            EnsureNotDisposed();

            var reply = (ActivityCompleteReply)await CallProxyAsync(
                new ActivityCompleteRequest()
                {
                    Domain    = ResolveDomain(domain),
                    TaskToken = Convert.FromBase64String(taskToken),
                    Error     = new TemporalError(new CancelledException("Cancelled"))
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Used to externally cancel an activity identified by <see cref="WorkflowExecution"/> and activity ID.
        /// </summary>
        /// <param name="execution">The workflow execution.</param>
        /// <param name="activityId">The activity ID.</param>
        /// <param name="domain">Optionally overrides the default <see cref="TemporalClient"/> domain.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="EntityNotExistsException">Thrown if the activity no longer exists.</exception>
        public async Task ActivityCancelByIdAsync(WorkflowExecution execution, string activityId, string domain = null)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(execution != null, nameof(execution));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(activityId), nameof(activityId));
            EnsureNotDisposed();

            var reply = (ActivityCompleteReply)await CallProxyAsync(
                new ActivityCompleteRequest()
                {
                    Domain     = ResolveDomain(domain),
                    WorkflowId = execution.WorkflowId,
                    RunId      = execution.RunId,
                    ActivityId = activityId,
                    Error      = new TemporalError(new CancelledException("Cancelled"))
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Used to externally fail an activity by task token.
        /// </summary>
        /// <param name="taskToken">The opaque base-64 encoded activity task token.</param>
        /// <param name="error">Specifies the activity error.</param>
        /// <param name="domain">Optionally overrides the default <see cref="TemporalClient"/> domain.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="EntityNotExistsException">Thrown if the activity no longer exists.</exception>
        public async Task ActivityErrorByTokenAsync(string taskToken, Exception error, string domain = null)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(taskToken), nameof(taskToken));
            Covenant.Requires<ArgumentNullException>(error != null, nameof(error));
            EnsureNotDisposed();

            var reply = (ActivityCompleteReply)await CallProxyAsync(
                new ActivityCompleteRequest()
                {
                    Domain    = ResolveDomain(domain),
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
        /// <param name="domain">Optionally overrides the default <see cref="TemporalClient"/> domain.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="EntityNotExistsException">Thrown if the activity no longer exists.</exception>
        public async Task ActivityErrorByIdAsync(WorkflowExecution execution, string activityId, Exception error, string domain = null)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(execution != null, nameof(execution));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(activityId), nameof(activityId));
            Covenant.Requires<ArgumentNullException>(error != null, nameof(error));
            EnsureNotDisposed();

            var reply = (ActivityCompleteReply)await CallProxyAsync(
                new ActivityCompleteRequest()
                {
                    Domain     = ResolveDomain(domain),
                    WorkflowId = execution.WorkflowId,
                    RunId      = execution.RunId,
                    ActivityId = activityId,
                    Error      = new TemporalError(error)
                });

            reply.ThrowOnError();
        }
    }
}
