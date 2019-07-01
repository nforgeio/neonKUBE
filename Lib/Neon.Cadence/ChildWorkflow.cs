//-----------------------------------------------------------------------------
// FILE:	    ChildWorkflow.cs
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
using System.Threading;

using Neon.Cadence;
using Neon.Common;

namespace Neon.Cadence
{
    /// <summary>
    /// Returned by <see cref="WorkflowBase.StartChildWorkflowAsync(string, byte[], Internal.ChildWorkflowOptions, CancellationToken)"/>
    /// to identify the new child workflow.  This valie can then be used to perform
    /// operations on the workflow like: <see cref="WorkflowBase.SignalChildWorkflowAsync(ChildWorkflow, string, byte[])"/>,
    /// <see cref="WorkflowBase.CancelChildWorkflowAsync(ChildWorkflow)"/> and <see cref="WorkflowBase.WaitForChildWorkflowAsync(ChildWorkflow, CancellationToken)"/>.
    /// </summary>
    public struct ChildWorkflow
    {
        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="childId">The child workflow's local ID.</param>
        internal ChildWorkflow(long childId)
        {
            this.Id = childId;
        }

        /// <summary>
        /// Returns the child workflow's local ID.
        /// </summary>
        internal long Id { get; private set; }
    }
}
