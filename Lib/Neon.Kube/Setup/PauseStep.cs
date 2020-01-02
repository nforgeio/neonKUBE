//-----------------------------------------------------------------------------
// FILE:	    PauseStep.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
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
    /// Pauses cluster configuration for a period of time.
    /// </summary>
    public class PauseStep : ConfigStep
    {
        private TimeSpan    delay;

        /// <summary>
        /// Constructs a configuration step that pauses setup for a period of time.
        /// </summary>
        /// <param name="delay">The amount of time to pause.</param>
        public PauseStep(TimeSpan delay)
        {
            Covenant.Requires<ArgumentException>(delay >= TimeSpan.Zero, nameof(delay));

            this.delay = delay;
        }

        /// <inheritdoc/>
        public override void Run(ClusterProxy cluster)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null, nameof(cluster));

            foreach (var node in cluster.Nodes)
            {
                node.Status = $"pause {delay}";
            }

            Thread.Sleep(delay);

            foreach (var node in cluster.Nodes)
            {
                node.Status = string.Empty;
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"pause delay={delay}";
        }
    }
}
