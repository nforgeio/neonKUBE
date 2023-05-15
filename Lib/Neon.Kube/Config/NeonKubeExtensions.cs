//-----------------------------------------------------------------------------
// FILE:	    NeonKubeExtensions.cs
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

namespace Neon.Kube.Config
{
    /// <summary>
    /// Defines our custom Kubernetes context extension names.
    /// </summary>
    public static class NeonKubeExtensions
    {
        /// <summary>
        /// The prefix added to NEONKUBE related KubeContext extension property names
        /// used to avoid conflicts with other vendor extensions.
        /// </summary>
        private const string ExtensionPrefix = "neonkube.io.";

        /// <summary>
        /// <see cref="KubeClusterInfo"/>: Holds additional information for NEONKUBE clusters.
        /// </summary>
        public const string ClusterInfo = $"{ExtensionPrefix}cluster-info";

        /// <summary>
        /// <see cref="HostingEnvironment"/>: Used to identify a cluster's host environment.
        /// </summary>
        public const string HostingEnvironment = $"{ExtensionPrefix}hosting-environment";

        /// <summary>
        /// <see cref="Hosting"/>: Details about the hosting environment including credentials.
        /// </summary>
        public const string Hosting = $"{ExtensionPrefix}hosting";

        /// <summary>
        /// Holds the prefix (if any) prepended by the hosting environment to node
        /// names to identify the node within the hosting environment.  This is typically
        /// set to the cluster name and is useful for avoiding node name conflicts when
        /// hosting multiple clusters within the same environment.
        /// </summary>
        public const string HostingNamePrefix = $"{ExtensionPrefix}hosting-name-prefix";

        /// <summary>
        /// <c>bool</c>: Used to indicate that a cluster belongs to neon-desktop.
        /// </summary>
        public const string IsNeonDesktop = $"{ExtensionPrefix}is-neon-desktop";

        /// <summary>
        /// <c>bool</c>: Used to indicate that a cluster belongs to NEONKUBE.
        /// </summary>
        public const string IsNeonKube = $"{ExtensionPrefix}is-neonkube";

        /// <summary>
        /// <c>string</c>: Specifies the cluster's SSO admin password.
        /// </summary>
        public const string SsoUsername = $"{ExtensionPrefix}sso-username";

        /// <summary>
        /// <c>string</c>: Specifies the cluster's SSO admin password.
        /// </summary>
        public const string SsoPassword = $"{ExtensionPrefix}sso-password";

        /// <summary>
        /// <c>string</c>: Specifies the cluster's SSH admin username.
        /// </summary>
        public const string SshUsername = $"{ExtensionPrefix}ssh-username";

        /// <summary>
        /// <c>string</c>: Specifies the cluster's SSH admin password.
        /// </summary>
        public const string SshPassword = $"{ExtensionPrefix}ssh-password";

        /// <summary>
        /// Used by our xUnit <b>ClusterFixture</b> to persist the cluster definition so
        /// the fixture can determine when to provision a new cluster when the definition
        /// has changed.
        /// </summary>
        public const string TestClusterDefinition = $"{ExtensionPrefix}test-cluster-definition";
    }
}
