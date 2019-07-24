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

namespace Neon.Cadence
{
    /// <inheritdoc/>
    public class Activity : IActivity
    {
        private ActivityBase    activity;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="activity">The parent activity implementation.</param>
        internal Activity(ActivityBase activity)
        {
            Covenant.Requires<ArgumentNullException>(activity != null);

            this.activity = activity;
        }

        /// <inheritdoc/>
        public CadenceClient Client => activity.Client;

        /// <inheritdoc/>
        public bool IsLocal => activity.IsLocal;

        /// <inheritdoc/>
        public CancellationToken CancellationToken => activity.CancellationToken;

        /// <inheritdoc/>
        public ActivityTask Task => activity.ActivityTask;

        /// <inheritdoc/>
        public async Task SendHeartbeatAsync(byte[] details = null)
        {
            activity.EnsureNotLocal();

            var reply = (ActivityRecordHeartbeatReply)await Client.CallProxyAsync(
                new ActivityRecordHeartbeatRequest()
                {
                    ContextId = activity.ContextId.Value,
                    Details   = details
                });

            reply.ThrowOnError();
        }

        /// <inheritdoc/>
        public async Task<bool> HasLastHeartbeatDetailsAsync()
        {
            activity.EnsureNotLocal();

            var reply = (ActivityHasHeartbeatDetailsReply)await Client.CallProxyAsync(
                new ActivityHasHeartbeatDetailsRequest()
                {
                    ContextId = activity.ContextId.Value
                });

            reply.ThrowOnError();

            return reply.HasDetails;
        }

        /// <inheritdoc/>
        public async Task<byte[]> GetLastHeartbeatDetailsAsync()
        {
            activity.EnsureNotLocal();

            var reply = (ActivityGetHeartbeatDetailsReply)await Client.CallProxyAsync(
                new ActivityGetHeartbeatDetailsRequest()
                {
                    ContextId = activity.ContextId.Value
                });

            reply.ThrowOnError();

            return reply.Details;
        }

        /// <inheritdoc/>
        public async Task CompleteExternallyAsync()
        {
            activity.EnsureNotLocal();

            await System.Threading.Tasks.Task.CompletedTask;
            throw new CadenceActivityExternalCompletionException();
        }
    }
}
