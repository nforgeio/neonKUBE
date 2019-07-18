//-----------------------------------------------------------------------------
// FILE:	    DescribedActivityInfo.cs
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

using Neon.Cadence;
using Neon.Common;

namespace Neon.Cadence
{
    /// <summary>
    /// Describes the current state of a scheduled or executing activity.
    /// </summary>
    public class DescribedActivityInfo
    {
        /// <summary>
        /// The activity ID.
        /// </summary>
        public string ActivityID { get; internal set; }

        /// <summary>
        /// Identifies the activity type.
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// The activity state.
        /// </summary>
        public ActivityStatus Status { get; internal set; }

        /// <summary>
        /// Details from the last activity heartbeart.
        /// </summary>
        public byte[] HeartbeatDetails { get; internal set; }

        /// <summary>
        /// Time when the last activity heartbeat was received.
        /// </summary>
        public DateTime LastHeartbeatTimeUtc { get; internal set; }

        /// <summary>
        /// Time when the activity was most recently started.
        /// </summary>
        public DateTime LastStartedTimeUtc { get; internal set; }

        /// <summary>
        /// The number of times the activity has been started/restarted.
        /// </summary>
        public int Attempt { get; internal set; }

        /// <summary>
        /// The maximum times the activity may be started.
        /// </summary>
        public int MaximumAttempts { get; internal set; }

        /// <summary>
        /// Time when the activity is scheduled to run.
        /// </summary>
        public DateTime ScheduledTimeUtc { get; internal set; }

        /// <summary>
        /// Time when the activity must complete.
        /// </summary>
        public DateTime ExpirationTimeUtc { get; internal set; }
    }
}
