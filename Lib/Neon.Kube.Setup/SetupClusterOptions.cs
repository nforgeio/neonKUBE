//-----------------------------------------------------------------------------
// FILE:        SetupClusterOptions.cs
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
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Neon.Collections;
using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Clients;
using Neon.Kube.ClusterDef;
using Neon.Kube.Proxy;
using Neon.Kube.Hosting;
using Neon.Kube.Setup;
using Neon.Net;
using Neon.Retry;
using Neon.SSH;
using Neon.Tasks;

namespace Neon.Kube.Setup
{
    /// <summary>
    /// Optionally used to specify options for <see cref="KubeSetup.CreateClusterSetupControllerAsync(ClusterDefinition, bool, SetupClusterOptions)"/>.
    /// </summary>
    public class SetupClusterOptions
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public SetupClusterOptions()
        {
        }

        /// <summary>
        /// Optionally specifies the maximum number of node operations to be performed in parallel.
        /// This <b>defaults to 0</b> which means that we'll use <see cref="IHostingManager.MaxParallel"/>.
        /// </summary>
        public int MaxParallel { get; set; } = 500;

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
        /// <para>
        /// Optionally specifies that the current Helm charts should be uploaded to replace the charts in the base image.
        /// </para>
        /// <note>
        /// This will be treated as <c>true</c> when <see cref="DebugMode"/>> is passed as <c>true</c>.
        /// </note>
        /// </summary>
        public bool UploadCharts { get; set; } = false;

        /// <summary>
        /// Optionally overrides the NeonCLOUD headend service URI.  This defaults to <see cref="KubeEnv.HeadendUri"/>.
        /// </summary>
        public string NeonCloudHeadendUri { get; set; } = null;

        /// <summary>
        /// Optionally disables status output to the console.  This is typically
        /// enabled for non-console applications.
        /// </summary>
        public bool DisableConsoleOutput { get; set; } = false;

        /// <summary>
        /// Optionally indicates that we're setting up a NEONDESKTOP cluster
        /// from a completely prebuilt desktop image.  In this case, the controller
        /// returned will fully deploy the cluster (so no setup controller needs to
        /// be created and run afterwards).
        /// </summary>
        public bool DesktopReadyToGo { get; set; } = false;
    }
}
