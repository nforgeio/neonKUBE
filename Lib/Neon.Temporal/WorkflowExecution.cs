//-----------------------------------------------------------------------------
// FILE:	    WorkflowExecution.cs
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
using System.Diagnostics.Contracts;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Temporal;
using Neon.Temporal.Internal;

namespace Neon.Temporal
{
    /// <summary>
    /// Describes the state of an executed workflow.
    /// </summary>
    public class WorkflowExecution
    {
        /// <summary>
        /// Defaulty constructor.
        /// </summary>
        public WorkflowExecution()
        {
        }

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="workflowId">The original ID for the workflow.</param>
        /// <param name="runId">Optionally specifies the current run ID for the workflow.</param>
        public WorkflowExecution(string workflowId, string runId = null)
        {
            this.WorkflowId = workflowId;
            this.RunId      = runId;
        }

        /// <summary>
        /// Returns the current ID for workflow execution.  This will be different
        /// than <see cref="RunId"/> when the workflow has been continued as new
        /// or potentially restarted.
        /// </summary>
        [JsonProperty(PropertyName = "Id", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "id", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string WorkflowId { get; private set; }

        /// <summary>
        /// The original ID assigned to the workflow when it was started.
        /// </summary>
        [JsonProperty(PropertyName = "RunId", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "runId", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string RunId { get; private set; }
    }
}
