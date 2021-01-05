//-----------------------------------------------------------------------------
// FILE:	    InternalPendingChildExecutionInfo.cs
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

using Neon.Cadence;
using Neon.Common;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// <b>INTERNAL USE ONLY:</b> Describes a pending child workflow execution.
    /// </summary>
    internal class InternalPendingChildExecutionInfo
    {
        /// <summary>
        /// The workflow ID.
        /// </summary>
        [JsonProperty(PropertyName = "workflowID", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string WorkflowId { get; set; }

        /// <summary>
        /// The workflow run ID.
        /// </summary>
        [JsonProperty(PropertyName = "runID", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string RunId { get; set; }

        /// <summary>
        /// The workflow type name.
        /// </summary>
        [JsonProperty(PropertyName = "workflowTypName", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]     // $note(jefflill): This property is misspelled in the Thrift definition
        [DefaultValue(InternalPendingActivityState.SCHEDULED)]                                                              //                  We're retaining the misspelling here to match.
        public string WorkflowTypeName { get; set; }

        /// <summary>
        /// $todo(jefflill): Don't know what this is.
        /// </summary>
        [JsonProperty(PropertyName = "initiatedID", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public long InitiatedId { get; set; }

        /// <summary>
        /// Converts the instance into the corresponding public <see cref="PendingChildExecutionInfo"/>.
        /// </summary>
        public PendingChildExecutionInfo ToPublic()
        {
            return new PendingChildExecutionInfo()
            {
                WorkflowId        = this.WorkflowId,
                RunId             = this.RunId,
                WorkflowTypeName  = this.WorkflowTypeName,
                InitatedId        = this.InitiatedId
            };
        }
    }
}
