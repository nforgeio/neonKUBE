//-----------------------------------------------------------------------------
// FILE:	    KubeVersions.cs
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

using Neon.Common;
using Neon.Net;

namespace Neon.Kube
{
    /// <summary>
    /// Specifies deployment related component versions for the current
    /// neonKUBE release.  Kubernetes release information can be found here:
    /// https://kubernetes.io/releases/
    /// </summary>
    public static class KubeVersions
    {
        /// <summary>
        /// The current neonKUBE version.
        /// </summary>
        public const string NeonKube = "0.3.0-alpha";

        /// <summary>
        /// Returns the container image tag for the current neonKUBE release.  This adds the
        /// <b>neonkube-</b> prefix to <see cref="NeonKube"/>.
        /// </summary>
        public const string NeonKubeContainerImageTag = "neonkube-" + NeonKube;

        /// <summary>
        /// The version of Kubernetes to be installed.
        /// </summary>
        public const string Kubernetes = "1.21.4";

        /// <summary>
        /// The version of the Kubernetes dashboard to be installed.
        /// </summary>
        public const string KubernetesDashboard = "2.3.1";

        /// <summary>
        /// The version of the Kubernetes dashboard metrics scraper to be installed.
        /// </summary>
        public const string KubernetesDashboardMetrics = "v1.0.6";

        /// <summary>
        /// The package version for Kubernetes admin service.
        /// </summary>
        public const string KubeAdminPackage = Kubernetes + "-00";

        /// <summary>
        /// The version of the Kubernetes client tools to be installed with neonDESKTOP.
        /// </summary>
        public const string Kubectl = Kubernetes;

        /// <summary>
        /// The package version for the Kubernetes cli.
        /// </summary>
        public const string KubectlPackage = Kubectl + "-00";

        /// <summary>
        /// The package version for the Kubelet service.
        /// </summary>
        public const string KubeletPackage = Kubernetes + "-00";

        /// <summary>
        /// <para>
        /// The version of CRI-O container runtime to be installed.
        /// </para>
        /// <para>
        /// Versions can be seen here: https://download.opensuse.org/repositories/devel:/kubic:/libcontainers:/stable:/cri-o:/
        /// Make sure the package has actually been uploaded.
        /// </para>
        /// </summary>
        public const string Crio = Kubernetes;

        /// <summary>
        /// The version of Calico to install.
        /// </summary>
        public const string Calico = "3.16";

        /// <summary>
        /// The version of dnsutils to install.
        /// </summary>
        public const string DnsUtils = "1.3";

        /// <summary>
        /// The version of HaProxy to install.
        /// </summary>
        public const string Haproxy = "1.9.2-alpine";

        /// <summary>
        /// The version of Istio to install.
        /// </summary>
        public const string Istio = "1.11.4";

        /// <summary>
        /// The version of Helm to be installed.
        /// </summary>
        public const string Helm = "3.7.1";

        /// <summary>
        /// The version of Kustomize to be installed.
        /// </summary>
        public const string Kustomize = "4.4.1";

        /// <summary>
        /// The version of CoreDNS to be installed.
        /// </summary>
        public const string CoreDNS = "1.6.2";

        /// <summary>
        /// The version of CoreDNS plugin to be installed.
        /// </summary>
        public const string CoreDNSPlugin = "0.2-istio-1.1";

        /// <summary>
        /// The version of Prometheus to be installed.
        /// </summary>
        public const string Prometheus = "v2.22.1";

        /// <summary>
        /// The version of AlertManager to be installed.
        /// </summary>
        public const string AlertManager = "v0.21.0";

        /// <summary>
        /// The version of pause image to be installed.
        /// </summary>
        public const string Pause = "3.4.1";

        /// <summary>
        /// The version of busybox image to be installed.
        /// </summary>
        public const string Busybox = "1.32.0";
    }
}
