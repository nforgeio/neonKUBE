//-----------------------------------------------------------------------------
// FILE:	    WorkflowOptions.cs
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
using System.Diagnostics.Contracts;
using System.Reflection;

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
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Normalizes the options passed by creating or cloning a new instance as 
        /// required and filling unset properties using default client settings.
        /// </summary>
        /// <param name="client">The associated Cadence client.</param>
        /// <param name="options">The input options or <c>null</c>.</param>
        /// <param name="workflowInterface">Optionally specifies the workflow interface definition.</param>
        /// <param name="method">Optionally specifies the target workflow method.</param>
        /// <returns>The normalized options.</returns>
        /// <exception cref="ArgumentNullException">Thrown if a valid task list is not specified.</exception>
        internal static WorkflowOptions Normalize(CadenceClient client, WorkflowOptions options, Type workflowInterface = null, MethodInfo method = null)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));

            WorkflowInterfaceAttribute  interfaceAttribute = null;
            WorkflowMethodAttribute     methodAttribute    = null;

            if (options == null)
            {
                options = new WorkflowOptions();
            }
            else
            {
                options = options.Clone();
            }

            if (workflowInterface != null)
            {
                CadenceHelper.ValidateWorkflowInterface(workflowInterface);

                interfaceAttribute = workflowInterface.GetCustomAttribute<WorkflowInterfaceAttribute>();
            }

            if (method != null)
            {
                methodAttribute = method.GetCustomAttribute<WorkflowMethodAttribute>();
            }

            if (string.IsNullOrEmpty(options.Domain))
            {
                if (!string.IsNullOrEmpty(methodAttribute?.Domain))
                {
                    options.Domain = methodAttribute.Domain;
                }

                if (string.IsNullOrEmpty(options.Domain) && !string.IsNullOrEmpty(interfaceAttribute?.Domain))
                {
                    options.Domain = interfaceAttribute.Domain;
                }

                if (string.IsNullOrEmpty(options.Domain))
                {
                    options.Domain = client.Settings.DefaultDomain;
                }

                if (string.IsNullOrEmpty(options.Domain))
                {
                    throw new ArgumentNullException(nameof(options), "You must specify a valid domain explicitly in [CadenceSettings], [ActivityOptions] or via an [ActivityInterface] or [ActivityMethod] attribute on the target activity interface or method.");
                }
            }

            if (string.IsNullOrEmpty(options.TaskList))
            {
                if (!string.IsNullOrEmpty(methodAttribute?.TaskList))
                {
                    options.TaskList = methodAttribute.TaskList;
                }

                if (string.IsNullOrEmpty(options.TaskList) && !string.IsNullOrEmpty(interfaceAttribute?.TaskList))
                {
                    options.TaskList = interfaceAttribute.TaskList;
                }

                if (string.IsNullOrEmpty(options.TaskList))
                {
                    options.TaskList = client.Settings.DefaultTaskList;
                }

                if (string.IsNullOrEmpty(options.TaskList))
                {
                    throw new ArgumentNullException(nameof(options), "You must specify a valid task list explicitly via [WorkflowOptions] or using an [WorkflowInterface] or [WorkflowMethod] attribute on the target workflow interface or method.");
                }
            }

            if (options.StartToCloseTimeout <= TimeSpan.Zero)
            {
                if (methodAttribute != null && methodAttribute.StartToCloseTimeoutSeconds > 0)
                {
                    options.StartToCloseTimeout = TimeSpan.FromSeconds(methodAttribute.StartToCloseTimeoutSeconds);
                }

                if (options.StartToCloseTimeout <= TimeSpan.Zero)
                {
                    options.StartToCloseTimeout = client.Settings.WorkflowStartToCloseTimeout;
                }
            }

            if (options.ScheduleToStartTimeout <= TimeSpan.Zero)
            {
                if (methodAttribute != null && methodAttribute.ScheduleToStartTimeoutSeconds > 0)
                {
                    options.ScheduleToStartTimeout = TimeSpan.FromSeconds(methodAttribute.ScheduleToStartTimeoutSeconds);
                }

                if (options.ScheduleToStartTimeout <= TimeSpan.Zero)
                {
                    options.ScheduleToStartTimeout = client.Settings.WorkflowScheduleToStartTimeout;
                }
            }

            if (options.DecisionTaskTimeout <= TimeSpan.Zero)
            {
                if (methodAttribute != null && methodAttribute.DecisionTaskTimeoutSeconds > 0)
                {
                    options.DecisionTaskTimeout = TimeSpan.FromSeconds(methodAttribute.DecisionTaskTimeoutSeconds);
                }

                if (options.DecisionTaskTimeout <= TimeSpan.Zero)
                {
                    options.DecisionTaskTimeout = client.Settings.WorkflowDecisionTaskTimeout;
                }
            }

            if (options.WorkflowIdReusePolicy == Cadence.WorkflowIdReusePolicy.UseDefault)
            {
                if (methodAttribute != null && methodAttribute.WorkflowIdReusePolicy != WorkflowIdReusePolicy.UseDefault)
                {
                    options.WorkflowIdReusePolicy = methodAttribute.WorkflowIdReusePolicy;
                }

                if (options.WorkflowIdReusePolicy == Cadence.WorkflowIdReusePolicy.UseDefault)
                {
                    options.WorkflowIdReusePolicy = client.Settings.WorkflowIdReusePolicy;
                }
            }

            if (string.IsNullOrEmpty(options.CronSchedule) && !string.IsNullOrEmpty(methodAttribute?.CronSchedule))
            {
                options.CronSchedule = methodAttribute.CronSchedule;
            }

            return options;
        }

        //---------------------------------------------------------------------
        // Instance members

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
        /// Optionally specifies the target Cadence domain.  This defaults to the domain
        /// specified by <see cref="WorkflowMethodAttribute.Domain"/>, 
        /// <see cref="WorkflowInterfaceAttribute.Domain"/>, or 
        /// to the client's <see cref="CadenceSettings"/>, in that 
        /// order of precedence.
        /// </summary>
        public string Domain { get; set; } = null;

        /// <summary>
        /// Optionally specifies the target Cadence task list.  This defaults to the task list
        /// specified by <see cref="WorkflowMethodAttribute.TaskList"/> or
        /// <see cref="WorkflowInterfaceAttribute.TaskList"/>or 
        /// to the client's <see cref="CadenceSettings"/>, in that 
        /// order of precedence.
        /// </summary>
        public string TaskList { get; set; } = null;

        /// <summary>
        /// Optionally specifies the default maximum time a workflow can wait between being scheduled
        /// and actually begin executing.  This defaults to <c>24 hours</c>.
        /// </summary>
        public TimeSpan ScheduleToStartTimeout { get; set; }

        /// <summary>
        /// <para>
        /// Optionally specifies the maximum time the workflow may execute from start to finish.
        /// This defaults to 24 hours.
        /// </para>
        /// <note>
        /// This overrides the optional corresponding value specified in the
        /// <see cref="WorkflowMethodAttribute"/> tagging the workflow entry 
        /// point method.
        /// </note>
        /// </summary>
        public TimeSpan StartToCloseTimeout { get; set; }

        /// <summary>
        /// Optionally specifies the timeout for processing decision task from the time the worker
        /// pulled a task.  If a decision task is not completed within this interval, it will be retried 
        /// as specified by the retry policy.   This defaults to <b>10 seconds</b> when not specified.
        /// The maximum timeout is <b>60 seconds</b>.
        /// </summary>
        public TimeSpan DecisionTaskTimeout { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Optionally determines how Cadence handles workflows that attempt to reuse workflow IDs.
        /// This generally defaults to <see cref="WorkflowIdReusePolicy.AllowDuplicate"/>
        /// but the default can be customized via the <see cref="WorkflowMethodAttribute"/> tagging
        /// the workflow entry point method or <see cref="CadenceSettings.WorkflowIdReusePolicy"/>
        /// (which also defaults to <see cref="WorkflowIdReusePolicy.AllowDuplicate"/>.
        /// </summary>
        public WorkflowIdReusePolicy WorkflowIdReusePolicy { get; set; } = Cadence.WorkflowIdReusePolicy.UseDefault;
        
        /// <summary>
        /// Optional retry options for the workflow.
        /// </summary>
        public RetryOptions RetryOptions { get; set; }

        /// <summary>
        /// Optionally specifies a recurring schedule for the workflow.  This can be set to a string specifying
        /// the minute, hour, day of month, month, and day of week scheduling parameters using the standard Linux
        /// CRON format described here: <a href="https://en.wikipedia.org/wiki/Cron">https://en.wikipedia.org/wiki/Cron</a>
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
        /// You can use this handy CRON calculator to see how this works: <a href="https://crontab.guru">https://crontab.guru</a>
        /// </para>
        /// </remarks>
        public string CronSchedule { get; set; }

        /// <summary>
        /// <para>
        /// Optionally specifies workflow metadata as a dictionary of named object values.
        /// </para>
        /// <note>
        /// The object values will be serialized into bytes using the the client's
        /// <see cref="IDataConverter"/>.
        /// </note>
        /// </summary>
        public Dictionary<string, object> Memo { get; set; }

        /// <summary>
        /// Converts the instance into an internal <see cref="InternalStartWorkflowOptions"/>.
        /// </summary>
        /// <returns>The corresponding <see cref="InternalStartWorkflowOptions"/>.</returns>
        internal InternalStartWorkflowOptions ToInternal()
        {
            Dictionary<string, byte[]> encodedMemos = null;

            if (Memo != null && Memo.Count > 0)
            {
                encodedMemos = new Dictionary<string, byte[]>();

                foreach (var item in Memo)
                {
                    encodedMemos.Add(item.Key, NeonHelper.JsonSerializeToBytes(item.Value));
                }
            }

            return new InternalStartWorkflowOptions()
            {
                ID                              = this.WorkflowId,
                TaskList                        = this.TaskList,
                DecisionTaskStartToCloseTimeout = CadenceHelper.ToCadence(this.DecisionTaskTimeout),
                ExecutionStartToCloseTimeout    = CadenceHelper.ToCadence(this.StartToCloseTimeout),
                RetryPolicy                     = this.RetryOptions?.ToInternal(),
                WorkflowIdReusePolicy           = (int)(this.WorkflowIdReusePolicy == WorkflowIdReusePolicy.UseDefault ? Cadence.WorkflowIdReusePolicy.AllowDuplicate : this.WorkflowIdReusePolicy),
                CronSchedule                    = this.CronSchedule,
                Memo                            = encodedMemos
            };
        }

        /// <summary>
        /// Returns a shallow clone of the current instance.
        /// </summary>
        /// <returns>The cloned <see cref="WorkflowOptions"/>.</returns>
        public WorkflowOptions Clone()
        {
            return new WorkflowOptions()
            {
                Domain                 = this.Domain,
                TaskList               = this.TaskList,
                CronSchedule           = this.CronSchedule,
                ScheduleToStartTimeout = this.ScheduleToStartTimeout,
                StartToCloseTimeout    = this.StartToCloseTimeout,
                Memo                   = this.Memo,
                RetryOptions           = this.RetryOptions,
                DecisionTaskTimeout    = this.DecisionTaskTimeout,
                WorkflowId             = this.WorkflowId,
                WorkflowIdReusePolicy  = this.WorkflowIdReusePolicy
            };
        }
    }
}
