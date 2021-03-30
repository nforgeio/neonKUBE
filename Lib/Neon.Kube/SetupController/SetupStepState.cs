//-----------------------------------------------------------------------------
// FILE:	    SetupStepState.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.Kube
{
    /// <summary>
    /// Holds state information about a setup step.
    /// </summary>
    public class SetupStepState
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="stepLabel">The setup step label.</param>
        /// <param name="stepStatus">The current status for the step..</param>
        /// <param name="runTime">Specifies the runtime for the step or <see cref="TimeSpan.Zero"/> when the step hasn't been executed yet.</param>
        internal SetupStepState(string stepLabel, StepStatus stepStatus, TimeSpan runTime)
        {
            this.Label   = string.IsNullOrEmpty(stepLabel) ? "<unlabeled step>" : stepLabel;
            this.Status  = stepStatus;
            this.Runtime = runTime > TimeSpan.Zero ? runTime : TimeSpan.Zero;
        }

        /// <summary>
        /// Returns the step label.
        /// </summary>
        public string Label { get; private set; }

        /// <summary>
        /// Returns the step status.
        /// </summary>
        public StepStatus Status { get; private set; }

        /// <summary>
        /// Returns how long the step has been executing for the current step or how
        /// long completed steps too to run.
        /// </summary>
        public TimeSpan Runtime { get; private set; }
    }
}
