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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;

using Newtonsoft.Json;
using YamlDotNet.Serialization;

using Neon.Cadence;
using Neon.Common;
using Neon.Retry;
using Neon.Time;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// Describes a workflow execution.  This maps directly to the Cadence GOLANG <b>WorkflowExecutionInfo</b> structure. 
    /// </summary>
    public class InternalWorkflowExecutionInfo
    {
        /// <summary>
        /// Describes the original workflow ID as well as the currrent run ID.
        /// </summary>
        [JsonProperty(PropertyName = "Execution", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public InternalWorkflowExecution Execution { get; set; }

        /// <summary>
        /// Identifies the workflow implementation.
        /// </summary>
        [JsonProperty(PropertyName = "WorkflowType", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
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
        [JsonProperty(PropertyName = "WorkflowCloseStatus", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public int WorkflowCloseStatus { get; set;}

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
        public InternalWorkflowExecution ParentExecution { get; set; }

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
        /// Not sure what these are.
        /// </summary>
        [JsonProperty(PropertyName = "AutoResetPoints", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public InternalResetPoints AutoResetPoints { get; set; }

        /// <summary>
        /// Converts the instance into a public <see cref="WorkflowInfo"/>.
        /// </summary>
        public WorkflowInfo ToPublic()
        {
            var info = new WorkflowInfo()
            {
                Execution           = this.Execution.ToPublic(),
                Name                = this.WorkflowType.Name,
                WorkflowCloseStatus = (WorkflowCloseStatus)this.WorkflowCloseStatus,
                HistoryLength       = this.HistoryLength,
                ParentDomain        = this.ParentDomainId,
                ExecutionTime       = TimeSpan.FromTicks(this.ExecutionTime / 100)
            };

            if (this.StartTime > 0)
            {
                info.StartTime = new DateTime(this.StartTime);
            }

            if (this.CloseTime > 0)
            {
                info.CloseTime = new DateTime(this.CloseTime);
            }

            info.AutoResetPoints = new List<ResetPoint>();

            foreach (var point in this.AutoResetPoints.Points)
            {
                info.AutoResetPoints.Add(point.ToPublic());
            }

            return info;
        }
    }
}
