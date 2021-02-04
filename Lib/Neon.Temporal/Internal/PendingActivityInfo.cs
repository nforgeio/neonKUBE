//-----------------------------------------------------------------------------
// FILE:	    PendingActivityInfo.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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

using Newtonsoft.Json;

using Neon.Data;

namespace Neon.Temporal.Internal
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
        [JsonProperty(PropertyName = "activity_id")]
        public string ActivityId { get; set; }

        /// <summary>
        /// The activiy type name.
        /// </summary>
        [JsonProperty(PropertyName = "activity_type")]
        public ActivityType ActivityType { get; set; }

        /// <summary>
        /// The activity state.
        /// </summary>
        [JsonProperty(PropertyName = "state")]
        [JsonConverter(typeof(IntegerEnumConverter<PendingActivityState>))]
        public PendingActivityState State { get; set; }

        /// <summary>
        /// Details from the last activity heartbeart.
        /// </summary>
        [JsonProperty(PropertyName = "heartbeat_details")]
        public Payloads HeartbeatDetails { get; set; }

        /// <summary>
        /// Time when the last activity heartbeat was received.
        /// </summary>
        [JsonProperty(PropertyName = "last_heartbeat_time")]
        public DateTime? LastHeartbeatTime { get; set; }

        /// <summary>
        /// Time when the activity was most recently started.
        /// </summary>
        [JsonProperty(PropertyName = "last_started_time")]
        public DateTime? LastStartedTime { get; set; }

        /// <summary>
        /// The number of times the activity has been started/restarted.
        /// </summary>
        [JsonProperty(PropertyName = "attempt")]
        public int Attempt { get; set; }

        /// <summary>
        /// The maximum times the activity may be started.
        /// </summary>
        [JsonProperty(PropertyName = "maximum_attempts")]
        public int MaximumAttempts { get; set; }

        /// <summary>
        /// Time when the activity is scheduled to run.
        /// </summary>
        [JsonProperty(PropertyName = "scheduled_time")]
        public DateTime? ScheduledTime { get; set; }

        /// <summary>
        /// Time when the activity must complete.
        /// </summary>
        [JsonProperty(PropertyName = "expiration_time")]
        public DateTime? ExpirationTime { get; set; }

        /// <summary>
        /// The last failure.
        /// </summary>
        [JsonProperty(PropertyName = "last_failure")]
        public Failure LastFailure { get; set; }

        /// <summary>
        /// The identity of the last worker that processed this activity.
        /// </summary>
        [JsonProperty(PropertyName = "last_worker_identity")]
        public string LastWorkerIdentity { get; set; }
    }
}
