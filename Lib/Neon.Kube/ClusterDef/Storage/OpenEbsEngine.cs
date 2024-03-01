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
    /// Identifies the OpenEBS storage engines supported by NEONKUBE clusters.
    /// </summary>
    public enum OpenEbsEngine
    {
        /// <summary>
        /// <para>
        /// Lets NEONKUBE select the storage engine based on the cluster size.  <see cref="HostPath"/>
        /// will choosen for single node clusters and <see cref="Jiva"/> will be selected otherwise.
        /// </para>
        /// <para>
        /// You can override this behavior be selecting one of the other engines.
        /// </para>
        /// </summary>
        [EnumMember(Value = "default")]
        Default = 0,

        /// <summary>
        /// Provisions persistent volumes on the host node's disk.  This will result in the
        /// best possible I/O performance but at the cost of requiring that containers mounting
        /// the volume be scheduled on the same node as well as losing volume replication.
        /// </summary>
        [EnumMember(Value = "hostpath")]
        HostPath,

        /// <summary>
        /// The currently recommended OpenEBS storage engine.  This is very feature rich but
        /// requires one or more raw block devices and quite a bit of RAM.  See: 
        /// <a href="https://docs.openebs.io/v090/docs/next/cstor.html">cStor Overview</a>
        /// </summary>
        [EnumMember(Value = "cstor")]
        cStor,

        /// <summary>
        /// <para>
        /// This was the original OpenEBS storage engine and hosts the data in a Linux
        /// sparse file rather than requiring raw block devices.  This is suitable
        /// for smaller clusters running workloads with lower I/O requirements.  See:
        /// <a href="https://docs.openebs.io/v090/docs/next/jiva.html">Jiva Overview</a>
        /// </para>
        /// <note>
        /// Jiva is not currently supported for NEONKUBE clusters.
        /// </note>
        /// </summary>
        [EnumMember(Value = "jiva")]
        Jiva,

        /// <summary>
        /// <para>
        /// This will be [Mayadata's premier storage engine](https://docs.openebs.io/docs/next/mayastor.html) using 
        /// the [NVMe-oF](https://nvmexpress.org/developers/nvme-of-specification/) protocol for accessing data rather
        /// than the old iSCSI protocol which is quite slow.  Mayastor is still in Beta.
        /// </para>
        /// <note>
        /// Mayastor is not currently supported for NEONKUBE clusters.
        /// </note>
        /// </summary>
        [EnumMember(Value = "mayastor")]
        Mayastor
    }
}
