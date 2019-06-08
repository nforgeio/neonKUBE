//-----------------------------------------------------------------------------
// FILE:	    InternalStartWorkflowOptions.cs
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
    /// <para>
    /// <b>INTERNAL USE ONLY:</b> Specifies workflow execution options.  This maps 
    /// pretty closely to this Cadence GOLANG structure:
    /// </para>
    /// <para>
    /// https://godoc.org/go.uber.org/cadence/internal#StartWorkflowOptions
    /// </para>
    /// </summary>
    internal class InternalStartWorkflowOptions
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public InternalStartWorkflowOptions()
        {
        }

        /// <summary>
        /// ID - The business identifier of the workflow execution.
        /// Optional: defaulted to a uuid.
        /// </summary>
        [JsonProperty(PropertyName = "ID", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string ID { get; set; } = null;

        /// <summary>
        /// TaskList - The decisions of the workflow are scheduled on this queue.
        /// This is also the default task list on which activities are scheduled. The workflow author can choose
        /// to override this using activity options.  Mandatory: No default.
        /// </summary>
        [JsonProperty(PropertyName = "TaskList", Required = Required.Always)]
        public string TaskList { get; set; }

        /// <summary>
        /// ExecutionStartToCloseTimeout - The time out for duration of workflow execution (expressed
        /// in nanoseconds).  Mandatory: No default.
        /// </summary>
        [JsonProperty(PropertyName = "ExecutionStartToCloseTimeout", Required = Required.Always)]
        public long ExecutionStartToCloseTimeout { get; set; }

        /// <summary>
        /// DecisionTaskStartToCloseTimeout - The time out for processing decision task from the time the worker
        /// pulled this task. If a decision task is lost, it is retried after this timeout.
        /// Expressed as nanoseconds.  Optional: defaulted to 10 secs.
        /// </summary>
        [JsonProperty(PropertyName = "DecisionTaskStartToCloseTimeout", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(10 * CadenceHelper.NanosecondsPerSecond)]
        public long DecisionTaskStartToCloseTimeout { get; set; } = 10 * CadenceHelper.NanosecondsPerSecond;

        /// <summary>
        /// WorkflowIDReusePolicy - Whether server allow reuse of workflow ID, can be useful
        /// for dedup logic if set to WorkflowIdReusePolicyRejectDuplicate.
        /// Optional: defaulted to WorkflowIDReusePolicyAllowDuplicateFailedOnly.
        /// </summary>
        [JsonProperty(PropertyName = "WorkflowIdReusePolicy", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(Cadence.WorkflowIdReusePolicy.AllowDuplicateFailedOnly)]
        public int WorkflowIdReusePolicy { get; set; } = (int)Cadence.WorkflowIdReusePolicy.AllowDuplicateFailedOnly;
        
        /// <summary>
        /// RetryPolicy - Optional retry policy for workflow. If a retry policy is specified, in case of workflow failure
        /// server will start new workflow execution if needed based on the retry policy.
        /// </summary>
        [JsonProperty(PropertyName = "RetryPolicy", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public InternalRetryPolicy RetryPolicy { get; set; } = null;

        /// <summary>
        /// <para>
        /// CronSchedule - Optional cron schedule for workflow. If a cron schedule is specified, the workflow will run
        /// as a cron based on the schedule. The scheduling will be based on UTC time. Schedule for next run only happen
        /// after the current run is completed/failed/timeout. If a RetryPolicy is also supplied, and the workflow failed
        /// or timeout, the workflow will be retried based on the retry policy. While the workflow is retrying, it won't
        /// schedule its next run. If next schedule is due while workflow is running (or retrying), then it will skip that
        /// schedule. Cron workflow will not stop until it is terminated or cancelled (by returning cadence.CanceledError).
        /// The cron spec is as following:
        /// </para>
        /// <code>
        /// ┌───────────── minute (0 - 59)
        /// │ ┌───────────── hour (0 - 23)
        /// │ │ ┌───────────── day of the month (1 - 31)
        /// │ │ │ ┌───────────── month (1 - 12)
        /// │ │ │ │ ┌───────────── day of the week (0 - 6) (Sunday to Saturday)
        /// │ │ │ │ │
        /// │ │ │ │ │
        /// * * * * *
        /// </code>
        /// </summary>
        [JsonProperty(PropertyName = "CronSchedule", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string CronSchedule { get; set; } = null;

        /// <summary>
        /// Memo - Optional info that will be shown in list workflow.
        /// </summary>
        [JsonProperty(PropertyName = "Memo", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public Dictionary<string, byte[]> Memo = null;
    }
}
