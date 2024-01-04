//-----------------------------------------------------------------------------
// FILE:        SetupStepDetails.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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

namespace Neon.Kube.Setup
{
    /// <summary>
    /// Holds information about an executing setup step.  This is the argument
    /// passed when the <see cref="ISetupController.StepStarted"/> event is raised.
    /// </summary>
    public class SetupStepDetails
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="number">The step number.</param>
        /// <param name="label">The step label.</param>
        public SetupStepDetails(int number, string label)
        {
            this.Number = number;
            this.Label  = label;
        }

        /// <summary>
        /// Returns the step number.
        /// </summary>
        public int Number { get; private set; }

        /// <summary>
        /// Returns the step label.
        /// </summary>
        public string Label { get; private set; }
    }
}
