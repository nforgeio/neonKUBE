//-----------------------------------------------------------------------------
// FILE:	    WorkflowMethodAttribute.cs
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
        private int  	    startToCloseTimeoutSeconds;
        private int         decisionTaskTimeoutSeconds;
        private int         scheduleToStartTimeoutSeconds;
        private string      taskList;
        private string      domain;
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
        /// where <b>WORKFLOW_TYPENAME</b> is either the workflow interface's fully qualified 
        /// name or the name specified by <see cref="WorkflowAttribute.Name"/> and 
        /// <b>METHOD_NAME</b> is from <see cref="WorkflowMethodAttribute.Name"/>.  This
        /// is the same convention implemented by the Java client.
        /// </para>
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
                    CadenceHelper.ValidateWorkflowTypeName(value);

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
        /// <para>
        /// Optionally specifies the maximum workflow execution time.
        /// </para>
        /// </summary>
        public int StartToCloseTimeoutSeconds
        {
            get => startToCloseTimeoutSeconds;
            set => startToCloseTimeoutSeconds = Math.Max(value, 0);
        }

        /// <summary>
        /// <para>
        /// Optionally specifies the maximum execution time for an individual workflow decision
        /// task.  The maximum possible duration is <b>60 seconds</b>.
        /// </para>
        /// </summary>
        public int DecisionTaskTimeoutSeconds
        {
            get => decisionTaskTimeoutSeconds;

            set
            {
                Covenant.Requires<ArgumentException>(value <= 60, nameof(value), $"[{nameof(DecisionTaskTimeoutSeconds)}={value}] cannot exceed 60 seconds.");

                decisionTaskTimeoutSeconds = Math.Max(value, 0);
            }
        }

        /// <summary>
        /// <para>
        /// Optionally specifies the maximum time a workflow can wait
        /// between being scheduled and being actually executed on a
        /// worker.
        /// </para>
        /// </summary>
        public int ScheduleToStartTimeoutSeconds
        {
            get => scheduleToStartTimeoutSeconds;
            set => scheduleToStartTimeoutSeconds = Math.Max(value, 0);
        }

        /// <summary>
        /// <para>
        /// Optionally specifies the target Cadence task list.
        /// </para>
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
        /// Optionally specifies the target Cadence domain.
        /// </para>
        /// </summary>
        public string Domain
        {
            get => domain;

            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    domain = null;
                }
                else
                {
                    domain = value;
                }
            }
        }

        /// <summary>
        /// <para>
        /// Optionally specifies the workflow ID.
        /// </para>
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
        /// </summary>
        public WorkflowIdReusePolicy WorkflowIdReusePolicy { get; set; } = WorkflowIdReusePolicy.UseDefault;
    }
}
