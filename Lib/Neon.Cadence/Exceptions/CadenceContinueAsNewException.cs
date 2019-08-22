//-----------------------------------------------------------------------------
// FILE:	    CadenceContinueAsNewException.cs
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

using Newtonsoft.Json;

using Neon.Cadence;
using Neon.Common;

namespace Neon.Cadence
{
    /// <summary>
    /// Thrown by <see cref="Workflow.ContinueAsNewAsync(ContinueAsNewOptions, object[])"/>
    /// or <see cref="Workflow.ContinueAsNewAsync(object[])"/> to be handled internally by
    /// <see cref="WorkflowBase"/> as one of the special case  mechanisms for completing
    /// a workflow.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If your workflow needs a general exception handler, you should include
    /// a <c>catch</c> clause that catches and rethrows any <see cref="CadenceInternalException"/>
    /// derived exceptions before your custom handler.  This will look something like:
    /// </para>
    /// <code language="c#">
    /// public class MyWorkflow
    /// {
    ///     public Task Entrypoint()
    ///     {
    ///         try
    ///         {
    ///             // Workflow implementation.
    ///         }
    ///         catch (CadenceInternalException)
    ///         {
    ///             // Rethrow so Cadence can handle these exceptions.        
    /// 
    ///             throw;
    ///         }
    ///         catch (Exception e)
    ///         {
    ///             // Your exception handler.
    ///         }
    ///     }
    /// }
    /// </code>
    /// </remarks>
    public class CadenceContinueAsNewException : CadenceInternalException
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="args">Optional arguments for the new execution.</param>
        /// <param name="domain">Optional domain for the new execution.</param>
        /// <param name="taskList">Optional task list for the new execution.</param>
        /// <param name="executionToStartTimeout">Optional execution to start timeout for the new execution.</param>
        /// <param name="scheduleToCloseTimeout">Optional schedule to close timeout for the new execution.</param>
        /// <param name="scheduleToStartTimeout">Optional schedule to start timeout for the new execution.</param>
        /// <param name="taskStartToCloseTimeout">Optional decision task start to close timeout for the new execution.</param>
        /// <param name="retryPolicy">Optional retry policy for the new execution.</param>
        public CadenceContinueAsNewException(
            byte[]          args                    = null,
            string          domain                  = null,
            string          taskList                = null,
            TimeSpan        executionToStartTimeout = default,
            TimeSpan        scheduleToCloseTimeout  = default,
            TimeSpan        scheduleToStartTimeout  = default,
            TimeSpan        taskStartToCloseTimeout = default,
            RetryOptions    retryPolicy             = null)

            : base()
        {
            this.Args                         = args;
            this.Domain                       = domain;
            this.TaskList                     = taskList;
            this.ExecutionStartToCloseTimeout = executionToStartTimeout;
            this.ScheduleToStartTimeout       = scheduleToStartTimeout;
            this.ScheduleToCloseTimeout       = scheduleToCloseTimeout;
            this.TaskStartToCloseTimeout          = taskStartToCloseTimeout;
            this.RetryPolicy                  = retryPolicy;
        }

        /// <summary>
        /// Returns the arguments for the next workflow execution.
        /// </summary>
        public byte[] Args { get; private set; }

        /// <summary>
        /// Optionally specifies the new domain for the next workflow execution.
        /// </summary>
        public string Domain { get; private set; }

        /// <summary>
        /// Optionally specifies the new task list for the next workflow execution.
        /// </summary>
        public string TaskList { get; private set; }

        /// <summary>
        /// Optionally specifies the new timeout for the next workflow execution.
        /// </summary>
        public TimeSpan ExecutionStartToCloseTimeout { get; private set; }

        /// <summary>
        /// Optionally specifies the new timeout for the next workflow execution.
        /// </summary>
        public TimeSpan ScheduleToCloseTimeout { get; private set; }

        /// <summary>
        /// Optionally specifies the new timeout for the next workflow execution.
        /// </summary>
        public TimeSpan ScheduleToStartTimeout { get; private set; }

        /// <summary>
        /// Optionally specifies the new decision task timeout for the next workflow execution.
        /// </summary>
        public TimeSpan TaskStartToCloseTimeout { get; private set; }

        /// <summary>
        /// Optionally specifies the new retry policy for the next workflow execution.
        /// </summary>
        public RetryOptions RetryPolicy { get; private set; } 
    }
}
