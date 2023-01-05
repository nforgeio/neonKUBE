//-----------------------------------------------------------------------------
// FILE:	    KubeService.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
    public static class KubeService
    {
        /// <summary>
        /// Minio.
        /// </summary>
        public const string Minio = "minio";

        /// <summary>
        /// Neon Blazor Proxy.
        /// </summary>
        public const string NeonBlazorProxy = "neon-blazor-proxy";

        /// <summary>
        /// Dex.
        /// </summary>
        public const string Dex = "neon-sso-dex";

        /// <summary>
        /// Neon Dashboard.
        /// </summary>
        public const string NeonDashboard = "neon-dashboard";

        /// <summary>
        /// Neon cluster operator.  This implements several control loops that help
        /// manage the cluster.
        /// </summary>
        public const string NeonClusterOperator = "neon-cluster-operator";

        /// <summary>
        /// Neon node agent.  This is an operator provisioned on each cluster node as
        /// a daemonset that performs node managment tasks.
        /// </summary>
        public const string NeonNodeAgent = "neon-node-agent";

        /// <summary>
        /// SSO Proxy.
        /// </summary>
        public const string NeonSsoSessionProxy = "neon-sso-session-proxy";

        /// <summary>
        /// Neon system database.
        /// </summary>
        public const string NeonSystemDb = "neon-system-db";
    }
}
