//-----------------------------------------------------------------------------
// FILE:	    IHostingLoader.cs
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
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;
using Neon.Time;

namespace Neon.Kube
{
    /// <summary>
    /// <b>INTERNAL USE ONLY:</b> This interface describes a low-level class implementations
    /// that are registered by the <b>Neon.Kube.Hosting</b> class library with 
    /// <see cref="HostingManager"/> to provide a way to access the various hosting
    /// implementations without having to bake this into the <b>Neon.Kube</b> assembly.
    /// </summary>
    public interface IHostingLoader
    {
        /// <summary>
        /// Returns the <see cref="HostingManager"/> for an environment that can be used 
        /// for validating the hosting related options.
        /// </summary>
        /// <param name="environment">The target hosting environment.</param>
        /// <returns>
        /// The <see cref="HostingManager"/> or <c>null</c> if no hosting manager
        /// could be located for the specified cluster environment.
        /// </returns>
        HostingManager GetManager(HostingEnvironment environment);

        /// <summary>
        /// Returns the <see cref="HostingManager"/> for provisioning a specific environment.
        /// </summary>
        /// <param name="cluster">The cluster being managed.</param>
        /// <param name="logFolder">
        /// The folder where log files are to be written, otherwise or <c>null</c> or 
        /// empty if logging is disabled.
        /// </param>
        /// <returns>
        /// The <see cref="HostingManager"/> or <c>null</c> if no hosting manager
        /// could be located for the specified cluster environment.
        /// </returns>
        /// <exception cref="KubeException">Thrown if the multiple managers implement support for the same hosting environment.</exception>
        HostingManager GetManager(ClusterProxy cluster, string logFolder = null);

        /// <summary>
        /// Determines whether a hosting environment is hosted in the cloud.
        /// </summary>
        /// <param name="environment">The target hosting environment.</param>
        /// <returns><c>true</c> for cloud environments.</returns>
        bool IsCloudEnvironment(HostingEnvironment environment);
    }
}
