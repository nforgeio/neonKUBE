//-----------------------------------------------------------------------------
// FILE:	    InternalPendingActivityInfo.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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

using Newtonsoft.Json;

using Neon.Cadence;
using Neon.Common;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// <b>INTERNAL USE ONLY:</b> Describes an executing activity.
    /// </summary>
    internal class InternalPendingActivityInfo
    {
        /// <summary>
        /// The activity ID.
        /// </summary>        [JsonProperty(PropertyName = "activityID", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]

        public string ActivityID { get; set; }

        /// <summary>
        /// The activity type.
        /// </summary>
        [JsonProperty(PropertyName = "activityType", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public InternalActivityType ActivityType { get; set; }

        /// <summary>
        /// The activity state.
        /// </summary>
        [JsonProperty(PropertyName = "state", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(InternalPendingActivityState.SCHEDULED)]
        public InternalPendingActivityState State { get; set; }

        /// <summary>
        /// Details from the last activity heartbeart.
        /// </summary>
        [JsonProperty(PropertyName = "heartbeatDetails", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public byte[] HeartbeatDetails { get; set; }

        /// <summary>
        /// Time when the last activity heartbeat was received.
        /// </summary>
        [JsonProperty(PropertyName = "lastHeartbeatTimestamp", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public long LastHeartbeatTimestamp { get; set; }

        /// <summary>
        /// Time when the activity was most recently started.
        /// </summary>
        [JsonProperty(PropertyName = "lastStartedTimestamp", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public long LastStartedTimestamp { get; set; }

        /// <summary>
        /// The number of times the activity has been started/restarted.
        /// </summary>
        [JsonProperty(PropertyName = "attempt", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public int Attempt { get; set; }

        /// <summary>
        /// The maximum times the activity may be started.
        /// </summary>
        [JsonProperty(PropertyName = "maximumAttempts", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public int MaximumAttempts { get; set; }

        /// <summary>
        /// Time when the activity is scheduled to run.
        /// </summary>
        [JsonProperty(PropertyName = "scheduledTimestamp", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public long ScheduledTimestamp { get; set; }

        /// <summary>
        /// Time when the activity must complete.
        /// </summary>
        [JsonProperty(PropertyName = "expirationTimestamp", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public long ExpirationTimestamp { get; set; }

        /// <summary>
        /// Converts the instance into the corresponding public <see cref="PendingActivityInfo"/>.
        /// </summary>
        public PendingActivityInfo ToPublic()
        {
            return new PendingActivityInfo()
            {
                ActivityID           = this.ActivityID,
                Name                 = this.ActivityType?.Name,
                Status               = (ActivityStatus)this.State,
                HeartbeatDetails     = this.HeartbeatDetails,
                LastHeartbeatTimeUtc = new DateTime(this.LastHeartbeatTimestamp),
                LastStartedTimeUtc   = new DateTime(this.LastStartedTimestamp),
                Attempt              = this.Attempt,
                MaximumAttempts      = this.MaximumAttempts,
                ScheduledTimeUtc     = new DateTime(this.ScheduledTimestamp),
                ExpirationTimeUtc    = new DateTime(this.ExpirationTimestamp)
            };
        }
    }
}
