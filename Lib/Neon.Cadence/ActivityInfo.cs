//-----------------------------------------------------------------------------
// FILE:	    ActivityInfo.cs
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

using Newtonsoft.Json;

using Neon.Cadence;
using Neon.Common;

namespace Neon.Cadence
{
    /// <summary>
    /// Holds information about an executing activity.
    /// </summary>
    public class ActivityInfo
    {
        /// <summary>
        /// The activity task token.
        /// </summary>
        public byte[] TaskToken { get; internal set; }

        /// <summary>
        /// The parent workflow type name.
        /// </summary>
        public string WorkflowTypeName { get; internal set; }

        /// <summary>
        /// The parent workflow domain.
        /// </summary>
        public string WorkflowDomain { get; internal set; }

        /// <summary>
        /// The parent workflow execution details.
        /// </summary>
        public WorkflowRun WorkflowRun { get; internal set; }

        /// <summary>
        /// The activity ID.
        /// </summary>
        public string ActivityId { get; internal set; }

        /// <summary>
        /// The activity type name.
        /// </summary>
        public string ActivityTypeName { get; internal set; }

        /// <summary>
        /// The activity task list.
        /// </summary>
        public string TaskList { get; internal set; }

        /// <summary>
        /// The maximum time between heartbeats.  0 means no heartbeat needed.
        /// </summary>
        public TimeSpan HeartbeatTimeout { get; internal set; }

        /// <summary>
        /// Time (UTC) when the activity was scheduled.
        /// </summary>
        public DateTime ScheduledTimeUtc { get; internal set; }

        /// <summary>
        /// Time (UTC) when the activity was started.
        /// </summary>
        public DateTime StartedTimeUtc { get; internal set; }

        /// <summary>
        /// Time (UTC) when the activity will timeout.
        /// </summary>
        public DateTime DeadlineTimeUtc { get; internal set; }

        /// <summary>
        /// Indicates how many times the activity was been restarted.  This will be zero
        /// for the first execution, 1 for the second, and so on.
        /// </summary>
        public int Attempt { get; internal set; }
    }
}
