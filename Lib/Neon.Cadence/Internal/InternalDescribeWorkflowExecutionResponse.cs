//-----------------------------------------------------------------------------
// FILE:	    InternalWorkflowExecution.cs
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
    /// <b>INTERNAL USE ONLY:</b> Cadence workflow execution details response.
    /// </summary>
    internal class InternalDescribeWorkflowExecutionResponse
    {
        /// <summary>
        /// Execution configuration.
        /// </summary>
        [JsonProperty(PropertyName = "ExecutionConfiguration", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public InternalWorkflowExecutionConfiguration ExecutionConfiguration { get; set; }

        /// <summary>
        /// Execution info.
        /// </summary>
        [JsonProperty(PropertyName = "WorkflowExecutionInfo", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public InternalWorkflowExecutionInfo WorkflowExecutionInfo { get; set; }

        /// <summary>
        /// Pending activities.
        /// </summary>
        [JsonProperty(PropertyName = "PendingActivities", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<InternalPendingActivityInfo> PendingActivities { get; set; }

        /// <summary>
        /// Converts the instance into a public <see cref="WorkflowExecutionDetails"/>.
        /// </summary>
        public WorkflowExecutionDetails ToPublic()
        {
            var details = new WorkflowExecutionDetails()
            {
                Configuration = this.ExecutionConfiguration?.ToPublic(),
                Execution     = this.WorkflowExecutionInfo?.ToPublic()
            };

            details.Activities = new List<ActivityInfo>();

            foreach (var activity in this.PendingActivities)
            {
                details.Activities.Add(activity.ToPublic());
            }

            return details;
        }
    }
}
