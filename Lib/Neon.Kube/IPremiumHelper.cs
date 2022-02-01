//-----------------------------------------------------------------------------
// FILE:	    IPremiumHelper.cs
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
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Deployment;

using Renci.SshNet;

namespace Neon.Kube
{
    /// <summary>
    /// <para>
    /// Defines neonKUBE premium related helper methods that will be available
    /// for premium releases of neonDESKTOP and <b>neon-cli</b> via dependency
    /// injection by registering an <see cref="IPremiumHelper"/> implementation
    /// via the <see cref="NeonHelper.ServiceContainer"/>.
    /// </para>
    /// <para>
    /// Any code that requires or can be enhanced by premium capabilities can
    /// check whether an implementation for this interface is registered with
    /// <see cref="NeonHelper.ServiceContainer"/> to gain access to enhanced
    /// methods as necessary.
    /// </para>
    /// </summary>
    public interface IPremiumHelper
    {
        /// <summary>
        /// Loads the premium hosting managers.
        /// </summary>
        void LoadHostingManagers();

        /// <summary>
        /// Returns the premium hosting manager for a specific hosting environment.
        /// </summary>
        /// <param name="environment">Specifies the hosting environment.</param>
        /// <returns>The hosting manager for the environment.</returns>
        /// <exception cref="KubeException">Thrown if the environment is not implemented by a premium hosting manager.</exception>
        HostingManager GetHostingManager(HostingEnvironment environment);

        /// <summary>
        /// Returns the <see cref="HostingManager"/> for provisioning a cluster using
        /// the default node image URI for the cluster environment.
        /// </summary>
        /// <param name="cluster">The cluster being managed.</param>
        /// <param name="logFolder">
        /// <para>
        /// The folder where log files are to be written, otherwise or <c>null</c> or 
        /// empty if logging is disabled.
        /// </para>
        /// <note>
        /// Specific hosting managers may choose to ignore this when it doesn't make sense.
        /// </note>
        /// </param>
        /// <returns>
        /// The <see cref="HostingManager"/> or <c>null</c> if no hosting manager
        /// could be located for the specified cluster environment.
        /// </returns>
        /// <exception cref="KubeException">Thrown if the multiple managers implement support for the same hosting environment.</exception>
        HostingManager GetManager(ClusterProxy cluster, string logFolder = null);

        /// <summary>
        /// Returns the premium <see cref="HostingManager"/> for provisioning a cluster by
        /// downloading a node image from a URI that references a single image 
        /// file or a multi-part <see cref="DownloadManifest"/> image.
        /// </summary>
        /// <param name="cluster">The cluster proxy,</param>
        /// <param name="nodeImageUri">The node image URI.</param>
        /// <param name="logFolder">Optionally specifies the log folder where the hosting manager will log progress.</param>
        /// <returns>The hosting manager for the environment.</returns>
        /// <exception cref="KubeException">Thrown if the environment is not implemented by a premium hosting manager.</exception>
        HostingManager GetManagerWithNodeImageUri(ClusterProxy cluster, string nodeImageUri, string logFolder = null);

        /// <summary>
        /// Returns the premium <see cref="HostingManager"/> for provisioning a cluster from
        /// an already downloaded image file already downloaded.
        /// </summary>
        /// <param name="cluster">The cluster proxy,</param>
        /// <param name="nodeImagePath">Specifies the path to the local node image file.</param>
        /// <param name="logFolder">Optionally specifies the log folder where the hosting manager will log progress.</param>
        /// <returns>The hosting manager for the environment.</returns>
        /// <exception cref="KubeException">Thrown if the environment is not implemented by a premium hosting manager.</exception>
        HostingManager GetManagerWithNodeImageFile(ClusterProxy cluster, string nodeImagePath, string logFolder = null);

        /// <summary>
        /// Gets the current WSL2 cluster IP address.
        /// </summary>
        /// <returns>The <see cref="IPAddress"/>.</returns>
        /// <exception cref="KubeException">Thrown if the environment is not implemented.</exception>
        IPAddress GetWsl2Address();
    }
}
