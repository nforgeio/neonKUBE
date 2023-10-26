// -----------------------------------------------------------------------------
// FILE:	    ClusterJobOptions.cs
// CONTRIBUTOR: NEONFORGE Team
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License").
// You may not use this file except in compliance with the License.
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Kube.ClusterDef
{
    /// <summary>
    /// Specifies enhanced Quartz cron schedules for various NEONKUBE cluster component jobs.
    /// </summary>
    public class ClusterJobOptions
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ClusterJobOptions()
        {
        }

        /// <summary>
        /// Configures the <b>neon-clister-operator</b> jobs.
        /// </summary>
        public NeonClusterOperatorJobs NeonClusterOperator { get; set; }

        /// <summary>
        /// Validates the options.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        internal void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            NeonClusterOperator ??= new NeonClusterOperatorJobs();
            NeonClusterOperator.Validate(clusterDefinition);
        }
    }
}
