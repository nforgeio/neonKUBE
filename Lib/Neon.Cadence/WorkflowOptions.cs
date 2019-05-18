//-----------------------------------------------------------------------------
// FILE:	    WorkflowOptions.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;

using Newtonsoft.Json;
using YamlDotNet.Serialization;

using Neon.Cadence;
using Neon.Common;
using Neon.Retry;
using Neon.Time;

namespace Neon.Cadence
{
    /// <summary>
    /// Specifies the options to use when starting a workflow.
    /// </summary>
    internal class WorkflowOptions
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public WorkflowOptions()
        {
        }

        /// <summary>
        /// Optionally specifies the business ID for a workflow.  This defaults
        /// to a generated UUID.
        /// </summary>
        public string ID { get; set; } = null;

        /// <summary>
        /// Specifies the tasklist where this workflow will be scheduled.
        /// </summary>
        public string TaskList { get; set; }

        /// <summary>
        /// Specifies the maximum time the workflow may run from start
        /// to finish.  This is required.
        /// </summary>
        public TimeSpan ExecutionStartToCloseTimeout { get; set; }

        /// <summary>
        /// Op[tionally specifies the time out for processing decision task from the time the worker
        /// pulled this task.  If a decision task is lost, it is retried after this timeout.
        /// This defaults to <b>10 seconds</b>.
        /// </summary>
        public TimeSpan DecisionTaskStartToCloseTimeout { get; set; }

        /// <summary>
        /// Controls how Cadence handles workflows that attempt to reuse workflow IDs.
        /// This defaults to <see cref="WorkflowIDReusePolicy.WorkflowIDReusePolicyAllowDuplicateFailedOnly"/>.
        /// </summary>
        public WorkflowIDReusePolicy WorkflowIdReusePolicy { get; set; } = WorkflowIDReusePolicy.WorkflowIDReusePolicyAllowDuplicateFailedOnly;
        
        /// <summary>
        /// RetryPolicy - Optional retry policy for workflow. If a retry policy is specified, in case of workflow failure
        /// server will start new workflow execution if needed based on the retry policy.
        /// </summary>
        public CadenceRetryPolicy RetryPolicy { get; set; } = null;

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
    }
}
