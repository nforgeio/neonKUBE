//-----------------------------------------------------------------------------
// FILE:	    IWorkflowStub.cs
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
    /// Defines the low-level operations for an untyped workflow stub.
    /// </summary>
    public interface IWorkflowStub
    {
        /// <summary>
        /// Returns the associated workflow execution details.
        /// </summary>
        WorkflowExecution Execution { get; }

        /// <summary>
        /// Returns the associated workflow options.
        /// </summary>
        WorkflowOptions Options { get; }

        /// <summary>
        /// Attempts to cancel the associated workflow.
        /// </summary>
        /// <returns></returns>
        Task CancelAsync();

        /// <summary>
        /// Attempts to retrieve the associated workflow result.
        /// </summary>
        /// <typeparam name="TResult">The result type.</typeparam>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <returns>The result.</returns>
        Task<TResult> GetResultAsync<TResult>(TimeSpan timeout = default);

        /// <summary>
        /// Attempts to retrieve the associated workflow result.
        /// </summary>
        /// <param name="resultType">Specifies the result type.</param>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <returns>The result.</returns>
        Task<object> GetResultAsync(Type resultType, TimeSpan timeout = default);

        /// <summary>
        /// Queries the associated workflow.
        /// </summary>
        /// <typeparam name="TResult">The query result type.</typeparam>
        /// <param name="queryType">Specifies the query type.</param>
        /// <param name="args">Specifies the query arguments.</param>
        /// <returns>The query result.</returns>
        Task<TResult> QueryAsync<TResult>(string queryType, params object[] args);

        /// <summary>
        ///  Queries the associated workflow.
        /// </summary>
        /// <param name="resultType">Specifies the query result type.</param>
        /// <param name="queryType">Specifies the query type.</param>
        /// <param name="args">Specifies the query arguments.</param>
        /// <returns>The query result.</returns>
        Task<object> QueryAsync(Type resultType, string queryType, params object[] args);

        /// <summary>
        /// Signals the associated workflow.
        /// </summary>
        /// <param name="signalName">Specifies the signal name.</param>
        /// <param name="args">Specifies the signal arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        Task SignalAsync(string signalName, params object[] args);

        /// <summary>
        /// Signals the associated workflow, starting it if it hasn't already been started.
        /// </summary>
        /// <param name="signalName">Specifies the signal name.</param>
        /// <param name="signalArgs">Specifies the signal arguments.</param>
        /// <param name="startArgs">Specifies the workflow start arguments.</param>
        /// <returns></returns>
        Task<WorkflowExecution> SignalWithStartAsync(string signalName, object[] signalArgs, object[] startArgs);

        /// <summary>
        /// Starts the associated workflow.
        /// </summary>
        /// <param name="args">The workflow arguments.</param>
        /// <returns>The <see cref="WorkflowExecution"/>.</returns>
        Task<WorkflowExecution> StartAsync(params object[] args);
    }
}
