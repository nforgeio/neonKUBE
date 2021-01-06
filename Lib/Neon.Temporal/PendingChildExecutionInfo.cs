//-----------------------------------------------------------------------------
// FILE:	    PendingChildExecutionInfo.cs
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

using Neon.Temporal;
using Neon.Common;

namespace Neon.Temporal
{
    /// <summary>
    /// Decribes the current state of a pending; child workflow.
    /// </summary>
    public class PendingChildExecutionInfo
    {
        /// <summary>
        /// Internal constructor.
        /// </summary>
        internal PendingChildExecutionInfo()
        {
        }

        /// <summary>
        /// Returns the workflow ID.
        /// </summary>
        [JsonProperty(PropertyName = "workflow_id")]
        public string WorkflowId { get; set; }

        /// <summary>
        /// Returns the workflow run ID.
        /// </summary>
        [JsonProperty(PropertyName = "run_id")]
        public string RunId { get; set; }

        /// <summary>
        /// Returns the workflow type name.
        /// </summary>
        [JsonProperty(PropertyName = "workflow_type_name")]
        public string WorkflowTypeName { get; set; }

        /// <summary>
        /// $todo(jefflill): What is this?
        /// </summary>
        [JsonProperty(PropertyName = "initiated_id")]
        public long InitiatedId { get; set; }

        /// <summary>
        /// Returns policy used to close this child when its parent is closed.
        /// </summary>
        [JsonProperty(PropertyName = "parent_close_policy")]
        public ParentClosePolicy ParentClosePolicy { get; set; }
    }
}
