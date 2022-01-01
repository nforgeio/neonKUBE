//-----------------------------------------------------------------------------
// FILE:	    ActivityTask.cs
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

using Neon.Temporal;
using Neon.Common;

namespace Neon.Temporal.Internal
{
    /// <summary>
    /// Holds information about an executing activity.
    /// </summary>
    public class ActivityInfo
    {
        /// <summary>
        /// The opaque base-64 encoded activity task token.
        /// </summary>
        public string TaskToken { get; set; }

        /// <summary>
        /// The parent workflow type name.
        /// </summary>
        public WorkflowType WorkflowType { get; set; }

        /// <summary>
        /// The parent workflow namespace.
        /// </summary>
        public string WorkflowNamespace { get; set; }

        /// <summary>
        /// The parent workflow execution details.
        /// </summary>
        public WorkflowExecution WorkflowExecution { get; set; }

        /// <summary>
        /// The activity ID.
        /// </summary>
        public string ActivityId { get; set; }

        /// <summary>
        /// The activity type name.
        /// </summary>
        public ActivityType ActivityType { get; set; }

        /// <summary>
        /// The activity task queue.
        /// </summary>
        public string TaskQueue { get; set; }

        /// <summary>
        /// The maximum time between heartbeats.  <see cref="TimeSpan.Zero"/> 
        /// indicates that no heartbeating is required.
        /// </summary>
        [JsonConverter(typeof(GoTimeSpanJsonConverter))]
        public TimeSpan HeartbeatTimeout { get; set; }

        /// <summary>
        /// Time when the activity was scheduled.
        /// </summary>
        public DateTime ScheduledTime { get; set; }

        /// <summary>
        /// Time when the activity was started.
        /// </summary>
        public DateTime StartedTime { get; set; }

        /// <summary>
        /// Time when the activity will timeout.
        /// </summary>
        public DateTime Deadline { get; set; }

        /// <summary>
        /// Indicates how many times the activity was been restarted.  This will be zero
        /// for the first execution, 1 for the second, and so on.
        /// </summary>
        public int Attempt { get; set; }
    }
}
