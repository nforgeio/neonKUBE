//-----------------------------------------------------------------------------
// FILE:	    WorkflowConfiguration.cs
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

using Newtonsoft.Json;

namespace Neon.Temporal
{
    /// <summary>
    /// Describes a workflow's configuration.
    /// </summary>
    public class WorkflowExecutionConfig
    {
        /// <summary>
        /// Identifies the task queue where the workflow was scheduled.
        /// </summary>
        [JsonProperty(PropertyName = "task_queue")]
        public TaskQueue TaskQueue { get; set; }

        /// <summary>
        /// Maximum time the entire workflow may take to complete end-to-end.
        /// </summary>
        [JsonProperty(PropertyName = "workflow_execution_timeout")]
        [JsonConverter(typeof(GoTimeSpanJsonConverter))]
        public TimeSpan WorkflowExecutionTimeout { get; set; }

        /// <summary>
        /// Maximum time a single workflow run may take to complete.
        /// </summary>
        [JsonProperty(PropertyName = "workflow_run_timeout")]
        [JsonConverter(typeof(GoTimeSpanJsonConverter))]
        public TimeSpan WorkflowRunTimeout { get; set; }

        /// <summary>
        /// Maximum time a workflow task/decision may take to complete.
        /// </summary>
        [JsonProperty(PropertyName = "default_workflow_task_timeout")]
        [JsonConverter(typeof(GoTimeSpanJsonConverter))]
        public TimeSpan DefaultWorkflowTaskTimeout { get; set; }
    }
}
