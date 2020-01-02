//-----------------------------------------------------------------------------
// FILE:	    WorkflowMethodAttribute.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
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

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;

namespace Neon.Cadence
{
    /// <summary>
    /// Used to identify a workflow interface method as a workflow entry point.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class WorkflowMethodAttribute : Attribute
    {
        private string      name;
        private int  	    executionStartToCloseTimeoutSeconds;
        private int         taskStartToCloseTimeoutSeconds;
        private int         scheduleToStartTimeoutSeconds;
        private string      taskList;
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
        /// and executing a workflow started via the method.  This will look like:
        /// </para>
        /// <code>
        /// WORKFLOW_TYPENNAME::METHODNAME
        /// </code>
        /// <para>
        /// where <b>WORKFLOW_TYPENAME</b> is either the workflow interface's fully qualified 
        /// name or the name specified by <see cref="WorkflowAttribute.Name"/> and 
        /// <b>METHOD_NAME</b> is from <see cref="WorkflowMethodAttribute.Name"/>.  This
        /// is the same convention implemented by the Java client.
        /// </para>
        /// <note>
        /// Some implications of this scheme are that we'll need to register multiple workflow
        /// types for each workflow interface when there are multiple entry points (one per
        /// method) and that external workflow invocations will need to explicitly specify
        /// workflow types that include the method name when one is specified to the target
        /// method.
        /// </note>
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
                    CadenceHelper.ValidateWorkflowTypeName(value);

                    name = value;
                }
            }
        }

        /// <summary>
        /// <para>
        /// Optionally specifies the maximum workflow execution time.
        /// </para>
        /// <note>
        /// This can be overridden when the workflow is executed using
        /// <see cref="WorkflowOptions"/>,
        /// </note>
        /// </summary>
        public int ExecutionStartToCloseTimeoutSeconds
        {
            get => executionStartToCloseTimeoutSeconds;
            set => executionStartToCloseTimeoutSeconds = Math.Max(value, 0);
        }

        /// <summary>
        /// <para>
        /// Optionally specifies the maximum execution time for
        /// an individual workflow task.  The maximum possible duration
        /// is <b>60 seconds</b>.
        /// </para>
        /// <note>
        /// This can be overridden when the workflow is executed using
        /// <see cref="WorkflowOptions"/>,
        /// </note>
        /// </summary>
        public int TaskStartToCloseTimeoutSeconds
        {
            get => taskStartToCloseTimeoutSeconds;

            set
            {
                Covenant.Requires<ArgumentException>(value <= 60, nameof(value), $"[TaskStartToCloseTimeoutSeconds={value}] cannot exceed 60 seconds.");

                taskStartToCloseTimeoutSeconds = Math.Max(value, 0);
            }
        }

        /// <summary>
        /// <para>
        /// Optionally specifies the maximum time a workflow can wait
        /// between being scheduled and being actually scheduled on a
        /// worker.
        /// </para>
        /// <note>
        /// This can be overridden when the workflow is executed using
        /// <see cref="WorkflowOptions"/>,
        /// </note>
        /// </summary>
        public int ScheduleToStartTimeoutSeconds
        {
            get => scheduleToStartTimeoutSeconds;
            set => scheduleToStartTimeoutSeconds = Math.Max(value, 0);
        }

        /// <summary>
        /// <para>
        /// Optionally specifies the target task list.
        /// </para>
        /// <note>
        /// This can be overridden when the workflow is executed using
        /// <see cref="WorkflowOptions"/>,
        /// </note>
        /// </summary>
        public string TaskList
        {
            get => taskList;

            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    taskList = null;
                }
                else
                {
                    taskList = value;
                }
            }
        }

        /// <summary>
        /// <para>
        /// Optionally specifies the workflow ID.
        /// </para>
        /// <note>
        /// This can be overridden when the workflow is executed using
        /// <see cref="WorkflowOptions"/>.
        /// </note>
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
        /// <para>
        /// Specifies the workflow ID reuse policy.
        /// </para>
        /// <note>
        /// This can be overridden when the workflow is executed using
        /// <see cref="WorkflowOptions"/>,
        /// </note>
        /// </summary>
        public WorkflowIdReusePolicy WorkflowIdReusePolicy { get; set; } = WorkflowIdReusePolicy.UseDefault;
    }
}
