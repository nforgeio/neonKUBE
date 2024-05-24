//-----------------------------------------------------------------------------
// FILE:        OpenEbsEngine.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
using System.Runtime.Serialization;

namespace Neon.Kube.ClusterDef
{
    /// <summary>
    /// Identifies the OpenEBS storage engines supported by NeonKUBE clusters.
    /// </summary>
    public enum OpenEbsEngine
    {
        /// <summary>
        /// <para>
        /// Selects a reasonable default storage engine for the cluster.  Currently, this 
        /// selects <see cref="HostPath"/> for single-node clusters and <see cref="Mayastor"/>
        /// for multi-node clusters.
        /// </para>
        /// <para>
        /// You can override this behavior be selecting one of the other engines.
        /// </para>
        /// </summary>
        [EnumMember(Value = "default")]
        Default = 0,

        /// <summary>
        /// <para>
        /// Provisions persistent volumes on the host node's disk.  This will result in the
        /// best possible I/O performance but at the cost of requiring that containers mounting
        /// volumes be scheduled on the same node where the volume is provisioned.
        /// </para>
        /// <note>
        /// Volume data is <b>not replicated</b> by this engine.
        /// </note>
        /// </summary>
        [EnumMember(Value = "hostpath")]
        HostPath,

        /// <summary>
        /// <para>
        /// This will be [Mayastor](https://openebs.io/docs/concepts/data-engines/replicated-storage)
        /// using  the [NVMe-oF](https://nvmexpress.org/developers/nvme-of-specification/) protocol
        /// for better performance.
        /// </para>
        /// <note>
        /// <para>
        /// By default, NeonKUBE will select up to three nodes to host the Mayastor disks from
        /// the nodes being deployed for the cluster.  If the cluster has only control-plane
        /// nodes, up to three of those will be selected to host the Mayastor engine along with
        /// the necessary block devices.  If the cluster has worker nodes, NeonKUBE will select
        /// up to three worker nodes for Mayastor and leave the control-plane nodes alone.
        /// </para>
        /// <para>
        /// You can explicitly control which nodes will host Mayastor by setting this for the
        /// node in your cluster definition:
        /// </para>
        /// <code language="yaml">
        /// name: jeff-aws-large
        /// hosting:
        ///   environment: hyperv
        /// nodes:
        ///   control-0:
        ///     role: control-plane
        ///   worker-0:
        ///     role: worker
        ///     systemOpenEbsStorage: true    &lt;--- Hosts Mayastor storage
        /// </code>
        /// </note>
        /// </summary>
        [EnumMember(Value = "mayastor")]
        Mayastor
    }
}
