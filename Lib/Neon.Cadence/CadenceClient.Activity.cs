//-----------------------------------------------------------------------------
// FILE:	    CadenceClient.Activity.cs
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

namespace Neon.Cadence
{
    public partial class CadenceClient
    {
        //---------------------------------------------------------------------
        // Cadence activity related operations.

        /// <summary>
        /// Registers an activity implementation with Cadence.
        /// </summary>
        /// <typeparam name="TActivity">The <see cref="ActivityBase"/> derived class implementing the activity.</typeparam>
        /// <param name="activityTypeName">
        /// Optionally specifies a custom activity type name that will be used 
        /// for identifying the activity implementation in Cadence.  This defaults
        /// to the fully qualified <typeparamref name="TActivity"/> type name.
        /// </param>
        /// <param name="domain">Optionally overrides the default client domain.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if a different activity class has already been registered for <paramref name="activityTypeName"/>.</exception>
        /// <exception cref="CadenceActivityWorkerStartedException">
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
            CadenceHelper.ValidateActivityImplementation(typeof(TActivity));
            CadenceHelper.ValidateActivityTypeName(activityTypeName);
            EnsureNotDisposed();

            if (activityWorkerStarted)
            {
                throw new CadenceActivityWorkerStartedException();
            }

            var activityType = typeof(TActivity);

            if (string.IsNullOrEmpty(activityTypeName))
            {
                activityTypeName = CadenceHelper.GetActivityTypeName(activityType, activityType.GetCustomAttribute<ActivityAttribute>());
            }

            await ActivityBase.RegisterAsync(this, activityType, activityTypeName, ResolveDomain(domain));
        }

        /// <summary>
        /// Scans the assembly passed looking for activity implementations derived from
        /// <see cref="ActivityBase"/> and tagged by <see cref="ActivityAttribute"/> and
        /// registers them with Cadence.
        /// </summary>
        /// <param name="assembly">The target assembly.</param>
        /// <param name="domain">Optionally overrides the default client domain.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="TypeLoadException">
        /// Thrown for types tagged by <see cref="ActivityAttribute"/> that are not 
        /// derived from <see cref="ActivityBase"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">Thrown if one of the tagged classes conflict with an existing registration.</exception>
        /// <exception cref="CadenceActivityWorkerStartedException">
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
            Covenant.Requires<ArgumentNullException>(assembly != null);
            EnsureNotDisposed();

            if (activityWorkerStarted)
            {
                throw new CadenceActivityWorkerStartedException();
            }

            foreach (var type in assembly.GetTypes().Where(t => t.IsClass))
            {
                var activityAttribute = type.GetCustomAttribute<ActivityAttribute>();

                if (activityAttribute != null && activityAttribute.AutoRegister)
                {
                    var activityTypeName = CadenceHelper.GetActivityTypeName(type, activityAttribute);

                    await ActivityBase.RegisterAsync(this, type, activityTypeName, ResolveDomain(domain));
                }
            }
        }

        /// <summary>
        /// Used to send record activity heartbeat externally by task token.
        /// </summary>
        /// <param name="taskToken">The opaque activity task token.</param>
        /// <param name="details">Optional heartbeart details.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task RecordActivityHeartbeatAsync(byte[] taskToken, byte[] details = null)
        {
            Covenant.Requires<ArgumentNullException>(taskToken != null && taskToken.Length > 0);
            EnsureNotDisposed();

            var reply = (ActivityRecordHeartbeatReply)await CallProxyAsync(
                new ActivityRecordHeartbeatRequest()
                {
                    TaskToken = taskToken,
                    Details   = details
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Used to send record activity heartbeat externally by activity ID.
        /// </summary>
        /// <param name="workflowId">The workflow ID.</param>
        /// <param name="runId">The workflow run ID.</param>
        /// <param name="activityId">The activity ID.</param>
        /// <param name="details">Optional heartbeart details.</param>
        /// <param name="domain">Optionally overrides the default <see cref="CadenceClient"/> domain.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task RecordActivityHeartbeatByIdAsync(string workflowId, string runId, string activityId, byte[] details = null, string domain = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowId));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(runId));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(activityId));
            EnsureNotDisposed();

            var reply = (ActivityRecordHeartbeatReply)await CallProxyAsync(
                new ActivityRecordHeartbeatRequest()
                {
                    Domain     = ResolveDomain(domain),
                    WorkflowId = workflowId,
                    RunId      = runId,
                    ActivityId = activityId,
                    Details    = details
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Used to externally complete an activity identified by task token.
        /// </summary>
        /// <param name="taskToken">The opaque activity task token.</param>
        /// <param name="result">Passed as the activity result for activity success.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the activity no longer exists.</exception>
        public async Task RespondActivityCompletedAsync(byte[] taskToken, byte[] result = null)
        {
            Covenant.Requires<ArgumentNullException>(taskToken != null && taskToken.Length > 0);
            EnsureNotDisposed();

            var reply = (ActivityCompleteReply)await CallProxyAsync(
                new ActivityCompleteRequest()
                {
                    TaskToken = taskToken,
                    Result    = result
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Used to externally complete an activity identified by activity ID.
        /// </summary>
        /// <param name="workflowId">The workflow ID.</param>
        /// <param name="runId">The workflow run ID.</param>
        /// <param name="activityId">The activity ID.</param>
        /// <param name="result">Passed as the activity result for activity success.</param>
        /// <param name="domain">Optionally overrides the default <see cref="CadenceClient"/> domain.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the activity no longer exists.</exception>
        public async Task RespondActivityCompletedByIdAsync(string workflowId, string runId, string activityId, byte[] result = null, string domain = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowId));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(runId));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(activityId));
            EnsureNotDisposed();

            var reply = (ActivityCompleteReply)await CallProxyAsync(
                new ActivityCompleteRequest()
                {
                    Domain     = ResolveDomain(domain),
                    WorkflowId = workflowId,
                    RunId      = runId,
                    ActivityId = activityId,
                    Result     = result
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Used to externally cancel an activity identified by task token.
        /// </summary>
        /// <param name="taskToken">The opaque activity task token.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the activity no longer exists.</exception>
        public async Task RespondActivityCancelAsync(byte[] taskToken)
        {
            Covenant.Requires<ArgumentNullException>(taskToken != null && taskToken.Length > 0);
            EnsureNotDisposed();

            var reply = (ActivityCompleteReply)await CallProxyAsync(
                new ActivityCompleteRequest()
                {
                    TaskToken = taskToken,
                    Error     = new CadenceError(new CadenceCancelledException("Cancelled"))
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Used to externally cancel an activity identified by activity ID.
        /// </summary>
        /// <param name="workflowId">The workflow ID.</param>
        /// <param name="runId">The workflow run ID.</param>
        /// <param name="activityId">The activity ID.</param>
        /// <param name="domain">Optionally overrides the default <see cref="CadenceClient"/> domain.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the activity no longer exists.</exception>
        public async Task RespondActivityCancelByIdAsync(string workflowId, string runId, string activityId, string domain = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowId));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(runId));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(activityId));
            EnsureNotDisposed();

            var reply = (ActivityCompleteReply)await CallProxyAsync(
                new ActivityCompleteRequest()
                {
                    Domain     = ResolveDomain(domain),
                    WorkflowId = workflowId,
                    RunId      = runId,
                    ActivityId = activityId,
                    Error      = new CadenceError(new CadenceCancelledException("Cancelled"))
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Used to externally fail an activity by task token.
        /// </summary>
        /// <param name="taskToken">The opaque activity task token.</param>
        /// <param name="error">Specifies the activity error.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the activity no longer exists.</exception>
        public async Task RespondActivityFailAsync(byte[] taskToken, Exception error)
        {
            Covenant.Requires<ArgumentNullException>(taskToken != null && taskToken.Length > 0);
            Covenant.Requires<ArgumentNullException>(error != null);
            EnsureNotDisposed();

            var reply = (ActivityCompleteReply)await CallProxyAsync(
                new ActivityCompleteRequest()
                {
                    TaskToken = taskToken,
                    Error     = new CadenceError(error)
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Used to externally fail an activity by task token.
        /// </summary>
        /// <param name="workflowId">The workflow ID.</param>
        /// <param name="runId">The workflow run ID.</param>
        /// <param name="activityId">The activity ID.</param>
        /// <param name="error">Specifies the activity error.</param>
        /// <param name="domain">Optionally overrides the default <see cref="CadenceClient"/> domain.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the activity no longer exists.</exception>
        public async Task RespondActivityFailByIdAsync(string workflowId, string runId, string activityId, Exception error, string domain = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowId));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(runId));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(activityId));
            Covenant.Requires<ArgumentNullException>(error != null);
            EnsureNotDisposed();

            var reply = (ActivityCompleteReply)await CallProxyAsync(
                new ActivityCompleteRequest()
                {
                    Domain     = ResolveDomain(domain),
                    WorkflowId = workflowId,
                    RunId      = runId,
                    ActivityId = activityId,
                    Error      = new CadenceError(error)
                });

            reply.ThrowOnError();
        }
    }
}
