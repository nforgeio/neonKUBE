//-----------------------------------------------------------------------------
// FILE:        KubeConst.cs
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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using k8s.Models;

using Neon.Collections;
using Neon.Common;
using Neon.Kube.BuildInfo;
using Neon.Kube.ClusterDef;
using Neon.Net;

namespace Neon.Kube
{
    /// <summary>
    /// <para>
    /// Important cluster constants.
    /// </para>
    /// <note>
    /// Many of these constants are tagged with <see cref="KubeValueAttribute"/> so they can
    /// be referenced directly from Helm charts like: $&lt;KubeConst.LocalClusterRegistryHostName&gt;
    /// </note>
    /// </summary>
    public static class KubeConst
    {
        /// <summary>
        /// Timespan used to introduce some random jitter before an operation
        /// is performed.  This is typically used when it's possible that a 
        /// large number of entities will tend to perform an operation at
        /// nearly the same time (e.g. when a message signalling that an
        /// operation should be performed is broadcast to a large number
        /// of listeners).  Components can pass this to <see cref="NeonHelper.PseudoRandomTimespan(TimeSpan)"/>
        /// to obtain a random delay timespan.
        /// </summary>
        public static readonly TimeSpan MaxJitter = TimeSpan.FromMilliseconds(250);

        /// <summary>
        /// The maximum number of cluster control-plane nodes.
        /// </summary>
        public const int MaxControlPlaneNodes = 5;

        /// <summary>
        /// The minimum number of vCPUs required by control-plane nodes.
        /// </summary>
        public const int MinControlNodeVCpus = 2;

        /// <summary>
        /// The minimum number of vCPUs required by worker nodes.
        /// </summary>
        public const int MinWorkerNodeVCpus = 4;

        /// <summary>
        /// The minimum RAM (MiB) required for control-plane nodes.
        /// </summary>
        public const int MinControlPlaneNodeRamMiB = 8192;

        /// <summary>
        /// The minimum RAM (MiB) required for worker nodes.
        /// </summary>
        public const int MinWorkerNodeRamMiB = 8192;

        /// <summary>
        /// The minimum required network interface cards for control-plane nodes.
        /// </summary>
        public const int MinControlPlaneNodeNics = 1;

        /// <summary>
        /// The minimum required network interface cards for worker nodes.
        /// </summary>
        public const int MinWorkerNodeNics = 1;

        /// <summary>
        /// The root Kubernetes context username for provisioned clusters. 
        /// </summary>
        [KubeValue]
        public const string RootUser = "root";

        /// <summary>
        /// <para>
        /// The fixed SSO password for desktop clusters.
        /// </para>
        /// <note>
        /// This isn't really a security risk because the desktop cluster cannot be
        /// reached from outside the computer because the cluster IP address is not
        /// routable.
        /// </note>
        /// </summary>
        [KubeValue]
        public const string RootDesktopPassword = "root";

        /// <summary>
        /// The NEONKUBE domain used to host NEONKUBE cluster DNS records.
        /// </summary>
        [KubeValue]
        public const string NeonClusterDomain = "neoncluster.io";

        /// <summary>
        /// The fixed ID for all desktop clusters.
        /// </summary>
        public const string DesktopClusterId = $"desktop";

        /// <summary>
        /// The fixed domain for all desktop clusters.
        /// </summary>
        public const string DesktopClusterDomain = $"{DesktopClusterId}.{NeonClusterDomain}";

        /// <summary>
        /// The default host machine sysadmin username.
        /// </summary>
        [KubeValue]
        public const string SysAdminUser = "sysadmin";

        /// <summary>
        /// The default host machine sysadmin user ID.
        /// </summary>
        [KubeValue]
        public const int SysAdminUID = 1000;

        /// <summary>
        /// The default host machine sysadmin group.
        /// </summary>
        [KubeValue]
        public const string SysAdminGroup = "sysadmin";

        /// <summary>
        /// The default host machine sysadmin group ID.
        /// </summary>
        [KubeValue]
        public const int SysAdminGID = 1000;

        /// <summary>
        /// The default <b>sysadmin</b> account password baked into NEONKUBE
        /// base images.  This will generally be changed to a secure password 
        /// during cluster provisioning.
        /// </summary>
        [KubeValue]
        public const string SysAdminPassword = "sysadmin0000";

        /// <summary>
        /// <b>$/etc/hosts</b> section name used by NEONKUBE applications for persisting
        /// DNS host entries via <see cref="NetHelper.ModifyLocalHosts(string, Dictionary{string, System.Net.IPAddress})"/>.
        /// </summary>
        public const string EtcHostsSectionName = "Added for NEONKUBE";

        /// <summary>
        /// <para>
        /// The default name for the local <see cref="k8s.Models.V1StorageClass"/>
        /// </para>
        /// </summary>
        [KubeValue]
        public const string LocalStorageClassName = "local-storage";

        /// <summary>
        /// <para>
        /// The default path for the <see cref="LocalStorageClassName"/>
        /// </para>
        /// <note>
        /// This is temporary, once Kubernetes supports dynamic provisioning of local storage volumes, we'll use
        /// that instead.
        /// </note>
        /// </summary>
        [KubeValue]
        public const string LocalVolumePath = "/var/lib/neonkube/volumes";

        /// <summary>
        /// Path to the node image file holding the image type defined by <see cref="KubeImageType"/>.
        /// </summary>
        public const string ImageTypePath = "/etc/neonkube/image-type";

        /// <summary>
        /// Path to the node file holding the NEONKUBE version.
        /// </summary>
        public const string ImageVersionPath = "/etc/neonkube/image-version";

        /// <summary>
        /// Path to the node file indicating whether the node hosts a pre-built 
        /// desktop cluster.
        /// </summary>
        public const string ImagePrebuiltDesktopPath = "/etc/neonkube/prebuilt-desktop";

        /// <summary>
        /// The number of IP addresses reserved by cloud deployments at the beginning of the 
        /// node subnet by the cloud provider and also for future NEONKUBE features.
        /// This typically includes the cloud default gateway and DNS forwarding IPs as well
        /// as potential future NEONKUBE features such as an integrated VPN and perhaps 
        /// management VMs.
        /// </summary>
        public const int CloudSubnetStartReservedIPs = 10;

        /// <summary>
        /// The number of IP addresses reserved by cloud deployments at the end of the node
        /// subnet by the cloud provider.  This typically includes the network UDP broadcast
        /// address.
        /// </summary>
        public const int CloudSubnetEndReservedIPs = 1;

        /// <summary>
        /// Default subnet for Kubernetes pods.
        /// </summary>
        public const string DefaultPodSubnet = "10.254.0.0/16";

        /// <summary>
        /// Default subnet for Kubernetes services.
        /// </summary>
        public const string DefaultServiceSubnet = "10.253.0.0/16";

        /// <summary>
        /// The container image tag used to reference cluster container images tagged 
        /// with our prefix and the cluster version number.
        /// </summary>
        [KubeValue]
        public const string NeonKubeImageTag = "neonkube-" + KubeVersion.NeonKube;

        /// <summary>
        /// The size of the OS disk used for base images.
        /// </summary>
        public const int BaseDiskSizeGiB = 10;

        /// <summary>
        /// <para>
        /// The minimum supported cluster node disk size in GiB.
        /// </para>
        /// <note>
        /// This size should match the size of the virtual disks created the base
        /// Hyper-V and XenServer Ubuntu images.
        /// </note>
        /// </summary>
        public const int MinNodeDiskSizeGiB = 48;

        /// <summary>
        /// The maximum support cluster node disk size in GiB.
        /// </summary>
        public const int MaxNodeDiskSizeGiB = 16 * 1024;

        /// <summary>
        /// Returns the URL to the NEONKUBE GitHub repository.
        /// </summary>
        public const string KubeGitHubRepoUrl = "https://github.com/nforgeio/NEONKUBE";

        /// <summary>
        /// Returns the URL to th NEONKUBE help site.
        /// </summary>
        public const string KubeHelpUrl = "https://github.com/nforgeio/NEONKUBE";

        /// <summary>
        /// Returns the domain used to configure cluster DNS names that can
        /// be resolved on the cluster nodes to access internal Kubernetes
        /// services like the Harbor registry etc.
        /// </summary>
        [KubeValue]
        public const string ClusterNodeDomain = "neon.local";

        /// <summary>
        /// Hostname used to reference the local Harbor registry within the cluster.
        /// </summary>
        [KubeValue]
        public const string LocalClusterRegistryHostName = $"registry.{ClusterNodeDomain}";

        /// <summary>
        /// The local cluster registry project.
        /// </summary>
        [KubeValue]
        public const string LocalClusterRegistryProject = "neonkube";

        /// <summary>
        /// Hostname used to reference the local Harbor registry within the cluster.
        /// </summary>
        [KubeValue]
        public const string LocalClusterRegistry = $"{LocalClusterRegistryHostName}/{LocalClusterRegistryProject}";

        /// <summary>
        /// User name used to log CRI-O on the cluster nodes into the local
        /// Harbor registry via <b>podman</b>.
        /// </summary>
        [KubeValue]
        public const string HarborCrioUser = "root";    // $todo(jefflill): change this to "neon-harbor-crio" (https://github.com/nforgeio/neonKUBE/issues/1404)

        /// <summary>
        /// Returns the Harbor Project name.
        /// </summary>
        [KubeValue]
        public const string ClusterRegistryProjectName = "neon-internal";

        /// <summary>
        /// Identifies the GitHub organization where we host released NEONKUBE container images.
        /// </summary>
        [KubeValue]
        public const string NeonKubeReleaseOrganization = "neonkube-release";

        /// <summary>
        /// Identifies the GitHub organization where we host staged NEONKUBE container images.
        /// </summary>
        [KubeValue]
        public const string NeonKubeStageOrganization = "neonkube-stage";

        /// <summary>
        /// Identifies the NEONKUBE release container image registry.
        /// </summary>
        [KubeValue]
        public const string NeonKubeReleaseRegistry = $"ghcr.io/{NeonKubeReleaseOrganization}";

        /// <summary>
        /// Identifies the NEONKUBE stage container image registry.
        /// </summary>
        [KubeValue]
        public const string NeonKubeStageRegistry = $"ghcr.io/{NeonKubeStageOrganization}";

        /// <summary>
        /// Returns the appropriate public container NEONKUBE registry to be used for the git 
        /// branch the assembly was built from.  This returns <see cref="NeonKubeReleaseRegistry"/> for
        /// release branches and <see cref="NeonKubeStageRegistry"/> for all other branches.
        /// </summary>
        [KubeValue]
        public static string NeonKubeBranchRegistry => ThisAssembly.Git.Branch.StartsWith("release-", StringComparison.InvariantCultureIgnoreCase) ? NeonKubeReleaseRegistry : NeonKubeStageRegistry;

        /// <summary>
        /// Identifies the username of the neon-system-db superuser.
        /// </summary>
        [KubeValue]
        public const string NeonSystemDbAdminUser = "neon_admin";

        /// <summary>
        /// Identifies the secret containing the password for the <see cref="NeonSystemDbAdminUser"/>.
        /// </summary>
        [KubeValue]
        public const string NeonSystemDbAdminSecret = "neon-admin.neon-system-db.credentials.postgresql";

        /// <summary>
        /// Identifies the secret containing Dex credentials.
        /// </summary>
        [KubeValue]
        public const string DexSecret = "neon-sso-dex";

        /// <summary>
        /// Identifies the secret containing Neon SSO Session Proxy credentials.
        /// </summary>
        [KubeValue]
        public const string NeonSsoSessionProxySecret = "neon-sso-session-proxy";

        /// <summary>
        /// Identifies the secret containing Neon SSO Oauth2 Proxy credentials.
        /// </summary>
        [KubeValue]
        public const string NeonSsoOauth2Proxy = "neon-sso-oauth2-proxy";

        /// <summary>
        /// Identifies the neon-system-db superuser database.
        /// </summary>
        [KubeValue]
        public const string NeonClusterOperatorDatabase = "neon_cluster_operator";

        /// <summary>
        /// Identifies the neon-system-db username used by neon services.
        /// </summary>
        [KubeValue]
        public const string NeonSystemDbServiceUser = "neon_service";

        /// <summary>
        /// Identifies the secret containing the password for the <see cref="NeonSystemDbServiceUser"/>.
        /// </summary>
        [KubeValue]
        public const string NeonSystemDbServiceSecret = "neon-service.neon-system-db.credentials.postgresql";

        /// <summary>
        /// Identifies the prefix to be used by the Harbor Operator when creating Harbor related databases in neon-system-db.
        /// </summary>
        [KubeValue]
        public const string NeonSystemDbHarborPrefix = "harbor";

        /// <summary>
        /// Identifies the database name to be used by Grafana.
        /// </summary>
        [KubeValue]
        public const string NeonSystemDbGrafanaDatabase = "grafana";

        /// <summary>
        /// Identifies the the secret containing credentials used by Grafana.
        /// </summary>
        [KubeValue]
        public const string GrafanaSecret = "grafana-secret";

        /// <summary>
        /// Identifies the the secret containing admin credentials for Grafana.
        /// </summary>
        [KubeValue]
        public const string GrafanaAdminSecret = "grafana-admin-credentials";

        /// <summary>
        /// Identifies the secret name where the harbor credentials are stored.
        /// </summary>
        [KubeValue]
        public const string RegistrySecretKey = "registry";

        /// <summary>
        /// Identifies the secret name where the harbor token cert is stored.
        /// </summary>
        [KubeValue]
        public const string RegistryTokenCertSecretKey = "registry-token-cert";

        /// <summary>
        /// Identifies the secret name where the citus credentials are stored.
        /// </summary>
        [KubeValue]
        public const string CitusSecretKey = "citus";

        /// <summary>
        /// Identifies the Kubernetes Job that is deployed to setup Grafana.
        /// </summary>
        [KubeValue]
        public const string NeonJobSetupGrafana = "setup-grafana";

        /// <summary>
        /// Identifies the Kubernetes Job that is deployed to setup Harbor.
        /// </summary>
        [KubeValue]
        public const string NeonJobSetupHarbor = "setup-harbor";

        /// <summary>
        /// The name used by the <see cref="HostingEnvironment.HyperV"/> hosting manager
        /// for creating the internal virtual switch where the NEONDESKTOP cluster
        /// as well as user-defined internal clusters will be attached.
        /// </summary>
        public const string HyperVInternalSwitchName = "neon-internal";

        /// <summary>
        /// The NEONDESKTOP cluster name.
        /// </summary>
        public const string NeonDesktopClusterName = "neon-desktop";

        /// <summary>
        /// Identifies the Kubernetes context name for the NEONDESKTOP cluster.
        /// </summary>
        public const string NeonDesktopContextName = $"{RootUser}@{NeonDesktopClusterName}";

        /// <summary>
        /// Identifies the Hyper-V virtual machine used to host the NEONDESKTOP cluster.
        /// </summary>
        public const string NeonDesktopHyperVVmName = "neon-desktop";

        /// <summary>
        /// Specifies the file name to use for the global cluster (non-node) log file.
        /// </summary>
        public const string ClusterLogName = "cluster.log";

        /// <summary>
        /// The maximum size in bytes of a node image part published as a GitHub release.
        /// </summary>
        public const long NodeImagePartSize = (long)(100 * ByteUnits.MebiBytes);

        /// <summary>
        /// Identifies the Kubernetes group where NEONKUBE custom resources will be located.
        /// </summary>
        [KubeValue]
        public const string NeonKubeResourceGroup = "neonkube.io";

        /// <summary>
        /// The minimum amount of OS disk on a cluster node after accounting for Minio volumes.
        /// </summary>
        public const string MinimumOsDiskAfterMinio = "40 GiB";

        /// <summary>
        /// The CIR-O socket.
        /// </summary>
        [KubeValue]
        public const string CrioSocketPath = "/var/run/crio/crio.sock";

        /// <summary>
        /// The maximum label length allowed.
        /// </summary>
        public const byte MaxLabelLength = 63;

        /// <summary>
        /// Returns the remote path on the virtual machine where the packed container
        /// images file will be uploaded when creating a node image.
        /// </summary>
        public const string RemoteNodePackedImagePath = "/tmp/container-images.tar.gz";

        /// <summary>
        /// Returns the cluster wide crio config name.
        /// </summary>
        [KubeValue]
        public const string ClusterCrioConfigName = "cluster";

        /// <summary>
        /// Neon SSO client ID.
        /// </summary>
        [KubeValue]
        public const string NeonSsoClientId = "neon-sso";

        /// <summary>
        /// Neon SSO Public client ID.
        /// </summary>
        [KubeValue]
        public const string NeonSsoPublicClientId = "neon-sso-public";

        /// <summary>
        /// Returns the fully qualified path to our <b>safe-apt-get</b> script
        /// that wraps the <b>apt-get</b> tool to handle situations where the
        /// package manager is already busy performing another operation, such
        /// as checking for daily updates.
        /// </summary>
        public const string SafeAptGetToolPath = $"{KubeNodeFolder.Bin}/safe-apt-get";
    }
}
