//-----------------------------------------------------------------------------
// FILE:	    ChildExecution.cs
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

using Neon.Cadence;
using Neon.Common;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// <b>INTERNAL USE ONLY:</b> Holds information about a child workflow execution.
    /// </summary>
    public class ChildExecution
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="execution">The child workflow's execution information.</param>
        /// <param name="childId">
        /// The child workflow's local ID.  This is used to identify the 
        /// child when communicating with <b>cadence-proxy</b>.
        /// </param>
        internal ChildExecution(WorkflowExecution execution, long childId)
        {
            this.Execution = execution;
            this.ChildId   = childId;
        }

        /// <summary>
        /// The child workflow's execution information.
        /// </summary>
        public WorkflowExecution Execution { get; private set; }

        /// <summary>
        /// The child workflow's local ID.  This is used to identify the 
        /// child when communicating with <b>cadence-proxy</b>.
        /// </summary>
        public long ChildId { get; private set; }
    }
}
