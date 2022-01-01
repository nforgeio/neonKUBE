//-----------------------------------------------------------------------------
// FILE:	    WorkflowMethodAttribute.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Threading;

using Neon.Common;
using Neon.Temporal;
using Neon.Temporal.Internal;

namespace Neon.Temporal
{
    /// <summary>
    /// Used to identify a workflow interface method as a workflow entry point.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class WorkflowMethodAttribute : Attribute
    {
        private string      name;
        private int  	    workflowExecutionTimeoutSeconds;
        private int         workflowTaskTimeoutSeconds;
        private int         workflowRunTimeoutSeconds;
        private string      taskQueue;
        private string      @namespace;
        private string      workflowId;

        /// <summary>
        /// Constructor.
        /// </summary>
        public WorkflowMethodAttribute()
        {
        }

        /// <summary>
        /// Specifies the name to be used to identify a specific workflow method.  This is optional
        /// for workflow interfaces that have only one workflow entry point method but is required
        /// for interfaces with multiple entry points.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When specified, this name will be combined with the workflow type name when registering
        /// and executing a workflow started via the method.  This will typically look like:
        /// </para>
        /// <code>
        /// WORKFLOW_TYPENAME::METHODNAME
        /// </code>
        /// <para>
        /// where <b>WORKFLOW_TYPENAME</b> defaults to the the workflow interface's fully qualified 
        /// name, with any leading "I" character removed and <b>METHOD_NAME</b> is from
        /// <see cref="WorkflowMethodAttribute.Name"/>.  This is the same convention implemented by 
        /// the Java client.
        /// </para>
        /// <note>
        /// Settings <see cref="IsFullName"/><c>true</c> specifies that <see cref="WorkflowMethodAttribute.Name"/>
        /// by itself specifies the workflow type name so you can easily interoperate with other
        /// clients and type naming conventions.
        /// </note>
        /// <para>
        /// Sometimes it's useful to be able to specify a workflow type name that doesn't
        /// follow the convention above, for example to interoperate with workflows written
        /// in another language..  You can do this by setting <see cref="Name"/> to the
        /// required workflow type name and then setting <see cref="IsFullName"/><c>=true</c>.
        /// </para>
        /// </remarks>
        public string Name
        {
            get => name;

            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    name = null;
                }
                else
                {
                    name = value;
                }
            }
        }

        /// <summary>
        /// <para>
        /// Optionally indicates that <see cref="Name"/> holds the fully qualified type name for
        /// the workflow and that the .NET client will not add a prefix to <see cref="Name"/>
        /// when registering the workflow.
        /// </para>
        /// <para>
        /// This is useful when interoperating with workflows written in another language by
        /// providing a way to specify a specific workflow type name. 
        /// </para>
        /// <note>
        /// <see cref="Name"/> cannot be <c>null</c> or empty when this is <c>true</c>.
        /// </note>
        /// </summary>
        public bool IsFullName { get; set; } = false;

        /// <summary>
        /// Optionally specifies the maximum workflow execution time.
        /// </summary>
        public int WorkflowExecutionTimeoutSeconds
        {
            get => workflowExecutionTimeoutSeconds;
            set => workflowExecutionTimeoutSeconds = Math.Max(value, 0);
        }

        /// <summary>
        /// Optionally specifies the maximum execution time for an individual workflow decision
        /// task.  The maximum possible duration is <b>60 seconds</b>.
        /// </summary>
        public int WorkflowTaskTimeoutSeconds
        {
            get => workflowTaskTimeoutSeconds;

            set
            {
                Covenant.Requires<ArgumentException>(value <= 60, nameof(value), $"[WorkflowTaskTimeoutSeconds={value}] cannot exceed 60 seconds.");

                workflowTaskTimeoutSeconds = Math.Max(value, 0);
            }
        }

        /// <summary>
        /// Optionally specifies the maximum time a workflow can wait
        /// between being scheduled and being actually executed on a
        /// worker.
        /// </summary>
        public int WorkflowRunTimeoutSeconds
        {
            get => workflowRunTimeoutSeconds;
            set => workflowRunTimeoutSeconds = Math.Max(value, 0);
        }

        /// <summary>
        /// Optionally specifies the target Temporal task queue.
        /// </summary>
        public string TaskQueue
        {
            get => taskQueue;

            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    taskQueue = null;
                }
                else
                {
                    taskQueue = value;
                }
            }
        }

        /// <summary>
        /// Optionally specifies the target Temporal namespace.
        /// </summary>
        public string Namespace
        {
            get => @namespace;

            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    @namespace = null;
                }
                else
                {
                    @namespace = value;
                }
            }
        }

        /// <summary>
        /// Optionally specifies the workflow ID.
        /// </summary>
        public string WorkflowId
        {
            get => workflowId;

            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    workflowId = null;
                }
                else
                {
                    workflowId = value;
                }
            }
        }

        /// <summary>
        /// Specifies the workflow ID reuse policy.
        /// </summary>
        public WorkflowIdReusePolicy WorkflowIdReusePolicy { get; set; } = WorkflowIdReusePolicy.UseDefault;

        /// <summary>
        /// Optionally specifies a recurring schedule for the workflow method.  This can be set to a string specifying
        /// the minute, hour, day of month, month, and day of week scheduling parameters using the standard Linux
        /// CRON format described here: <a href="https://en.wikipedia.org/wiki/Cron">https://en.wikipedia.org/wiki/Cron</a>
        /// </summary>
        /// <remarks>
        /// <para>
        /// Temporal accepts a CRON string formatted as a single line of text with 5 parameters separated by
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
        public string CronSchedule { get; set; } = null;
    }
}
