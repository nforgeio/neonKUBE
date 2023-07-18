//-----------------------------------------------------------------------------
// FILE:        KubeVersions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

using Neon.BuildInfo;
using Neon.Common;
using Neon.Net;

namespace Neon.Kube
{
    /// <summary>
    /// Specifies deployment related component versions for the current
    /// NEONKUBE release.  Kubernetes release information can be found here:
    /// https://kubernetes.io/releases/
    /// </summary>
    public static class KubeVersions
    {
        /// <summary>
        /// Returns the name of the branch from which this assembly was built.
        /// </summary>
        public const string BuildBranch = BuildInfo.ThisAssembly.Git.Branch;

        /// <summary>
        /// The current NEONKUBE version.
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
        ///     <term><b>-alpha[.##]</b></term>
        ///     <description>
        ///     <para>
        ///     Used for internal releases that are not meant to be consumed by the
        ///     public.
        ///     </para>
        ///     <para>
        ///     The <b>.##</b> part is optional and can be used when it's necessary to
        ///     retain artifacts like container and node images for multiple pre-releases.  
        ///     This must include two digits so a leading "0" will be required for small numbers.
        ///     </para>
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>-preview[.##]</b></term>
        ///     <description>
        ///     <para>
        ///     This is used for public preview releases where NEONFORGE is not making
        ///     any short or long term support promises.  We may remove, change, or break
        ///     features included in this release for subsequent releases.
        ///     </para>
        ///     <para>
        ///     The <b>.##</b> part is optional and can be used when it's necessary to
        ///     retain artifacts like container and node images for multiple internal
        ///     pre-releases.  This must include two digits so a leading "0" will
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
        ///     features included in this release for subsequent releases.
        ///     </para>
        ///     <para>
        ///     The <b>.##</b> part is optional and can be used when it's necessary to
        ///     retain artifacts like container and node images for multiple pre-releases.
        ///     This must include two digits so a leading "0" will be required for small numbers.
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
        ///     retain artifacts like container and node images for multiple pre-releases.
        ///     This must include two digits so a leading "0" will be required for small numbers.
        ///     </para>
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>NONE</b></term>
        ///     <description>
        ///     Generally available non-preview public releases.
        ///     </description>
        /// </item>
        /// </list>
        /// <para>
        /// The NEONCLOUD stage/publish tools will use this version as is when tagging
        /// container images as well as node/desktop virtual machine images when publishing
        /// <b>Neon.Kube</b> libraries build from a <b>release-*</b> branch.  Otherwise,
        /// the tool will append the branch name to the release like:
        /// </para>
        /// <para>
        /// 0.9.2-alpha.BRANCH
        /// </para>
        /// <note>
        /// <b>IMPORTANT: </b>This convention allows multiple developers to work with their 
        /// own versions of intermediate releases in parallel while avoiding merge conflicts 
        /// caused by differing per-developer version numbers.
        /// </note>
        /// </remarks>
        public const string NeonKube = "0.10.0-beta";

        /// <summary>
        /// Returns the branch part of the NEONKUBE version.  This will be blank for release
        /// branches whose names starts with <b>release-</b> and will be <b>.BRANCH</b> for
        /// all other branches.
        /// </summary>
        public static string BranchPart
        {
            get
            {
                if (BuildBranch.StartsWith("release-"))
                {
                    return string.Empty;
                }
                else
                {
                    return $".{BuildBranch}";
                }
            }
        }

        /// <summary>
        /// Returns the full NEONKUBE release including the <see cref="BranchPart"/>, if any.
        /// </summary>
        public static readonly string NeonKubeWithBranchPart = $"{NeonKube}{BranchPart}";

        /// <summary>
        /// Returns the prefix used for NEONKUBE container tags.
        /// </summary>
        public const string NeonKubeContainerImageTagPrefix = "neonkube-";

        /// <summary>
        /// <para>
        /// Returns the container image tag for the current NEONKUBE release.  This adds the
        /// <b>neonkube-</b> prefix to <see cref="NeonKube"/>.
        /// </para>
        /// <note>
        /// This also includes the <b>.BRANCH</b> part when the assembly was built
        /// from a non-release branch.
        /// </note>
        /// </summary>
        public static string NeonKubeContainerImageTag => $"{NeonKubeContainerImageTagPrefix}{NeonKube}{BranchPart}";

        /// <summary>
        /// The version of Kubernetes to be installed.
        /// </summary>
        public const string Kubernetes = "1.24.0";

        /// <summary>
        /// The version of Kubernetes to be installed, without the patch component.
        /// </summary>
        public const string KubernetesNoPatch = "1.24";

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
        /// The version of the Kubernetes client tools to be installed with NEONDESKTOP.
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
        /// revision/patch properties may differ.
        /// </para>
        /// <para>
        /// Versions can be seen here: https://download.opensuse.org/repositories/devel:/kubic:/libcontainers:/stable:/cri-o:/
        /// Make sure the package has actually been uploaded.
        /// </para>
        /// </note>
        /// </summary>
        public static readonly string Crio = PatchVersion(Kubernetes, 0);

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
        public const string Helm = "3.12.0";

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

        /// <summary>
        /// The minimum supported XenServer/XCP-ng hypervisor host version.
        /// </summary>
        public static readonly SemanticVersion MinXenServerVersion = SemanticVersion.Parse("8.2.0");

        /// <summary>
        /// Static constructor.
        /// </summary>
        static KubeVersions()
        {
            // Ensure that some of the constants are reasonable.

            if (!Kubernetes.StartsWith(KubernetesNoPatch) || KubernetesNoPatch.Count(ch => ch == '.') != 1)
            {
                throw new InvalidDataException($"[KubernetesNoPatch={KubernetesNoPatch}] must be the same as [Kubernetes={Kubernetes}] without the patch part,");
            }
        }

        /// <summary>
        /// Ensures that the XenServer version passed is supported for building
        /// NEONKUBE virtual machines images.  Currently only <b>8.2.*</b> versions
        /// are supported.
        /// </summary>
        /// <param name="version">The XenServer version being checked.</param>
        /// <exception cref="NotSupportedException">Thrown for unsupported versions.</exception>
        public static void CheckXenServerHostVersion(SemanticVersion version)
        {
            if (version.Major != MinXenServerVersion.Major || version.Minor != MinXenServerVersion.Minor)
            {
                throw new NotSupportedException($"XenServer version [{version}] is not supported for building NEONKUBE VM images.  Only versions like [{MinXenServerVersion.Major}.{MinXenServerVersion.Minor}.*] are allowed.");
            }
        }

        /// <summary>
        /// Optionally modifies the patch component of a <see cref="SemanticVersion"/> string.
        /// </summary>
        /// <param name="version">The source <see cref="SemanticVersion"/> string.</param>
        /// <param name="patch">Optionally specifies the new patch commponent.</param>
        /// <returns>The updated version.</returns>
        /// <remarks>
        /// This is used for situations like when the Kubernetes and CRI-O versions differ
        /// by just a patch version.
        /// </remarks>
        private static string PatchVersion(string version, int? patch = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(version), nameof(version));
            Covenant.Requires<ArgumentException>(patch == null || patch >= 0, nameof(patch));

            var semanticVersion = SemanticVersion.Parse(version);

            if (patch == null)
            {
                return version;
            }
            else
            {
                semanticVersion.Patch = patch.Value;

                return semanticVersion.ToString();
            }
        }
    }
}
