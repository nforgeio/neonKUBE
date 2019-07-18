//-----------------------------------------------------------------------------
// FILE:	    InternalChildWorkflowOptions.cs
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
    /// <b>INTERNAL USE ONLY:</b> Specifies child workflow execution options.  This maps 
    /// closely to this Cadence GOLANG structure:
    /// </para>
    /// <para>
    /// https://godoc.org/go.uber.org/cadence/internal#ChildWorkflowOptions
    /// </para>
    /// </summary>
    internal class InternalChildWorkflowOptions
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public InternalChildWorkflowOptions()
        {
        }

        /// <summary>
        /// Domain of the child workflow.
        /// Optional: the current workflow (parent)'s domain will be used if this is not provided.
        /// </summary>
        [JsonProperty(PropertyName = "Domain", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Domain { get; set; } = null;

        /// <summary>
        /// WorkflowID of the child workflow to be scheduled.
        /// Optional: an auto generated workflowID will be used if this is not provided.        /// </summary>
        [JsonProperty(PropertyName = "WorkflowID", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string WorkflowID { get; set; } = null;

        /// <summary>
        /// TaskList that the child workflow needs to be scheduled on.
        /// Optional: the parent workflow task list will be used if this is not provided.
        /// </summary>
        [JsonProperty(PropertyName = "TaskList", Required = Required.Always)]
        public string TaskList { get; set; }

        /// <summary>
        /// ExecutionStartToCloseTimeout - The end to end timeout for the child workflow execution.
        /// Mandatory: no default
        /// </summary>
        [JsonProperty(PropertyName = "ExecutionStartToCloseTimeout", Required = Required.Always)]
        public long ExecutionStartToCloseTimeout { get; set; }

        /// <summary>
        /// TaskStartToCloseTimeout - The decision task timeout for the child workflow.
        /// Optional: default is 10s if this is not provided (or if 0 is provided).
        /// </summary>
        [JsonProperty(PropertyName = "TaskStartToCloseTimeout", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(10 * CadenceHelper.NanosecondsPerSecond)]
        public long TaskStartToCloseTimeout { get; set; } = 10 * CadenceHelper.NanosecondsPerSecond;

        /// <summary>
        /// ChildPolicy defines the behavior of child workflow when parent workflow is terminated.
        /// Optional: default to use ChildWorkflowPolicyAbandon. We currently only support this policy.
        /// </summary>
        [JsonProperty(PropertyName = "ChildPolicy", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue((int)Cadence.ChildPolicy.Abandon)]
        public int ChildPolicy { get; set; } = (int)Cadence.ChildPolicy.Abandon;

        /// <summary>
        /// WaitForCancellation - Whether to wait for cancelled child workflow to be ended (child workflow can be ended
        /// as: completed/failed/timedout/terminated/canceled)
        /// Optional: default false
        /// </summary>
        [JsonProperty(PropertyName = "WaitForCancellation", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool WaitForCancellation { get; set; } = false;

        /// <summary>
        /// WorkflowIDReusePolicy - Whether server allow reuse of workflow ID, can be useful
        /// for dedup logic if set to WorkflowIdReusePolicyRejectDuplicate
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
        /// as a CRON workflow based on the schedule. The scheduling will be based on UTC time. Schedule for next execution only happen
        /// after the current execution is completed/failed/timeout. If a RetryPolicy is also supplied, and the workflow failed
        /// or timeout, the workflow will be retried based on the retry policy. While the workflow is retrying, it won't
        /// schedule its next execution. If next schedule is due while workflow is running (or retrying), then it will skip that
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
    }
}
