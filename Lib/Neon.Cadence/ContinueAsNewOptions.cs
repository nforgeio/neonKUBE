//-----------------------------------------------------------------------------
// FILE:	    ContinueAsNewOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
    /// Specifies the options to be used when continuing a workflow as a 
    /// new instance.
    /// </summary>
    public class ContinueAsNewOptions
    {
        /// <summary>
        /// Optionally overrides the current workflow's timeout for the restarted
        /// workflow when this value is greater than <see cref="TimeSpan.Zero"/>.
        /// </summary>
        public TimeSpan ExecutionStartToCloseTimeout { get; set; }

        /// <summary>
        /// Optionally overrides the current workflow's timeout for the restarted
        /// workflow when this value is greater than <see cref="TimeSpan.Zero"/>.
        /// </summary>
        public TimeSpan ScheduleToCloseTimeout { get; set; }

        /// <summary>
        /// Optionally overrides the current workflow's timeout for the restarted
        /// workflow when this value is greater than <see cref="TimeSpan.Zero"/>.
        /// </summary>
        public TimeSpan ScheduleToStartTimeout { get; set; }

        /// <summary>
        /// Optionally overrides the current workflow's decision task timeout for 
        /// the restarted workflow when this value is greater than <see cref="TimeSpan.Zero"/>.
        /// </summary>
        public TimeSpan TaskStartToCloseTimeout { get; set; }

        /// <summary>
        /// Optionally overrides the name of the workflow to continue as new.
        /// </summary>
        public string Workflow { get; set; }

        /// <summary>
        /// Optionally overrides the current workflow's task list when restarting.
        /// </summary>
        public string TaskList { get; set; }

        /// <summary>
        /// Optionally overrides the current workflow's domain when restarting.
        /// </summary>
        public string Domain { get; set; }

        /// <summary>
        /// Optionally overrides the current workflow's retry options when restarting.
        /// </summary>
        public RetryOptions RetryOptions { get; set; }
    }
}
