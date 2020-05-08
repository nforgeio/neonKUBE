//-----------------------------------------------------------------------------
// FILE:	    ContinueAsNewException.cs
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

using Newtonsoft.Json;

using Neon.Cadence;
using Neon.Common;

namespace Neon.Cadence
{
    /// <summary>
    /// <b>INTERNAL USE ONLY:</b> Thrown by <see cref="Workflow.ContinueAsNewAsync(ContinueAsNewOptions, object[])"/>
    /// or <see cref="Workflow.ContinueAsNewAsync(object[])"/> as well as any continue-as-new stubs to be handled 
    /// internally by <see cref="WorkflowBase"/>.
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
    public class ContinueAsNewException : CadenceInternalException
    {
        /// <summary>
        /// Constructs an instance using explicit arguments.
        /// </summary>
        /// <param name="args">Optional arguments for the new execution.</param>
        /// <param name="workflow">Optional workflow for the new execution.</param>
        /// <param name="domain">Optional domain for the new execution.</param>
        /// <param name="taskList">Optional task list for the new execution.</param>
        /// <param name="startToCloseTimeout">Optional execution to start timeout for the new execution.</param>
        /// <param name="scheduleToCloseTimeout">Optional schedule to close timeout for the new execution.</param>
        /// <param name="scheduleToStartTimeout">Optional schedule to start timeout for the new execution.</param>
        /// <param name="decisionTaskTimeout">Optional decision task start to close timeout for the new execution.</param>
        /// <param name="retryOptions">Optional retry options for the new execution.</param>
        public ContinueAsNewException(
            byte[]          args                   = null,
            string          workflow               = null,
            string          domain                 = null,
            string          taskList               = null,
            TimeSpan        startToCloseTimeout    = default,
            TimeSpan        scheduleToCloseTimeout = default,
            TimeSpan        scheduleToStartTimeout = default,
            TimeSpan        decisionTaskTimeout    = default,
            RetryOptions    retryOptions           = null)

            : base()
        {
            this.Args                   = args;
            this.Workflow               = workflow;
            this.Domain                 = domain;
            this.TaskList               = taskList;
            this.StartToCloseTimeout    = startToCloseTimeout;
            this.ScheduleToStartTimeout = scheduleToStartTimeout;
            this.ScheduleToCloseTimeout = scheduleToCloseTimeout;
            this.DecisionTaskTimeout    = decisionTaskTimeout;
            this.RetryOptions           = retryOptions;
        }

        /// <summary>
        /// Constructs an instance using a <see cref="ContinueAsNewOptions"/>.
        /// </summary>
        /// <param name="args">Arguments for the new execution (this may be <c>null)</c>).</param>
        /// <param name="options">Options for the new execution  (this may be <c>null</c>).</param>
        public ContinueAsNewException(byte[] args, ContinueAsNewOptions options)
        {
            this.Args = args;

            if (options != null)
            {
                this.Workflow                     = options.Workflow;
                this.Domain                       = options.Domain;
                this.TaskList                     = options.TaskList;
                this.StartToCloseTimeout = options.ExecutionStartToCloseTimeout;
                this.ScheduleToStartTimeout       = options.ScheduleToStartTimeout;
                this.ScheduleToCloseTimeout       = options.ScheduleToCloseTimeout;
                this.DecisionTaskTimeout      = options.TaskStartToCloseTimeout;
                this.RetryOptions                 = options.RetryOptions;
            }
        }

        /// <summary>
        /// Returns the arguments for the next workflow execution.
        /// </summary>
        public byte[] Args { get; private set; }

        /// <summary>
        /// Optionally overrides the name of the workflow to continue as new.
        /// </summary>
        public string Workflow { get; set; }

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
        public TimeSpan StartToCloseTimeout { get; private set; }

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
        public TimeSpan DecisionTaskTimeout { get; private set; }

        /// <summary>
        /// Optionally specifies the new retry options for the next workflow execution.
        /// </summary>
        public RetryOptions RetryOptions { get; private set; } 
    }
}
