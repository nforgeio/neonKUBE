//-----------------------------------------------------------------------------
// FILE:	    ConfigStep.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
    /// The <c>abstract</c> base class for node configuration step implementations.
    /// </summary>
    public abstract class ConfigStep
    {
        /// <summary>
        /// Implements the configuration step.
        /// </summary>
        /// <param name="cluster">The cluster proxy instance.</param>
        public abstract void Run(ClusterProxy cluster);

        /// <summary>
        /// Pause briefly to allow the configuration UI a chance to display
        /// step information.
        /// </summary>
        protected void StatusPause()
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(500));
        }
    }
}
