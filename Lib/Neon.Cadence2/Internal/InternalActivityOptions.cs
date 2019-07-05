//-----------------------------------------------------------------------------
// FILE:	    InternalActivityOptions.cs
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
    internal class InternalActivityOptions
    {
        /// <summary>
        /// TaskList that the activity needs to be scheduled on.
        /// optional: The default task list with the same name as the workflow task list.
        /// </summary>
        [JsonProperty(PropertyName = "TaskList", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string TaskList { get; set; }

        /// <summary>
        /// ScheduleToCloseTimeout - The end to end time out for the activity needed.
        /// The zero value of this uses default value.
        /// Optional: The default value is the sum of ScheduleToStartTimeout and StartToCloseTimeout
        /// </summary>
        [JsonProperty(PropertyName = "ScheduleToCloseTimeout", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public long ScheduleToCloseTimeout { get; set; }

        /// <summary>
        /// ScheduleToStartTimeout - The queue time out before the activity starts executed.
        /// Mandatory: No default.
        /// </summary>
        [JsonProperty(PropertyName = "ScheduleToStartTimeout", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public long ScheduleToStartTimeout { get; set; }

        /// <summary>
        /// StartToCloseTimeout - The time out from the start of execution to end of it.
        /// Mandatory: No default.
        /// </summary>
        [JsonProperty(PropertyName = "StartToCloseTimeout", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public long StartToCloseTimeout { get; set; }

        /// <summary>
        /// HeartbeatTimeout - The periodic timeout while the activity is in execution. This is
        /// the max interval the server needs to hear at-least one ping from the activity.
        /// Optional: Default zero, means no heart beating is needed.
        /// </summary>
        [JsonProperty(PropertyName = "HeartbeatTimeout", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public long HeartbeatTimeout { get; set; }

        /// <summary>
        /// WaitForCancellation - Whether to wait for cancelled activity to be completed(
        /// activity can be failed, completed, cancel accepted)
        /// Optional: default false
        /// </summary>
        [JsonProperty(PropertyName = "WaitForCancellation", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public bool WaitForCancellation { get; set; }

        /// <summary>
        /// ActivityID - Business level activity ID, this is not needed for most of the cases if you have
        /// to specify this then talk to cadence team. This is something will be done in future.
        /// Optional: default empty string
        /// </summary>
        [JsonProperty(PropertyName = "ActivityID", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue("")]
        public string ActivityID { get; set; } = string.Empty;

        /// <summary>
        /// RetryPolicy specify how to retry activity if error happens. When RetryPolicy.ExpirationInterval is specified
        /// and it is larger than the activity's ScheduleToStartTimeout, then the ExpirationInterval will override activity's
        /// ScheduleToStartTimeout. This is to avoid retrying on ScheduleToStartTimeout error which only happen when worker
        /// is not picking up the task within the timeout. Retrying ScheduleToStartTimeout does not make sense as it just
        /// mark the task as failed and create a new task and put back in the queue waiting worker to pick again. Cadence
        /// server also make sure the ScheduleToStartTimeout will not be larger than the workflow's timeout.
        /// Same apply to ScheduleToCloseTimeout. See more details about RetryPolicy on the doc for RetryPolicy.
        /// Optional: default is no retry
        /// </summary>
        [JsonProperty(PropertyName = "RetryPolicy", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public InternalRetryPolicy RetryPolicy { get; set; }
    }
}
