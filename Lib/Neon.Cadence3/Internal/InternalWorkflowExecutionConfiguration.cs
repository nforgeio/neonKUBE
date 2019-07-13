//-----------------------------------------------------------------------------
// FILE:	    InternalWorkflowExecutionConfiguration.cs
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

using Newtonsoft.Json;

using Neon.Cadence;
using Neon.Common;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// <b>INTERNAL USE ONLY:</b> Describes a workflow configuration.  This maps directly
    /// to the Cadence GOLANG  <b>WorkflowExecutionConfiguration </b> structure.
    /// </summary>
    internal class InternalWorkflowExecutionConfiguration
    {
        /// <summary>
        /// Identifies the task list where the workflow was scheduled.
        /// </summary>
        [JsonProperty(PropertyName = "TaskList", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public InternalTaskList TaskList { get; set; }

        /// <summary>
        /// Maximum time the entire workflow may take to complete end-to-end (nanoseconds).
        /// </summary>
        [JsonProperty(PropertyName = "ExecutionStartToCloseTimeoutSeconds", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public long ExecutionStartToCloseTimeout { get; set; }

        /// <summary>
        /// Maximum time a workflow task/decision may take to complete (nanoseconds).
        /// </summary>
        [JsonProperty(PropertyName = "TaskStartToCloseTimeoutSeconds", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public long TaskStartToCloseTimeoutSeconds { get; set; }

        /// <summary>
        /// The termination policy to apply to the child workflow when
        /// the parent workflow is terminated.
        /// </summary>
        [JsonProperty(PropertyName = "ChildPolicy", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(InternalChildPolicy.ABANDON)]
        public InternalChildPolicy ChildPolicy { get; set; } = InternalChildPolicy.ABANDON;

        /// <summary>
        /// Coverts the instance to a public <see cref="WorkflowConfig"/>.
        /// </summary>
        public WorkflowConfig ToPublic()
        {
            return new WorkflowConfig()
            {
                 TaskList                       = this.TaskList.Name,
                 TaskListKind                   = (TaskListKind)this.TaskList.TaskListKind,
                 ExecutionStartToCloseTimeout   = TimeSpan.FromTicks(this.ExecutionStartToCloseTimeout/100),
                 TaskStartToCloseTimeoutSeconds = TimeSpan.FromTicks(this.TaskStartToCloseTimeoutSeconds/100),
                 ChildPolicy                    = (ChildPolicy)this.ChildPolicy
            };
        }
    }
}
