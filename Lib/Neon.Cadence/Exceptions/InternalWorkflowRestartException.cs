//-----------------------------------------------------------------------------
// FILE:	    InternalWorkflowRestartException.cs
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

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// Thrown by <see cref="Workflow.Restart(byte[], string, string, TimeSpan, TimeSpan, TimeSpan, TimeSpan, CadenceRetryPolicy)"/>
    /// to be handled by <see cref="Workflow.InvokeAsync(CadenceClient, WorkflowInvokeRequest)"/>
    /// as one of the special case mechanisms for completing a workflow.
    /// </summary>
    internal class InternalWorkflowRestartException : Exception
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="args">Optional arguments for the new run.</param>
        /// <param name="domain">Optional domain for the new run.</param>
        /// <param name="tasklist">Optional tasklist for the new run.</param>
        /// <param name="executionToStartTimeout">Optional execution to start timeout for the new run.</param>
        /// <param name="scheduleToCloseTimeout">Optional schedule to close timeout for the new run.</param>
        /// <param name="scheduleToStartTimeout">Optional schedule to start timeout for the new run.</param>
        /// <param name="startToCloseTimeout">Optional start to close timeout for the new run.</param>
        /// <param name="retryPolicy">Optional retry policy for the new run.</param>
        public InternalWorkflowRestartException(
            byte[]              args                    = null,
            string              domain                  = null,
            string              tasklist                = null,
            TimeSpan            executionToStartTimeout = default,
            TimeSpan            scheduleToCloseTimeout  = default,
            TimeSpan            scheduleToStartTimeout  = default,
            TimeSpan            startToCloseTimeout     = default,
            CadenceRetryPolicy  retryPolicy             = null)

            : base()
        {
            this.Args                         = args;
            this.Domain                       = domain;
            this.TaskList                     = tasklist;
            this.ExecutionStartToCloseTimeout = executionToStartTimeout;
            this.ScheduleToStartTimeout       = scheduleToStartTimeout;
            this.ScheduleToCloseTimeout       = scheduleToCloseTimeout;
            this.StartToCloseTimeout          = startToCloseTimeout;
            this.RetryPolicy                  = retryPolicy;
        }

        /// <summary>
        /// Returns the arguments for the next workflow run.
        /// </summary>
        public byte[] Args { get; private set; }

        /// <summary>
        /// Optionally specifies the new domain for the next workflow run.
        /// </summary>
        public string Domain { get; private set; }

        /// <summary>
        /// Optionally specifies the new tasklist for the next workflow run.
        /// </summary>
        public string TaskList { get; private set; }

        /// <summary>
        /// Optionally specifies the new timeout for the next workflow run.
        /// </summary>
        public TimeSpan ExecutionStartToCloseTimeout { get; private set; }

        /// <summary>
        /// Optionally specifies the new timeout for the next workflow run.
        /// </summary>
        public TimeSpan ScheduleToCloseTimeout { get; private set; }

        /// <summary>
        /// Optionally specifies the new timeout for the next workflow run.
        /// </summary>
        public TimeSpan ScheduleToStartTimeout { get; private set; }

        /// <summary>
        /// Optionally specifies the new timeout for the next workflow run.
        /// </summary>
        public TimeSpan StartToCloseTimeout { get; private set; }

        /// <summary>
        /// Optionally specifies the new retry policy for the next workflow run.
        /// </summary>
        public CadenceRetryPolicy RetryPolicy { get; private set; } 
    }
}
