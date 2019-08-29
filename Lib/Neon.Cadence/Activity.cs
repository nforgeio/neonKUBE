//-----------------------------------------------------------------------------
// FILE:	    Activity.cs
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
    /// This will be available via the <see cref="WorkflowBase.Workflow"/> property.
    /// </summary>
    public class Activity
    {
        private ActivityBase    parent;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="parent">The parent activity implementation.</param>
        internal Activity(ActivityBase parent)
        {
            Covenant.Requires<ArgumentNullException>(parent != null);

            this.parent = parent;
            this.Logger = LogManager.Default.GetLogger(sourceModule: Client.Settings.ClientIdentity, contextId: parent.ActivityTask?.WorkflowExecution?.RunId);
        }

        /// <summary>
        /// Returns the <see cref="CadenceClient"/> managing this activity.
        /// </summary>
        public CadenceClient Client => parent.Client;

        /// <summary>
        /// Returns <c>true</c> for a local activity execution.
        /// </summary>
        public bool IsLocal => parent.IsLocal;

        /// <summary>
        /// Returns the activity's cancellation token.  Activities can monitor this
        /// to gracefully handle activity cancellation.
        /// </summary>
        /// <remarks>
        /// <para>
        /// We recommend that all non-local activities that execute for relatively long periods,
        /// monitor <see cref="CancellationToken"/> for activity cancellation so that they
        /// can gracefully terminate including potentially calling <see cref="SendHeartbeatAsync(byte[])"/>
        /// to checkpoint the current activity state.
        /// </para>
        /// <para>
        /// Cancelled activities should throw a <see cref="TaskCanceledException"/> from
        /// their entry point method rather than returning a result so that Cadence will 
        /// reschedule the activity if necessary.
        /// </para>
        /// </remarks>
        public CancellationToken CancellationToken => parent.CancellationToken;

        /// <summary>
        /// Returns the additional information about the activity and the workflow
        /// that invoked it.  Note that this doesn't work for local activities.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown for local activities.</exception>
        public ActivityTask Task => parent.ActivityTask;

        /// <summary>
        /// Returns the logger to be used for logging activity related events.
        /// </summary>
        public INeonLogger Logger { get; private set; }

        /// <summary>
        /// <para>
        /// Sends a heartbeat with optional details to Cadence.
        /// </para>
        /// <note>
        /// <b>IMPORTANT:</b> Heartbeats are not supported for local activities.
        /// </note>
        /// </summary>
        /// <param name="details">Optional heartbeart details.</param>
        /// <returns>The tracking <see cref="System.Threading.Tasks.Task"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown for local activity executions.</exception>
        /// <remarks>
        /// <para>
        /// Long running activities need to send periodic heartbeats back to
        /// Cadence to prove that the activity is still alive.  This can also
        /// be used by activities to implement checkpoints or record other
        /// details.  This method sends a heartbeat with optional details
        /// encoded as a byte array.
        /// </para>
        /// <note>
        /// The maximum allowed time period between heartbeats is specified in 
        /// <see cref="ActivityOptions"/> when activities are executed and it's
        /// also possible to enable automatic heartbeats sent by the Cadence client.
        /// </note>
        /// </remarks>
        public async Task SendHeartbeatAsync(byte[] details = null)
        {
            Client.EnsureNotDisposed();
            parent.EnsureNotLocal();

            var reply = (ActivityRecordHeartbeatReply)await Client.CallProxyAsync(
                new ActivityRecordHeartbeatRequest()
                {
                    ContextId = parent.ContextId.Value,
                    Details   = details
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// <para>
        /// Determines whether the details from the last recorded heartbeat last
        /// failed attempt exist.
        /// </para>
        /// <note>
        /// <b>IMPORTANT:</b> Heartbeats are not supported for local activities.
        /// </note>
        /// </summary>
        /// <returns>The details from the last heartbeat or <c>null</c>.</returns>
        /// <exception cref="InvalidOperationException">Thrown for local activity executions.</exception>
        public async Task<bool> HasLastHeartbeatDetailsAsync()
        {
            Client.EnsureNotDisposed();
            parent.EnsureNotLocal();

            var reply = (ActivityHasHeartbeatDetailsReply)await Client.CallProxyAsync(
                new ActivityHasHeartbeatDetailsRequest()
                {
                    ContextId = parent.ContextId.Value
                });

            reply.ThrowOnError();

            return reply.HasDetails;
        }

        /// <summary>
        /// <para>
        /// Returns the details from the last recorded heartbeat last failed attempt
        /// at running the activity.
        /// </para>
        /// <note>
        /// <b>IMPORTANT:</b> Heartbeats are not supported for local activities.
        /// </note>
        /// </summary>
        /// <returns>The details from the last heartbeat or <c>null</c>.</returns>
        /// <exception cref="InvalidOperationException">Thrown for local activity executions.</exception>
        public async Task<byte[]> GetLastHeartbeatDetailsAsync()
        {
            Client.EnsureNotDisposed();
            parent.EnsureNotLocal();

            var reply = (ActivityGetHeartbeatDetailsReply)await Client.CallProxyAsync(
                new ActivityGetHeartbeatDetailsRequest()
                {
                    ContextId = parent.ContextId.Value
                });

            reply.ThrowOnError();

            return reply.Details;
        }

        /// <summary>
        /// This method may be called within the activity entry point to indicate that the
        /// activity will be completed externally.
        /// </summary>
        /// <returns>The tracking <see cref="System.Threading.Tasks.Task"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown for local activities.</exception>
        /// <remarks>
        /// <para>
        /// This method works by throwing an <see cref="CadenceActivityExternalCompletionException"/> which
        /// will be caught and handled by the base <see cref="ActivityBase"/> class.  You'll need to allow
        /// this exception to exit your entry point method for this to work.
        /// </para>
        /// <note>
        /// This method doesn't work for local activities.
        /// </note>
        /// </remarks>
        public async Task CompleteExternallyAsync()
        {
            Client.EnsureNotDisposed();
            parent.EnsureNotLocal();

            await global::System.Threading.Tasks.Task.CompletedTask;
            throw new CadenceActivityExternalCompletionException();
        }
    }
}
