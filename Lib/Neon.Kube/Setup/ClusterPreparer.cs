//-----------------------------------------------------------------------------
// FILE:	    ClusterPreparer.cs
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
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Neon.Common;
using Neon.Cryptography;
using Neon.Kube;
using Neon.Net;
using Neon.SSH;

namespace Neon.Kube
{
    /// <summary>
    /// Handles cluster preparation.
    /// </summary>
    public class ClusterPreparer
    {
        private const string        logBeginMarker  = "# CLUSTER-BEGIN-PREPARE ##########################################################";
        private const string        logEndMarker    = "# CLUSTER-END-PREPARE-SUCCESS ####################################################";
        private const string        logFailedMarker = "# CLUSTER-END-PREPARE-FAILED #####################################################";

        private ClusterProxy        cluster;
        private IHostingManager     hostingManager;
        private string              packageCaches;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="cluster">The cluster proxy to be used for configuring the cluster.</param>
        /// <param name="hostingManager">The hosting manager.</param>
        /// <param name="packageCaches">Optionally specifies the package cache server endpoints separated by spaces.</param>
        public ClusterPreparer(ClusterProxy cluster, IHostingManager hostingManager, string packageCaches = null)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null, nameof(cluster));
            Covenant.Requires<ArgumentNullException>(hostingManager != null, nameof(hostingManager));

            this.cluster        = cluster;
            this.hostingManager = hostingManager;
            this.packageCaches  = packageCaches;
        }

        /// <summary>
        /// Prepares the cluster.
        /// </summary>
        /// <param name="showStatus">Display progress.</param>
        /// <param name="maxParallel">Maximum individual nodes operations to perform in parallel.</param>
        /// <param name="waitSeconds">Seconds to wait between critical operations to allow things to stablize.</param>
        public void Run(bool showStatus, int maxParallel, double waitSeconds)
        {
        }
    }
}
