//-----------------------------------------------------------------------------
// FILE:	    KubeNodePorts.cs
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
    public static class KubeNodePorts
    {
        /// <summary>
        /// The port exposed by the Kubernetes API servers on the master nodes.
        /// </summary>
        public const int KubeApiServer = NetworkPorts.KubernetesApiServer;

        /// <summary>
        /// The first port reserved by Kubernetes for exposing service node ports.
        /// </summary>
        public const int KubeFirstNodePort = 30000;

        /// <summary>
        /// The last port reserved by Kubernetes for exposing service node ports.
        /// </summary>
        public const int KubeLastNodePort = 32767;

        /// <summary>
        /// The node port exposed by the Kubernetes dashboard service.
        /// </summary>
        public const int KubeDashboard = KubeFirstNodePort;

        /// <summary>
        /// The node port exposed by the Grafana dashboard service.
        /// </summary>
        public const int GrafanaDashboard = 30001;

        /// <summary>
        /// The node port exposed by the Harbor dashboard service.
        /// </summary>
        public const int HarborDashboard = 30002;

        /// <summary>
        /// The node port exposed by the Harbor dashboard service.
        /// </summary>
        public const int KialiDashboard = 30005;

        /// <summary>
        /// The node port exposed by the Minio dashboard service.
        /// </summary>
        public const int MinioDashboard = 30006;

        /// <summary>
        /// The node port exposed by the Prometheus dashboard service.
        /// </summary>
        public const int PrometheusDashboard = 30007;
    }
}
