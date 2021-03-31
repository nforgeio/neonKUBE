//-----------------------------------------------------------------------------
// FILE:	    SetupStepStatus.cs
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

using Newtonsoft.Json;

using Neon.Common;

namespace Neon.Kube
{
    /// <summary>
    /// Holds state information about a setup step.
    /// </summary>
    public class SetupStepStatus
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="stepNumber">The step number or zero for quiet steps.</param>
        /// <param name="stepLabel">The setup step label.</param>
        /// <param name="stepStatus">The current status for the step..</param>
        /// <param name="runTime">Specifies the runtime for completed steps or <see cref="TimeSpan.Zero"/> when the step hasn't completed execution.</param>
        /// <param name="internalStep">Specifies the internal setup controller step.</param>
        internal SetupStepStatus(int stepNumber, string stepLabel, SetupStepState stepStatus, TimeSpan runTime, object internalStep)
        {
            Covenant.Requires<ArgumentException>(stepNumber >= 0, nameof(stepNumber));
            Covenant.Requires<ArgumentException>(runTime >= TimeSpan.Zero, nameof(runTime));
            Covenant.Requires<ArgumentNullException>(internalStep != null, nameof(internalStep));

            this.Number       = stepNumber;
            this.Label        = string.IsNullOrEmpty(stepLabel) ? "<unlabeled step>" : stepLabel;
            this.State        = stepStatus;
            this.IsQuiet      = stepNumber == 0;
            this.Runtime      = runTime > TimeSpan.Zero ? runTime : TimeSpan.Zero;
            this.InternalStep = internalStep;
        }

        /// <summary>
        /// Returns the step number.
        /// </summary>
        public int Number { get; private set; }

        /// <summary>
        /// Returns the step label.
        /// </summary>
        public string Label { get; private set; }

        /// <summary>
        /// Returns the current step state.
        /// </summary>
        public SetupStepState State { get; private set; }

        /// <summary>
        /// Returns <c>true</c> for steps that where progress not intended to be reported 
        /// to the user.
        /// </summary>
        public bool IsQuiet { get; private set; }

        /// <summary>
        /// Returns how long the step has been executing for the current step or
        /// the total runtime for when the step has completed or failed.
        /// </summary>
        public TimeSpan Runtime { get; private set; }

        /// <summary>
        /// Rerturns the internal <see cref="SetupController{NodeMetadata}"/> step.
        /// </summary>
        [JsonIgnore]
        public object InternalStep { get; private set; }
    }
}
