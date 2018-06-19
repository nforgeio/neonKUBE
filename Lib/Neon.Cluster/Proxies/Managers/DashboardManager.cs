//-----------------------------------------------------------------------------
// FILE:	    DashboardManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

namespace Neon.Cluster
{
    /// <summary>
    /// Handles cluster dashboard related operations for a <see cref="ClusterProxy"/>.
    /// </summary>
    public sealed class DashboardManager
    {
        private ClusterProxy cluster;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="cluster">The parent <see cref="ClusterProxy"/>.</param>
        internal DashboardManager(ClusterProxy cluster)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null);

            this.cluster = cluster;
        }

        /// <summary>
        /// Removes a cluster dashboard if it exists.
        /// </summary>
        /// <param name="name">The dashboard name.</param>
        public void Remove(string name)
        {
            Covenant.Requires<ArgumentException>(ClusterDefinition.IsValidName(name));

            cluster.Consul.Client.KV.Delete(GetDashboardConsulKey(name)).Wait();
        }

        /// <summary>
        /// Retrieves a cluster dashboard.
        /// </summary>
        /// <param name="name">The dashboard name.</param>
        /// <returns>The dashboard if present or <c>null</c> if it doesn't exist.</returns>
        public ClusterDashboard Get(string name)
        {
            Covenant.Requires<ArgumentException>(ClusterDefinition.IsValidName(name));

            return cluster.Consul.Client.KV.GetObjectOrDefault<ClusterDashboard>(GetDashboardConsulKey(name)).Result;

        }

        /// <summary>
        /// Lists the cluster dashboards.
        /// </summary>
        /// <returns>The cluster dashboards.</returns>
        public List<ClusterDashboard> List()
        {
            var result = cluster.Consul.Client.KV.ListOrDefault<ClusterDashboard>(NeonClusterConst.ConsulDashboardsKey).Result;

            if (result == null)
            {
                return new List<ClusterDashboard>();
            }
            else
            {
                return result.ToList();
            }
        }

        /// <summary>
        /// Adds or updates a cluster dashboard.
        /// </summary>
        /// <param name="dashboard">The dashboard.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the dashboard is not valid.</exception>
        public void Set(ClusterDashboard dashboard)
        {
            Covenant.Requires<ArgumentNullException>(dashboard != null);

            var errors = dashboard.Validate(cluster.Definition);

            if (errors.Count > 0)
            {
                throw new ClusterDefinitionException($"Invalid dashboard: {errors.First()}");
            }

            cluster.Consul.Client.KV.PutObject(GetDashboardConsulKey(dashboard.Name), dashboard, Formatting.Indented).Wait();
        }

        /// <summary>
        /// Returns the Consul key for a dashboard based on its name.
        /// </summary>
        /// <param name="name">The dashboard name.</param>
        /// <returns>The Consul key path.</returns>
        private string GetDashboardConsulKey(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            return $"{NeonClusterConst.ConsulDashboardsKey}/{name.ToLowerInvariant()}";
        }
    }
}
