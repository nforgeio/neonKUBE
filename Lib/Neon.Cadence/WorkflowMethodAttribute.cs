//-----------------------------------------------------------------------------
// FILE:	    WorkflowMethodAttribute.cs
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
using System.Threading;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;

namespace Neon.Cadence
{
    /// <summary>
    /// Used to identify a workflow interface method as a workflow entry point.
    /// </summary>
    public class WorkflowMethodAttribute : Attribute
    {
        private string      name;
        private int  	    executionStartToCloseTimeoutSeconds;
        private string      taskList;
        private int         taskStartToCloseTimeoutSeconds;
        private string      workflowId;

        /// <summary>
        /// Constructor.
        /// </summary>
        public WorkflowMethodAttribute()
        {
        }

        /// <summary>
        /// <para>
        /// Optionally specifies the default workflow type.
        /// </para>
        /// <note>
        /// This can be overridden when the workflow is executed.
        /// </note>
        /// </summary>
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
        /// Optionally specifies the default maximum workflow execution time.
        /// </para>
        /// <note>
        /// This can be overridden when the workflow is executed.
        /// </note>
        /// </summary>
        public int ExecutionStartToCloseTimeoutSeconds
        {
            get => executionStartToCloseTimeoutSeconds;
            set => executionStartToCloseTimeoutSeconds = Math.Max(value, 0);
        }

        /// <summary>
        /// <para>
        /// Optionally specifies the default maximum execution time for
        /// an individual workflow task.  This defaults to 10 seconds and
        /// may be a maximum of 60 seconds.
        /// </para>
        /// <note>
        /// This can be overridden when the workflow is executed.
        /// </note>
        /// </summary>
        public int TaskStartToCloseTimeoutSeconds
        {
            get => taskStartToCloseTimeoutSeconds;

            set
            {
                Covenant.Requires<ArgumentException>(value <= 60, $"[TaskStartToCloseTimeoutSeconds={value}] exceeds 60 seconds, the maximum allowed.");

                taskStartToCloseTimeoutSeconds = Math.Max(value, 0);
            }
        }

        /// <summary>
        /// <para>
        /// Optionally specifies the default target task list.
        /// </para>
        /// <note>
        /// This can be overridden when the workflow is executed.
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
        /// Optionally specifies the default workflow ID.
        /// </para>
        /// <note>
        /// This can be overridden when the workflow is executed.
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
        /// Specifies the default workflow ID reuse policy.  This defaults
        /// to <see cref="WorkflowIdReusePolicy.AllowDuplicateFailedOnly"/>
        /// when not initialized.
        /// </para>
        /// <note>
        /// This can be overridden when the workflow is executed.
        /// </note>
        /// </summary>
        public WorkflowIdReusePolicy? WorkflowIdReusePolicy { get; set; }
    }
}
