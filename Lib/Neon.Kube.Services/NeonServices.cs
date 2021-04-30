//-----------------------------------------------------------------------------
// FILE:	    NeonServices.cs
// CONTRIBUTOR: Marcus Bowyer
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

namespace Neon.Kube
{
    /// <summary>
    /// Defines the Neon service names.  
    /// </summary>
    public static class NeonServices
    {
        /// <summary>
        /// Cluster operator.
        /// </summary>
        public const string ClusterOperator = "neon-cluster-operator";

        /// <summary>
        /// Neon identity service, AKA a Secure Token Service (STS).
        /// </summary>
        public const string IdentityService = "neon-identity-service";

        /// <summary>
        /// Operator that manages the <see cref="IdentityService"/> instances as well as
        /// the related database.
        /// </summary>
        public const string IdentityOperator = "neon-identity-operator";

        /// <summary>
        /// Service that runs Grafana setup.
        /// </summary>
        public const string SetupGrafana = "neon-setup-grafana";

        /// <summary>
        /// Service that runs Harbor setup.
        /// </summary>
        public const string SetupHarbor = "neon-setup-harbor";

        /// <summary>
        /// Non-production service used to test Cadence running in a Linux container.
        /// </summary>
        public const string TestCadence = "test-cadence";

        /// <summary>
        /// Non-production service used to test Temporal running in a Linux container.
        /// </summary>
        public const string TestTemporal = "test-temporal";
    }
}
