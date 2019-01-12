//-----------------------------------------------------------------------------
// FILE:	    KubeConst.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

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
        public const string NeonProdRegistry = "nhive";

        /// <summary>
        /// Identifies the development cluster public Docker registry.
        /// </summary>
        public const string NeonDevRegistry = "nhivedev";

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
    }
}
