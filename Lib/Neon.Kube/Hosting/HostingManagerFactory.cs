//-----------------------------------------------------------------------------
// FILE:	    HostingManagerFactory.cs
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
    /// Provides for the creation of <see cref="HostingManager"/> implementations
    /// for a target hosting environment.
    /// </summary>
    public class HostingManagerFactory : IHostingManagerFactory
    {
        //---------------------------------------------------------------------

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> The driver providing low-level access to hosting
        /// manager implementations.  This is initialized by a call to <c>HostingLoader.Initialize()</c>
        /// defined withing the <b>Neon.Kube.Hosting</b> assembly.
        /// </summary>
        public static IHostingLoader Loader { get; set; }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="loaderAction">
        /// The optional action that will initialize the static <see cref="Loader"/> property with
        /// the <see cref="IHostingLoader"/> implemention.
        /// </param>
        public HostingManagerFactory(Action loaderAction = null)
        {
            if (loaderAction != null)
            {
                loaderAction();
                Covenant.Assert(Loader != null);
            }
        }

        /// <summary>
        /// Ensures that the factory has been initialized.
        /// </summary>
        private void CheckInitialized()
        {
            Covenant.Assert(Loader != null, $"[{nameof(Loader)}] is not initialized.  You must call [HostingLoader.Initialize()] in the [Neon.Kube.Hosting] assembly first.");
        }

        /// <inheritdoc/>
        public HostingManager GetMaster(ClusterProxy cluster, string logFolder = null)
        {
            CheckInitialized();

            return Loader.GetManager(cluster, logFolder);
        }

        /// <inheritdoc/>
        public bool IsCloudEnvironment(HostingEnvironments environment)
        {
            CheckInitialized();

            return Loader.IsCloudEnvironment(environment);
        }

        /// <inheritdoc/>
        public void Validate(ClusterDefinition clusterDefinition)
        {
            CheckInitialized();

            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            var cluster = new ClusterProxy(clusterDefinition);
            var master = GetMaster(cluster);

            if (master == null)
            {
                throw new KubeException($"Cannot locate a [{nameof(IHostingManager)}] implementation for the [{clusterDefinition.Hosting.Environment}] hosting environment.");
            }

            master.Validate(clusterDefinition);
        }
    }
}
