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

using Newtonsoft.Json;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;

namespace Neon.Cadence
{
    /// <summary>
    /// Specifies the options to use when starting a workflow.
    /// </summary>
    public class WorkflowOptions
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
        /// This defaults to <see cref="WorkflowIdReusePolicy.WorkflowIDReusePolicyAllowDuplicateFailedOnly"/>.
        /// </summary>
        public WorkflowIdReusePolicy WorkflowIdReusePolicy { get; set; } = WorkflowIdReusePolicy.WorkflowIDReusePolicyAllowDuplicateFailedOnly;
        
        /// <summary>
        /// RetryPolicy - Optional retry policy for workflow. If a retry policy is specified, in case of workflow failure
        /// server will start new workflow execution if needed based on the retry policy.
        /// </summary>
        public CadenceRetryPolicy RetryPolicy { get; set; } = null;

        /// <summary>
        /// Optionally specifies a recurring schedule for the workflow.  See <see cref="CronSchedule"/>
        /// for more information.
        /// </summary>
        [JsonProperty(PropertyName = "CronSchedule", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public CronSchedule CronSchedule { get; set; }

        /// <summary>
        /// Converts the instance into an internal <see cref="InternalStartWorkflowOptions"/>.
        /// </summary>
        /// <returns>The corresponding <see cref="InternalStartWorkflowOptions"/>.</returns>
        internal InternalStartWorkflowOptions ToInternal()
        {
            if (string.IsNullOrEmpty(TaskList))
            {
                throw new ArgumentException($"[{nameof(TaskList)}] property is required.");
            }

            return new InternalStartWorkflowOptions()
            {
                ID                              = this.ID,
                TaskList                        = this.TaskList,
                DecisionTaskStartToCloseTimeout = CadenceHelper.ToCadence(this.DecisionTaskStartToCloseTimeout),
                ExecutionStartToCloseTimeout    = CadenceHelper.ToCadence(this.ExecutionStartToCloseTimeout),
                RetryPolicy                     = this.RetryPolicy.ToInternal(),
                WorkflowIdReusePolicy           = (int)this.WorkflowIdReusePolicy,
                CronSchedule                    = this.CronSchedule.ToInternal(),
            };
        }
    }
}
