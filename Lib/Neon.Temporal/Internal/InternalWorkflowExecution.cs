//-----------------------------------------------------------------------------
// FILE:	    WorkflowExecution.cs
// CONTRIBUTOR: John C. Burns
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
using System.Text;

using Newtonsoft.Json;
using YamlDotNet.Serialization;

namespace Neon.Temporal.Internal
{
    /// <summary>
    /// Describes the state of an executed workflow.  Used
    /// for serialization in a set of message types.
    /// </summary>
    public class InternalWorkflowExecution
    {
        /// <summary>
        /// Defaulty constructor.
        /// </summary>
        public InternalWorkflowExecution()
        {
        }

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="execution">The workflow's execution information.</param>
        internal InternalWorkflowExecution(WorkflowExecution execution)
        {
            this.WorkflowId = execution.WorkflowId;
            this.RunId      = execution.RunId;
        }

        /// <summary>
        /// Returns the current ID for workflow execution.  This will be different
        /// than <see cref="RunId"/> when the workflow has been continued as new
        /// or potentially restarted.
        /// </summary>
        [JsonProperty(PropertyName = "workflow_id", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "workflow_id", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string WorkflowId { get; private set; }

        /// <summary>
        /// The original ID assigned to the workflow when it was started.
        /// </summary>
        [JsonProperty(PropertyName = "run_id", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "runi_d", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string RunId { get; private set; }

        public WorkflowExecution ToWorkflowExecution()
        {
            return new WorkflowExecution(WorkflowId, RunId);
        }
    }
}
