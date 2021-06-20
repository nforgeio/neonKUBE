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
    /// neonKUBE release.
    /// </summary>
    public static class KubeVersions
    {
        /// <summary>
        /// The current neonKUBE version.
        /// </summary>
        public const string NeonKubeVersion = "0.1.0-alpha";

        /// <summary>
        /// The version of Kubernetes to be installed.
        /// </summary>
        public const string KubernetesVersion = "1.21.2";

        /// <summary>
        /// The version of the Kubernetes dashboard to be installed.
        /// </summary>
        public const string KubernetesDashboardVersion = "2.1.0";

        /// <summary>
        /// The version of the Kubernetes dashboard metrics scraper to be installed.
        /// </summary>
        public const string KubernetesDashboardMetricsVersion = "v1.0.1";

        /// <summary>
        /// The package version for Kubernetes admin service.
        /// </summary>
        public const string KubeAdminPackageVersion = "1.21.2-00";

        /// <summary>
        /// The package version for the Kubernetes cli.
        /// </summary>
        public const string KubeCtlPackageVersion = "1.21.2-00";

        /// <summary>
        /// The package version for the Kubelet service.
        /// </summary>
        public const string KubeletPackageVersion = "1.21.2-00";

        /// <summary>
        /// The version of CRI-O container runtime to be installed.
        /// </summary>
        public const string CrioVersion = "1.21";

        /// <summary>
        /// The version of Calico to install.
        /// </summary>
        public const string CalicoVersion = "3.16";

        /// <summary>
        /// The version of dnsutils to install.
        /// </summary>
        public const string DnsUtilsVersion = "1.3";

        /// <summary>
        /// The version of HaProxy to install.
        /// </summary>
        public const string HaproxyVersion = "1.9.2-alpine";

        /// <summary>
        /// The version of Istio to install.
        /// </summary>
        public const string IstioVersion = "1.7.6";

        /// <summary>
        /// The version of Helm to be installed.
        /// </summary>
        public const string HelmVersion = "3.3.1";

        /// <summary>
        /// The version of CoreDNS to be installed.
        /// </summary>
        public const string CoreDNSVersion = "1.6.2";

        /// <summary>
        /// The version of CoreDNS plugin to be installed.
        /// </summary>
        public const string CoreDNSPluginVersion = "0.2-istio-1.1";

        /// <summary>
        /// The version of Prometheus to be installed.
        /// </summary>
        public const string PrometheusVersion = "v2.22.1";

        /// <summary>
        /// The version of AlertManager to be installed.
        /// </summary>
        public const string AlertManagerVersion = "v0.21.0";
    }
}
