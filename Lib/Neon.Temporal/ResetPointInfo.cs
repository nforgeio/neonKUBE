//-----------------------------------------------------------------------------
// FILE:	    ResetPointinfo.cs
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
using System.Text;

using Newtonsoft.Json;

namespace Neon.Temporal
{
    /// <summary>
    /// Defines workflow execution reset points.
    /// </summary>
    public class ResetPointInfo
    {
        /// <summary>
        /// The binary checksum of the reset point.
        /// </summary>
        [JsonProperty(PropertyName = "binary_checksum")]
        public string BinaryChecksum { get; set; }

        /// <summary>
        /// The run id of the workflow exeuction.
        /// </summary>
        [JsonProperty(PropertyName = "run_id")]
        public string RunId { get; set; }

        /// <summary>
        /// The id of the first completed workflow task.
        /// </summary>
        [JsonProperty(PropertyName = "first_workflow_task_completed_id")]
        public long FirstWorkflowTaskCompletedId { get; set; }

        /// <summary>
        /// The create time of the workflow execution.
        /// </summary>
        [JsonProperty(PropertyName = "create_time")]
        public DateTime? CreateTime { get; set; }

        /// <summary>
        /// The expire time of the workflow execution.
        /// </summary>
        [JsonProperty(PropertyName = "expire_time")]
        public DateTime? ExpireTime { get; set; }

        /// <summary>
        /// Indicates if the workflow exeuction is resettable.
        /// </summary>
        [JsonProperty(PropertyName = "resettable")]
        public bool Resettable { get; set; }
    }
}
