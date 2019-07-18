//-----------------------------------------------------------------------------
// FILE:	    InternalWorkflowExecutionInfo.cs
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
    /// <b>INTERNAL USE ONLY:</b> Describes a workflow execution.  This maps directly to the Cadence GOLANG <b>WorkflowExecutionInfo</b> structure. 
    /// </summary>
    internal class InternalWorkflowExecutionInfo
    {
        /// <summary>
        /// Describes the workflow run.
        /// </summary>
        [JsonProperty(PropertyName = "Execution", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public InternalWorkflowExecution2 Execution { get; set; }

        /// <summary>
        /// Identifies the workflow implementation.
        /// </summary>
        [JsonProperty(PropertyName = "Type", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public InternalWorkflowType WorkflowType { get; set; }

        /// <summary>
        /// Workflow start time.
        /// </summary>
        [JsonProperty(PropertyName = "StartTime", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public long StartTime { get; set; }

        /// <summary>
        /// Workflow close time.
        /// </summary>
        [JsonProperty(PropertyName = "CloseTime", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public long CloseTime { get; set; }

        /// <summary>
        /// Workflow close status.
        /// </summary>
        [JsonProperty(PropertyName = "CloseStatus", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(InternalWorkflowCloseStatus.COMPLETED)]
        public InternalWorkflowCloseStatus WorkflowCloseStatus { get; set;}

        /// <summary>
        /// Workflow history length.
        /// </summary>
        [JsonProperty(PropertyName = "HistoryLength", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public long HistoryLength { get; set; }

        /// <summary>
        /// Identifies the domain where the parent workflow is running.
        /// </summary>
        [JsonProperty(PropertyName = "ParentDomainId", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string ParentDomainId { get; set; }

        /// <summary>
        /// Identfies the parent workflow.
        /// </summary>
        [JsonProperty(PropertyName = "ParentExecution", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public InternalWorkflowExecution2 ParentExecution { get; set; }

        /// <summary>
        /// The workflow execution time.
        /// </summary>
        [JsonProperty(PropertyName = "ExecutionTime", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public long ExecutionTime { get; set; }

        /// <summary>
        /// Optional workflow metadata.
        /// </summary>
        [JsonProperty(PropertyName = "Memo", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public InternalMemo Memo { get; set; }

        /// <summary>
        /// Converts the instance into a public <see cref="WorkflowState"/>.
        /// </summary>
        public WorkflowState ToPublic()
        {
            var executionState = new WorkflowState()
            {
                Run                 = this.Execution.ToPublic(),
                TypeName                = this.WorkflowType.Name,
                WorkflowCloseStatus = (WorkflowExecutionCloseStatus)this.WorkflowCloseStatus,
                HistoryLength       = this.HistoryLength,
                ParentDomain        = this.ParentDomainId,
                ExecutionTime       = TimeSpan.FromTicks(this.ExecutionTime / 100),
                Memo                = this.Memo?.Fields
        };

            if (this.StartTime > 0)
            {
                executionState.StartTime = new DateTime(this.StartTime);
            }

            if (this.CloseTime > 0)
            {
                executionState.CloseTime = new DateTime(this.CloseTime);
            }

            return executionState;
        }
    }
}
