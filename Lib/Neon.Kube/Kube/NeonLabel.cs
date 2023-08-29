//-----------------------------------------------------------------------------
// FILE:        NeonLabel.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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

using Neon.Kube.ClusterDef;
using Neon.Kube.Proxy;

namespace Neon.Kube
{
    /// <summary>
    /// <para>
    /// Defines the non-node cluster definition labels used to tag objects by NEONKUBE.
    /// </para>
    /// <note>
    /// Labels specified by the cluster definition and assigned to nodes are defined
    /// here: <see cref="NodeLabels"/>.
    /// </note>
    /// </summary>
    public static class NeonLabel
    {
        /// <summary>
        /// Used to label custom NEONKUBE resources that should be removed by <b>ClusterFixture</b> or
        /// <see cref="ClusterProxy"/> when resetting a test cluster.
        /// </summary>
        public const string RemoveOnClusterReset = ClusterDefinition.ReservedPrefix + "remove-on-cluster-reset";

        /// <summary>
        /// Used to identify which service manages the entity.
        /// </summary>
        public const string ManagedBy = "app.kubernetes.io/managed-by";

        /// <summary>
        /// Specifies the type of node task.
        /// </summary>
        public const string NodeTaskType = ClusterDefinition.ReservedPrefix + "nodetask-type";
    }
}
