//-----------------------------------------------------------------------------
// FILE:	    KubeVersions.cs
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
using System.Collections.Generic;
using System.Diagnostics.Contracts;
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
        /// <remarks>
        /// <para><b>RELEASE CONVENTIONS:</b></para>
        /// <para>
        /// We're going to use this version to help manage public releases as well as
        /// to help us isolate development changes made by individual developers or 
        /// by multiple developers colloborating on common features.
        /// </para>
        /// <list type="table">
        /// <item>
        ///     <term><b>-alpha[.FEATURE][.##]</b></term>
        ///     <description>
        ///     <para>
        ///     Used for internal releases that are not meant to be consumed by the
        ///     public.  FEATURE optionally specifies the feature being worked on and
        ///     is typically set the name of the feature as specified in the GitHub
        ///     feature branch; i.e. <b>FOO</b> from the feature branch <b>feature-FOO</b>.
        ///     </para>
        ///     <para>
        ///     Individual developers may also set <b>FEATURE</b> to one of their private
        ///     branches while work is in progress.
        ///     </para>
        ///     <para>
        ///     The <b>.##</b> part is optional and can be used when it's necessary to
        ///     retain artifacts like container and node images for multiple internal
        ///     feature releases.  This must include two digits so a leading "0" will
        ///     be required for small numbers.
        ///     </para>
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>-master</b></term>
        ///     <description>
        ///     This is used for code in the <b>master</b> branch to avoid overwriting already published node/desktop images.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>-preview[.##]</b></term>
        ///     <description>
        ///     <para>
        ///     This is used for public preview releases where NEONFORGE is not making
        ///     any short or long term support promises.  We may remove, change, or break
        ///     features included in this release in subsequent releases.
        ///     </para>
        ///     <para>
        ///     The <b>.##</b> part is optional and can be used when it's necessary to
        ///     retain artifacts like container and node images for multiple internal
        ///     feature releases.  This must include two digits so a leading "0" will
        ///     be required for small numbers.
        ///     </para>
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>-preview[.##]</b></term>
        ///     <description>
        ///     <para>
        ///     This is used for public preview releases where NEONFORGE is not making
        ///     any short or long term support promises.  We may remove, change, or break
        ///     features included in this release in subsequent releases.
        ///     </para>
        ///     <para>
        ///     The <b>.##</b> part is optional and can be used when it's necessary to
        ///     retain artifacts like container and node images for multiple internal
        ///     feature releases.  This must include two digits so a leading "0" will
        ///     be required for small numbers.
        ///     </para>
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>-rc[.##]</b></term>
        ///     <description>
        ///     <para>
        ///     This is used for public release candidate releases.  For these releases,
        ///     NEONFORGE is still not making any short or long term support promises, but
        ///     we're going to try a lot harder to avoid future incompatibilities.  RC
        ///     release will typically be feature complete and reasonably well tested.
        ///     </para>
        ///     <para>
        ///     The <b>.##</b> part is optional and can be used when it's necessary to
        ///     retain artifacts like container and node images for multiple internal
        ///     feature releases.  This must include two digits so a leading "0" will
        ///     be required for small numbers.
        ///     </para>
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>NONE</b></term>
        ///     <description>
        ///     Generally available public releases do not include a prerelease part in
        ///     their semantic version.
        ///     </description>
        /// </item>
        /// </list>
        /// </remarks>
        public const string NeonKube = "0.8.6-alpha";

        /// <summary>
        /// Returns the prefix used for neonKUBE container tags.
        /// </summary>
        public const string NeonKubeContainerImageTagPrefix = "neonkube-";

        /// <summary>
        /// Returns the container image tag for the current neonKUBE release.  This adds the
        /// <b>neonkube-</b> prefix to <see cref="NeonKube"/>.
        /// </summary>
        public const string NeonKubeContainerImageTag = NeonKubeContainerImageTagPrefix + NeonKube;

        /// <summary>
        /// The version of Kubernetes to be installed.
        /// </summary>
        public const string Kubernetes = "1.24.0";

        /// <summary>
        /// The version of the Kubernetes dashboard to be installed.
        /// </summary>
        public const string KubernetesDashboard = "2.5.1";

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
        /// <note>
        /// <para>
        /// CRI-O is tied to specific Kubernetes releases and the CRI-O major and minor
        /// versions must match the Kubernetes major and minor version numbers.  The 
        /// revision/patch properties may be different.
        /// </para>
        /// <para>
        /// Versions can be seen here: https://download.opensuse.org/repositories/devel:/kubic:/libcontainers:/stable:/cri-o:/
        /// Make sure the package has actually been uploaded.
        /// </para>
        /// </note>
        /// </summary>
        public const string Crio = Kubernetes;

        /// <summary>
        /// The version of Podman to be installed.
        /// </summary>
        public const string Podman = "3.4.2";

        /// <summary>
        /// The version of Calico to install.
        /// </summary>
        public const string Calico = "3.22.2";

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
        public const string Istio = "1.14.1";

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
        public const string Pause = "3.7";

        /// <summary>
        /// The version of busybox image to be installed.
        /// </summary>
        public const string Busybox = "1.32.0";
    }
}
