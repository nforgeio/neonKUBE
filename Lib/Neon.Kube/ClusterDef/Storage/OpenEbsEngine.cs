//-----------------------------------------------------------------------------
// FILE:        OpenEbsEngine.cs
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
        /// Selects a reasonable default storage engine for the cluster.  Currently, this will
        /// select <see cref="HostPath"/> for single-node clusters or <see cref="Jiva"/> for
        /// multi-node clusters.
        /// </para>
        /// <note>
        /// These defaults were selected to reduce the storage and RAM required for smaller
        /// clusters, or clusters that don't really require OpenEBS for user workloads.  Larger
        /// clusters that depend on OpenEBS for user workloads should consider configuring
        /// <see cref="cStor"/> instead.
        /// </note>
        /// </summary>
        [EnumMember(Value = "default")]
        Default = 0,

        /// <summary>
        /// A temporary storage engine that will be replaced by <see cref="Jiva"/> once we've
        /// implemented support for that.  This option works only for single node clusters and
        /// will be removed in the near future.  We don't recommend that you reference this 
        /// explicitly in your cluster definitions; use <see cref="Default"/> instead.
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
        /// sparse file rather than requiring raw block devices.  This may be suitable
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
        /// than the old iSCSI protocol which is quite slow.
        /// </para>
        /// <note>
        /// Mayastor is not currently supported for NEONKUBE clusters, but will be supported in the near future.
        /// </note>
        /// </summary>
        [EnumMember(Value = "mayastor")]
        Mayastor
    }
}
