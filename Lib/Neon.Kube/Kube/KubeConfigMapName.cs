//-----------------------------------------------------------------------------
// FILE:	    KubeConfigMapName.cs
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
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.SSH;

using Renci.SshNet;

namespace Neon.Kube
{
    /// <summary>
    /// Defines internal neonKUBE global cluster configmap names.
    /// </summary>
    public static class KubeConfigMapName
    {
        /// <summary>
        /// Identifes the neonKUBE configmap used to report cluster health.  This configmap is
        /// located in the <see cref="KubeNamespace.NeonStatus"/> namespace and is initially
        /// created during cluster setup and is maintained by the neon-cluster-operator
        /// thereafter.
        /// </summary>
        public const string ClusterHealth = "cluster-health";

        /// <summary>
        /// Identifes the neonKUBE configmap used to report cluster info.  This configmap is
        /// located in the <see cref="KubeNamespace.NeonStatus"/> namespace and is initially
        /// created during cluster setup and is maintained by the neon-cluster-operator
        /// thereafter.
        /// </summary>
        public const string ClusterInfo = "cluster-info";

        /// <summary>
        /// <para>
        /// Identifies the neonKUBE configmap used to indicate whether the cluster is considered
        /// to be locked.  <b>neon-desktop</b>, <b>neon-cli</b>, and <b>ClusterFixture</b> use 
        /// this to block operations like cluster <b>reset</b>, <b>remove</b>, <b>pause</b>, and 
        /// <b>stop</b> when the cluster  is locked in an attempt to avoid harmful operations on
        /// production or otherwise important clusters.
        /// </para>
        /// <para>
        /// This configmap is located in the <see cref="KubeNamespace.NeonStatus"/> namespace.
        /// </para>
        /// </summary>
        public const string ClusterLock = "cluster-lock";

        /// <summary>
        /// <para>
        /// Identifies the configmap holding the <see cref="ClusterManifest"/>.
        /// </para>
        /// <para>
        /// This configmap is located in the <see cref="KubeNamespace.NeonSystem"/> namespace.
        /// </para>
        /// </summary>
        public const string ClusterManifest = "cluster-manifest";

        /// <summary>
        /// <para>
        /// Identifies the configmap holding the <see cref="ClusterDeployment"/>.
        /// </para>
        /// <para>
        /// This configmap is located in the <see cref="KubeNamespace.NeonSystem"/> namespace.
        /// </para>
        /// </summary>
        public const string ClusterDeployment = "cluster-deployment";
    }
}
