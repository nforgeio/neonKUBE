//-----------------------------------------------------------------------------
// FILE:	    KubeConst.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using Neon.Net;

namespace Neon.Kube
{
    /// <summary>
    /// Important cluster constants.
    /// </summary>
    public static class KubeConst
    {
        /// <summary>
        /// Timespan used to introduce some random jitter before an operation
        /// is performed.  This is typically used when it's possible that a 
        /// large number of entities will tend to perform an operation at
        /// nearly the same time (e.g. when a message signalling that an
        /// operation should be performed is broadcast to a large number
        /// of listeners.  Components can pass this to <see cref="NeonHelper.PseudoRandomTimespan(TimeSpan)"/>
        /// to obtain a random delay timespan.
        /// </summary>
        public static readonly TimeSpan MaxJitter = TimeSpan.FromMilliseconds(250);

        /// <summary>
        /// The maximum number of cluster control-plane nodes.
        /// </summary>
        public const int MaxControlNodes = 5;

        /// <summary>
        /// The minimum number of cores required by control-plane nodes.
        /// </summary>
        public const int MinControlNodeCores = 4;

        /// <summary>
        /// The minimum number of cores required by worker nodes.
        /// </summary>
        public const int MinWorkerCores = 4;

        /// <summary>
        /// The minimum RAM (MiB) required for control-plane nodes.
        /// </summary>
        public const int MinControlNodeRamMiB = 8192;

        /// <summary>
        /// The minimum RAM (MiB) required for worker nodes.
        /// </summary>
        public const int MinWorkerRamMiB = 8192;

        /// <summary>
        /// The minimum required network interface cards for control-plane nodes.
        /// </summary>
        public const int MinControlNodeNics = 1;

        /// <summary>
        /// The minimum required network interface cards for worker nodes.
        /// </summary>
        public const int MinWorkerNics = 1;

        /// <summary>
        /// The root Kubernetes context username for provisioned clusters. 
        /// </summary>
        public const string RootUser = "root";

        /// <summary>
        /// The default host machine sysadmin username.
        /// </summary>
        public const string SysAdminUser = "sysadmin";

        /// <summary>
        /// The default host machine sysadmin user ID.
        /// </summary>
        public const int SysAdminUID = 1000;

        /// <summary>
        /// The default host machine sysadmin group.
        /// </summary>
        public const string SysAdminGroup = "sysadmin";

        /// <summary>
        /// The default host machine sysadmin group ID.
        /// </summary>
        public const int SysAdminGID = 1000;

        /// <summary>
        /// The default <b>sysadmin</b> account password baked into neonKUBE
        /// base images.  This will be set to a secure password during cluster
        /// provisioning.
        /// </summary>
        public const string SysAdminPassword = "sysadmin0000";

        /// <summary>
        /// <para>
        /// The default name for the local <see cref="k8s.Models.V1StorageClass"/>
        /// </para>
        /// </summary>
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
        public const string LocalVolumePath = "/var/lib/neonkube/volumes";

        /// <summary>
        /// Path to the node image file holding the image type defined by <see cref="KubeImageType"/>.
        /// </summary>
        public const string ImageTypePath = "/etc/neonkube/image-type";

        /// <summary>
        /// Path to the node image file holding the neonKUBE version.
        /// </summary>
        public const string ImageVersionPath = "/etc/neonkube/image-version";

        /// <summary>
        /// The minimum supported XenServer/XCP-ng hypervisor host version.
        /// </summary>
        public static readonly SemanticVersion MinXenServerVersion = SemanticVersion.Parse("8.2.0");

        /// <summary>
        /// The number of IP addresses reserved by cloud deployments at the beginning of the 
        /// node subnet by the cloud provider and also for future neonKUBE features.
        /// This typically includes the cloud default gateway and DNS forwarding IPs as well
        /// as potential future neonKUBE features such as an integrated VPN and perhaps 
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
        /// our prefix and the cluster version number.
        /// </summary>
        public const string NeonKubeImageTag = "neonkube-" + KubeVersions.NeonKube;

        /// <summary>
        /// <para>
        /// The minimum supported cluster node disk size in GiB.
        /// </para>
        /// <note>
        /// This size should match the size of the virtual disks created the base
        /// Hyper-V and XenServer Ubuntu images.
        /// </note>
        /// </summary>
        public const int MinNodeDiskSizeGiB = 32;

        /// <summary>
        /// Returns the URL to the neonKUBE GitHub repository.
        /// </summary>
        public const string KubeGitHubRepoUrl = "https://github.com/nforgeio/neonKUBE";

        /// <summary>
        /// Returns the URL to th neonKUBE help site.
        /// </summary>
        public const string KubeHelpUrl = "https://github.com/nforgeio/neonKUBE";

        /// <summary>
        /// Returns the domain used to configure cluster DNS name that can
        /// be resolved on the cluster nodes to access internal Kubernetes
        /// services like the Harbor registry etc.
        /// </summary>
        public const string ClusterNodeDomain = "neon.local";

        /// <summary>
        /// Hostname used to reference the local Harbor registry within the cluster.
        /// </summary>
        public const string LocalClusterRegistry = "registry.neon.local";

        /// <summary>
        /// User name used to log CRI-O on the cluster nodes into the local
        /// Harbor registry via <b>podman</b>.
        /// </summary>
        public const string HarborCrioUser = "root";    // $todo(jefflill): change this to "neon-harbor-crio" (https://github.com/nforgeio/neonKUBE/issues/1404)

        /// <summary>
        /// Returns the Harbor Project name.
        /// </summary>
        public const string ClusterRegistryProjectName = "neon-internal";

        /// <summary>
        /// Identifies the production neonKUBE container image registry.
        /// </summary>
        public const string NeonKubeProdRegistry = "ghcr.io/neonkube-release";

        /// <summary>
        /// Identifies the development neonKUBE container image registry.
        /// </summary>
        public const string NeonKubeDevRegistry = "ghcr.io/neonkube-dev";

        /// <summary>
        /// Returns the appropriate public container neonKUBE registry to be used for the git 
        /// branch the assembly was built from.  This returns <see cref="NeonKubeProdRegistry"/> for
        /// release branches and <see cref="NeonKubeDevRegistry"/> for all other branches.
        /// </summary>
        public static string NeonKubeBranchRegistry => ThisAssembly.Git.Branch.StartsWith("release-", StringComparison.InvariantCultureIgnoreCase) ? NeonKubeProdRegistry : NeonKubeDevRegistry;

        /// <summary>
        /// Identifies the username of the neon-system-db superuser.
        /// </summary>
        public const string NeonSystemDbAdminUser = "neon_admin";

        /// <summary>
        /// Identifies the secret containing the password for the <see cref="NeonSystemDbAdminUser"/>.
        /// </summary>
        public const string NeonSystemDbAdminSecret = "neon-admin.neon-system-db.credentials.postgresql";

        /// <summary>
        /// Identifies the secret containing Dex credentials.
        /// </summary>
        public const string DexSecret = "neon-sso-dex";

        /// <summary>
        /// Identifies the secret containing Neon SSO Session Proxy credentials.
        /// </summary>
        public const string NeonSsoSessionProxySecret = "neon-sso-session-proxy";

        /// <summary>
        /// Identifies the secret containing Neon SSO Oauth2 Proxy credentials.
        /// </summary>
        public const string NeonSsoOauth2Proxy = "neon-sso-oauth2-proxy";

        /// <summary>
        /// Identifies the neon-system-db superuser database.
        /// </summary>
        public const string NeonClusterOperatorDatabase = "neon_cluster_operator";

        /// <summary>
        /// Identifies the neon-system-db username used by neon services.
        /// </summary>
        public const string NeonSystemDbServiceUser = "neon_service";

        /// <summary>
        /// Identifies the secret containing the password for the <see cref="NeonSystemDbServiceUser"/>.
        /// </summary>
        public const string NeonSystemDbServiceSecret = "neon-service.neon-system-db.credentials.postgresql";

        /// <summary>
        /// Identifies the prefix to be used by the Harbor Operator when creating Harbor related databases in neon-system-db.
        /// </summary>
        public const string NeonSystemDbHarborPrefix = "harbor";

        /// <summary>
        /// Identifies the database name to be used by Grafana.
        /// </summary>
        public const string NeonSystemDbGrafanaDatabase = "grafana";

        /// <summary>
        /// Identifies the the secret containing credentials used by Grafana.
        /// </summary>
        public const string GrafanaSecret = "grafana-secret";

        /// <summary>
        /// Identifies the the secret containing admin credentials for Grafana.
        /// </summary>
        public const string GrafanaAdminSecret = "grafana-admin-credentials";

        /// <summary>
        /// Identifies the secret name where the harbor credentials are stored.
        /// </summary>
        public const string RegistrySecretKey = "registry";

        /// <summary>
        /// Identifies the secret name where the harbor token cert is stored.
        /// </summary>
        public const string RegistryTokenCertSecretKey = "registry-token-cert";

        /// <summary>
        /// Identifies the secret name where the citus credentials are stored.
        /// </summary>
        public const string CitusSecretKey = "citus";

        /// <summary>
        /// Identifies the Kubernetes Job that is deployed to setup Grafana.
        /// </summary>
        public const string NeonJobSetupGrafana = "setup-grafana";

        /// <summary>
        /// Identifies the Kubernetes Job that is deployed to setup Harbor.
        /// </summary>
        public const string NeonJobSetupHarbor = "setup-harbor";

        /// <summary>
        /// Entry storing the last time cluster images were checked.
        /// </summary>
        public const string ClusterImagesLastChecked = "cluster-images-last-checked";

        /// <summary>
        /// The name used by the <see cref="HostingEnvironment.HyperV"/> hosting manager
        /// for creating the internal virtual switch where the neonDESKTOP built-in cluster
        /// as well as user-defined internal clusters will be attached.
        /// </summary>
        public const string HyperVInternalSwitchName = "neon-internal";

        /// <summary>
        /// Identifies the Kubernetes context name for the neon-desktop built-in cluster.
        /// </summary>
        public const string NeonDesktopContextName = $"{RootUser}@neon-desktop";

        /// <summary>
        /// Identifies the Hyper-V virtual machine used to host the neonDESKTOP built-in cluster.
        /// </summary>
        public const string NeonDesktopHyperVBuiltInVmName = "neon-desktop";

        /// <summary>
        /// Specifies the file name to use for the global cluster (non-node) log file.
        /// </summary>
        public const string ClusterLogName = "cluster.log";

        /// <summary>
        /// The maximum size in bytes of a node image part published as a GitHub release.
        /// </summary>
        public const long NodeImagePartSize = (long)(100 * ByteUnits.MebiBytes);

        /// <summary>
        /// Identifies the Kubernetes group where neonKUBE custom resources will be located.
        /// </summary>
        public const string NeonKubeResourceGroup = "neonkube.io";

        /// <summary>
        /// The minimum amount of OS disk on a cluster node after accounting for Minio volumes.
        /// </summary>
        public const string MinimumOsDiskAfterMinio = "40 GiB";

        /// <summary>
        /// The CIR-O socket.
        /// </summary>
        public const string CrioSocketPath = "/var/run/crio/crio.sock";

        /// <summary>
        /// The maximum label length allowed.
        /// </summary>
        public const byte MaxLabelLength = 63;
    }
}
