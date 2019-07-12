//-----------------------------------------------------------------------------
// FILE:	    InternalActivityInfo.cs
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

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// Holds information about an executing activity.  This maps directly to the
    /// GOLANG client's <b>ActivityInfo</b> structure.
    /// </summary>
    internal class InternalActivityInfo
    {
        /// <summary>
        /// The activity task token.
        /// </summary>
        public byte[] TaskToken { get; set; }

        /// <summary>
        /// The parent workflow type name.
        /// </summary>
        public InternalWorkflowType WorkflowType { get; set; }

        /// <summary>
        /// The parent workflow domain.
        /// </summary>
        public string WorkflowDomain { get; set; }

        /// <summary>
        /// The parent workflow execution details.
        /// </summary>
        public InternalWorkflowExecution WorkflowExecution { get; set; }

        /// <summary>
        /// The activity ID.
        /// </summary>
        public string ActivityId { get; set; }

        /// <summary>
        /// The activity type.
        /// </summary>
        public InternalActivityType ActivityType { get; set; }

        /// <summary>
        /// The activity task list.
        /// </summary>
        public string TaskList { get; set; }

        /// <summary>
        /// The maximum time between heartbeats.  0 means no heartbeat needed.
        /// </summary>
        public long HeartbeatTimeout { get; set; }

        /// <summary>
        /// Time (UTC) when the activity was scheduled.
        /// </summary>
        public string ScheduledTimestamp { get; set; }

        /// <summary>
        /// Time (UTC) when the activity was started.
        /// </summary>
        public string StartedTimestamp { get; set; }

        /// <summary>
        /// Time (UTC) when the activity will timeout.
        /// </summary>
        public string Deadline { get; set; }

        /// <summary>
        /// Indicates how many times the activity was been restarted.  This will be zero
        /// for the first execution, 1 for the second, and so on.
        /// </summary>
        public int Attempt { get; set; }

        /// <summary>
        /// Converts the instance into a public <see cref="ActivityInfo"/>.
        /// </summary>
        public ActivityInfo ToPublic()
        {
            return new ActivityInfo()
            {
                TaskToken           = this.TaskToken,
                WorkflowTypeName    = this.WorkflowType?.Name,
                WorkflowDomain      = this.WorkflowDomain,
                WorkflowRun         = this.WorkflowExecution.ToPublic(),
                ActivityId          = this.ActivityId,
                ActivityTypeName    = this.ActivityType?.Name,
                TaskList            = this.TaskList,
                HeartbeatTimeout    = TimeSpan.FromTicks(this.HeartbeatTimeout / 100),
                ScheduledTimeUtc    = CadenceHelper.ParseCadenceTimestamp(this.ScheduledTimestamp),
                StartedTimeUtc      = CadenceHelper.ParseCadenceTimestamp(this.StartedTimestamp),
                DeadlineTimeUtc     = CadenceHelper.ParseCadenceTimestamp(this.Deadline),
                Attempt             = this.Attempt
            };
        }
    }
}
