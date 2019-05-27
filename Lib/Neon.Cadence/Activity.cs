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
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Common;

namespace Neon.Cadence
{
    /// <summary>
    /// Base class for all application Cadence activity implementations.
    /// </summary>
    public abstract class Activity
    {
        private long contextId;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="args">The low-level worker initialization arguments.</param>
        /// <param name="cancellationToken">The activity stop cancellation token.</param>
        internal Activity(WorkerArgs args, CancellationToken cancellationToken)
        {
            Covenant.Requires<ArgumentNullException>(args != null);
            Covenant.Requires<ArgumentNullException>(cancellationToken != null);

            this.Client            = args.Client;
            this.contextId         = args.WorkerContextId;
            this.CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Returns the <see cref="CadenceClient"/> managing this activity.
        /// </summary>
        public CadenceClient Client { get; private set; }

        /// <summary>
        /// Returns the <see cref="CancellationToken"/> that will be cancelled when the activity
        /// is being stopped.  This can be monitored by activity implementations that would like
        /// to handle this gracefully by recording a heartbeat with progress information or
        /// by doing other cleanup.
        /// </summary>
        public CancellationToken CancellationToken { get; private set; }

        /// <summary>
        /// Called by Cadence to execute an activity.  Derived classes will need to implement
        /// their activity logic here.
        /// </summary>
        /// <param name="args">The activity arguments encoded into a byte array or <c>null</c>.</param>
        /// <returns>The activity result encoded as a byte array or <c>null</c>.</returns>
        protected abstract Task<byte[]> RunAsync(byte[] args);
    }
}
