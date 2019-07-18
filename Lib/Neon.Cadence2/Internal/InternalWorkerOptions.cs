//-----------------------------------------------------------------------------
// FILE:	    InternalWorkerOptions.cs
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
    /// <b>INTERNAL USE ONLY:</b> Specifies the options Cadence will use when assigning
    /// workflow and activity executions to a user's worker service.  This maps fairly 
    /// closely to the  Cadence GOLANG <b>WorkerOptions</b>, but we removed a few fields
    /// that don't make sense to serialize.  See the remarks for more information.
    /// </summary>
    /// <remarks>
    /// <list type="table">
    /// <item>
    ///     <term><b>MetricsScope</b></term>
    ///     <description>
    ///     I don't believe we'll really need to specify this on a per-workflow basis.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>Logger</b></term>
    ///     <description>
    ///     We're not going to support custom workflow loggers.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>BackgroundActivityContext </b></term>
    ///     <description>
    ///     I believe the <b>cadence-proxy</b> can a common context for all
    ///     workflow executions.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>DataConverter </b></term>
    ///     <description>
    ///     This is not required because <b>cadence-proxy</b> deals only with
    ///     raw byte arrays.
    ///     </description>
    /// </item>
    /// </list>
    /// </remarks>
    internal class InternalWorkerOptions
    {
        /// <summary>
        /// Optional: To set the maximum concurrent activity executions this worker can have.
        /// The zero value of this uses the default value.
        /// default: defaultMaxConcurrentActivityExecutionSize(1k)
        /// </summary>
        [JsonProperty(PropertyName = "MaxConcurrentActivityExecutionSize", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public int MaxConcurrentActivityExecutionSize { get; set; }

        /// <summary>
        /// Optional: Sets the rate limiting on number of activities that can be executed per second per
        /// worker. This can be used to limit resources used by the worker.
        /// Notice that the number is represented in float, so that you can set it to less than
        /// 1 if needed. For example, set the number to 0.1 means you want your activity to be executed
        /// once for every 10 seconds. This can be used to protect down stream services from flooding.
        /// The zero value of this uses the default value. Default: 100k
        /// </summary>
        [JsonProperty(PropertyName = "WorkerActivitiesPerSecond", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0.0)]
        public double WorkerActivitiesPerSecond { get; set; }

        /// <summary>
        /// Optional: To set the maximum concurrent local activity executions this worker can have.
        /// The zero value of this uses the default value.
        /// default: 1k
        /// </summary>
        [JsonProperty(PropertyName = "MaxConcurrentLocalActivityExecutionSize", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0.0)]
        public int MaxConcurrentLocalActivityExecutionSize { get; set; }

        /// <summary>
        /// Optional: Sets the rate limiting on number of local activities that can be executed per second per
        /// worker. This can be used to limit resources used by the worker.
        /// Notice that the number is represented in float, so that you can set it to less than
        /// 1 if needed. For example, set the number to 0.1 means you want your local activity to be executed
        /// once for every 10 seconds. This can be used to protect down stream services from flooding.
        /// The zero value of this uses the default value. Default: 100k
        /// </summary>
        [JsonProperty(PropertyName = "WorkerLocalActivitiesPerSecond", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0.0)]
        public double WorkerLocalActivitiesPerSecond { get; set; }

        /// <summary>
        /// Optional: Sets the rate limiting on number of activities that can be executed per second.
        /// This is managed by the server and controls activities per second for your entire task list
        /// whereas WorkerActivityTasksPerSecond controls activities only per worker.
        /// Notice that the number is represented in float, so that you can set it to less than
        /// 1 if needed. For example, set the number to 0.1 means you want your activity to be executed
        /// once for every 10 seconds. This can be used to protect down stream services from flooding.
        /// The zero value of this uses the default value. Default: 100k
        /// </summary>
        [JsonProperty(PropertyName = "TaskListActivitiesPerSecond", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0.0)]
        public double TaskListActivitiesPerSecond { get; set; }

        /// <summary>
        /// Optional: To set the maximum concurrent decision task executions this worker can have.
        /// The zero value of this uses the default value.
        /// default: defaultMaxConcurrentTaskExecutionSize(1k)
        /// </summary>
        [JsonProperty(PropertyName = "MaxConcurrentDecisionTaskExecutionSize", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public int MaxConcurrentDecisionTaskExecutionSize { get; set; }

        /// <summary>
        /// Optional: Sets the rate limiting on number of decision tasks that can be executed per second per
        /// worker. This can be used to limit resources used by the worker.
        /// The zero value of this uses the default value. Default: 100k
        /// </summary>
        [JsonProperty(PropertyName = "WorkerDecisionTasksPerSecond", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0.0)]
        public double WorkerDecisionTasksPerSecond { get; set; }

        /// <summary>
        /// Optional: if the activities need auto heart beating for those activities
        /// by the framework
        /// default: false not to heartbeat.
        /// </summary>
        [JsonProperty(PropertyName = "AutoHeartBeat", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool AutoHeartBeat { get; set; }

        /// <summary>
        /// Optional: Sets an identify that can be used to track this host for debugging.
        /// default: default identity that include hostname, groupName and process ID.
        /// </summary>
        [JsonProperty(PropertyName = "Identity", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Identity { get; set; }

        /// <summary>
        /// Optional: Enable logging in replay.
        /// In the workflow code you can use workflow.GetLogger(ctx) to write logs. By default, the logger will skip log
        /// entry during replay mode so you won't see duplicate logs. This option will enable the logging in replay mode.
        /// This is only useful for debugging purpose.
        /// default: false
        /// </summary>
        [JsonProperty(PropertyName = "EnableLoggingInReplay", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool EnableLoggingInReplay { get; set; }

        /// <summary>
        /// Optional: Disable running workflow workers.
        /// default: false
        /// </summary>
        [JsonProperty(PropertyName = "DisableWorkflowWorker", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool DisableWorkflowWorker { get; set; }

        /// <summary>
        /// Optional: Disable running activity workers.
        /// default: false
        /// </summary>
        [JsonProperty(PropertyName = "DisableActivityWorker", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool DisableActivityWorker { get; set; }

        /// <summary>
        /// Optional: Disable sticky execution.
        /// default: false
        /// Sticky Execution is to run the decision tasks for one workflow execution on same worker host. This is an
        /// optimization for workflow execution. When sticky execution is enabled, worker keeps the workflow state in
        /// memory. New decision task contains the new history events will be dispatched to the same worker. If this
        /// worker crashes, the sticky decision task will timeout after StickyScheduleToStartTimeout, and cadence server
        /// will clear the stickiness for that workflow execution and automatically reschedule a new decision task that
        /// is available for any worker to pick up and resume the progress.
        /// </summary>
        [JsonProperty(PropertyName = "DisableStickyExecution", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool DisableStickyExecution { get; set; }

        /// <summary>
        /// Optional: Sticky schedule to start timeout.
        /// default: 5s
        /// </summary>
        [JsonProperty(PropertyName = "StickyScheduleToStartTimeout", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(5 * CadenceHelper.NanosecondsPerSecond)]
        public long StickyScheduleToStartTimeout { get; set; } = 5 * CadenceHelper.NanosecondsPerSecond;

        /// <summary>
        /// Optional: Sets how decision worker deals with non-deterministic history events
        /// (presumably arising from non-deterministic workflow definitions or non-backward compatible workflow definition changes).
        /// default: NonDeterministicWorkflowPolicyBlockWorkflow, which just logs error but reply nothing back to server
        /// </summary>
        [JsonProperty(PropertyName = "NonDeterministicWorkflowPolicy", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public int NonDeterministicWorkflowPolicy { get; set; } = (int)Neon.Cadence.NonDeterministicPolicy.BlockWorkflow;

        /// <summary>
        /// Optional: worker graceful shutdown timeout.
        /// default: 0s
        /// </summary>
        [JsonProperty(PropertyName = "WorkerStopTimeout", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0L)]
        public long WorkerStopTimeout { get; set; } = 0L;
    }
}
