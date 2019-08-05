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
        public string WorkflowId { get; set; } = null;

        /// <summary>
        /// <para>
        /// Optionally specifies the maximum time the workflow may execute from start to finish.
        /// This will defaults to 24 hours.
        /// </para>
        /// <note>
        /// This overrides the optional corresponding value specified in the
        /// <see cref="WorkflowMethodAttribute"/> tagging the workflow entry 
        /// point method.
        /// </note>
        /// </summary>
        public TimeSpan? ExecutionStartToCloseTimeout { get; set; }

        /// <summary>
        /// Optionally specifies the default maximum time a workflow can wait betweem being scheduled
        /// and actually begin executing.  This defaults to <c>24 hours</c>.
        /// </summary>
        public TimeSpan? ScheduleToStartTimeout { get; set; }

        /// <summary>
        /// Optionally specifies the time out for processing decision task from the time the worker
        /// pulled a task.  If a decision task is not completed within this interval, it will be retried 
        /// as specified by the retry policy.   This defaults to <b>10 seconds</b> when not specified.
        /// The maximum timeout is <b>60 seconds</b>.
        /// </summary>
        public TimeSpan? TaskStartToCloseTimeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// <para>
        /// Optionally determines how Cadence handles workflows that attempt to reuse workflow IDs.
        /// This defaults to <see cref="WorkflowIdReusePolicy.AllowDuplicateFailedOnly"/>
        /// for workflows when not specified.
        /// </para>
        /// <note>
        /// This overrides the optional corresponding value specified in the
        /// <see cref="WorkflowMethodAttribute"/> tagging the workflow entry 
        /// point method.
        /// </note>
        /// </summary>
        public WorkflowIdReusePolicy? WorkflowIdReusePolicy { get; set; }
        
        /// <summary>
        /// Optional retry options for the workflow.
        /// </summary>
        public RetryOptions RetryOptions { get; set; }

        /// <summary>
        /// Optionally specifies a recurring schedule for the workflow.  This can be set to a string specifying
        /// the minute, hour, day of month, month, and day of week scheduling parameters using the standard Linux
        /// CRON format described here: <a href="https://en.wikipedia.org/wiki/Cron"/>
        /// </summary>
        /// <remarks>
        /// <para>
        /// Cadence accepts a CRON string formatted as a single line of text with 5 parameters separated by
        /// spaces.  The parameters specified the minute, hour, day of month, month, and day of week values:
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
        /// <para>
        /// Each parameter may be set to one of:
        /// </para>
        /// <list type="table">
        /// <item>
        ///     <term><b>*</b></term>
        ///     <description>
        ///     Matches any value.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>value</b></term>
        ///     <description>
        ///     Matches a specific integer value.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>value1-value2</b></term>
        ///     <description>
        ///     Matches a range of values to be matched (inclusive).
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>value1,value2,...</b></term>
        ///     <description>
        ///     Matches a list of values to be matched.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>value1/value2</b></term>
        ///     <description>
        ///     Matches values starting at <b>value1</b> and then those incremented by <b>value2</b>.
        ///     </description>
        /// </item>
        /// </list>
        /// <para>
        /// You can use this handy CRON calculator to see how this works: <a href="https://crontab.guru"/>
        /// </para>
        /// </remarks>
        public string CronSchedule { get; set; }

        /// <summary>
        /// Optionally specifies workflow metadata as a dictionary of named byte array values.
        /// </summary>
        public Dictionary<string, byte[]> Memo { get; set; }

        /// <summary>
        /// Converts the instance into an internal <see cref="InternalStartWorkflowOptions"/>.
        /// </summary>
        /// <param name="client">The <see cref="CadenceClient"/>.</param>
        /// <param name="taskList">Optionally specifies the target task list.</param>
        /// <param name="methodAttribute">Optionally specifies a <see cref="WorkflowMethodAttribute"/>.</param>
        /// <returns>The corresponding <see cref="InternalStartWorkflowOptions"/>.</returns>
        internal InternalStartWorkflowOptions ToInternal(CadenceClient client, string taskList = null, WorkflowMethodAttribute methodAttribute = null)
        {
            Covenant.Requires<ArgumentNullException>(client != null);

            taskList = client.ResolveTaskList(taskList);

            // Merge optional settings from these options and the method attribute.

            var taskStartToCloseTimeout      = TimeSpan.FromSeconds(10);
            var executionStartToCloseTimeout = client.Settings.WorkflowScheduleToCloseTimeout;
            var workflowIdReusePolicy        = global::Neon.Cadence.WorkflowIdReusePolicy.AllowDuplicateFailedOnly;

            if (methodAttribute != null)
            {
                if (string.IsNullOrEmpty(taskList))
                {
                    if (methodAttribute.TaskList != null)
                    {
                        taskList = methodAttribute.TaskList;
                    }
                    else
                    {
                        taskList = client.Settings.DefaultTaskList;
                    }
                }

                if (!TaskStartToCloseTimeout.HasValue && methodAttribute.TaskStartToCloseTimeoutSeconds > 0)
                {
                    taskStartToCloseTimeout = TimeSpan.FromSeconds(methodAttribute.TaskStartToCloseTimeoutSeconds);
                }

                if (!ExecutionStartToCloseTimeout.HasValue && methodAttribute.ExecutionStartToCloseTimeoutSeconds > 0)
                {
                    executionStartToCloseTimeout = TimeSpan.FromSeconds(methodAttribute.ExecutionStartToCloseTimeoutSeconds);
                }

                if (!WorkflowIdReusePolicy.HasValue && methodAttribute.WorkflowIdReusePolicy.HasValue)
                {
                    workflowIdReusePolicy = methodAttribute.WorkflowIdReusePolicy.Value;
                }
            }

            return new InternalStartWorkflowOptions()
            {
                ID                              = this.WorkflowId,
                TaskList                        = taskList,
                DecisionTaskStartToCloseTimeout = CadenceHelper.ToCadence(taskStartToCloseTimeout),
                ExecutionStartToCloseTimeout    = CadenceHelper.ToCadence(executionStartToCloseTimeout),
                RetryPolicy                     = this.RetryOptions?.ToInternal(),
                WorkflowIdReusePolicy           = (int)workflowIdReusePolicy,
                CronSchedule                    = this.CronSchedule,
                Memo                            = this.Memo
            };
        }

        /// <summary>
        /// Retuurns a shallow clone of the current instance.
        /// </summary>
        /// <returns>The cloned <see cref="WorkflowOptions"/>.</returns>
        public WorkflowOptions Clone()
        {
            return new WorkflowOptions()
            {
                CronSchedule                 = this.CronSchedule,
                ExecutionStartToCloseTimeout = this.ExecutionStartToCloseTimeout,
                Memo                         = this.Memo,
                RetryOptions                 = this.RetryOptions,
                TaskStartToCloseTimeout      = this.TaskStartToCloseTimeout,
                WorkflowId                   = this.WorkflowId,
                WorkflowIdReusePolicy        = this.WorkflowIdReusePolicy
            };
        }
    }
}
