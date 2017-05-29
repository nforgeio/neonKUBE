//-----------------------------------------------------------------------------
// FILE:	    HostingManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;
using Neon.Time;

namespace Neon.Cluster
{
    /// <summary>
    /// Base class for environment specific hosting managers. 
    /// </summary>
    public abstract class HostingManager : IHostingManager
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the <see cref="HostingManager"/> for a specific environment.
        /// </summary>
        /// <param name="provider">The target hosting provider.</param>
        /// <param name="cluster">The cluster being managed.</param>
        /// <returns>The <see cref="HostingManager"/>.</returns>
        public static HostingManager GetManager(HostingProviders provider, ClusterProxy cluster)
        {
            switch (cluster.Definition.Hosting.Provider)
            {
                case HostingProviders.Aws:

                    return new AwsHostingManager(cluster);

                case HostingProviders.Azure:

                    return new AzureHostingManager(cluster);

                case HostingProviders.Google:

                    return new GoogleHostingManager(cluster);

                case HostingProviders.OnPremise:

                    return new PrivateHostingManager(cluster);

                default:

                    throw new NotImplementedException($"Hosting manager for [{cluster.Definition.Hosting.Provider}] is not implemented.");
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~HostingManager()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases any important resources associated with the instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Releases any important resources associated with the instance.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if the instance is being disposed as opposed to being finalized.</param>
        public abstract void Dispose(bool disposing);

        /// <summary>
        /// The initial host username to use when creating and/or configuring cluster nodes.
        /// </summary>
        public string HostUsername { get; set; }

        /// <summary>
        /// The initial host password to use when creating and/or configuring cluster nodes.
        /// </summary>
        public string HostPassword { get; set; }

        /// <summary>
        /// Specifies whether the class should print setup status to the console.
        /// This defaults to <c>false</c>.
        /// </summary>
        public bool ShowStatus { get; set; } = false;

        /// <summary>
        /// The maximum number of nodes that will execute setup steps in parallel.  This
        /// defaults to essentially unconstrained.
        /// </summary>
        public int MaxParallel { get; set; } = int.MaxValue;

        /// <summary>
        /// Number of seconds to delay after specific operations (e.g. to allow services to stablize).
        /// This defaults to <b>0.0</b>.
        /// </summary>
        public double WaitSeconds { get; set; } = 0.0;

        /// <inheritdoc/>
        public abstract bool Provision();

        /// <inheritdoc/>
        public abstract (string Address, int Port) GetSshEndpoint(string nodeName);

        /// <inheritdoc/>
        public abstract void AddPostProvisionSteps(SetupController controller);

        /// <inheritdoc/>
        public abstract void AddPostVpnSteps(SetupController controller);

        /// <inheritdoc/>
        public abstract List<HostedEndpoint> GetPublicEndpoints();

        /// <inheritdoc/>
        public abstract bool CanUpdatePublicEndpoints { get; }

        /// <inheritdoc/>
        public abstract void UpdatePublicEndpoints(List<HostedEndpoint> endpoints);
    }
}
