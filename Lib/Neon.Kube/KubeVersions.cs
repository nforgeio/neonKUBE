//-----------------------------------------------------------------------------
// FILE:	    KubeVersions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
        /// The version of Kubernetes to be installed.
        /// </summary>
        public const string KubernetesVersion = "1.18.6";

        /// <summary>
        /// The version of the Kubernetes dashboard to be installed.
        /// </summary>
        public const string KubernetesDashboardVersion = "2.0.0-rc2";

        /// <summary>
        /// The package version for Kubernetes admin service.
        /// </summary>
        public const string KubeAdminPackageVersion = "1.18.6-00";

        /// <summary>
        /// The package version for the Kubernetes cli.
        /// </summary>
        public const string KubeCtlPackageVersion = "1.18.6-00";

        /// <summary>
        /// The package version for the Kubelet service.
        /// </summary>
        public const string KubeletPackageVersion = "1.18.6-00";

        /// <summary>
        /// The version of Docker to be installed.
        /// </summary>
        public const string DockerVersion = "docker.ce-18.06.1";

        /// <summary>
        /// The version of Calico to install.
        /// </summary>
        public const string CalicoVersion = "3.14";

        /// <summary>
        /// The version of Istio to install.
        /// </summary>
        public const string IstioVersion = "1.6.4";

        /// <summary>
        /// The version of Helm to be installed.
        /// </summary>
        public const string HelmVersion = "3.2.4";
    }
}
