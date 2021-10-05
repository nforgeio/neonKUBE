//-----------------------------------------------------------------------------
// FILE:	    OpenEbsEngine.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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

namespace Neon.Kube
{
    /// <summary>
    /// Identifies the OpenEBS storage engines supported by neonKUBE clusters.
    /// </summary>
    public enum OpenEbsEngine
    {
        /// <summary>
        /// The recommended OpenEBS storage engine.  This is very feature rich but
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
        /// Jiva is not currently supported for neonKUBE clusters.
        /// </note>
        /// </summary>
        [EnumMember(Value = "jiva")]
        Jiva
    }
}
