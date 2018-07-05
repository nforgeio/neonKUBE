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
using System.Reflection;
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

namespace Neon.Hive
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
        /// <param name="hive">The hive being managed.</param>
        /// <param name="logFolder">
        /// The folder where log files are to be written, otherwise or <c>null</c> or 
        /// empty if logging is disabled.
        /// </param>
        /// <returns>The <see cref="HostingManager"/>.</returns>
        /// <exception cref="HiveException">Thrown if the multiple managers implement support for the same hosting environment.</exception>
        /// <exception cref="NotImplementedException">Thrown if no hosting manager could be located for the environment.</exception>
        public static HostingManager GetManager(HiveProxy hive, string logFolder = null)
        {
            Covenant.Requires<ArgumentNullException>(hive != null);

            // We're going to reflected all loaded assemblies for classes that implement
            // [IHostingManager] and are decorated with an [HostingProviderAttribute],
            // end then use the environment specified in the attributes to determine
            // which manager class to instantiate and return.

            var enviromentToManager = new Dictionary<HostingEnvironments, Type>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsSubclassOf(typeof(HostingManager)))
                    {
                        var providerAttribute = type.GetCustomAttribute<HostingProviderAttribute>();

                        if (providerAttribute != null)
                        {
                            if (enviromentToManager.TryGetValue(providerAttribute.Environment, out var existingProviderType))
                            {
                                throw new HiveException($"Hosting provider types [{existingProviderType.FullName}] and [{type.FullName}] cannot both implement the [{providerAttribute.Environment}] hosting environment.");
                            }
                        }

                        enviromentToManager.Add(providerAttribute.Environment, type);
                    }
                }
            }

            if (!enviromentToManager.TryGetValue(hive.Definition.Hosting.Environment, out var managerType))
            {
                throw new NotImplementedException($"Cannot locate an [{nameof(IHostingManager)}] for the [{hive.Definition.Hosting.Environment}] environment.");
            }

            return (HostingManager)Activator.CreateInstance(managerType, hive, logFolder);
        }

        /// <summary>
        /// Verifies that a hive is valid for the hosting manager, customizing 
        /// properties as required.
        /// </summary>
        /// <param name="hiveDefinition">The hive definition.</param>
        /// <exception cref="HiveDefinitionException">Thrown if any problems were detected.</exception>
        public static void ValidateCluster(HiveDefinition hiveDefinition)
        {
            Covenant.Requires<ArgumentNullException>(hiveDefinition != null);

            var hive    = new HiveProxy(hiveDefinition);
            var manager = HostingManager.GetManager(hive);

            manager.Validate(hiveDefinition);
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
                case HostingEnvironments.HyperVDev:
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
        /// The initial host username to use when creating and/or configuring hive nodes.
        /// </summary>
        public string HostUsername { get; set; }

        /// <summary>
        /// The initial host password to use when creating and/or configuring hive nodes.
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
        public abstract void Validate(HiveDefinition hiveDefinition);

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
