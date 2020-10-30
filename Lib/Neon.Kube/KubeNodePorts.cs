//-----------------------------------------------------------------------------
// FILE:	    KubeNodePorts.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Kube
{
    /// <summary>
    /// Defines reserved node and cluster network ports.
    /// </summary>
    public static class KubeNodePorts
    {
        /// <summary>
        /// Port exposed by the Kubernetes API servers on the master nodes.
        /// </summary>
        public const int KubeApiServer = 6443;

        /// <summary>
        /// The first port reserved by Kubernetes for exposing service node ports.
        /// </summary>
        public const int KubeFirstNodePort = 30000;

        /// <summary>
        /// The last port reserved by Kubernetes for exposing service node ports.
        /// </summary>
        public const int KubeLastNodePort = 32767;

        // $todo(jefflill):
        //
        // Remove the [KubeDashboard] definition after we implement
        // the neonKUBE gateway.

        /// <summary>
        /// The node port exposed by the Kubernetes dashboard service.
        /// </summary>
        public const int KubeDashboard = KubeFirstNodePort;
    }
}
