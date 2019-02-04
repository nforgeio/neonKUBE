//-----------------------------------------------------------------------------
// FILE:	    KubeConst.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
        /// of listeners.  Components can pass this to <see cref="NeonHelper.RandTimespan(TimeSpan)"/>
        /// to obtain a random delay timespan.
        /// </summary>
        public static readonly TimeSpan MaxJitter = TimeSpan.FromMilliseconds(250);

        /// <summary>
        /// Identifies the Git production branch.
        /// </summary>
        public const string GitProdBranch = "prod";

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
    }
}
