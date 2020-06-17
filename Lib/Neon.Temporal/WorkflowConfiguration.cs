//-----------------------------------------------------------------------------
// FILE:	    WorkflowConfiguration.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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

using Neon.Common;
using Neon.Temporal;
using Neon.Temporal.Internal;

namespace Neon.Temporal
{
    /// <summary>
    /// Describes a workflow's configuration.
    /// </summary>
    public class WorkflowConfiguration
    {
        /// <summary>
        /// Identifies the task list where the workflow was scheduled.
        /// </summary>
        public string TaskList { get; set; }

        /// <summary>
        /// Identifies the type of a task list.
        /// </summary>
        public TaskListType TaskListKind { get; set; }

        /// <summary>
        /// Maximum time the entire workflow may take to complete end-to-end.
        /// </summary>
        public TimeSpan StartToCloseTimeout { get; set; }

        /// <summary>
        /// Maximum time a workflow task/decision may take to complete.
        /// </summary>
        public TimeSpan TaskStartToCloseTimeout { get; set; }

        /// <summary>
        /// The termination policy to apply to the child workflow when
        /// the parent workflow is terminated.
        /// </summary>
        public ParentClosePolicy ParentClosePolicy { get; set; }
    }
}
