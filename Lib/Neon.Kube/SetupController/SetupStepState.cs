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
        /// <param name="step">The setup step.</param>
        /// <param name="isCurrent">Indicates whether this is the currently executing step.</param>
        internal SetupStepState(SetupStep step, bool isCurrent)
        {
            Covenant.Requires<ArgumentNullException>(step != null, nameof(step));
        }

        public string 
    }
}
