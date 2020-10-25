//-----------------------------------------------------------------------------
// FILE:	    NeonServices.cs
// CONTRIBUTOR: Marcus Bowyer
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

namespace Neon.Service
{
    /// <summary>
    /// Defines the Neon service names.  
    /// </summary>
    public static class NeonServices
    {
        /// <summary>
        /// Cluster Manager Operator.
        /// </summary>
        public const string NeonClusterManager = "neon-cluster-manager";

        /// <summary>
        /// Elasticsearch.
        /// </summary>
        public const string Elasticsearch = "neon-logs-elasticsearch";

        /// <summary>
        /// Kibana.
        /// </summary>
        public const string Kibana = "neon-logs-kibana";

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
