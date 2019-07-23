//-----------------------------------------------------------------------------
// FILE:	    IChildWorkflowStub.cs
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
    /// Manages starting and signalling a child workflow instance based on
    /// its workflow type name and arguments.  This is useful when the child
    /// workflow type is not known at compile time as well provinding a way
    /// to call child workflows written in another language.
    /// </summary>
    public interface IChildWorkflowStub
    {
        /// <summary>
        /// Returns the child workflow options.
        /// </summary>
        ChildWorkflowOptions Options { get; }

        /// <summary>
        /// Returns the workflow type name.
        /// </summary>
        string WorkflowType { get; }

        /// <summary>
        /// Waits for the workflow to begin executing and then returns
        /// its <see cref="WorkflowExecution"/>.
        /// </summary>
        /// <returns>The <see cref="WorkflowExecution"/>.</returns>
        Task<WorkflowExecution> GetExecutionAsync();

        /// <summary>
        /// Executes the workflow, specifying the result type as a generic parameter.
        /// </summary>
        /// <typeparam name="TResult">The workflow result type.</typeparam>
        /// <param name="args">The workflow arguments.</param>
        /// <returns>The workflow result.</returns>
        Task<TResult> ExecuteAsync<TResult>(params object[] args);

        /// <summary>
        /// Executes the workflow, specifying the result type as a parameter.
        /// </summary>
        /// <param name="resultType">The result type.</param>
        /// <typeparam name="TResult">The workflow result type.</typeparam>
        /// <param name="args">The workflow arguments.</param>
        /// <returns>The workflow result as an <see cref="object"/>.</returns>
        Task<object> ExecuteAsync<TResult>(Type resultType, params object[] args);

        /// <summary>
        /// Signals the workflow.
        /// </summary>
        /// <param name="signalName">The signal name.</param>
        /// <param name="args">The signal arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        Task SignalAsync(string signalName, params object[] args);
    }
}
