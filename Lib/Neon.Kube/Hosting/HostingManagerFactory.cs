//-----------------------------------------------------------------------------
// FILE:	    HostingManagerFactory.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;

using Neon.Kube.ClusterDef;
using Neon.Kube.Proxy;

namespace Neon.Kube.Hosting
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
        public HostingManager GetManager(HostingEnvironment environment)
        {
            CheckInitialized();

            return Loader.GetManager(environment);
        }

        /// <inheritdoc/>
        public HostingManager GetManager(ClusterProxy cluster, bool cloudMarketplace, string logFolder = null)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null, nameof(cluster));

            CheckInitialized();

            return Loader.GetManager(cluster, cloudMarketplace, logFolder);
        }

        /// <inheritdoc/>
        public HostingManager GetManagerWithNodeImageUri(ClusterProxy cluster, bool cloudMarketplace, string nodeImageUri, string logFolder = null)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null, nameof(cluster));
            Covenant.Requires<InvalidOperationException>(!IsCloudEnvironment(cluster.Hosting.Environment) || cloudMarketplace, $"[{nameof(cloudMarketplace)}=true] is not allowed for on-premise hosting environments.");
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nodeImageUri), nameof(nodeImageUri));

            CheckInitialized();

            return Loader.GetManagerWithNodeImageUri(cluster, cloudMarketplace, nodeImageUri, logFolder);
        }

        /// <inheritdoc/>
        public HostingManager GetManagerWithNodeImageFile(ClusterProxy cluster, string nodeImagePath, string logFolder = null)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null, nameof(cluster));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nodeImagePath), nameof(nodeImagePath));

            CheckInitialized();

            return Loader.GetManagerWithNodeImageFile(cluster, nodeImagePath, logFolder);
        }

        /// <inheritdoc/>
        public bool IsCloudEnvironment(HostingEnvironment environment)
        {
            CheckInitialized();

            return Loader.IsCloudEnvironment(environment);
        }

        /// <summary>
        /// Ensures that that a cluster definition has valid hosting options.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        public void Validate(ClusterDefinition clusterDefinition)
        {
            CheckInitialized();

            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            var manager = GetManager(clusterDefinition.Hosting.Environment);

            if (manager == null)
            {
                throw new NeonKubeException($"Cannot locate a [{nameof(IHostingManager)}] implementation for the [{clusterDefinition.Hosting.Environment}] hosting environment.  This may be a premium-only feature.");
            }

            manager.Validate(clusterDefinition);
        }
    }
}
