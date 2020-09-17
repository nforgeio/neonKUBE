//-----------------------------------------------------------------------------
// FILE:	    WorkerOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Reflection.Metadata;
using Neon.Common;
using Neon.Data;
using Neon.Temporal;
using Neon.Temporal.Internal;
using Neon.Time;
using Newtonsoft.Json;

namespace Neon.Temporal
{
    /// <summary>
    /// Specifies the options Temporal will use when assigning workflow and activity
    /// executions to a user worker service.
    /// </summary>
    public class WorkerOptions
    {
        /// <summary>
        /// Optionally specifies the Temporal namespace for the worker.  This defaults to
        /// <see cref="TemporalSettings.Namespace"/>.
        /// </summary>
        public string Namespace { get; set; }

        /// <summary>
        /// <para>
        /// Optionally specifies the Temporal task queue for the worker.  This defaults to
        /// <see cref="TemporalSettings.DefaultTaskQueue"/>.
        /// </para>
        /// <note>
        /// You must ensure that this is not <c>null</c> or empty.
        /// </note>
        /// </summary>
        public string TaskQueue { get; set; }

        /// <summary>
        /// Optionally sets set the maximum concurrent activity executions this worker can have.
        /// The zero value of this uses the default value.  Defaults to <b>1000</b>.
        /// </summary>
        public int MaxConcurrentActivityExecutionSize { get; set; } = 1000;

        /// <summary>
        /// <para>
        /// Optionally sets the rate limiting on number of activities that can be executed per second per
        /// worker. This can be used to limit resources used by the worker.
        /// </para>
        /// <note>
        /// Notice that the number is represented in float, so that you can set it to less than
        /// 1 if needed. For example, set the number to 0.1 means you want your activity to be executed
        /// once for every 10 seconds. This can be used to protect down stream services from flooding.
        /// The zero value of this uses the default value..
        /// </note>
        /// <para>
        /// This defaults to <b>100,000</b>.
        /// </para>
        /// </summary>
        public double WorkerActivitiesPerSecond { get; set; } = 100000;

        /// <summary>
        /// Optionally sets the maximum concurrent local activity executions this worker can have.
        /// The zero value of this uses the default value.  This defaults to <b>1000</b>.
        /// </summary>
        public int MaxConcurrentLocalActivityExecutionSize { get; set; } = 1000;

        /// <summary>
        /// <para>
        /// Optionally sets the rate limiting on number of local activities that can be executed per second per
        /// worker. This can be used to limit resources used by the worker.
        /// </para>
        /// <note>
        /// Notice that the number is represented in float, so that you can set it to less than
        /// 1 if needed. For example, set the number to 0.1 means you want your local activity to be executed
        /// once for every 10 seconds. This can be used to protect down stream services from flooding.
        /// The zero value of this uses the default value.
        /// </note>
        /// <para>
        /// This defaults to <b>100,000</b>.
        /// </para>
        /// </summary>
        public double WorkerLocalActivitiesPerSecond { get; set; } = 100000;

        /// <summary>
        /// <para>
        /// Optionally sets the rate limiting on number of activities that can be executed per second.
        /// This is managed by the server and controls activities per second for your entire task queue
        /// whereas WorkerActivityTasksPerSecond controls activities only per worker.
        /// </para>
        /// <note>
        /// Notice that the number is represented in float, so that you can set it to less than
        /// 1 if needed. For example, set the number to 0.1 means you want your activity to be executed
        /// once for every 10 seconds. This can be used to protect down stream services from flooding.
        /// </note>
        /// <para>
        /// The zero value of this uses the default value. This defaults to <b>100,000</b>.
        /// </para>
        /// </summary>
        public double TaskQueueActivitiesPerSecond { get; set; } = 100000;

        /// <summary>
        /// Optionally sets the maximum concurrent decision task executions this worker can have.
        /// The zero value of this uses the default value.  This defaults to <b>100,000</b>.
        /// </summary>
        public int MaxConcurrentDecisionTaskExecutionSize { get; set; } = 100000;

        /// <summary>
        /// Optionally stes the rate limiting on number of decision tasks that can be executed per
        /// second per worker. This can be used to limit resources used by the worker.
        /// The zero value of this uses the default value.  This defaults to <b>1000</b>.
        /// </summary>
        public double WorkerDecisionTasksPerSecond { get; set; } = 1000;

        /// <summary>
        /// Optionally enables logging in replay.  This defaults to <c>false</c>.
        /// </summary>
        /// <remarks>
        /// In the workflow code you can use workflow.GetLogger(ctx) to write logs. By default, the logger will skip log
        /// entry during replay mode so you won't see duplicate logs. This option will enable the logging in replay mode.
        /// This is only useful for debugging purpose.
        /// </remarks>
        public bool EnableLoggingInReplay { get; set; } = false;

        /// <summary>
        /// Optionally disable workflow processing on the worker.  This defaults to <c>false</c>.
        /// </summary>
        public bool DisableWorkflowWorker { get; set; } = false;

        /// <summary>
        /// Optionally disable activity processing on the worker.  This defaults to <c>false</c>.
        /// </summary>
        public bool DisableActivityWorker { get; set; } = false;

        /// <summary>
        /// Returns the worker mode.
        /// </summary>
        internal WorkerMode Mode
        {
            get
            {
                if (DisableActivityWorker && DisableWorkflowWorker)
                {
                    throw new InvalidOperationException("A Temporal worker cannot disable both activity and workflow processing.");
                }
                else if (!DisableActivityWorker && !DisableWorkflowWorker)
                {
                    return WorkerMode.Both;
                }
                else if (!DisableActivityWorker)
                {
                    return WorkerMode.Activity;
                }
                else if (!DisableWorkflowWorker)
                {
                    return WorkerMode.Workflow;
                }

                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Optionally disables sticky execution.  This defaults to <c>false</c>.
        /// </summary>
        /// <remarks>
        /// This is an optimization for workflow execution. When sticky execution is enabled, the worker can maintain
        /// workflow state and history making workflow replaying faster.
        /// </remarks>
        internal bool DisableStickyExecution { get; set; }

        /// <summary>
        /// Optionally sets the sticky schedule to start timeout.  Defaults to <b>5 seconds</b>.
        /// </summary>
        [JsonConverter(typeof(GoTimeSpanJsonConverter))]
        public TimeSpan StickyScheduleToStartTimeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Optionally sets how decision workers deals with non-deterministic history events,
        /// presumably arising from non-deterministic workflow definitions or non-backward compatible workflow definition changes.
        /// This defaults to <see cref="NonDeterministicPolicy.BlockWorkflow"/> which 
        /// just logs error and does not fail the workflow.
        /// </summary>
        [JsonConverter(typeof(IntegerEnumConverter<NonDeterministicPolicy>))]
        public NonDeterministicPolicy NonDeterministicWorkflowPolicy { get; set; } = NonDeterministicPolicy.BlockWorkflow;

        /// <summary>
        /// Optionally sets the graceful shutdown timeout.  Defaults to zero.  Time is represented in nanoseconds.
        /// </summary>
        [JsonConverter(typeof(GoTimeSpanJsonConverter))]
        public TimeSpan WorkerStopTimeout { get; set; } = TimeSpan.Zero;
    }
}
