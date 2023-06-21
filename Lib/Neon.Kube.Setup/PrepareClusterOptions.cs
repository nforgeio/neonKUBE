//-----------------------------------------------------------------------------
// FILE:        PrepareClusterOptions.cs
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Neon.Kube.ClusterDef;
using Neon.Kube.Hosting;

namespace Neon.Kube.Setup
{
    /// <summary>
    /// Optionally used to specify options for <see cref="KubeSetup.CreateClusterPrepareControllerAsync(ClusterDefinition, bool, PrepareClusterOptions)"/>.
    /// </summary>
    public class PrepareClusterOptions
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public PrepareClusterOptions()
        {
        }

        /// <summary>
        /// Optionally specifies the node image URI.
        /// </summary>
        public string NodeImageUri { get; set; } = null;

        /// <summary>
        /// <para>
        /// Optionally specifies the node image path.
        /// </para>
        /// <note>
        /// One of <see cref="NodeImageUri"/> or <see cref="NodeImagePath"/> must be specified for 
        /// on-premise hypervisor based environments.  These are ignored for cloud hosting.
        /// </note>
        /// </summary>
        public string NodeImagePath { get; set; } = null;

        /// <summary>
        /// Optionally specifies the maximum number of node operations to be performed in parallel.
        /// This <b>defaults to 0</b> which means that we'll use <see cref="IHostingManager.MaxParallel"/>.
        /// </summary>
        public int MaxParallel { get; set; } = 0;

        /// <summary>
        /// <para>
        /// Optionally specifies the IP endpoints for the APT package caches to be used by
        /// the cluster, overriding the cluster definition settings.  This is useful when
        /// package caches are already deployed in an environment.
        /// </para>
        /// <note>
        /// Package cache servers are deployed to the control-plane nodes by default.
        /// </note>
        /// </summary>
        public IEnumerable<IPEndPoint> PackageCacheEndpoints { get; set; } = null;

        /// <summary>
        /// Optionally indicates that sensitive information <b>won't be redacted</b> from the setup logs 
        /// (typically used when debugging).
        /// </summary>
        public bool Unredacted { get; set; } = false;

        /// <summary>
        /// Optionally indicates that the cluster will be prepared in debug mode.
        /// </summary>
        public bool DebugMode { get; set; } = false;

        /// <summary>
        /// Optionally specifies the base image name to use for debug mode.
        /// </summary>
        public string BaseImageName { get; set; } = null;

        /// <summary>
        /// Optionally remove any existing cluster with the same name in the target environment.
        /// </summary>
        public bool RemoveExisting { get; set; } = false;

        /// <summary>
        /// Optionally disables status output to the console.  This is typically
        /// enabled for cluster being provisioned by non-console applications.
        /// </summary>
        public bool DisableConsoleOutput { get; set; } = false;

        /// <summary>
        /// Optionally indicates that we're building a ready-to-go neon desktop image.
        /// </summary>
        public bool BuildDesktopImage { get; set; } = false;

        /// <summary>
        /// Optionally indicates that we're setting up a NEONDESKTOP cluster
        /// from a completely prebuilt desktop image.  In this case, the controller
        /// returned will fully deploy the cluster (so no setup controller needs to
        /// be created and run afterwards).
        /// </summary>
        public bool DesktopReadyToGo { get; set; } = false;
    }
}
