//-----------------------------------------------------------------------------
// FILE:	    SetupStepStatus.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Neon.Common;
using Neon.Data;

namespace Neon.Kube
{
    /// <summary>
    /// Holds state information about a setup step.
    /// </summary>
    public class SetupStepStatus : NotifyPropertyChanged
    {
        private bool                    isClone;
        private int                     number;
        private string                  label;
        private SetupStepState          state;
        private bool                    isQuiet;
        private TimeSpan                runtime;
        private ISetupControllerStep    internalStep;

        /// <summary>
        /// Default constructor used by <see cref="Clone"/>.
        /// </summary>
        private SetupStepStatus()
        {
            this.isClone = true;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="stepNumber">The step number or zero for quiet steps.</param>
        /// <param name="stepLabel">The setup step label.</param>
        /// <param name="stepState">The current status for the step.</param>
        /// <param name="internalStep">Specifies the internal setup controller step.</param>
        /// <param name="runTime">Optionally specifies the runtime for completed steps or <see cref="TimeSpan.Zero"/> when the step hasn't completed execution.</param>
        public SetupStepStatus(int stepNumber, string stepLabel, SetupStepState stepState, ISetupControllerStep internalStep, TimeSpan runTime = default)
        {
            Covenant.Requires<ArgumentException>(stepNumber >= 0, nameof(stepNumber));
            Covenant.Requires<ArgumentException>(internalStep != null, nameof(internalStep));
            Covenant.Requires<ArgumentException>(runTime >= TimeSpan.Zero, nameof(runTime));

            this.isClone      = false;
            this.Number       = stepNumber;
            this.Label        = string.IsNullOrEmpty(stepLabel) ? "<unlabeled step>" : stepLabel;
            this.State        = stepState;
            this.IsQuiet      = stepNumber == 0;
            this.Runtime      = runTime > TimeSpan.Zero ? runTime : TimeSpan.Zero;
            this.InternalStep = internalStep;
        }

        /// <summary>
        /// Returns the step number.
        /// </summary>
        public int Number
        {
            get => number;

            set
            {
                if (value != number)
                {
                    number = value;
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary>
        /// Returns the step label.
        /// </summary>
        public string Label
        {
            get => label;

            set
            {
                if (value != label)
                {
                    label = value;
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary>
        /// Returns the current step state.
        /// </summary>
        public SetupStepState State
        {
            get => state;

            set
            {
                if (value != state)
                {
                    state = value;
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary>
        /// Returns <c>true</c> for steps that where progress not intended to be reported 
        /// to the user.
        /// </summary>
        public bool IsQuiet
        {
            get => isQuiet;

            set
            {
                if (value != isQuiet)
                {
                    isQuiet = value;
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary>
        /// Returns <c>true</c> for global steps, <c>false</c> for node steps.
        /// </summary>
        public bool IsGlobalStep => internalStep.IsGlobalStep;

        /// <summary>
        /// Returns how long the step has been executing for the current step or
        /// the total runtime for when the step has completed or failed.
        /// </summary>
        public TimeSpan Runtime
        {
            get => runtime;

            set
            {
                if (value != runtime)
                {
                    runtime = value;
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary>
        /// Returns the internal <see cref="SetupController{NodeMetadata}"/> step.
        /// </summary>
        [JsonIgnore]
        public ISetupControllerStep InternalStep
        {
            get => internalStep;

            set
            {
                if (!ReferenceEquals(value, internalStep))
                {
                    internalStep = value;
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary>
        /// Returns a clone of a source (not cloned) instance.
        /// </summary>
        /// <returns>The clone.</returns>
        public SetupStepStatus Clone()
        {
            if (this.isClone)
            {
                throw new NotSupportedException("Cannot clone a cloned instance.");
            }

            return new SetupStepStatus()
            {
                Number       = this.Number,
                Label        = this.Label,
                State        = this.State,
                IsQuiet      = this.IsQuiet,
                Runtime      = this.Runtime,
                InternalStep = this.InternalStep
            };
        }

        /// <summary>
        /// Copies the properties from the source status to this instance, raising
        /// <see cref="INotifyPropertyChanged"/> related events as require.
        /// </summary>
        /// <param name="source">The source instance.</param>
        public void UpdateFrom(SetupStepStatus source)
        {
            Covenant.Requires<ArgumentNullException>(source != null, nameof(source));
            Covenant.Assert(this.isClone, "Target must be cloned.");
            Covenant.Assert(!source.isClone, "Source cannot be cloned.");

            this.Number       = source.Number;
            this.Label        = source.Label;
            this.State        = source.State;
            this.IsQuiet      = source.IsQuiet;
            this.Runtime      = source.Runtime;
            this.InternalStep = source.InternalStep;
        }
    }
}
