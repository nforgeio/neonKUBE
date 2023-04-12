//-----------------------------------------------------------------------------
// FILE:	    NeonKubeExtensionName.cs
// CONTRIBUTOR: Jeff Lill
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

using System;

namespace Neon.Kube.Login
{
    /// <summary>
    /// Defines our custom Kubernetes context extension names.
    /// </summary>
    public static class NeonKubeExtensionName
    {
        /// <summary>
        /// Suffix added to neonKUBE related KubeContext extension property names used
        /// to avoid conflicts with other vendor extensions.
        /// </summary>
        private const string Suffix = "neonkube.io";

        /// <summary>
        /// <c>bool</c>: Used to indicate that the parent object belongs to neonKUBE.
        /// </summary>
        public const string IsNeonKube = $"isNeonKube.{Suffix}";

        /// <summary>
        /// <c>bool</c>: Used to indicate that the parent object belongs to neon-desktop.
        /// </summary>
        public const string IsNeonDesktop = $"isNeonDesktop.{Suffix}";

        /// <summary>
        /// <see cref="KubeClusterInfo"/>: Holds additional information for neonKUBE clusters.
        /// </summary>
        public const string ClusterInfo = $"clusterInfo.{Suffix}";

        /// <summary>
        /// Used by our xUnit <b>ClusterFixture</b> to persist the cluster definition so
        /// the fixture can determine when to provision a new cluster when the definition
        /// has changed.
        /// </summary>
        public const string TestClusterDefinition = $"testClusterDefinition.{Suffix}";
    }
}
