//-----------------------------------------------------------------------------
// FILE:	    CadenceWorkflowRestartException.cs
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
    /// Thrown by <see cref="Workflow.ContinueAsNew(byte[], string, string, TimeSpan, TimeSpan, TimeSpan, TimeSpan, RetryOptions)"/>
    /// to be handled internally by <see cref="Workflow"/> as one of the special case 
    /// mechanisms for completing a workflow.
    /// </summary>
    /// <remarks>
    /// <note>
    /// Workflow entry points must allow this exception to be caught by the
    /// calling <see cref="CadenceClient"/> so that <see cref="Workflow.ContinueAsNew(byte[], string, string, TimeSpan, TimeSpan, TimeSpan, TimeSpan, RetryOptions)"/>
    /// will work properly.
    /// </note>
    /// </remarks>
    public class CadenceWorkflowRestartException : Exception
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
        /// <param name="startToCloseTimeout">Optional start to close timeout for the new execution.</param>
        /// <param name="retryPolicy">Optional retry policy for the new execution.</param>
        public CadenceWorkflowRestartException(
            byte[]              args                    = null,
            string              domain                  = null,
            string              taskList                = null,
            TimeSpan            executionToStartTimeout = default,
            TimeSpan            scheduleToCloseTimeout  = default,
            TimeSpan            scheduleToStartTimeout  = default,
            TimeSpan            startToCloseTimeout     = default,
            RetryOptions  retryPolicy             = null)

            : base()
        {
            this.Args                         = args;
            this.Domain                       = domain;
            this.TaskList                     = taskList;
            this.ExecutionStartToCloseTimeout = executionToStartTimeout;
            this.ScheduleToStartTimeout       = scheduleToStartTimeout;
            this.ScheduleToCloseTimeout       = scheduleToCloseTimeout;
            this.StartToCloseTimeout          = startToCloseTimeout;
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
        /// Optionally specifies the new timeout for the next workflow execution.
        /// </summary>
        public TimeSpan StartToCloseTimeout { get; private set; }

        /// <summary>
        /// Optionally specifies the new retry policy for the next workflow execution.
        /// </summary>
        public RetryOptions RetryPolicy { get; private set; } 
    }
}
