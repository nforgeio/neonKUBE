//-----------------------------------------------------------------------------
// FILE:	    InternalDescribeWorkflowExecutionResponse.cs
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
    /// <b>INTERNAL USE ONLY:</b> Cadence workflow execution details response.
    /// </summary>
    internal class InternalDescribeWorkflowExecutionResponse
    {
        /// <summary>
        /// Execution configuration.
        /// </summary>
        [JsonProperty(PropertyName = "executionConfiguration", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public InternalWorkflowExecutionConfiguration ExecutionConfiguration { get; set; }

        /// <summary>
        /// Execution info.
        /// </summary>
        [JsonProperty(PropertyName = "workflowExecutionInfo", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public InternalWorkflowExecutionInfo WorkflowExecutionInfo { get; set; }

        /// <summary>
        /// Pending activities.
        /// </summary>
        [JsonProperty(PropertyName = "pendingActivities", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<InternalPendingActivityInfo> PendingActivities { get; set; }

        /// <summary>
        /// Pending child workflows.
        /// </summary>
        [JsonProperty(PropertyName = "pendingWorkflows", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<InternalPendingChildExecutionInfo> PendingChildren { get; set; }

        /// <summary>
        /// Converts the instance into a public <see cref="WorkflowDescription"/>.
        /// </summary>
        public WorkflowDescription ToPublic()
        {
            var details = new WorkflowDescription()
            {
                Configuration = this.ExecutionConfiguration?.ToPublic(),
                Execution     = this.WorkflowExecutionInfo?.ToPublic()
            };

            details.PendingActivities = new List<PendingActivityInfo>();

            if (this.PendingActivities != null)
            {
                foreach (var activity in this.PendingActivities)
                {
                    details.PendingActivities.Add(activity.ToPublic());
                }
            }

            details.PendingChildren = new List<PendingChildExecutionInfo>();

            if (this.PendingChildren != null)
            {
                foreach (var child in this.PendingChildren)
                {
                    details.PendingChildren.Add(child.ToPublic());
                }
            }

            return details;
        }
    }
}
