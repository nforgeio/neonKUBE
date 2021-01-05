//-----------------------------------------------------------------------------
// FILE:	    InternalWorkerOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
        /// Specifies the maximum concurrent activity executions this worker can have.
        /// The zero value of this uses the default value of <b>1K</b>.
        /// </summary>
        [JsonProperty(PropertyName = "MaxConcurrentActivityExecutionSize", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public int MaxConcurrentActivityExecutionSize { get; set; }

        /// <summary>
        /// Specifies the rate limiting on number of activities that can be executed per second per
        /// worker.  This defaults to <b>100K</b> when the value is zero.
        /// </summary>
        /// <remarks>
        /// This can be used to limit resources used by the worker.   Notice that the number is represented 
        /// in float, so that you can set it to less than 1 if needed. For example, set the number to 0.1 
        /// means you want your activity to be executed once for every 10 seconds. This can be used to
        /// protect down stream services from flooding.
        /// </remarks>
        [JsonProperty(PropertyName = "WorkerActivitiesPerSecond", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0.0)]
        public double WorkerActivitiesPerSecond { get; set; }

        /// <summary>
        /// Specifies the maximum concurrent local activity executions this worker can have.
        /// This defaults to <b>1K</b> when the value is zero.
        /// </summary>
        [JsonProperty(PropertyName = "MaxConcurrentLocalActivityExecutionSize", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0.0)]
        public int MaxConcurrentLocalActivityExecutionSize { get; set; }

        /// <summary>
        /// Specifies the rate limiting on number of local activities that can be executed per second per
        /// worker. This can be used to limit resources used by the worker.  This defaults to <b>100K</b> when
        /// the value is zero.
        /// </summary>
        /// <remarks>
        /// This can be used to limit resources used by the worker.  Notice that the number is represented in float,
        /// so that you can set it to less than 1 if needed. For example, set the number to 0.1 means you want your
        /// local activity to be executed once for every 10 seconds. This can be used to protect down stream services 
        /// from flooding.
        /// </remarks>
        [JsonProperty(PropertyName = "WorkerLocalActivitiesPerSecond", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0.0)]
        public double WorkerLocalActivitiesPerSecond { get; set; }

        /// <summary>
        /// Specifies rate limiting on number of activities that can be executed per second.
        /// This defaults to <b>100K</b> when the value is zero.
        /// </summary>
        /// <remarks>
        /// This is managed by the server and controls activities per second for your entire task list
        /// whereas WorkerActivityTasksPerSecond controls activities only per worker.
        /// Notice that the number is represented in float, so that you can set it to less than
        /// 1 if needed. For example, set the number to 0.1 means you want your activity to be executed
        /// once for every 10 seconds. This can be used to protect down stream services from flooding.
        /// </remarks>
        [JsonProperty(PropertyName = "TaskListActivitiesPerSecond", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0.0)]
        public double TaskListActivitiesPerSecond { get; set; }

        /// <summary>
        /// Specifies the maximum concurrent decision task executions this worker can have.
        /// This defaults to <b>1K</b> when the value is zero.
        /// </summary>
        [JsonProperty(PropertyName = "MaxConcurrentDecisionTaskExecutionSize", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public int MaxConcurrentDecisionTaskExecutionSize { get; set; }

        /// <summary>
        /// Specifies the rate limiting on number of decision tasks that can be executed per second per
        /// worker. This can be used to limit resources used by the worker.
        /// The zero value of this uses the default value. Default: 100k
        /// </summary>
        [JsonProperty(PropertyName = "WorkerDecisionTasksPerSecond", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0.0)]
        public double WorkerDecisionTasksPerSecond { get; set; }

        /// <summary>
        /// Enables auto heart-beating for activities.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "AutoHeartBeat", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool AutoHeartBeat { get; set; }

        /// <summary>
        /// Specifies an identity that will be used to track this host for debugging.
        /// This will be included in the Cadence woirkflow history.  This defaults to
        /// a string including the current hostname, groupName and process ID.
        /// </summary>
        [JsonProperty(PropertyName = "Identity", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Identity { get; set; }

        /// <summary>
        /// Optionally enables logging during workflow replay.  This defaults to <c>false</c>.
        /// </summary>
        /// <remarks>
        /// By default, the workflow logger will not log events while workflows are replaying to
        /// avoid duplicate logs.   Enabling this is generally useful only for debugging purposes.
        /// </remarks>
        [JsonProperty(PropertyName = "EnableLoggingInReplay", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool EnableLoggingInReplay { get; set; }

        /// <summary>
        /// Optionally prevents the worker from executing workflows.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "DisableWorkflowWorker", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool DisableWorkflowWorker { get; set; }

        /// <summary>
        /// Optionally prevents the worker from executing activities.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "DisableActivityWorker", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool DisableActivityWorker { get; set; }

        /// <summary>
        /// Optionally disables sticky execution.  This defaults to <c>false</c>.
        /// </summary>
        /// <remarks>
        /// Sticky Execution is to run the decision tasks for one workflow execution on same worker host. This is an
        /// optimization for workflow execution. When sticky execution is enabled, worker keeps the workflow state in
        /// memory. New decision task contains the new history events will be dispatched to the same worker. If this
        /// worker crashes, the sticky decision task will timeout after StickyScheduleToStartTimeout, and cadence server
        /// will clear the stickiness for that workflow execution and automatically reschedule a new decision task that
        /// is available for any worker to pick up and resume the progress.
        /// </remarks>
        [JsonProperty(PropertyName = "DisableStickyExecution", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool DisableStickyExecution { get; set; }

        /// <summary>
        /// Optionally disables the sticky schedule to start timeout.  This defaults to <b>5 seconds</b>.
        /// </summary>
        [JsonProperty(PropertyName = "StickyScheduleToStartTimeout", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(5 * CadenceHelper.NanosecondsPerSecond)]
        public long StickyScheduleToStartTimeout { get; set; } = 5 * CadenceHelper.NanosecondsPerSecond;

        /// <summary>
        /// Configures how decision worker deals with non-deterministic history events.  This defaults to
        /// <see cref="NonDeterministicPolicy.BlockWorkflow"/> which logs an error but does not fail the
        /// workflow.
        /// </summary>
        [JsonProperty(PropertyName = "NonDeterministicWorkflowPolicy", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public int NonDeterministicWorkflowPolicy { get; set; } = (int)Neon.Cadence.NonDeterministicPolicy.BlockWorkflow;

        /// <summary>
        /// Specifies the maximum time the client will wait for the worker to shutdown gracefully
        /// before it will be forcefully terminated.
        /// </summary>
        [JsonProperty(PropertyName = "WorkerStopTimeout", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0L)]
        public long WorkerStopTimeout { get; set; } = 0L;
    }
}
