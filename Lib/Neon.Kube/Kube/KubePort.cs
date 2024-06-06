//-----------------------------------------------------------------------------
// FILE:        KubePort.cs
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Net;

namespace Neon.Kube
{
    /// <summary>
    /// <para>
    /// Defines reserved NeonKUBE node and cluster network ports.
    /// </para>
    /// <note>
    /// These constants are tagged with <see cref="KubeValueAttribute"/> so they can
    /// be referenced directly from Helm charts like: $&lt;KubePort.NeonNodeAgent&gt;
    /// </note>
    /// </summary>
    public static class KubePort
    {
        /// <summary>
        /// Specifies the <b>neon-sso-service</b> port.
        /// </summary>
        [KubeValue]
        public const int NeonSsoService = 4180;

        /// <summary>
        /// Specifies the <b>neon-sso-service</b> Prometheus metrics port.
        /// </summary>
        [KubeValue]
        public const int NeonSsoServiceMetrics = 44180;

        /// <summary>
        /// Specifies the node port exposed by the Istio Ingress HTTP service.
        /// </summary>
        [KubeValue]
        public const int IstioIngressHttp = 30080;

        /// <summary>
        /// Specifies the node port exposed by the Istio Ingress HTTPS service.
        /// </summary>
        [KubeValue]
        public const int IstioIngressHttps = 30443;

        /// <summary>
        /// <para>
        /// Specifies the <b>neon-node-agent</b> network port.
        /// </para>
        /// <note>
        /// <b>IMPORTANT:</b> <b>neon-node-agent</b> runs in the host node's network
        /// namespace, so it's important that this port not conflict any other processes
        /// running on the node.
        /// </note>
        /// </summary>
        [KubeValue]
        public const int NeonNodeAgent = 9000;

        /// <summary>
        /// Specifies the OpenTelemetry OpenCensus/OLTP pot exposed by the Grafana Node Agent.
        /// </summary>
        [KubeValue]
        public const int GrafanaNodeAgentOpenCensus = 4320;

        /// <summary>
        /// Specifies the port exposed by the Kubernetes API servers on the control-plane nodes.
        /// </summary>
        [KubeValue]
        public const int KubeApiServer = NetworkPorts.KubernetesApiServer;

        /// <summary>
        /// Specifies the port exposed by the Mayastor gRPC API server running on storage nodes.
        /// </summary>
        [KubeValue]
        public const int MayastorApi = 10124;

        /// <summary>
        /// Specifies a port exposed by Mayastor as an NVMe TCP target on storage nodes.
        /// </summary>
        [KubeValue]
        public const int MayastorNvme1 = 4421;

        /// <summary>
        /// Specifies a port exposed by Mayastor as an NVMe TCP target on storage nodes.
        /// </summary>
        [KubeValue]
        public const int MayastorNvme2 = 8420;

        /// <summary>
        /// Specifies the first port reserved by NeonKUBE SSO redirects.
        /// </summary>
        [KubeValue]
        public const int KubeFirstSso = 13051;

        /// <summary>
        /// Specifies the last port reserved by NeonKUBE SSO redirects.
        /// </summary>
        [KubeValue]
        public const int KubeLastSso = 13074;

        /// <summary>
        /// Specifies the first port reserved by Kubernetes for exposing node ports.
        /// </summary>
        [KubeValue]
        public const int KubeFirstNodePort = 30000;

        /// <summary>
        /// Specifies the last port reserved by Kubernetes for exposing node ports.
        /// </summary>
        [KubeValue]
        public const int KubeLastNodePort = 32767;
    }
}
