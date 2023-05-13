//-----------------------------------------------------------------------------
// FILE:	    NetworkCni.cs
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
using System.Runtime.Serialization;

namespace Neon.Kube.ClusterDef
{
    /// <summary>
    /// Enumerates the supported of cluster network providers.
    /// </summary>
    public enum NetworkCni
    {
        /// <summary>
        /// The <a href="https://projectcalico.org">Calico</a> network provider.  As of 01/2019, this is probably
        /// the most popular network provider.  This is currently the default provider deployed for a NEONKUBE
        /// but we expect to change this to the <see cref="Istio"/> integrated provider when that is ready.
        /// </summary>
        [EnumMember(Value = "calico")]
        Calico = 0,

        /// <summary>
        /// The <a href="https://istio.io">Istio</a> integrated provider.  This isn't quite ready for prime time
        /// yet but will eventually become the default provider.
        /// </summary>
        [EnumMember(Value = "istio")]
        Istio,
    }
}
