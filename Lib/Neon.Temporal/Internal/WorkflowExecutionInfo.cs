//-----------------------------------------------------------------------------
// FILE:	    WorkflowExecutionInfo.cs
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
using System.Diagnostics;

using Newtonsoft.Json;

using Neon.Data;

namespace Neon.Temporal.Internal
{
    /// <summary>
    /// Describes the current state of a workflow.
    /// </summary>
    public class WorkflowExecutionInfo
    {
        /// <summary>
        /// Describes the workflow execution.
        /// </summary>
        [JsonIgnore]
        public WorkflowExecution Execution
        { 
            get
            {
                return InternalWorkflowExecution.ToWorkflowExecution();
            }
            set
            {
                InternalWorkflowExecution = new Internal.InternalWorkflowExecution(value);
            }
        }

        [JsonProperty(PropertyName = "execution")]
        private Internal.InternalWorkflowExecution InternalWorkflowExecution;

        /// <summary>
        /// Identifies the workflow implementation.
        /// </summary>
        public WorkflowType Type { get; set; }

        /// <summary>
        /// Workflow start time or <c>null</c> if the workflow hasn't started yet.
        /// </summary>
        [JsonProperty(PropertyName = "start_time")]
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// Workflow close time or <c>null</c> if the workflow hasn't completed yet.
        /// </summary>
        [JsonProperty(PropertyName = "close_time")]
        public DateTime? CloseTime { get; set; }

        /// <summary>
        /// Returns <c>true</c> if the workflow has been started and is still running
        /// or has already completed.
        /// </summary>
        public bool HasStarted => StartTime != null;

        /// <summary>
        /// Workflow execution status describing the state of the workflow.
        /// </summary>
        [JsonConverter(typeof(IntegerEnumConverter<WorkflowExecutionStatus>))]
        public WorkflowExecutionStatus Status { get; set; }

        /// <summary>
        /// Returns <c>true</c> if the workflow has been completed.
        /// </summary>
        public bool IsClosed => CloseTime != null;

        /// <summary>
        /// Returns <c>true</c> if the workflow is currently running.
        /// </summary>
        public bool IsRunning => HasStarted && !IsClosed;

        /// <summary>
        /// Workflow history length.
        /// </summary>
        [JsonProperty(PropertyName = "history_length")]
        public long HistoryLength { get; set; }

        /// <summary>
        /// Identifies the namespece where the parent workflow is running
        /// (or <c>null</c>).
        /// </summary>
        [JsonProperty(PropertyName = "parent_namespace_id")]
        public string ParentNamespaceId { get; set; }

        /// <summary>
        /// Identfies the parent workflow (or <c>null</c>).
        /// </summary>
        [JsonIgnore]
        public WorkflowExecution ParentExecution
        {
            get
            {
                return InternalParentWorkflowExecution.ToWorkflowExecution();
            }
            set
            {
                InternalParentWorkflowExecution = new Internal.InternalWorkflowExecution(value);
            }
        }

        [JsonProperty(PropertyName = "parent_execution")]
        private Internal.InternalWorkflowExecution InternalParentWorkflowExecution;

        /// <summary>
        /// The workflow execution time.
        /// </summary>
        [JsonProperty(PropertyName = "execution_time")]
        public DateTime? ExecutionTime { get; set; }

        /// <summary>
        /// Optional workflow metadata.
        /// </summary>
        public Memo Memo { get; set; }

        /// <summary>
        /// Workflow execution search attributes.
        /// </summary>
        [JsonProperty(PropertyName = "search_attributes")]
        public SearchAttributes SearchAttributes { get; set; }

        /// <summary>
        /// The auto reset points of the workflow execution.
        /// </summary>
        [JsonProperty(PropertyName = "auto_reset_points")]
        public ResetPoints AutoResetPoints { get; set; }

        /// <summary>
        /// The Task Queue the worker is running on.
        /// </summary>
        [JsonProperty(PropertyName = "task_queue")]
        public string TaskQueue { get; set; }
    }
}
