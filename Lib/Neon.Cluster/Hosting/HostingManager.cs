//-----------------------------------------------------------------------------
// FILE:	    HostingManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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
        /// <param name="cluster">The cluster being managed.</param>
        /// <param name="logFolder">
        /// The folder where log files are to be written, otherwise or <c>null</c> or 
        /// empty if logging is disabled.
        /// </param>
        /// <returns>The <see cref="HostingManager"/>.</returns>
        public static HostingManager GetManager(ClusterProxy cluster, string logFolder = null)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null);

            switch (cluster.Definition.Hosting.Environment)
            {
                case HostingEnvironments.Aws:

                    return new AwsHostingManager(cluster, logFolder);

                case HostingEnvironments.Azure:

                    return new AzureHostingManager(cluster, logFolder);

                case HostingEnvironments.Google:

                    return new GoogleHostingManager(cluster, logFolder);

                case HostingEnvironments.HyperV:

                    return new HyperVHostingManager(cluster, logFolder);

                case HostingEnvironments.LocalHyperV:

                    return new LocalHyperVHostingManager(cluster, logFolder);

                case HostingEnvironments.Machine:

                    return new MachineHostingManager(cluster, logFolder);

                case HostingEnvironments.XenServer:

                    return new XenServerHostingManager(cluster, logFolder);

                default:

                    throw new NotImplementedException($"Hosting manager for [{cluster.Definition.Hosting.Environment}] is not implemented.");
            }
        }

        /// <summary>
        /// Verifies that a cluster is valid for the hosting manager, customizing 
        /// properties as required.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if any problems were detected.</exception>
        public static void ValidateCluster(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null);

            var cluster = new ClusterProxy(clusterDefinition);
            var manager = HostingManager.GetManager(cluster);

            manager.Validate(clusterDefinition);
        }

        /// <summary>
        /// Determines whether a hosting environment is hosted in the cloud.
        /// </summary>
        /// <param name="environment">The target hosting environment.</param>
        /// <returns><c>true</c> for cloud environments.</returns>
        public static bool IsCloudEnvironment(HostingEnvironments environment)
        {
            switch (environment)
            {
                case HostingEnvironments.Aws:
                case HostingEnvironments.Azure:
                case HostingEnvironments.Google:

                    return true;

                case HostingEnvironments.HyperV:
                case HostingEnvironments.LocalHyperV:
                case HostingEnvironments.Machine:
                case HostingEnvironments.XenServer:

                    return false;

                default:

                    throw new NotImplementedException($"Hosting manager for [{environment}] is not implemented.");
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
        /// The maximum number of nodes that will execute provisioning steps in parallel.  This
        /// defaults to <b>5</b>.
        /// </summary>
        public int MaxParallel { get; set; } = 5;

        /// <summary>
        /// Number of seconds to delay after specific operations (e.g. to allow services to stablize).
        /// This defaults to <b>0.0</b>.
        /// </summary>
        public double WaitSeconds { get; set; } = 0.0;

        /// <inheritdoc/>
        public virtual bool IsProvisionNOP
        {
            get { return false; }
        }

        /// <inheritdoc/>
        public abstract void Validate(ClusterDefinition clusterDefinition);

        /// <inheritdoc/>
        public abstract bool Provision(bool force);

        /// <inheritdoc/>
        public abstract (string Address, int Port) GetSshEndpoint(string nodeName);

        /// <inheritdoc/>
        public abstract void AddPostProvisionSteps(SetupController<NodeDefinition> controller);

        /// <inheritdoc/>
        public abstract void AddPostVpnSteps(SetupController<NodeDefinition> controller);

        /// <inheritdoc/>
        public abstract List<HostedEndpoint> GetPublicEndpoints();

        /// <inheritdoc/>
        public abstract bool CanUpdatePublicEndpoints { get; }

        /// <inheritdoc/>
        public abstract void UpdatePublicEndpoints(List<HostedEndpoint> endpoints);

        /// <inheritdoc/>
        public abstract string DrivePrefix { get; }

        /// <inheritdoc/>
        public virtual bool RequiresAdminPrivileges
        {
            get { return false; }
        }
    }
}
