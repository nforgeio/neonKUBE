//-----------------------------------------------------------------------------
// FILE:        KubeVersions.cs
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Neon.BuildInfo;
using Neon.Common;
using Neon.IO;
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
        //---------------------------------------------------------------------
        // Local types

        /// <summary>
        /// Used to identify <see cref="KubeVersions"/> version constants for
        /// preprocessing Helm charts.
        /// </summary>
        [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
        internal class KubeVersionAttribute : Attribute
        {
        }

        //---------------------------------------------------------------------
        // Implementation

        private static object                       syncLock               = new object();
        private static Dictionary<string, string>   preprocessorDictionary = null;

        /// <summary>
        /// Returns the name of the branch from which this assembly was built.
        /// </summary>
        [KubeVersion]
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
        /// own versions of intermediate releases in parallel while avoiding conflicts with
        /// other developers.
        /// </note>
        /// </remarks>
        [KubeVersion]
        public const string NeonKube = "0.11.0-beta.0";

        /// <summary>
        /// Returns the branch part of the NEONKUBE version.  This will be blank for release
        /// branches whose names starts with <b>release-</b> and will be <b>.BRANCH</b> for
        /// all other branches.
        /// </summary>
        [KubeVersion]
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
        [KubeVersion]
        public static readonly string NeonKubeWithBranchPart = $"{NeonKube}{BranchPart}";

        /// <summary>
        /// Returns the prefix used for NEONKUBE container tags.
        /// </summary>
        [KubeVersion]
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
        [KubeVersion]
        public static string NeonKubeContainerImageTag => $"{NeonKubeContainerImageTagPrefix}{NeonKube}{BranchPart}";

        /// <summary>
        /// Specifies the version of Kubernetes to be installed.
        /// </summary>
        [KubeVersion]
        public const string Kubernetes = "1.29.0";

        /// <summary>
        /// Specifies the version of Kubernetes to be installed, without the patch component.
        /// </summary>
        [KubeVersion]
        public const string KubernetesNoPatch = "1.29";

        /// <summary>
        /// Specifies the version of the GOLANG compiler to use for building Kubernetes
        /// related components like <b>CRI-O</b>.
        /// </summary>
        [KubeVersion]
        public const string GoLang = "1.21.3";

        /// <summary>
        /// Specifies the version of the Kubernetes Dashboard to be installed.
        /// </summary>
        [KubeVersion]
        public const string KubernetesDashboard = "v2.7.0";

        /// <summary>
        /// Specifies the version of the Kubernetes dashboard metrics scraper to be installed.
        /// </summary>
        [KubeVersion]
        public const string KubernetesDashboardMetrics = "v1.0.6";

        /// <summary>
        /// Returns the package version for Kubernetes admin service.
        /// </summary>
        [KubeVersion]
        public const string KubeAdminPackage = Kubernetes + "-00";

        /// <summary>
        /// Specifies the version of the Kubernetes client tools to be installed with NEONDESKTOP.
        /// </summary>
        [KubeVersion]
        public const string Kubectl = Kubernetes;

        /// <summary>
        /// Returns the package version for the Kubernetes cli.
        /// </summary>
        [KubeVersion]
        public const string KubectlPackage = Kubectl + "-00";

        /// <summary>
        /// Returns the package version for the Kubelet service.
        /// </summary>
        [KubeVersion]
        public const string KubeletPackage = Kubernetes + "-00";

        /// <summary>
        /// Returns the package version for the Kubernetes metrics-server service to be installed.
        /// </summary>
        [KubeVersion]
        public const string MetricsServer = "v0.6.4";

        /// <summary>
        /// Returns the package version for the Kubernetes kube-state-metrics service to be installed.
        /// </summary>
        [KubeVersion]
        public const string KubeStateMetrics = "v2.10.1";

        /// <summary>
        /// Returns the package version for the Kubernetes kube-state-metrics service to be installed.
        /// </summary>
        [KubeVersion]
        public const string KubernetesUIMetricsScraper = "v1.0.9";

        /// <summary>
        /// <para>
        /// Specifies the version of CRI-O container runtime to be installed.
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
        [KubeVersion]
        public static readonly string Crio = PatchVersion(Kubernetes, 0);

        /// <summary>
        /// Specifies the version of Podman to be installed.
        /// </summary>
        [KubeVersion]
        public const string Podman = "3.4.4+ds1-1ubuntu1.22.04.2";

        /// <summary>
        /// Specifies the version of Etcd to install.
        /// </summary>
        [KubeVersion]
        public const string Etcd = "3.5.10-0";

        /// <summary>
        /// Specifies the version of Calico to install.
        /// </summary>
        [KubeVersion]
        public const string Calico = "v3.23.5";

        /// <summary>
        /// Specifies the version of Cilium to install.
        /// </summary>
        [KubeVersion]
        public const string Cilium = "v1.14.5";

        /// <summary>
        /// Specifies the version of Cilium-Certgen to be used for
        /// regenerating MTLS certificates.
        /// </summary>
        [KubeVersion]
        public const string CiliumCertGen = "v0.1.9";

        /// <summary>
        /// Specifies the version of Cilium CLI to install.
        /// </summary>
        [KubeVersion]
        public const string CiliumCli = "v0.15.19";

        /// <summary>
        /// Specifies the version of Cilium Envoy to install.
        /// </summary>
        public const string CiliumEnvoy = "v1.26.6-ad82c7c56e88989992fd25d8d67747de865c823b";

        /// <summary>
        /// Specifies the version of the Hubble CLI to install.
        /// </summary>
        [KubeVersion]
        public const string CiliumHubbleCli = "v0.12.3";

        /// <summary>
        /// Specifies the version of Hubble UI to install.
        /// </summary>
        [KubeVersion]
        public const string CiliumHubbleRelay = "v1.14.5";

        /// <summary>
        /// Specifies the version of Hubble UI to install.
        /// </summary>
        [KubeVersion]
        public const string CiliumHubbleUI = "v0.12.1";

        /// <summary>
        /// Specifies the version of Hubble UI Backend to install.
        /// </summary>
        [KubeVersion]
        public const string CiliumHubbleUIBackend = "v0.12.1";

        /// <summary>
        /// Specifies the version of the Cilium generic operator to base our custom image on.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Using the <see cref="Cilium"/> version as the tag for our base image doesn't
        /// work, even though Cilium has an image named with this tag up on quay.io.  The
        /// problem is that the Cilium Helm chart explicitly specifies the entrypoint command
        /// as <b>/usr/bin/cilium-operator-generic</b> but this file isn't actually present
        /// in the image, but <b>/usr/bin/cilium-operator</b> is present.
        /// </para>
        /// <para>
        /// To workaround this, we need to manually set this constant to <b>genericDigest</b>
        /// value from the Cilium Helm chart's <b>values.yaml file</b>, located at:
        /// <c>$\neonKUBE\Lib\Neon.Kube.Setup\Resources\Helm\cilium\values.yaml</c>
        /// </para>
        /// <para>
        /// Open the <c>values.yaml</c> file, search for <b>genericDigest</b> and then
        /// set this constant to the value there.
        /// </para>
        /// <note>
        /// The NEONCLOUD base image script for <b>cilium-operator</b> performs a check
        /// against the Helm chart to ensure that this constant has been set correctly.
        /// </note>
        /// </remarks>
        [KubeVersion]
        public const string CiliumGenericOperatorDigest = "sha256:303f9076bdc73b3fc32aaedee64a14f6f44c8bb08ee9e3956d443021103ebe7a";

        /// <summary>
        /// Specifies the version of dnsutils to install.
        /// </summary>
        [KubeVersion]
        public const string DnsUtils = "1.3";

        /// <summary>
        /// Specifies the version of HaProxy to install.
        /// </summary>
        [KubeVersion]
        public const string Haproxy = "1.9.2-alpine";

        /// <summary>
        /// Specifies the version of Istio to install.
        /// </summary>
        [KubeVersion]
        public const string Istio = "1.14.1-distroless";

        /// <summary>
        /// Specifies the version of Helm to be installed.
        /// </summary>
        [KubeVersion]
        public const string Helm = "3.12.0";

        /// <summary>
        /// Specifies the version of CoreDNS to be installed.
        /// </summary>
        [KubeVersion]
        public const string CoreDNS = "v1.11.1";

        /// <summary>
        /// Specifies the version of CoreDNS plugin to be installed.
        /// </summary>
        [KubeVersion]
        public const string CoreDNSPlugin = "0.2-istio-1.1";

        /// <summary>
        /// Specifies the version of Prometheus to be installed.
        /// </summary>
        [KubeVersion]
        public const string Prometheus = "v2.22.1";

        /// <summary>
        /// Specifies the version of AlertManager to be installed.
        /// </summary>
        [KubeVersion]
        public const string AlertManager = "v0.21.0";

        /// <summary>
        /// Specifies the version of pause image to be installed.
        /// </summary>
        [KubeVersion]
        public const string Pause = "3.9";

        /// <summary>
        /// Specifies the version of busybox image to be installed.
        /// </summary>
        [KubeVersion]
        public const string Busybox = "1.32.0";

        /// <summary>
        /// The minimum supported XenServer/XCP-ng hypervisor host version.
        /// </summary>
        [KubeVersion]
        public static readonly SemanticVersion MinXenServerVersion = SemanticVersion.Parse("8.2.0");

        /// <summary>
        /// Static constructor.
        /// </summary>
        static KubeVersions()
        {
            // Ensure that some of the version constants are reasonable.

            if (!Kubernetes.StartsWith(KubernetesNoPatch) || KubernetesNoPatch.Count(ch => ch == '.') != 1)
            {
                throw new InvalidDataException($"[KubernetesNoPatch={KubernetesNoPatch}] must be the same as [Kubernetes={Kubernetes}] without the patch part,");
            }
        }

        /// <summary>
        /// Constructs a <see cref="PreprocessReader"/> capable of preprocessing version
        /// constants named like <b>$&lt;KubeVersions.VERSION&gt;</b> to the referenced value.
        /// </summary>
        /// <param name="reader">Specifies the <see cref="TextReader"/> with the input text.</param>
        /// <param name="variableRegex">
        /// Optionally specified the regular expression to be used to identify preprocessor
        /// variable references within the preprocessed text.  This defaults to
        /// <see cref="PreprocessReader.DefaultVariableExpansionRegex"/>.
        /// </param>
        /// <returns>The <see cref="PreprocessReader"/>.</returns>
        /// <remarks>
        /// <note>
        /// The constant name name processing will be <b>case-sensitive</b>.
        /// </note>
        /// </remarks>
        public static PreprocessReader CreatePreprocessor(TextReader reader, Regex variableRegex = null)
        {
            Covenant.Requires<ArgumentNullException>(reader != null, nameof(reader));

            // We're going to cache the preprocessor dictionary so we'll only need
            // to reflect the values once.

            lock (syncLock)
            {
                if (preprocessorDictionary == null)
                {
                    preprocessorDictionary = new Dictionary<string, string>();

                    // We need to process version constants, fields, and properties.

                    foreach (var member in typeof(KubeVersions).GetMembers(BindingFlags.Public | BindingFlags.Static))
                    {
                        var versionAttribute = member.GetCustomAttribute<KubeVersionAttribute>();

                        if (versionAttribute == null)
                        {
                            continue;
                        }

                        string value;

                        switch (member.MemberType)
                        {
                            case MemberTypes.Property:

                                value = typeof(KubeVersions).GetProperty(member.Name).GetValue(null).ToString();
                                break;

                            case MemberTypes.Field:

                                var field = (FieldInfo)member;

                                value = field.GetValue(null).ToString();
                                break;

                            default:

                                continue;
                        }

                        preprocessorDictionary.Add($"KubeVersions.{member.Name}", value);
                    }
                }
            }

            var preprocessReader = new PreprocessReader(reader, preprocessorDictionary)
            {
                VariableExpansionRegex = variableRegex ?? PreprocessReader.DefaultVariableExpansionRegex
            };

            preprocessReader.SetYamlMode();

            return preprocessReader;
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
