//-----------------------------------------------------------------------------
// FILE:	    KubeNodePort.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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

using Neon.Net;

namespace Neon.Kube
{
    /// <summary>
    /// Defines reserved node and cluster network ports.
    /// </summary>
    public static class KubeNodePort
    {
        /// <summary>
        /// The first port reserved by Kubernetes for exposing node ports.
        /// </summary>
        public const int KubeFirstNodePort = 30000;

        /// <summary>
        /// The last port reserved by Kubernetes for exposing node ports.
        /// </summary>
        public const int KubeLastNodePort = 32767;

        /// <summary>
        /// The node port exposed by the Istio Ingress HTTP service.
        /// </summary>
        public const int IstioIngressHttp = 30080;

        /// <summary>
        /// The node port exposed by the Istio Ingress HTTPS service.
        /// </summary>
        public const int IstioIngressHttps = 30443;

        /// <summary>
        /// The port exposed by the Kubernetes API servers on the control-plane nodes.
        /// </summary>
        public const int KubeApiServer = NetworkPorts.KubernetesApiServer;
    }
}
