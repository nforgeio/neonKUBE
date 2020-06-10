//-----------------------------------------------------------------------------
// FILE:	    KubeConst.cs
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
    /// Important cluster constants.
    /// </summary>
    public static class KubeConst
    {
        /// <summary>
        /// Identifies the production cluster public Docker registry.
        /// </summary>
        public const string NeonProdRegistry = "nkubeio";

        /// <summary>
        /// Identifies the development cluster public Docker registry.
        /// </summary>
        public const string NeonDevRegistry = "nkubedev";

        /// <summary>
        /// Returns the appropriate public Docker registry to be used for the git branch the
        /// assembly was built from.  This returns <see cref="NeonProdRegistry"/> for release
        /// branches and <see cref="NeonDevRegistry"/> for all other branches.
        /// </summary>
        public static string NeonBranchRegistry => ThisAssembly.Git.Branch.StartsWith("release-", StringComparison.InvariantCultureIgnoreCase) ? NeonProdRegistry : NeonDevRegistry;

        /// <summary>
        /// The default username for component dashboards and management tools (like Ceph and RabbitMQ).
        /// </summary>
        public const string DefaultUsername = "sysadmin";

        /// <summary>
        /// The default password for component dashboards and management tools (like Ceph and RabbitMQ).
        /// </summary>
        public const string DefaultPassword = "password";

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
        /// The root account username baked into the Hyper-V and XenServer cluster
        /// host node virtual machine templates.  This is also used as the username
        /// for hosts provisioned to clouds like Azure, Aws, and Google Cloud. 
        /// </summary>
        public const string DefaulVmTemplateUsername = "sysadmin";

        /// <summary>
        /// The root account password baked into the Hyper-V and XenServer cluster
        /// host node virtual machine templates.  Note that this will not be
        /// used for hosts provisioned on public clouds for security reasons.
        /// </summary>
        public const string DefaulVmTemplatePassword = "sysadmin0000";

        /// <summary>
        /// The maximum number of cluster master nodes.
        /// </summary>
        public const int MaxMasters = 5;

        /// <summary>
        /// The minimum number of cores required by master nodes.
        /// </summary>
        public const int MinMasterCores = 2;

        /// <summary>
        /// The minimum number of cores required by worker nodes.
        /// </summary>
        public const int MinWorkerCores = 2;

        /// <summary>
        /// The minimum RAM (MiB) required for master nodes.
        /// </summary>
        public const int MinMasterRamMiB = 4096;

        /// <summary>
        /// The minimum RAM (MiB) required for worker nodes.
        /// </summary>
        public const int MinWorkerRamMiB = 4096;

        /// <summary>
        /// The minimum required network interface cards for master nodes.
        /// </summary>
        public const int MinMasterNics = 2;

        /// <summary>
        /// The minimum required network interface cards for worker nodes.
        /// </summary>
        public const int MinWorkerNics = 1;

        /// <summary>
        /// Hostname of the Docker public registry.
        /// </summary>
        public const string DockerPublicRegistry = "docker.io";

        /// <summary>
        /// The root Kubernetes context username for provisioned clusters. 
        /// </summary>
        public const string RootUser = "root";

        /// <summary>
        /// The environment variable used for unit testing that indicates
        /// that <see cref="KubeHelper"/> should run in test mode.  The
        /// value will be set to the path of the temporary directory where
        /// the Kubernetes and neonKUBE files will be located.
        /// </summary>
        public const string TestModeFolderVar = "NF_TESTMODE_FOLDER";

        //---------------------------------------------------------------------
        // The following constants define the default network endpoints exposed
        // by the neonKUBE Desktop application.  These can be customized by
        // editing the [KubeClientConfig] file persisted on the client
        // workstation.  I tried to select ports that would be unlikely
        // to conflict with important registrations:
        //
        //      https://en.wikipedia.org/wiki/List_of_TCP_and_UDP_port_numbers

        /// <summary>
        /// The default local network port for the neonKUBE desktop API
        /// used by the <b>neon-cli</b> tool for communicating with
        /// the neonKUBE desktop.
        /// </summary>
        public const int DesktopServicePort = 1058;

        /// <summary>
        /// The default local network port where <b>kubectl proxy</b> will 
        /// listen and forward traffic to the Kubernetes API server.
        /// </summary>
        public const int KubectlProxyPort = 1059;

        /// <summary>
        /// The default local network port used for proxying requests to
        /// the Kubernetes dashboard for the current cluster.
        /// </summary>
        public const int KubeDashboardProxyPort = 1060;

        /// <summary>
        /// The default local network port used for proxying requests to
        /// the Kibana dashboard for the current cluster.
        /// </summary>
        public const int KibanaDashboardProxyPort = 5601;

        /// <summary>
        /// The default local network port used for proxying requests to
        /// the Prometheus dashboard for the current cluster.
        /// </summary>
        public const int PrometheusDashboardProxyPort = 9090;

        /// <summary>
        /// The default local network port used for proxying requests to
        /// the Kiali dashboard for the current cluster.
        /// </summary>
        public const int KialiDashboardProxyPort = 20001;

        /// <summary>
        /// The default local network port used for proxying requests to
        /// the Grafana dashboard for the current cluster.
        /// </summary>
        public const int GrafanaDashboardProxyPort = 3000;

        /// <summary>
        /// The default host machine sysadmin username.
        /// </summary>
        public const string SysAdminUser = "sysadmin";

        /// <summary>
        /// The default host machine sysadmin user ID.
        /// </summary>
        public const int SysAdminUID = 1234;

        /// <summary>
        /// The default host machine sysadmin group.
        /// </summary>
        public const string SysAdminGroup = "sysadmin";

        /// <summary>
        /// The default host machine sysadmin group ID.
        /// </summary>
        public const int SysAdminGID = 1234;

        /// <summary>
        /// The default host machine container username.
        /// </summary>
        public const string ContainerUser = "container";

        /// <summary>
        /// <para>
        /// The default host machine container user ID.
        /// </para>
        /// <note>
        /// This explictly set to the first valid normal Linux user ID to
        /// be compatible with as many Docker images as possible.
        /// </note>
        /// </summary>
        public const int ContainerUID = 1000;

        /// <summary>
        /// The default host machine container group name.
        /// </summary>
        public const string ContainerGroup = "container";

        /// <summary>
        /// <para>
        /// The default host machine container group ID.
        /// </para>
        /// <note>
        /// This explictly set to the first valid normal Linux user ID to
        /// be compatible with as many Docker images as possible.
        /// </note>
        /// </summary>
        public const int ContainerGID = 1000;

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
        /// The primary disk size in bytes for VMs created using the standard neonKUBE
        /// node templates (XenServer and Hyper-V).  This is configured manually
        /// when node templates are periodically created.
        /// </summary>
        public const decimal NodeTemplateDiskSize = 10 * ByteUnits.GibiBytes;
    }
}
