//-----------------------------------------------------------------------------
// FILE:	    KubeDownloads.cs
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
    /// Kubernetes related component download URIs.
    /// </summary>
    public static class KubeDownloads
    {
        /// <summary>
        /// The <b>kubectl</b> binary download URI for Linux.
        /// </summary>
        public static readonly string KubeCtlLinuxUri = $"https://storage.googleapis.com/kubernetes-release/release/v{KubeVersions.KubernetesVersion}/linux/amd64/kubectl";

        /// <summary>
        /// The <b>kubectl</b> binary download URI for OS/X.
        /// </summary>
        public static readonly string KubeCtlOsxUri = $"https://storage.googleapis.com/kubernetes-release/release/v{KubeVersions.KubernetesVersion}/bin/darwin/amd64/kubectl";

        /// <summary>
        /// The <b>kubectl</b> binary download URI for Windows.
        /// </summary>
        public static readonly string KubeCtlWindowsUri = $"https://storage.googleapis.com/kubernetes-release/release/v{KubeVersions.KubernetesVersion}/bin/windows/amd64/kubectl.exe";

        /// <summary>
        /// The <b>kubeadm</b> binary download URI for Linux.
        /// </summary>
        public static readonly string KubeAdmLinuxUri = $"https://storage.googleapis.com/kubernetes-release/release/v{KubeVersions.KubernetesVersion}/linux/amd64/kubeadm";

        /// <summary>
        /// The <b>kubelet</b> binary download URI for Linux.
        /// </summary>
        public static readonly string KubeletLinuxUri = $"https://storage.googleapis.com/kubernetes-release/release/v{KubeVersions.KubernetesVersion}/linux/amd64/kubelet";

        /// <summary>
        /// The Helm binary URL for Linux.
        /// </summary>
        public static readonly string HelmLinuxUri = $"https://get.helm.sh/helm-v{KubeVersions.HelmVersion}-linux-amd64.tar.gz";

        /// <summary>
        /// The Helm binary URL for OS/X.
        /// </summary>
        public static readonly string HelmOsxUri = $"https://get.helm.sh/helm-v{KubeVersions.HelmVersion}-darwin-amd64.tar.gz";

        /// <summary>
        /// The Helm binary URL for Windows.
        /// </summary>
        public static readonly string HelmWindowsUri = $"https://get.helm.sh/helm-v{KubeVersions.HelmVersion}-windows-amd64.zip";

        /// <summary>
        /// The Calico RBAC rules download (YAML for kubectl).
        /// </summary>
        public static readonly string CalicoRbacYamlUri = $"https://docs.projectcalico.org/v{KubeVersions.CalicoVersion}/getting-started/kubernetes/installation/hosted/rbac-kdd.yaml";

        /// <summary>
        /// The Calico setup download (YAML for kubectl).
        /// </summary>
        public static readonly string CalicoSetupYamlUri = $"https://docs.projectcalico.org/v{KubeVersions.CalicoVersion}/manifests/calico.yaml";

        /// <summary>
        /// The Istio binary URL for Linux.
        /// </summary>
        public static readonly string IstioLinuxUri = $"https://github.com/istio/istio/releases/download/{KubeVersions.IstioVersion}/istioctl-{KubeVersions.IstioVersion}-linux-amd64.tar.gz";

        /// <summary>
        /// <para>
        /// Returns the URI to be used for downloading the prepared neonKUBE virtual machine image 
        /// for the current neonKUBE cluster version.
        /// </para>
        /// <note>
        /// This will return <c>null</c> for cloud and bare metal environments because we don't
        /// download images for those situations.
        /// </note>
        /// </summary>
        /// <param name="hostingEnvironment"></param>
        /// <returns>The dornload URI or <c>null</c>.</returns>
        public static string GetNodeImageUri(HostingEnvironment hostingEnvironment)
        {
            switch (hostingEnvironment)
            {
                case HostingEnvironment.Aws:
                case HostingEnvironment.Azure:
                case HostingEnvironment.Google:

                    return null;

                case HostingEnvironment.HyperV:
                case HostingEnvironment.HyperVLocal:

                    return $"https://neonkube.s3-us-west-2.amazonaws.com/images/hyperv/node/neonkube.{KubeVersions.NeonKubeVersion}.hyperv.vhdx";

                case HostingEnvironment.XenServer:

                    return $"https://neonkube.s3-us-west-2.amazonaws.com/images/xenserver/node/neonkube.{KubeVersions.NeonKubeVersion}.xva";

                case HostingEnvironment.Wsl2:

                    return $"https://neonkube.s3-us-west-2.amazonaws.com/images/wsl2/node/neonkube.{KubeVersions.NeonKubeVersion}.tar";

                default:

                    throw new NotImplementedException();
            }
        }
    }
}
