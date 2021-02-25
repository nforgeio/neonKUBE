//-----------------------------------------------------------------------------
// FILE:	    IEnterpriseHelper.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;

using Renci.SshNet;

namespace Neon.Kube
{
    /// <summary>
    /// <para>
    /// Defines neonKUBE Enterprise related helper methods that will be available
    /// for enterprise releases of neonDESKTOP and <b>neon-cli</b> via dependency
    /// injection by registering an <see cref="IEnterpriseHelper"/> implementation
    /// via the <see cref="NeonHelper.ServiceContainer"/>.
    /// </para>
    /// <para>
    /// Any code that requires or can be enhanced by enterprise capabilities can
    /// check whether an implementation for this interface is registered with
    /// <see cref="NeonHelper.ServiceContainer"/> to gain access to enhanced
    /// methods as necessary.
    /// </para>
    /// </summary>
    public interface IEnterpriseHelper
    {
        /// <summary>
        /// Loads the enterprise hosting managers.
        /// </summary>
        void LoadHostingManagers();

        /// <summary>
        /// Returns the enterprise hosting manager for a specific hosting environment.
        /// </summary>
        /// <param name="environment">Specifies the hosting environment.</param>
        /// <returns>The hosting manager for the environment.</returns>
        /// <exception cref="KubeException">Thrown if the environment is not implemented by an enterprise hosting manager.</exception>
        HostingManager GetHostingManager(HostingEnvironment environment);

        /// <summary>
        /// Returns the enterprise hosting manager for the hosting environment specified by the cluster definition
        /// within a cluster procy.
        /// </summary>
        /// <param name="cluster">The cluster proxy,</param>
        /// <param name="logFolder">Optionally specifies the log folder where the hosting manager will log progress.</param>
        /// <returns>The hosting manager for the environment.</returns>
        /// <exception cref="KubeException">Thrown if the environment is not implemented by an enterprise hosting manager.</exception>
        HostingManager GetHostingManager(ClusterProxy cluster, string logFolder = null);
    }
}
