//-----------------------------------------------------------------------------
// FILE:	    WorkflowExecutionStatus.cs
// CONTRIBUTOR: John C Burns
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
using System.Runtime.Serialization;
using System.Text;

namespace Neon.Temporal.Internal
{
    /// <summary>
    /// Used to identify the status of a Temporal workflow.
    /// </summary>
    public enum WorkflowExecutionStatus
    {
        /// <summary>
        /// Workflow has an unspecified status.
        /// </summary>
        [EnumMember(Value = "UNSPECIFIED")]
        Unspecified = 0,

        /// <summary>
        /// Workflow is running.
        /// </summary>
        [EnumMember(Value = "RUNNING")]
        Running = 1,

        /// <summary>
        /// Workflow is completed.
        /// </summary>
        [EnumMember(Value = "COMPLETED")]
        Completed = 2,

        /// <summary>
        /// Workflow has failed.
        /// </summary>
        [EnumMember(Value = "FAILED")]
        Failed = 3,

        /// <summary>
        /// Workflow canceled.
        /// </summary>
        [EnumMember(Value = "CANCELED")]
        CANCELED = 4,

        /// <summary>
        /// Workflow has been terminated.
        /// </summary>
        [EnumMember(Value = "TERMINATED")]
        Terminated = 5,

        /// <summary>
        /// Workflow has continued as new.
        /// </summary>
        [EnumMember(Value = "CONTINUEDASNEW")]
        ContinuedAsNew = 6,

        /// <summary>
        /// Workflow timed out.
        /// </summary>
        [EnumMember(Value = "TIMEDOUT")]
        TimedOut = 7,
    }
}
