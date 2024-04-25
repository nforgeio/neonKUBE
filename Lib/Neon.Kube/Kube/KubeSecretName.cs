// -----------------------------------------------------------------------------
// FILE:        KubeSecretName.cs
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Kube
{
    /// <summary>
    /// Defines internal NEONKUBE global cluster secret names.
    /// </summary>
    public static class KubeSecretName
    {
        /// <summary>
        /// <para>
        /// Identifies the configmap holding the <see cref="ClusterDeployment"/>.
        /// </para>
        /// <para>
        /// This configmap is located in the <see cref="KubeNamespace.NeonSystem"/> namespace.
        /// </para>
        /// </summary>
        public const string ClusterDeployment = "cluster-deployment";

        /// <summary>
        /// <para>
        /// <b>Hack:</b> Secret holding the cluster user credentials.  This will eventually
        /// be replaced by user CRDs.
        /// </para>
        /// <para>
        /// This configmap is located in the <see cref="KubeNamespace.NeonSystem"/> namespace.
        /// </para>
        /// </summary>
        public const string GlauthUsers = "glauth-users";

        /// <summary>
        /// <para>
        /// <b>Hack:</b> Secret holding the cluster user groups.  This will eventually
        /// be replaced by group CRDs.
        /// </para>
        /// <para>
        /// This configmap is located in the <see cref="KubeNamespace.NeonSystem"/> namespace.
        /// </para>
        /// </summary>
        public const string GlauthGroups = "glauth-groups";
    }
}
