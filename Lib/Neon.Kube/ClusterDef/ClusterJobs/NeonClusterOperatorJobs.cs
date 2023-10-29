// -----------------------------------------------------------------------------
// FILE:	    NeonClusterOperatorJobs.cs
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

using Neon.Kube.ClusterDef.ClusterJobs;

namespace Neon.Kube.ClusterDef
{
    /// <summary>
    /// Settings for the <b>neon-cluster-operator</b> jobs.
    /// </summary>
    public class NeonClusterOperatorJobs
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public NeonClusterOperatorJobs()
        {
        }

        /// <summary>
        /// Schedules Kubernetes control plane certificate renewal. 
        /// </summary>
        public JobSchedule ControlPlaneCertificates { get; set; }

        /// <summary>
        /// Schedules updates of standard certificate authority on the cluster nodes.
        /// </summary>
        public JobSchedule NodeCaCertificates { get; set; }

        /// <summary>
        /// Schedules the Linux security patches on the cluster nodes.
        /// </summary>
        public JobSchedule SecurityPatches { get; set; }

        /// <summary>
        /// Schedules the persisting of NEONKUBE cluster container images from
        /// cluster nodes to Harbor as required.
        /// </summary>
        public JobSchedule ContainerImages { get; set; }

        /// <summary>
        /// Schedules the transmission of cluster telemetry to NEONFORGE.
        /// </summary>
        public JobSchedule Telemetry { get; set; }

        /// <summary>
        /// Schedules renewal of the cluster certificate.
        /// </summary>
        public JobSchedule ClusterCertificate { get; set; }

        /// <summary>
        /// Validates the options.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        internal void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            ControlPlaneCertificates ??= new JobSchedule();
            ControlPlaneCertificates.Validate(clusterDefinition, $"{nameof(ClusterJobOptions)}.{nameof(NeonClusterOperatorJobs)}.{nameof(ControlPlaneCertificates)}");

            NodeCaCertificates ??= new JobSchedule();
            NodeCaCertificates.Validate(clusterDefinition, $"{nameof(ClusterJobOptions)}.{nameof(NeonClusterOperatorJobs)}.{nameof(NodeCaCertificates)}");

            SecurityPatches ??= new JobSchedule();
            SecurityPatches.Validate(clusterDefinition, $"{nameof(ClusterJobOptions)}.{nameof(NeonClusterOperatorJobs)}.{nameof(SecurityPatches)}");

            ContainerImages ??= new JobSchedule();
            ContainerImages.Validate(clusterDefinition, $"{nameof(ClusterJobOptions)}.{nameof(NeonClusterOperatorJobs)}.{nameof(ContainerImages)}");

            Telemetry ??= new JobSchedule();
            Telemetry.Validate(clusterDefinition, $"{nameof(ClusterJobOptions)}.{nameof(NeonClusterOperatorJobs)}.{nameof(Telemetry)}");

            ClusterCertificate ??= new JobSchedule();
            ClusterCertificate.Validate(clusterDefinition, $"{nameof(ClusterJobOptions)}.{nameof(NeonClusterOperatorJobs)}.{nameof(ClusterCertificate)}");
        }
    }
}
