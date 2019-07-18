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
        /// Specifies the maximum time the workflow may run from start
        /// to finish.  This defaults to 365 days.
        /// </summary>
        public TimeSpan ExecutionStartToCloseTimeout { get; set; } = CadenceClient.DefaultTimeout;

        /// <summary>
        /// Optionally specifies the time out for processing decision task from the time the worker
        /// pulled this task.  If a decision task is lost, it is retried after this timeout.
        /// This defaults to <b>10 seconds</b>.
        /// </summary>
        public TimeSpan DecisionTaskStartToCloseTimeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Controls how Cadence handles workflows that attempt to reuse workflow IDs.
        /// This defaults to <see cref="WorkflowIdReusePolicy.AllowDuplicateFailedOnly"/>.
        /// </summary>
        public WorkflowIdReusePolicy WorkflowIdReusePolicy { get; set; } = WorkflowIdReusePolicy.AllowDuplicateFailedOnly;
        
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
        /// <param name="taskList">The target task list.</param>
        /// <returns>The corresponding <see cref="InternalStartWorkflowOptions"/>.</returns>
        internal InternalStartWorkflowOptions ToInternal(string taskList)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(taskList));

            return new InternalStartWorkflowOptions()
            {
                ID                              = this.WorkflowId,
                TaskList                        = taskList,
                DecisionTaskStartToCloseTimeout = CadenceHelper.ToCadence(this.DecisionTaskStartToCloseTimeout),
                ExecutionStartToCloseTimeout    = CadenceHelper.ToCadence(this.ExecutionStartToCloseTimeout),
                RetryPolicy                     = this.RetryOptions?.ToInternal(),
                WorkflowIdReusePolicy           = (int)this.WorkflowIdReusePolicy,
                CronSchedule                    = this.CronSchedule,
                Memo                            = this.Memo
            };
        }
    }
}
