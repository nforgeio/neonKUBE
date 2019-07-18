//-----------------------------------------------------------------------------
// FILE:	    ActivityOptions.cs
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
    /// Specifies the options used for executing an activity.
    /// </summary>
    public class ActivityOptions
    {
        /// <summary>
        /// Optionally specifies the task list where the activity will be scheduled.
        /// This defaults to the same task list as the parent workflow.
        /// </summary>
        public string TaskList { get; set; } = null;

        /// <summary>
        /// Optionally specifies the end-to-end timeout for the activity.  The 
        /// default <see cref="TimeSpan.Zero"/> value uses the sum of 
        /// <see cref="ScheduleToStartTimeout"/> and <see cref="StartToCloseTimeout"/>.
        /// </summary>
        public TimeSpan ScheduleToCloseTimeout { get; set; }

        /// <summary>
        /// Specifies the maximum time the activity be queued, waiting to be scheduled
        /// on a worker.  This defaults to 365 days.
        /// </summary>
        public TimeSpan ScheduleToStartTimeout { get; set; } = CadenceClient.DefaultTimeout;

        /// <summary>
        /// Specifies the maximum time the activity may take to run.  This defaults
        /// to 365 days.
        /// </summary>
        public TimeSpan StartToCloseTimeout { get; set; } = CadenceClient.DefaultTimeout;

        /// <summary>
        /// Optionally specifies the maximum time the activity has to send a heartbeat
        /// back to Cadence.  This defaults to <see cref="TimeSpan.Zero"/> which indicates
        /// that no heartbeating is required.
        /// </summary>
        public TimeSpan HeartbeatTimeout { get; set; }

        /// <summary>
        /// Optionally specifies that the cancelled activities won't be consider to be
        /// finished until that actually complete.  This defaults to <c>false</c>.
        /// </summary>
        public bool WaitForCancellation { get; set; }

        /// <summary>
        /// Optionally specifies the activity retry policy.  The default value is <c>null</c> which specifies
        /// that there will be no retry attempts.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When <see cref="RetryOptions.ExpirationInterval"/> is specified and it is larger than the activity's 
        /// <see cref="ScheduleToStartTimeout"/>, then the <see cref="RetryOptions.ExpirationInterval"/> will override 
        /// activity's <see cref="ScheduleToStartTimeout"/>. This is to avoid retrying on <see cref="ScheduleToStartTimeout"/>
        /// error which only happen when worker is not picking up the task within the timeout.
        /// </para>
        /// <para>
        /// Retrying <see cref="ScheduleToStartTimeout"/> does not make sense as it just
        /// mark the task as failed and create a new task and put back in the queue waiting worker to pick again. Cadence
        /// server also make sure the <see cref="ScheduleToStartTimeout"/> will not be larger than the workflow's timeout.
        /// Same apply to <see cref="ScheduleToCloseTimeout"/>.
        /// </para>
        /// </remarks>
        public RetryOptions RetryOptions { get; set; }

        /// <summary>
        /// Converts the instance to its internal representation.
        /// </summary>
        internal InternalActivityOptions ToInternal()
        {
            return new InternalActivityOptions()
            {
                TaskList               = this.TaskList,
                ScheduleToCloseTimeout = CadenceHelper.ToCadence(this.ScheduleToCloseTimeout),
                ScheduleToStartTimeout = CadenceHelper.ToCadence(this.ScheduleToStartTimeout),
                StartToCloseTimeout    = CadenceHelper.ToCadence(this.StartToCloseTimeout),
                HeartbeatTimeout       = CadenceHelper.ToCadence(this.HeartbeatTimeout),
                WaitForCancellation    = WaitForCancellation,
                RetryPolicy            = RetryOptions?.ToInternal()
            };
        }
    }
}
