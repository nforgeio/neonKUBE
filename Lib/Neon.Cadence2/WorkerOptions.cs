//-----------------------------------------------------------------------------
// FILE:	    WorkerOptions.cs
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

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Time;

namespace Neon.Cadence
{
    /// <summary>
    /// Specifies the options Cadence will use when assigning workflow and activity
    /// executions to a user worker service.
    /// </summary>
    public class WorkerOptions
    {
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
        /// This is managed by the server and controls activities per second for your entire task list
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
        public double TaskListActivitiesPerSecond { get; set; } = 100000;

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
        /// Optionally sets an identify that can be used to track this host for debugging.
        /// This defaults to include the hostname, groupName and process ID.
        /// </summary>
        public string Identity { get; set; } = null;

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
                    throw new InvalidOperationException("A Cadence worker cannot disable both activity and workflow processing.");
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
        public bool DisableStickyExecution { get; set; }

        /// <summary>
        /// Optionally sets the sticky schedule to start timeout.  Defaults to <b>5 seconds</b>.
        /// </summary>
        public TimeSpan StickyScheduleToStartTimeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Optionally sets how decision workers deals with non-deterministic history events,
        /// presumably arising from non-deterministic workflow definitions or non-backward compatible workflow definition changes.
        /// This defaults to <see cref="NonDeterministicPolicy.BlockWorkflow"/> which 
        /// just logs error but reply nothing back to server
        /// </summary>
        public NonDeterministicPolicy NonDeterministicWorkflowPolicy { get; set; } = NonDeterministicPolicy.BlockWorkflow;

        /// <summary>
        /// Optionally sets the graceful shutdown timeout.  Defaults to <see cref="TimeSpan.Zero"/>.
        /// </summary>
        public TimeSpan WorkerStopTimeout { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Optionally sets the <see cref="IDataConverter"/> implementation used to manage
        /// serialization of paramaters and results for workflow and activity methods so they
        /// can be persisted to the Cadence cluster database.  This defaults to a <see cref="JsonConverter"/>
        /// instance which will serialize data as UTF-8 encoded JSON text.
        /// </summary>
        public IDataConverter DataConverter { get; set; } = new JsonConverter();

        /// <summary>
        /// Converts the instance into an <see cref="InternalWorkerOptions"/>.
        /// </summary>
        /// <returns>The converted instance.</returns>
        internal InternalWorkerOptions ToInternal()
        {
            return new InternalWorkerOptions()
            {
                MaxConcurrentActivityExecutionSize       = this.MaxConcurrentActivityExecutionSize,
                WorkerActivitiesPerSecond                = this.WorkerActivitiesPerSecond,
                MaxConcurrentLocalActivityExecutionSize  = this.MaxConcurrentLocalActivityExecutionSize,
                WorkerLocalActivitiesPerSecond           = this.WorkerLocalActivitiesPerSecond,
                TaskListActivitiesPerSecond              = this.TaskListActivitiesPerSecond,
                MaxConcurrentDecisionTaskExecutionSize   = this.MaxConcurrentDecisionTaskExecutionSize,
                WorkerDecisionTasksPerSecond             = this.WorkerDecisionTasksPerSecond,
                AutoHeartBeat                            = false,
                Identity                                 = this.Identity,
                EnableLoggingInReplay                    = this.EnableLoggingInReplay,
                DisableWorkflowWorker                    = this.DisableWorkflowWorker,
                DisableActivityWorker                    = this.DisableActivityWorker,
                DisableStickyExecution                   = this.DisableStickyExecution,
                StickyScheduleToStartTimeout             = GoTimeSpan.FromTimeSpan(this.StickyScheduleToStartTimeout).Ticks,
                NonDeterministicWorkflowPolicy           = (int)this.NonDeterministicWorkflowPolicy,
                WorkerStopTimeout                        = GoTimeSpan.FromTimeSpan(this.WorkerStopTimeout).Ticks
            };
        }
    }
}
