//-----------------------------------------------------------------------------
// FILE:	    WorkflowStub.cs
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
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;

namespace Neon.Cadence
{
    /// <summary>
    /// Performs low-level operations on a workflow instance.
    /// </summary>
    internal class WorkflowStub : IWorkflowStub
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the low-level <see cref="WorkflowStub"/> associated with a workflow instance.
        /// </summary>
        /// <typeparam name="IWorkflow">The source workflow interface.</typeparam>
        /// <param name="workflowStub">The source workflow stub.</param>
        /// <returns>The <see cref="WorkflowStub"/>.</returns>
        public static WorkflowStub FromTyped<IWorkflow>(IWorkflow workflowStub)
            where IWorkflow : ITypedWorkflowStub
        {
            Covenant.Requires<ArgumentNullException>(workflowStub != null);

            throw new NotImplementedException();
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Default constructor.
        /// </summary>
        internal WorkflowStub()
        {
        }

        /// <inheritdoc/>
        public WorkflowExecution Execution { get; internal set; }

        /// <inheritdoc/>
        public WorkflowOptions Options { get; internal set; }

        /// <inheritdoc/>
        public Task CancelAsync()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task<TResult> GetResultAsync<TResult>(TimeSpan timeout = default)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task<object> GetResultAsync(Type resultType, TimeSpan timeout = default)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task<TResult> QueryAsync<TResult>(string queryType, params object[] args)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task<object> QueryAsync(Type resultType, string queryType, params object[] args)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task SignalAsync(string signalName, params object[] args)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task<WorkflowExecution> SignalWithStartAsync(string signalName, object[] signalArgs, object[] startArgs)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task<WorkflowExecution> StartAsync(params object[] args)
        {
            throw new NotImplementedException();
        }
    }
}
