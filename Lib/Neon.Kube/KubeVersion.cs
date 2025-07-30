//-----------------------------------------------------------------------------
// FILE:        KubeVersion.cs
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
    /// <para>
    /// Specifies deployment related component versions for the current
    /// NeonKUBE release.  Kubernetes release information can be found here:
    /// https://kubernetes.io/releases/
    /// </para>
    /// <note>
    /// These constants are tagged with <see cref="KubeValueAttribute"/> so they can
    /// be referenced directly from Helm charts like: $&lt;KubeVersion.Kubernetes&gt;
    /// </note>
    /// </summary>
    public static class KubeVersion
    {
        /// <summary>
        /// Returns the name of the branch from which this assembly was built.
        /// </summary>
        [KubeValue]
        public const string BuildBranch = BuildInfo.ThisAssembly.Git.Branch;

        /// <summary>
        /// The current NeonKUBE version.
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
        /// The NeonCLOUD stage/publish tools will use this version as is when tagging
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
        [KubeValue]
        public const string NeonKube = "0.14.0-alpha.0";

        /// <summary>
        /// Returns the branch part of the NeonKUBE version.  This will be blank for release
        /// branches whose names starts with <b>release-</b> and will be <b>.BRANCH</b> for
        /// all other branches.
        /// </summary>
        [KubeValue]
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
        /// Returns the full NeonKUBE release including the <see cref="BranchPart"/>, if any.
        /// </summary>
        [KubeValue]
        public static readonly string NeonKubeWithBranchPart = $"{NeonKube}{BranchPart}";

        /// <summary>
        /// Returns the prefix used for NeonKUBE container tags.
        /// </summary>
        [KubeValue]
        public const string NeonKubeContainerImageTagPrefix = "neonkube-";

        /// <summary>
        /// <para>
        /// Returns the container image tag for the current NeonKUBE release.  This adds the
        /// <b>neonkube-</b> prefix to <see cref="NeonKube"/>.
        /// </para>
        /// <note>
        /// This also includes the <b>.BRANCH</b> part when the assembly was built
        /// from a non-release branch.
        /// </note>
        /// </summary>
        [KubeValue]
        public static string NeonKubeContainerImageTag => $"{NeonKubeContainerImageTagPrefix}{NeonKube}{BranchPart}";

        /// <summary>
        /// Specifies the version of Kiali to be installed.
        /// </summary>
        [KubeValue]
        public const string Kiali = "v1.79.0";

        /// <summary>
        /// Specifies the version of <b>Kubernetes</b> to be installed, <b>without the patch component</b>.
        /// </summary>
        [KubeValue]
        public const string KubernetesNoPatch = "1.33";

        /// <summary>
        /// Specifies the version of Kubernetes to be installed.
        /// </summary>
        [KubeValue]
        public const string Kubernetes = $"{KubernetesNoPatch}.0";

        /// <summary>
        /// Specifies the version of Kubernetes related container images to be installed.
        /// </summary>
        [KubeValue]
        public const string KubernetesImage = $"v{Kubernetes}";

        /// <summary>
        /// Specifies the package version for Kubernetes admin service.
        /// </summary>
        [KubeValue]
        public const string KubeAdminPackage = Kubernetes + "-00";

        /// <summary>
        /// Specifies the version of the Kubernetes <b>client tool</b> to be installed with NeonDESKTOP.
        /// </summary>
        [KubeValue]
        public const string Kubectl = Kubernetes;

        /// <summary>
        /// Returns the apt package version for the Kubernetes cli.
        /// </summary>
        [KubeValue]
        public const string KubectlPackage = Kubectl + "-00";

        /// <summary>
        /// Returns the apt package version for the Kubelet service.
        /// </summary>
        [KubeValue]
        public const string KubeletPackage = Kubernetes + "-00";

        /// <summary>
        /// Specifies the version of the <b>Kubernetes Dashboard</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string KubernetesDashboard = "v2.7.0";

        /// <summary>
        /// Returns the apt package version for the Kubernetes <b>metrics-server</b> service to be installed.
        /// </summary>
        [KubeValue]
        public const string MetricsServer = "v0.7.0";

        /// <summary>
        /// Returns the package version for the Kubernetes <b>kube-state-metrics</b> service to be installed.
        /// </summary>
        [KubeValue]
        public const string KubeStateMetrics = "v2.10.1";

        /// <summary>
        /// Returns the version for the Kubernetes <b>kube-state-metrics-scraper</b> service to be installed.
        /// </summary>
        [KubeValue]
        public const string KubernetesUIMetricsScraper = "v1.0.9";

        /// <summary>
        /// <para>
        /// Specifies the version of <b>CRI-O container runtime</b> to be installed.
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
        [KubeValue]
        public static readonly string Crio = PatchVersion(Kubernetes, 0);

        /// <summary>
        /// Specifies the version of <b>Podman</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string Podman = "5.3.1+ds1-1ubuntu1.22.04.2";

        /// <summary>
        /// Specifies the version of <b>Etcd</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string Etcd = "3.5.12-0";

        /// <summary>
        /// Specifies the version of <b>Cilium</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string Cilium = "v1.15.6";

        /// <summary>
        /// Specifies the version of Cilium-Certgen to be used for
        /// regenerating MTLS certificates.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Look here for the currently published container tags when upgrading
        /// Cilium: https://quay.io/repository/cilium/certgen?tab=tags
        /// </note>
        /// </remarks>
        [KubeValue]
        public const string CiliumCertGen = "v0.1.13";

        /// <summary>
        /// Specifies the version of <b>Cilium CLI</b> to be installed.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Look here for the currently published container tags when upgrading
        /// Cilium:https://github.com/cilium/cilium-cli/releases
        /// </note>
        /// </remarks>
        [KubeValue]
        public const string CiliumCli = "v0.15.23";

        /// <summary>
        /// Specifies the version of <b>Etcd</b> to be installed for Cilium (when enabled).
        /// </summary>
        /// <remarks>
        /// <note>
        /// Look here for the currently published container tags when upgrading
        /// Cilium: https://github.com/etcd-io/etcd/releases
        /// </note>
        /// </remarks>
        public const string CiliumEtcd = "v3.4.33";

        /// <summary>
        /// Specifies the version of <b>Cilium Envoy</b> to be installed.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Look here for the currently published container tags when upgrading
        /// Cilium: https://istio.io/latest/docs/releases/supported-releases/#supported-envoy-versions
        /// and https://quay.io/repository/cilium/cilium-envoy?tab=tags
        /// </note>
        /// </remarks>
        public const string CiliumEnvoy = "v1.29.5-6e60574aac77c9db6412e0db264bab0593fbe5f7";

        /// <summary>
        /// Specifies the version of <b>Cilium Etcd Operator</b> to be installed.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Look here for the currently published container tags when upgrading
        /// Cilium: https://quay.io/repository/cilium/cilium-etcd-operator?tab=tags
        /// </note>
        /// </remarks>
        public const string CiliumEtcdOperator = "v2.0.7";

        /// <summary>
        /// Specifies the version of <b>Cilium Node Startup Script</b> to be installed.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Look here for the currently published container tags when upgrading
        /// Cilium: https://quay.io/repository/cilium/startup-script?tab=tags
        /// </note>
        /// </remarks>
        [KubeValue]
        public const string CiliumStartupScript = "19fb149fb3d5c7a37d3edfaf10a2be3ab7386661";

        /// <summary>
        /// Specifies the version of <b>dnsutils</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string DnsUtils = "1.3";

        /// <summary>
        /// Specifies the version of <b>Istio</b> to be install installed.
        /// </summary>
        [KubeValue]
        public const string Istio = "1.22.1";

        /// <summary>
        /// Specifies the version of <b>Helm</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string Helm = "3.15.2";

        /// <summary>
        /// Specifies the version of <b>CoreDNS</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string CoreDNS = "v1.11.1";

        /// <summary>
        /// Specifies the version of <b>Prometheus</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string Prometheus = "v2.22.1";

        /// <summary>
        /// Specifies the version of <b>AlertManager</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string AlertManager = "v0.21.0";

        /// <summary>
        /// Specifies the version of <b>pause</b>  to be installed.
        /// </summary>
        [KubeValue]
        public const string Pause = "3.9";

        /// <summary>
        /// Specifies the version of <b>busybox</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string Busybox = "1.36.1";

        /// <summary>
        /// Specifies the version of <b>bitnami-kubectl</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string BitnamiKubectl = "1.27.10";

        /// <summary>
        /// Specifies the version of <b>bitnami-memcached-exporter</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string BitnamiMemcachedExporter = "0.14.2-debian-11-r11";

        /// <summary>
        /// Specifies the version of <b>dexidp-dex</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string DexIdpDex = "v2.32.0";

        /// <summary>
        /// Specifies the version of <b>glauth-plugins</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string Glauth = "v2.1.0-rc1";

        /// <summary>
        /// Specifies the version of <b>grafana (dashboard)</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string Grafana = "9.3.6-ubuntu";

        /// <summary>
        /// Specifies the version of <b>grafana agent</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string GrafanaAgent = "v0.31.3";

        /// <summary>
        /// Specifies the version of <b>grafana agent operator</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string GrafanaAgentOperator = "v0.31.3";

        /// <summary>
        /// Specifies the version of <b>grafana curl</b> to be installed.
        /// This is used to download dashboards.
        /// </summary>
        [KubeValue]
        public const string GrafanaCurl = "7.70.0";

        /// <summary>
        /// Specifies the version of <b>grafana kiwi-gird-sidecar</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string GrafanaKiwiGridSidecar = "0.1.151";

        /// <summary>
        /// Specifies the version of <b>grafana loki</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string GrafanaLoki = "2.7.3";

        /// <summary>
        /// Specifies the version of <b>grafana mimir</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string GrafanaMimir = "2.6.0";

        /// <summary>
        /// Specifies the version of <b>grafana operator</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string GrafanaOperator = "v4.9.0";

        /// <summary>
        /// Specifies the version of <b>grafana operator plug-ins</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string GrafanaOperatorPlugins = "0.0.5";

        /// <summary>
        /// Specifies the version of <b>grafana tempo</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string GrafanaTempo = "2.0.0";

        /// <summary>
        /// Specifies the version of <b>grafana tempo/query</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string GrafanaTempoQuery = "2.0.0";

        /// <summary>
        /// Specifies the version of <b>HAProxy</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string HAProxy = "1.9.2-alpine";

        /// <summary>
        /// Specifies the version of <b>Harbor</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string Harbor = "v2.5.2";

        /// <summary>
        /// Specifies the version of <b>Harbor Operator</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string HarborOperator = "v1.3.0";

        /// <summary>
        /// Specifies the version of <b>jetstack-cert-manager-cainjector</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string JetStackCertManagerCainjector = "v1.8.2";

        /// <summary>
        /// Specifies the version of <b>jetstack-cert-manager-controller</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string JetStackCertManagerController = "v1.8.2";

        /// <summary>
        /// Specifies the version of <b>jetstack-cert-manager-webhook</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string JetStackCertManagerWebhook = "v1.8.2";

        /// <summary>
        /// Specifies the version of <b>jettech-kube-webhook-certgen</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string JetStackKubeWebHookCertGen = "v1.5.2";

        /// <summary>
        /// Specifies the version of <b>k8scsi-csi-attacher</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string K8sCsiAttacher = "v4.5.0";

        /// <summary>
        /// Specifies the version of <b>k8scsi-csi-node-driver-registrar</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string K8sCsiNodeDriverRegistrar = "v2.10.0";

        /// <summary>
        /// Specifies the version of <b>k8scsi-csi-provisioner</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string K8sCsiProvisioner = "v4.0.0";

        /// <summary>
        /// Specifies the version of <b>k8scsi-csi-resizer</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string K8sCsiResizer = "v1.10.0";

        /// <summary>
        /// Specifies the version of <b>k8scsi-csi-snapshotter</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string K8sCsiSnapshotter = "v7.0.1";

        /// <summary>
        /// Specifies the version of <b>k8scsi-snapshot-controller</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string K8sCsiSnapshotController = "v7.0.1";

        /// <summary>
        /// Specifies the version of <b>kubernetes-e2e-test-images-dnsutils</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string KubernetesE2eTestImagesDnsUtils = "1.3";

        /// <summary>
        /// Specifies the version of <b>memcached</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string Memcached = "1.6.23-alpine";

        /// <summary>
        /// Specifies the version of <b>minio</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string Minio = "RELEASE.2022-07-04T21-02-54Z";

        /// <summary>
        /// Specifies the version of <b>minio-console</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string MinioConsole = "v0.19.0";

        /// <summary>
        /// Specifies the version of <b>minio-operator</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string MinioOperator = "v4.4.25";

        /// <summary>
        /// Specifies the version of <b>node-problem-detector</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string NodeProblemDetector = "v0.8.10";

        /// <summary>
        /// Specifies the version of <b>oauth2-proxy</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string Oauth2Proxy = "v7.4.0";

        /// <summary>
        /// Specifies the version of <b>oliver006-redis_exporter</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string OliverRedisExporter = "v1.13.1";

        /// <summary>
        /// Specifies the version of <b>OpenEBS</b> plugin to be installed.
        /// </summary>
        [KubeValue]
        public const string OpenEbs = "4.0.0";

        /// <summary>
        /// Specifies the version of <b>OpenEBS Mayastor</b> plugin to be installed.
        /// </summary>
        [KubeValue]
        public const string OpenEbsMayastor = "v2.6.1";

        /// <summary>
        /// Specifies the version of <b>OpenEBS HostPath Driver</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string OpenEbsHostPathDriver = "4.0.0";

        /// <summary>
        /// Specifies the version of <b>OpenEBS LVM Driver</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string OpenEbsLvmDriver = "1.5.0";

        /// <summary>
        /// Specifies the version of <b>OpenEBS LVM Driver</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string OpenEbsZfsDriver = "2.5.0";

        /// <summary>
        /// Specifies the version of <b>prom-blackbox-exporter</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string PromBlackboxExporter = "v0.21.1";

        /// <summary>
        /// Specifies the version of <b>prometheuscommunity-postgres-exporter</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string PromPostgresExporter = "v0.10.0";

        /// <summary>
        /// Specifies the version of <b>prometheus-operator-prometheus-config-reloader</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string PrometheusConfigReloader = "v0.47.0";

        /// <summary>
        /// Specifies the version of <b>Redis</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string Redis = "7.2.4-alpine";

        /// <summary>
        /// Specifies the version of <b>stakater-reloader</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string StakaterReloader = "v0.0.95";

        /// <summary>
        /// Specifies the Ubuntu version to use for the base <b>ubuntu-layer</b>.
        /// </summary>
        [KubeValue]
        public const string UbuntuLayer = "ubuntu@sha256:4e4bc990609ed865e07afc8427c30ffdddca5153fd4e82c20d8f0783a291e241";

        /// <summary>
        /// Specifies the version of <b>zalando-acid-pgbouncer</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string ZalandoAcidPgBouncer = "master-27";

        /// <summary>
        /// Specifies the version of <b>zalando-logical-backup</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string ZalandoLogicalBackup = "v1.10.0";

        /// <summary>
        /// Specifies the version of <b>zalando-postgres-health-check</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string ZalandoPostgresHeatlhCheck = "v1";

        /// <summary>
        /// Specifies the version of <b>zalando-postgres-operator</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string ZalandoPostgresOperator = "v1.10.1";

        /// <summary>
        /// Specifies the version of <b>zalando-spilo</b> to be installed.
        /// </summary>
        [KubeValue]
        public const string ZalandoSpilo = "3.0-p1";

        /// <summary>
        /// Specifies the version of the GOLANG compiler to use for building Kubernetes
        /// related components like <b>CRI-O</b>.
        /// </summary>
        [KubeValue]
        public const string GoLang = "1.23.3";

        /// <summary>
        /// The minimum supported XenServer/XCP-ng hypervisor host version.
        /// </summary>
        [KubeValue]
        public static readonly SemanticVersion MinXenServerVersion = SemanticVersion.Parse("8.2.0");

        /// <summary>
        /// Static constructor.
        /// </summary>
        static KubeVersion()
        {
            // Ensure that some of the version constants are reasonable.

            if (!Kubernetes.StartsWith(KubernetesNoPatch) || KubernetesNoPatch.Count(ch => ch == '.') != 1)
            {
                throw new InvalidDataException($"[KubernetesNoPatch={KubernetesNoPatch}] must be the same as [Kubernetes={Kubernetes}] without the patch part,");
            }
        }

        /// <summary>
        /// Ensures that the XenServer version passed is supported for building
        /// NeonKUBE virtual machines images.  Currently only <b>8.2.*</b> versions
        /// are supported.
        /// </summary>
        /// <param name="version">The XenServer version being checked.</param>
        /// <exception cref="NotSupportedException">Thrown for unsupported versions.</exception>
        public static void CheckXenServerHostVersion(SemanticVersion version)
        {
            if (version.Major != MinXenServerVersion.Major || version.Minor != MinXenServerVersion.Minor)
            {
                throw new NotSupportedException($"XenServer version [{version}] is not supported for building NeonKUBE VM images.  Only versions like [{MinXenServerVersion.Major}.{MinXenServerVersion.Minor}.*] are allowed.");
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
