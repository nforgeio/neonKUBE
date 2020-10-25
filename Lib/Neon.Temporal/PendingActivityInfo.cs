//-----------------------------------------------------------------------------
// FILE:	    PendingActivityInfo.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using Neon.Temporal;
using Neon.Common;

namespace Neon.Temporal
{
    /// <summary>
    /// Describes the current state of a scheduled or executing activity.
    /// </summary>
    public class PendingActivityInfo
    {
        /// <summary>
        /// The associated Temporal client.
        /// </summary>
        internal TemporalClient Client { get; set; }

        /// <summary>
        /// The activity ID.
        /// </summary>
        public string ActivityId { get; set; }

        /// <summary>
        /// The activiy type name.
        /// </summary>
        public string ActivityTypeName { get; set; }

        /// <summary>
        /// The activity state.
        /// </summary>
        public PendingActivityState State { get; set; }

        /// <summary>
        /// Details from the last activity heartbeart.
        /// </summary>
        public byte[] HeartbeatDetails { get; set; }

        /// <summary>
        /// Time when the last activity heartbeat was received.
        /// </summary>
        public DateTime LastHeartbeatTime { get; set; }

        /// <summary>
        /// Time when the activity was most recently started.
        /// </summary>
        public DateTime LastStartedTime { get; set; }

        /// <summary>
        /// The number of times the activity has been started/restarted.
        /// </summary>
        public int Attempt { get; set; }

        /// <summary>
        /// The maximum times the activity may be started.
        /// </summary>
        public int MaximumAttempts { get; set; }

        /// <summary>
        /// Time when the activity is scheduled to run.
        /// </summary>
        public DateTime ScheduledTime { get; set; }

        /// <summary>
        /// Time when the activity must complete.
        /// </summary>
        public DateTime ExpirationTime { get; set; }

        // $todo(jefflill): We need to implement [LastFailure]

        /// <summary>
        /// The identity of the last worker that processed this activity.
        /// </summary>
        public string LastWorkerIdentity { get; set; }
    }
}
