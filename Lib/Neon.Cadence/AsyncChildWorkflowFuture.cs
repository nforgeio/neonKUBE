//-----------------------------------------------------------------------------
// FILE:	    AsyncChildWorkflowFuture.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Tasks;

namespace Neon.Cadence
{
    /// <summary>
    /// Implements a child workflow future that returns <c>void</c>.
    /// </summary>
    public class AsyncChildWorkflowFuture : IAsyncFuture
    {
        private bool            completed = false;
        private Workflow        parentWorkflow;
        private ChildExecution  execution;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="parentWorkflow">Identifies the parent workflow context.</param>
        /// <param name="execution">The child workflow execution.</param>
        internal AsyncChildWorkflowFuture(Workflow parentWorkflow, ChildExecution execution)
        {
            this.parentWorkflow = parentWorkflow;
            this.execution      = execution;
        }

        /// <summary>
        /// Returns the workflow execution.
        /// </summary>
        public WorkflowExecution Execution => execution.Execution;

        /// <inheritdoc/>
        public async Task GetAsync()
        {
            if (completed)
            {
                throw new InvalidOperationException($"[{nameof(IAsyncFuture<object>)}.GetAsync()] may only be called once per stub instance.");
            }

            completed = true;

            await parentWorkflow.Client.GetChildWorkflowResultAsync(parentWorkflow, execution);
        }
    }

    /// <summary>
    /// Implements a child workflow future that returns a value.
    /// </summary>
    /// <typeparam name="TResult">The workflow result type.</typeparam>
    public class AsyncChildWorkflowFuture<TResult> : IAsyncFuture<TResult>
    {
        private bool            completed = false;
        private Workflow        parentWorkflow;
        private ChildExecution  execution;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="parentWorkflow">Identifies the parent workflow context.</param>
        /// <param name="execution">The child workflow execution.</param>
        /// <param name="resultType">Specifies the workflow result type or <c>null</c> for <c>void</c> workflow methods.</param>
        internal AsyncChildWorkflowFuture(Workflow parentWorkflow, ChildExecution execution, Type resultType)
        {
            this.parentWorkflow = parentWorkflow;
            this.execution      = execution;
        }

        /// <summary>
        /// Returns the workflow execution.
        /// </summary>
        public WorkflowExecution Execution => execution.Execution;

        /// <inheritdoc/>
        public async Task<TResult> GetAsync()
        {
            if (completed)
            {
                throw new InvalidOperationException($"[{nameof(IAsyncFuture<object>)}.GetAsync()] may only be called once per stub instance.");
            }

            completed = true;

            var resultBytes = await parentWorkflow.Client.GetChildWorkflowResultAsync(parentWorkflow, execution);

            return parentWorkflow.Client.DataConverter.FromData<TResult>(resultBytes);
        }
    }
}
